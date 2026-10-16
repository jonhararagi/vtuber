using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsPerformanceStatsTests
{
    [Fact]
    public void CalculatesRenderAndOutputSkippedPercentagesSeparately()
    {
        var stats = new ObsPerformanceStats(30, 8.5, 12, 120, 3, 120);

        Assert.Equal(10, stats.RenderSkippedPercent);
        Assert.Equal(2.5, stats.OutputSkippedPercent);
        Assert.Contains("30.0 FPS", stats.DisplayLabel);
    }

    [Fact]
    public void ZeroTotalFramesDoesNotProduceInvalidPercentages()
    {
        var stats = new ObsPerformanceStats(0, 0, 0, 0, 0, 0);

        Assert.Equal(0, stats.RenderSkippedPercent);
        Assert.Equal(0, stats.OutputSkippedPercent);
    }
}
