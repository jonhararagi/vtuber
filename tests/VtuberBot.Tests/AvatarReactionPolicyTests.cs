using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarReactionPolicyTests
{
    [Theory]
    [InlineData("channel.follow", "Happy")]
    [InlineData("channel.subscribe", "Happy")]
    [InlineData("channel.subscription.gift", "Happy")]
    [InlineData("channel.channel_points_custom_reward_redemption.add", "Happy")]
    [InlineData("channel.raid", "Surprised")]
    public void SupportedEventsMapToExpectedExpression(string eventType, string expected) =>
        Assert.Equal(expected, AvatarReactionPolicy.GetTransientState(eventType));

    [Theory]
    [InlineData("subscription_error")]
    [InlineData("revocation")]
    [InlineData("unknown.future.event")]
    [InlineData("")]
    public void NonViewerEventsDoNotTriggerExpression(string eventType) =>
        Assert.Null(AvatarReactionPolicy.GetTransientState(eventType));
}
