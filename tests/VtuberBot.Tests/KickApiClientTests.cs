using Xunit;
using VtuberBot.App;

namespace VtuberBot.Tests;

public sealed class KickApiClientTests
{
    [Theory]
    [InlineData(" Creator ", "creator")]
    [InlineData("@Channel_Name", "channel_name")]
    [InlineData("#MiXeD-Channel", "mixed-channel")]
    [InlineData("https://kick.com/MyChannel", "mychannel")]
    [InlineData("https://www.kick.com/MyChannel/", "mychannel")]
    [InlineData("https://kick.com/FirstChannel/extra/path", "firstchannel")]
    public void NormalizeChannelSlugAcceptsSupportedForms(string input, string expected)
    {
        Assert.Equal(expected, KickApiClient.NormalizeChannelSlug(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("channel!")]
    [InlineData("https://example.com/channel")]
    [InlineData("https://sub.kick.com/channel")]
    [InlineData("12345678901234567890123456")]
    public void NormalizeChannelSlugRejectsInvalidInputs(string input)
    {
        Assert.Throws<ArgumentException>(() => KickApiClient.NormalizeChannelSlug(input));
    }

    [Fact]
    public void NormalizeChannelSlugRejectsAnotherSiteBeforeParsingPath()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => KickApiClient.NormalizeChannelSlug("https://example.net/real-channel"));

        Assert.Contains("kick.com", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
