using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class TwitchIrcValidationTests
{
    [Theory]
    [InlineData("Naofumi_123", "naofumi_123")]
    [InlineData(" #MeriChannel ", "merichannel")]
    [InlineData("##ABC_9", "abc_9")]
    public void ChannelLoginIsNormalized(string input, string expected) =>
        Assert.Equal(expected, TwitchIrcValidation.NormalizeChannelLogin(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("channel name")]
    [InlineData("channel-name")]
    [InlineData("channel\r\nPRIVMSG #victim :injected")]
    [InlineData("channel/extra")]
    [InlineData("thischannelnameisfarlongerthan25")]
    public void InvalidChannelLoginIsRejected(string input) =>
        Assert.Throws<ArgumentException>(() => TwitchIrcValidation.NormalizeChannelLogin(input));

    [Theory]
    [InlineData("oauth:abc123", "abc123")]
    [InlineData(" OAuth:AbCdEf ", "AbCdEf")]
    [InlineData("abc123", "abc123")]
    public void OAuthPrefixIsRemovedWithoutChangingTokenContent(string input, string expected) =>
        Assert.Equal(expected, TwitchIrcValidation.NormalizeOAuthToken(input));

    [Fact]
    public void SendTargetMustMatchTheConnectedChannel()
    {
        Assert.Equal("channel_1", TwitchIrcValidation.NormalizeTargetChannel("#Channel_1", "channel_1"));
        Assert.Throws<InvalidOperationException>(() =>
            TwitchIrcValidation.NormalizeTargetChannel("streamer", "different_channel"));
        Assert.Throws<ArgumentException>(() =>
            TwitchIrcValidation.NormalizeTargetChannel("streamer", "target\r\nPRIVMSG #other :bad"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("token with spaces")]
    [InlineData("token\r\nJOIN #attacker")]
    [InlineData("token\u001bPING :attacker")]
    public void UnsafeOAuthTokenIsRejected(string input) =>
        Assert.Throws<ArgumentException>(() => TwitchIrcValidation.NormalizeOAuthToken(input));
}
