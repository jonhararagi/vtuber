using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class EventSubQueuePolicyTests
{
    [Theory]
    [InlineData("subscription_error")]
    [InlineData("revocation")]
    [InlineData("REVOCATION")]
    public void SubscriptionDiagnosticsUseReservedPriority(string type) =>
        Assert.True(EventSubQueuePolicy.IsCritical(type));

    [Theory]
    [InlineData("channel.follow")]
    [InlineData("channel.subscribe")]
    [InlineData("channel.raid")]
    [InlineData("")]
    [InlineData(null)]
    public void ViewerEventsDoNotConsumeCriticalQueue(string? type) =>
        Assert.False(EventSubQueuePolicy.IsCritical(type));
}
