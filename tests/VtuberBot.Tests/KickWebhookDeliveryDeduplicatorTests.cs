using System.Collections.Concurrent;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class KickWebhookDeliveryDeduplicatorTests
{
    [Fact]
    public void FailedAdmissionDoesNotConsumeMessageIdAndRetryCanSucceed()
    {
        var deduplicator = new KickWebhookDeliveryDeduplicator(8);

        Assert.Throws<InvalidOperationException>(() =>
            deduplicator.TryDispatchOnce("kick-message-1",
                () => throw new InvalidOperationException("queue full")));

        var dispatched = 0;
        Assert.True(deduplicator.TryDispatchOnce("kick-message-1", () => dispatched++));
        Assert.Equal(1, dispatched);
        Assert.False(deduplicator.TryDispatchOnce("kick-message-1",
            () => throw new InvalidOperationException("duplicate must not dispatch")));
        Assert.Equal(1, dispatched);
        Assert.Equal(1, deduplicator.RememberedCount);
    }

    [Fact]
    public async Task ConcurrentDuplicatesAreAdmittedOnlyOnce()
    {
        var deduplicator = new KickWebhookDeliveryDeduplicator(32);
        var dispatchCount = 0;
        var acceptedCount = 0;

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            if (deduplicator.TryDispatchOnce("kick-message-concurrent",
                    () => Interlocked.Increment(ref dispatchCount)))
                Interlocked.Increment(ref acceptedCount);
        })));

        Assert.Equal(1, acceptedCount);
        Assert.Equal(1, dispatchCount);
    }

    [Fact]
    public void DeduplicationWindowIsBoundedAndEvictsOldestId()
    {
        var deduplicator = new KickWebhookDeliveryDeduplicator(2);

        Assert.True(deduplicator.TryDispatchOnce("first", static () => { }));
        Assert.True(deduplicator.TryDispatchOnce("second", static () => { }));
        Assert.True(deduplicator.TryDispatchOnce("third", static () => { }));

        Assert.Equal(2, deduplicator.RememberedCount);
        Assert.True(deduplicator.TryDispatchOnce("first", static () => { }));
        Assert.Equal(2, deduplicator.RememberedCount);
    }

    [Fact]
    public void RejectsInvalidCapacityAndMessageId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KickWebhookDeliveryDeduplicator(0));
        var deduplicator = new KickWebhookDeliveryDeduplicator();
        Assert.Throws<ArgumentException>(() => deduplicator.TryDispatchOnce("", static () => { }));
        Assert.Throws<ArgumentException>(() => deduplicator.TryDispatchOnce(new string('x', 129), static () => { }));
    }
}
