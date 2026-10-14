using System.Collections.Concurrent;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class LocalSpeechQueuePlaybackStateTests
{
    private sealed class ControlledSpeechSession : ILocalSpeechSession
    {
        private int callCount;
        public ManualResetEventSlim FirstCallStarted { get; } = new(false);
        public ManualResetEventSlim ReleaseFirstCall { get; } = new(false);
        public int CallCount => Volatile.Read(ref callCount);

        public SpeechPlaybackResult Speak(string text, string voice, string output, int rate, int volume, Func<bool> cancelled)
        {
            var call = Interlocked.Increment(ref callCount);
            if (call == 1)
            {
                FirstCallStarted.Set();
                if (!ReleaseFirstCall.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                    throw new TimeoutException("La prueba no liberó la primera frase.");
            }
            return new SpeechPlaybackResult(false, cancelled());
        }
        public void Dispose() => ReleaseFirstCall.Set();
    }

    [Fact]
    public void AdjacentSpeechItemsDoNotFlickerToIdleBetweenItems()
    {
        using var session = new ControlledSpeechSession();
        using var queue = new LocalSpeechQueue(() => session);
        var states = new ConcurrentQueue<bool>();
        queue.PlaybackStateChanged += states.Enqueue;
        Assert.True(queue.Enqueue("Primera frase"));
        Assert.True(session.FirstCallStarted.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        Assert.True(queue.Enqueue("Segunda frase"));
        session.ReleaseFirstCall.Set();
        Assert.True(SpinWait.SpinUntil(() => { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); return states.Count >= 2; }, TimeSpan.FromSeconds(3)));
        Assert.Equal(new[] { true, false }, states.ToArray());
        Assert.Equal(2, session.CallCount);
    }

    private sealed class BrokenSession : ILocalSpeechSession
    {
        private readonly Action onSpeak;
        public BrokenSession(Action onSpeak) => this.onSpeak = onSpeak;
        public SpeechPlaybackResult Speak(string text, string voice, string output, int rate, int volume, Func<bool> cancelled)
        {
            onSpeak();
            throw new InvalidOperationException("Fallo de TTS simulado.");
        }
        public void Dispose() => throw new InvalidOperationException("Fallo de limpieza simulado.");
    }

    [Fact]
    public void SpeechWorkerContinuesAfterTtsAndCleanupErrors()
    {
        var calls = 0;
        using var queue = new LocalSpeechQueue(() => new BrokenSession(() => Interlocked.Increment(ref calls)));
        Assert.True(queue.Enqueue("Primera frase"));
        Assert.True(SpinWait.SpinUntil(() => { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); return Volatile.Read(ref calls) >= 1; }, TimeSpan.FromSeconds(3)));
        Assert.True(queue.Enqueue("Segunda frase"));
        Assert.True(SpinWait.SpinUntil(() => { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); return Volatile.Read(ref calls) >= 2; }, TimeSpan.FromSeconds(3)));
    }
}
