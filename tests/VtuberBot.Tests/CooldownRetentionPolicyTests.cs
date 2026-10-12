using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class CooldownRetentionPolicyTests
{
    [Fact]
    public void ExpiresOnlyAfterTheConfiguredRetention()
    {
        var recorded = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var retention = TimeSpan.FromMinutes(10);

        Assert.False(CooldownRetentionPolicy.IsExpired(recorded, recorded, retention));
        Assert.False(CooldownRetentionPolicy.IsExpired(recorded, recorded.Add(retention), retention));
        Assert.True(CooldownRetentionPolicy.IsExpired(recorded, recorded.Add(retention).AddTicks(1), retention));
    }

    [Fact]
    public void BackwardsClockDoesNotExpireCooldownEarly()
    {
        var recorded = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        Assert.False(CooldownRetentionPolicy.IsExpired(
            recorded, recorded.AddMinutes(-30), TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void NegativeRetentionIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CooldownRetentionPolicy.IsExpired(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(-1)));
}
