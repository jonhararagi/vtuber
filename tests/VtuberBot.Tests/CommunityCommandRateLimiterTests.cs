using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class CommunityCommandRateLimiterTests
{
    [Fact]
    public void RejectsRepeatedCommandFromSameUserDuringCooldown()
    {
        var limiter = new CommunityCommandRateLimiter();
        var now = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        Assert.True(limiter.TryAcquire("puntos", "Viewer", now));
        Assert.False(limiter.TryAcquire("top", "viewer", now.AddSeconds(1)));
        Assert.True(limiter.TryAcquire("top", "VIEWER", now.AddSeconds(6)));
    }

    [Fact]
    public void EnforcesGlobalBurstAndOneMinuteBudget()
    {
        var limiter = new CommunityCommandRateLimiter(maximumPerMinute: 3);
        var now = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        Assert.True(limiter.TryAcquire("puntos", "one", now));
        Assert.True(limiter.TryAcquire("top", "two", now.AddSeconds(1)));
        Assert.True(limiter.TryAcquire("jugar", "three", now.AddSeconds(2)));
        Assert.False(limiter.TryAcquire("puntos", "four", now.AddSeconds(3)));
        Assert.True(limiter.TryAcquire("puntos", "four", now.AddMinutes(1).AddSeconds(1)));
    }

    [Theory]
    [InlineData("", "viewer")]
    [InlineData("puntos", "")]
    [InlineData("puntos\nignored", "viewer")]
    [InlineData("puntos", "viewer\u0001")]
    public void RejectsInvalidCommandOrUsername(string command, string username)
    {
        var limiter = new CommunityCommandRateLimiter();
        Assert.False(limiter.TryAcquire(command, username, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ClampsExtremeConfigurationToSafeBounds()
    {
        var limiter = new CommunityCommandRateLimiter(maximumPerMinute: 10000, perUserCooldown: TimeSpan.Zero,
            minimumGlobalInterval: TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 60; i++)
            Assert.True(limiter.TryAcquire("puntos", "viewer-" + i, now.AddMilliseconds(i)));
        Assert.False(limiter.TryAcquire("puntos", "viewer-60", now.AddMilliseconds(100)));
    }
}
