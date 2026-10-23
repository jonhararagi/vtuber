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
    [InlineData(0d, ObsSystemCpuRisk.Normal)]
    [InlineData(79.9d, ObsSystemCpuRisk.Normal)]
    [InlineData(80d, ObsSystemCpuRisk.High)]
    [InlineData(94.9d, ObsSystemCpuRisk.High)]
    [InlineData(95d, ObsSystemCpuRisk.Critical)]
    [InlineData(100d, ObsSystemCpuRisk.Critical)]
    [InlineData(-1d, ObsSystemCpuRisk.Unavailable)]
    [InlineData(101d, ObsSystemCpuRisk.Unavailable)]
    [InlineData(double.NaN, ObsSystemCpuRisk.Unavailable)]
    [InlineData(double.PositiveInfinity, ObsSystemCpuRisk.Unavailable)]
    public void ClassifiesSystemCpuUsage(double usagePercent, ObsSystemCpuRisk expected) =>
        Assert.Equal(expected, ObsSystemResourceGuard.ClassifySystemCpuUsage(usagePercent));

    [Fact]
    public void CalculatesSystemCpuUsageFromGetSystemTimesDeltas()
    {
        var usage = ObsSystemResourceGuard.CalculateSystemCpuUsagePercent(
            idleDelta: 75,
            kernelDelta: 80,
            userDelta: 20);

        Assert.NotNull(usage);
        Assert.Equal(25d, usage.Value, precision: 5);
    }

    [Theory]
    [InlineData(10UL, 0UL, 0UL)]
    [InlineData(110UL, 80UL, 20UL)]
    public void RejectsInvalidSystemCpuCounters(ulong idleDelta, ulong kernelDelta, ulong userDelta) =>
        Assert.Null(ObsSystemResourceGuard.CalculateSystemCpuUsagePercent(idleDelta, kernelDelta, userDelta));

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