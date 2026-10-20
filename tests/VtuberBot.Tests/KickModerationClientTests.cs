using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class KickModerationClientTests
{
    private readonly KickApiClient client = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BanOrTimeoutRejectsInvalidTargetBeforeNetwork(long userId)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", userId, 10, ""));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10081)]
    public async Task BanOrTimeoutRejectsInvalidDurationBeforeNetwork(int durationMinutes)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", 123, durationMinutes, ""));
    }

    [Fact]
    public async Task BanOrTimeoutRejectsReasonOverApiLimitBeforeNetwork()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", 123, 10, new string('x', 101)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-123)]
    public async Task UnbanRejectsInvalidTargetBeforeNetwork(long userId)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.UnbanUserAsync("test-token", "valid-channel", userId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    public async Task DeleteMessageRejectsInvalidIdBeforeNetwork(string messageId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DeleteChatMessageAsync("test-token", messageId));
    }
}
