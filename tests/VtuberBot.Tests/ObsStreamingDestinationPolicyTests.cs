using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsStreamingDestinationPolicyTests
{
    [Theory]
    [InlineData(" Twitch ", "Twitch", "rtmps://live.twitch.tv:443/app", "obs-twitch-stream-key")]
    [InlineData("YOUTUBE", "YouTube", "rtmps://a.rtmp.youtube.com:443/live2", "obs-youtube-stream-key")]
    public void PlatformPresetHasSecureServerAndDedicatedSecretName(
        string input, string normalized, string server, string secretName)
    {
        Assert.Equal(normalized, ObsStreamingDestinationPolicy.NormalizePlatform(input));
        Assert.Equal(server, ObsStreamingDestinationPolicy.GetServer(input));
        Assert.Equal(secretName, ObsStreamingDestinationPolicy.GetSecretName(input));
        Assert.StartsWith("rtmps://", ObsStreamingDestinationPolicy.GetServer(input), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Kick")]
    [InlineData("YouTube/Twitch")]
    public void UnknownPlatformIsRejected(string platform)
    {
        Assert.ThrowsAny<ArgumentException>(() => ObsStreamingDestinationPolicy.NormalizePlatform(platform));
    }

    [Fact]
    public void StreamKeyIsTrimmedBeforePassingToObs()
    {
        Assert.Equal("example-secret-key", ObsStreamingDestinationPolicy.ValidateAndNormalizeStreamKey("  example-secret-key  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyStreamKeyIsRejected(string key)
    {
        Assert.Throws<ArgumentException>(() => ObsStreamingDestinationPolicy.ValidateAndNormalizeStreamKey(key));
    }

    [Fact]
    public void OversizedStreamKeyIsRejected()
    {
        var key = new string('k', ObsStreamingDestinationPolicy.MaximumStreamKeyCharacters + 1);
        Assert.Throws<ArgumentException>(() => ObsStreamingDestinationPolicy.ValidateAndNormalizeStreamKey(key));
    }
}
