using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoiceActivityDetectorTests
{
    [Fact]
    public void LoudSignalActivatesAndQuietSignalReleasesAfterHold()
    {
        var detector = new VoiceActivityDetector(
            thresholdPercent: 25,
            releaseHold: TimeSpan.FromMilliseconds(300),
            smoothingFactor: 0.5);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var loud = detector.Process(80, start);
        Assert.True(loud.IsActive);
        Assert.True(loud.StateChanged);
        Assert.True(loud.SmoothedLevelPercent >= 25);

        var held = detector.Process(0, start.AddMilliseconds(150));
        Assert.True(held.IsActive);
        Assert.False(held.StateChanged);

        var released = detector.Process(0, start.AddMilliseconds(301));
        Assert.False(released.IsActive);
        Assert.True(released.StateChanged);
    }

    [Fact]
    public void QuietSignalDoesNotActivateAndLevelIsSmoothed()
    {
        var detector = new VoiceActivityDetector(
            thresholdPercent: 25,
            releaseHold: TimeSpan.FromMilliseconds(200),
            smoothingFactor: 0.25);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var quiet = detector.Process(4, start);
        Assert.False(quiet.IsActive);
        Assert.Equal(1, quiet.SmoothedLevelPercent);

        var rising = detector.Process(80, start.AddMilliseconds(50));
        Assert.False(rising.IsActive);
        Assert.InRange(rising.SmoothedLevelPercent, 20, 21);
    }

    [Fact]
    public void ResetClearsActiveStateAndSmoothedLevel()
    {
        var detector = new VoiceActivityDetector(
            thresholdPercent: 20,
            releaseHold: TimeSpan.FromSeconds(1),
            smoothingFactor: 1);
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        Assert.True(detector.Process(80, now).IsActive);
        detector.Reset();

        Assert.False(detector.IsActive);
        var next = detector.Process(0, now.AddSeconds(2));
        Assert.False(next.IsActive);
        Assert.Equal(0, next.SmoothedLevelPercent);
        Assert.False(next.StateChanged);
    }

    [Fact]
    public void BackwardsClockDoesNotKeepTalkingStateActive()
    {
        var detector = new VoiceActivityDetector(
            thresholdPercent: 20,
            releaseHold: TimeSpan.FromSeconds(1),
            smoothingFactor: 1);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:01Z");

        var loud = detector.Process(80, start);
        Assert.True(loud.IsActive);

        var clockMovedBack = detector.Process(0, start.AddSeconds(-2));

        Assert.False(clockMovedBack.IsActive);
        Assert.True(clockMovedBack.StateChanged);
    }

    [Fact]
    public void RejectsInvalidSettingsAndSignalLevels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceActivityDetector(thresholdPercent: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceActivityDetector(smoothingFactor: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceActivityDetector(releaseHold: TimeSpan.FromSeconds(5)));

        var detector = new VoiceActivityDetector();
        Assert.Throws<ArgumentOutOfRangeException>(() => detector.Process(double.PositiveInfinity, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => detector.SetThreshold(0));
    }
}
