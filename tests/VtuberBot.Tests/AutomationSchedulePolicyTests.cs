using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AutomationSchedulePolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstBroadcastWaitsFullConfiguredIntervalFromFirstObservation()
    {
        Assert.False(AutomationSchedulePolicy.IsDue(Start, null, Start, 30));
        Assert.False(AutomationSchedulePolicy.IsDue(Start.AddMinutes(29), null, Start, 30));
        Assert.True(AutomationSchedulePolicy.IsDue(Start.AddMinutes(30), null, Start, 30));
    }

    [Fact]
    public void SubsequentBroadcastUsesLastSuccessfulRun()
    {
        Assert.False(AutomationSchedulePolicy.IsDue(Start.AddMinutes(4), Start, Start.AddDays(-1), 5));
        Assert.True(AutomationSchedulePolicy.IsDue(Start.AddMinutes(5), Start, Start.AddDays(-1), 5));
    }

    [Fact]
    public void ClockMovingBackwardsNeverMakesTimerDueEarly()
    {
        Assert.False(AutomationSchedulePolicy.IsDue(Start.AddMinutes(-1), Start, Start, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void InvalidIntervalsAreRejected(int interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AutomationSchedulePolicy.IsDue(Start, null, Start, interval));
    }
}
