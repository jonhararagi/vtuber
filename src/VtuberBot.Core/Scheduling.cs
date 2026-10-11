namespace VtuberBot.Core;

public sealed class TaskScheduler : IDisposable
{
    private readonly object gate = new();
    private readonly List<(long Id, DateTimeOffset DueAt, Action Callback)> queue = [];
    private readonly ILogger logger;

    [ThreadStatic]
    private static TaskScheduler? currentWorkerScheduler;

    private CancellationTokenSource? cancellation;
    private Task? worker;
    private long nextId;
    private bool stopping;

    public TaskScheduler(ILogger logger) => this.logger = logger;

    public long ScheduleOnce(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));

        lock (gate)
        {
            if (stopping)
                throw new InvalidOperationException("Task scheduler is shutting down.");

            var id = ++nextId;
            queue.Add((id, DateTimeOffset.UtcNow + delay, callback));
            queue.Sort((left, right) => left.DueAt.CompareTo(right.DueAt));

            cancellation ??= new CancellationTokenSource();
            worker ??= Task.Run(() => Run(cancellation.Token));
            return id;
        }
    }

    public bool Cancel(long id)
    {
        lock (gate)
            return queue.RemoveAll(item => item.Id == id) > 0;
    }

    private async Task Run(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                (long Id, DateTimeOffset DueAt, Action Callback)? next;

                lock (gate)
                    next = queue.Count == 0 ? null : queue[0];

                if (next is null)
                {
                    try
                    {
                        await Task.Delay(25, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                var delay = next.Value.DueAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                lock (gate)
                {
                    if (queue.Count == 0 || queue[0].Id != next.Value.Id)
                        continue;

                    queue.RemoveAt(0);
                }

                var previousWorkerScheduler = currentWorkerScheduler;
                currentWorkerScheduler = this;

                try
                {
                    try
                    {
                        next.Value.Callback();
                    }
                    catch (Exception exception)
                    {
                        logger.Error(exception, "Scheduled task {0} failed.", next.Value.Id);
                    }
                }
                finally
                {
                    currentWorkerScheduler = previousWorkerScheduler;
                }
            }
        }
        finally
        {
            CancellationTokenSource? cancellationToDispose = null;

            lock (gate)
            {
                if (cancellation?.Token == token)
                {
                    worker = null;
                    cancellationToDispose = cancellation;
                    cancellation = null;
                }
            }

            cancellationToDispose?.Dispose();
        }
    }

    public void Shutdown()
    {
        Task? workerToWait;

        lock (gate)
        {
            if (!stopping)
            {
                stopping = true;
                queue.Clear();
                cancellation?.Cancel();
            }

            // Every external caller must observe worker completion, including callers
            // arriving after another thread has already initiated shutdown.
            workerToWait = worker;
        }

        if (workerToWait is not null &&
            !ReferenceEquals(currentWorkerScheduler, this))
        {
            try
            {
                workerToWait.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Task scheduler worker failed during shutdown.");
            }
        }
    }

    public void Dispose() => Shutdown();
}
