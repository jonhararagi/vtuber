using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoicevoxEngineLaunchPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:50021", 50021)]
    [InlineData("http://localhost:50033", 50033)]
    [InlineData("http://[::1]:50042", 50042)]
    public void AcceptsLoopbackAndCanonicalizesToIpv4Loopback(string input, int expectedPort)
    {
        Assert.True(VoicevoxEngineLaunchPolicy.TryParseBaseUrl(input, out var endpoint));
        Assert.Equal("127.0.0.1", endpoint.Host);
        Assert.Equal(expectedPort, endpoint.Port);
        Assert.Equal($"http://127.0.0.1:{expectedPort}", endpoint.BaseUrl);
    }

    [Theory]
    [InlineData("https://127.0.0.1:50021")]
    [InlineData("http://192.168.1.5:50021")]
    [InlineData("http://example.com:50021")]
    [InlineData("http://127.0.0.1:50021/path")]
    [InlineData("http://127.0.0.1:50021/?token=secret")]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsRemoteNonHttpOrDecoratedEndpoints(string? input) =>
        Assert.False(VoicevoxEngineLaunchPolicy.TryParseBaseUrl(input, out _));
}
