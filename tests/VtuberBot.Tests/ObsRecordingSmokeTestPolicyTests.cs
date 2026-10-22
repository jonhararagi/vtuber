using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsRecordingSmokeTestPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public void OnlyAllowsTestWhenConnectedAndNeitherStreamingNorRecording(
        bool connected, bool streaming, bool recording, bool expected)
    {
        Assert.Equal(expected, ObsRecordingSmokeTestPolicy.CanStart(connected, streaming, recording));
    }

    [Theory]
    [InlineData(@"C:\Recordings\test.mkv", true)]
    [InlineData(@"C:\Recordings\test.mp4", true)]
    [InlineData(@"C:\Recordings\test.flv", true)]
    [InlineData(@"C:\Recordings\test.log", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyAcceptsSupportedRecordingContainers(string? path, bool expected)
    {
        Assert.Equal(expected, ObsRecordingSmokeTestPolicy.IsSupportedRecordingPath(path));
    }

    [Fact]
    public void SnapshotIgnoresNonRecordingFilesAndReadsRecordingLength()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VtuberBot-SmokeTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var moviePath = Path.Combine(directory, "recording.mkv");
            var ignoredPath = Path.Combine(directory, "notes.txt");
            File.WriteAllBytes(moviePath, new byte[512]);
            File.WriteAllText(ignoredPath, "not a recording");

            var result = ObsRecordingSmokeTestPolicy.CaptureDirectorySnapshot(directory);

            var recording = Assert.Single(result);
            Assert.Equal(Path.GetFullPath(moviePath), recording.FullPath);
            Assert.Equal(512, recording.Length);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void SnapshotFailsClearlyInsteadOfGrowingWithoutBound()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VtuberBot-SmokeBound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "one.mkv"), new byte[256]);
            File.WriteAllBytes(Path.Combine(directory, "two.mp4"), new byte[256]);

            var error = Assert.Throws<InvalidOperationException>(() =>
                ObsRecordingSmokeTestPolicy.CaptureDirectorySnapshot(directory, maximumCandidates: 1));

            Assert.Contains("límite seguro", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void FindsOnlyNewSupportedNonEmptyRecordingCreatedDuringTest()
    {
        var started = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var existing = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "old.mkv"), 3_000_000, started.AddSeconds(-30));
        var tooSmall = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "empty.mp4"), 60, started.AddSeconds(1));
        var wrongType = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "diagnostic.txt"), 900, started.AddSeconds(2));
        var created = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "VTuberBot-test.mkv"), 120_000, started.AddSeconds(10));

        var found = ObsRecordingSmokeTestPolicy.FindNewRecording(
            [existing],
            [existing, tooSmall, wrongType, created],
            started);

        Assert.Equal(created.FullPath, found?.FullPath);
        Assert.Equal(created.Length, found?.Length);
    }

    [Fact]
    public void DoesNotTreatExistingFileOrPreTestArtifactAsNew()
    {
        var started = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var existing = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "old.mp4"), 50_000, started.AddSeconds(1));
        var stale = new ObsRecordingFileSnapshot(
            Path.Combine(Path.GetTempPath(), "stale.mkv"), 50_000, started.AddMinutes(-2));

        Assert.Null(ObsRecordingSmokeTestPolicy.FindNewRecording(
            [existing],
            [existing, stale],
            started));
    }
}
