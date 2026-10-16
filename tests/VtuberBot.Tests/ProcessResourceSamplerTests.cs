using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ProcessResourceSamplerTests
{
    [Theory]
    [InlineData(500, 1000, 1, 50)]
    [InlineData(500, 1000, 4, 12.5)]
    [InlineData(250, 1000, 2, 12.5)]
    public void CpuPercentageIsNormalizedToTotalMachineCapacity(
        double cpuMilliseconds,
        double elapsedMilliseconds,
        int processors,
        double expected) =>
        Assert.Equal(expected, ProcessResourceSampler.CalculateCpuPercent(
            TimeSpan.FromMilliseconds(cpuMilliseconds),
            TimeSpan.FromMilliseconds(elapsedMilliseconds),
            processors), precision: 5);

    [Theory]
    [InlineData(0, 1000, 4)]
    [InlineData(-100, 1000, 4)]
    [InlineData(500, 0, 4)]
    [InlineData(500, 1000, 0)]
    public void InvalidSamplingInputReturnsZero(
        double cpuMilliseconds,
        double elapsedMilliseconds,
        int processors) =>
        Assert.Equal(0, ProcessResourceSampler.CalculateCpuPercent(
            TimeSpan.FromMilliseconds(cpuMilliseconds),
            TimeSpan.FromMilliseconds(elapsedMilliseconds),
            processors));

    [Fact]
    public void CpuPercentageIsClampedToTotalCapacity() =>
        Assert.Equal(100, ProcessResourceSampler.CalculateCpuPercent(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            processorCount: 1));
}
