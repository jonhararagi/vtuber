using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ReconnectBackoffTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 30)]
    [InlineData(7, 30)]
    [InlineData(100, 30)]
    public void DelayGrowsExponentiallyAndIsBounded(int failedAttempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ReconnectBackoff.GetDelay(failedAttempt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsAttemptNumbersBelowOne(int failedAttempt)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReconnectBackoff.GetDelay(failedAttempt));
    }
}
