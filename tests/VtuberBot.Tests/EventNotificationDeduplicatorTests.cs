using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class EventNotificationDeduplicatorTests
{
    [Fact]
    public void RejectsDuplicateIdsButAcceptsDistinctEvents()
    {
        var deduplicator = new EventNotificationDeduplicator(capacity: 3);

        Assert.True(deduplicator.TryAccept("event-1"));
        Assert.False(deduplicator.TryAccept("event-1"));
        Assert.True(deduplicator.TryAccept("event-2"));
        Assert.True(deduplicator.TryAccept("event-3"));
        Assert.False(deduplicator.TryAccept("event-2"));
    }

    [Fact]
    public void CacheCapacityIsBoundedAndOldIdsCanBeAcceptedAfterEviction()
    {
        var deduplicator = new EventNotificationDeduplicator(capacity: 2);

        Assert.True(deduplicator.TryAccept("first"));
        Assert.True(deduplicator.TryAccept("second"));
        Assert.True(deduplicator.TryAccept("third"));
        Assert.False(deduplicator.TryAccept("third"));
        Assert.True(deduplicator.TryAccept("first"));
    }

    [Fact]
    public async Task ConcurrentRedeliveriesAcceptAnIdOnlyOnce()
    {
        var deduplicator = new EventNotificationDeduplicator();
        var attempts = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => deduplicator.TryAccept("same-event")))
            .ToArray();

        var results = await Task.WhenAll(attempts);
        Assert.Single(results, accepted => accepted);
    }

    [Fact]
    public void AcceptsNotificationsWithoutAnIdAndRejectsInvalidCapacity()
    {
        var deduplicator = new EventNotificationDeduplicator();

        Assert.True(deduplicator.TryAccept(null));
        Assert.True(deduplicator.TryAccept(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventNotificationDeduplicator(0));
    }
}
