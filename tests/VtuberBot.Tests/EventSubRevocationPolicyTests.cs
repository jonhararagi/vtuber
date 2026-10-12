using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class EventSubRevocationPolicyTests
{
    [Theory]
    [InlineData("authorization_revoked", true)]
    [InlineData("AUTHORIZATION_REVOKED", true)]
    [InlineData("user_removed", false)]
    [InlineData("version_removed", false)]
    [InlineData("moderator_removed", false)]
    [InlineData("websocket_disconnected", false)]
    [InlineData("unknown-future-status", false)]
    public void DescribesRevocationWithoutInventingRecovery(string status, bool reauthorizationRequired)
    {
        var guidance = EventSubRevocationPolicy.Describe(status);

        Assert.Equal(status.ToLowerInvariant(), guidance.Status);
        Assert.Equal(reauthorizationRequired, guidance.RequiresReauthorization);
        Assert.NotEmpty(guidance.Message);
    }

    [Theory]
    [InlineData("websocket_network_timeout", true)]
    [InlineData("websocket_network_error", true)]
    [InlineData("websocket_internal_error", true)]
    [InlineData("websocket_disconnected", false)]
    [InlineData("websocket_failed_ping_pong", false)]
    [InlineData("websocket_received_inbound_traffic", false)]
    [InlineData("websocket_connection_unused", false)]
    [InlineData("authorization_revoked", false)]
    [InlineData("version_removed", false)]
    public void AutomaticRecoveryIsEnabledOnlyForTransientFailuresWeCanSafelyRetry(
        string status, bool canRecover)
    {
        Assert.Equal(canRecover, EventSubRevocationPolicy.Describe(status).CanAttemptAutomaticRecovery);
    }

    [Fact]
    public void MissingStatusProducesGenericActionableGuidance()
    {
        var guidance = EventSubRevocationPolicy.Describe(null);

        Assert.Equal("unknown", guidance.Status);
        Assert.False(guidance.RequiresReauthorization);
        Assert.Contains("Twitch no proporcionó el motivo", guidance.Message);
    }
}
