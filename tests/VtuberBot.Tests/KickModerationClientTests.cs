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
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", userId, 10, "", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10081)]
    public async Task BanOrTimeoutRejectsInvalidDurationBeforeNetwork(int durationMinutes)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", 123, durationMinutes, "", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BanOrTimeoutRejectsReasonOverApiLimitBeforeNetwork()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.BanOrTimeoutUserAsync("test-token", "valid-channel", 123, 10, new string('x', 101), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-123)]
    public async Task UnbanRejectsInvalidTargetBeforeNetwork(long userId)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.UnbanUserAsync("test-token", "valid-channel", userId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" bad id ")]
    [InlineData("id\nheader")]
    public async Task DeleteMessageRejectsInvalidIdBeforeNetwork(string messageId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DeleteChatMessageAsync("test-token", messageId, TestContext.Current.CancellationToken));
    }
}
