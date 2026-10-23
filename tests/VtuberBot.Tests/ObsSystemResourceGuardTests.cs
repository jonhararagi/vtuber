using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsSystemResourceGuardTests
{
    private const ulong MiB = 1024UL * 1024UL;
    private const ulong GiB = 1024UL * MiB;

    [Fact]
    public void CriticalMemoryIsBelowOneGiB()
    {
        Assert.Equal(ObsSystemResourceRisk.Critical,
            ObsSystemResourceGuard.ClassifyAvailableMemory(GiB - 1));
        Assert.False(ObsSystemResourceGuard.CanStartNewObsInstance(GiB - 1));
    }

    [Fact]
    public void ExactlyOneGiBIsAllowedWithLowMemoryWarningLevel()
    {
        Assert.Equal(ObsSystemResourceRisk.Low,
            ObsSystemResourceGuard.ClassifyAvailableMemory(GiB));
        Assert.True(ObsSystemResourceGuard.CanStartNewObsInstance(GiB));
    }

    [Fact]
    public void MemoryBetweenOneAndOneAndHalfGiBIsClassifiedAsLow()
    {
        Assert.Equal(ObsSystemResourceRisk.Low,
            ObsSystemResourceGuard.ClassifyAvailableMemory((GiB + 1536UL * MiB) / 2));
    }

    [Fact]
    public void AtLeastOneAndAHalfGiBIsNormal()
    {
        Assert.Equal(ObsSystemResourceRisk.Normal,
            ObsSystemResourceGuard.ClassifyAvailableMemory(1536UL * MiB));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    public void LightVideoProfileRequiresConnectedObsAndInactiveOutputs(
        bool connected,
        bool streaming,
        bool recording,
        bool expected)
    {
        Assert.Equal(expected,
            ObsSystemResourceGuard.CanApplyLightProfile(connected, streaming, recording));
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("[::1]", true)]
    [InlineData("192.168.1.30", false)]
    [InlineData("example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SimpleModeOnlyTargetsLoopbackObs(string? host, bool expected) =>
        Assert.Equal(expected, ObsSystemResourceGuard.IsLoopbackHost(host));
}