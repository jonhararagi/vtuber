using System.Threading.Channels;
using VtuberBot.Core;

namespace VtuberBot.App;

/// <summary>
/// Keeps SQLite writes off the WPF dispatcher. The bounded queue deliberately drops new archive
/// records during overload rather than slowing live chat commands, moderation or audio.
/// </summary>
public sealed class ChatArchiveService : IAsyncDisposable
{
    public const int QueueCapacity = 1_024;

    private sealed record PendingWrite(ChatArchiveMessage Message, int RetentionDays);

    private readonly Channel<PendingWrite> queue;
    private readonly Task worker;
    private int droppedWrites;
    private int failedWrites;
    private int pendingWrites;
    private string lastError = string.Empty;
    private volatile bool disposed;

    public ChatArchiveStore Store { get; }
    public int PendingWrites => Volatile.Read(ref pendingWrites);
    public int DroppedWrites => Volatile.Read(ref droppedWrites);
    public int FailedWrites => Volatile.Read(ref failedWrites);
    public string LastError => Volatile.Read(ref lastError);

    public ChatArchiveService(string databasePath)
    {
        Store = new ChatArchiveStore(databasePath);
        queue = Channel.CreateBounded<PendingWrite>(new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        worker = Task.Run(ProcessWritesAsync);
    }

    public bool TryEnqueue(ChatArchiveMessage message, int retentionDays)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (disposed)
            return false;

        Interlocked.Increment(ref pendingWrites);
        if (queue.Writer.TryWrite(new PendingWrite(message, retentionDays)))
            return true;

        Interlocked.Decrement(ref pendingWrites);
        Interlocked.Increment(ref droppedWrites);
        return false;
    }

    private async Task ProcessWritesAsync()
    {
        var writesSinceMaintenance = 0;
        await foreach (var pending in queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Store.TryAdd(pending.Message, pending.RetentionDays);
                writesSinceMaintenance++;
                if (writesSinceMaintenance >= 100)
                {
                    Store.Prune(pending.RetentionDays);
                    writesSinceMaintenance = 0;
                }
                Volatile.Write(ref lastError, string.Empty);
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failedWrites);
                Volatile.Write(ref lastError, exception.GetType().Name);
            }
            finally
            {
                Interlocked.Decrement(ref pendingWrites);
            }
        }
    }

    public async Task FlushAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (PendingWrites > 0)
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("La cola del historial no se vació dentro del tiempo permitido.");
            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        queue.Writer.TryComplete();
        await worker.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
    }
}
