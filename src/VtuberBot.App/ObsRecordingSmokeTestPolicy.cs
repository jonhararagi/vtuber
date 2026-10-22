using System.IO;

namespace VtuberBot.App;

public sealed record ObsRecordingFileSnapshot(string FullPath, long Length, DateTime LastWriteTimeUtc);

/// <summary>
/// Pure policy for a user-confirmed, local OBS recording smoke test. It prevents the test
/// from starting while the user is already streaming/recording and identifies only a new,
/// non-empty supported recording artifact.
/// </summary>
public static class ObsRecordingSmokeTestPolicy
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".mov", ".flv", ".ts", ".m3u8", ".avi", ".webm"
    };

    public static bool CanStart(bool connected, bool streaming, bool recording) =>
        connected && !streaming && !recording;

    public static IReadOnlyList<ObsRecordingFileSnapshot> CaptureDirectorySnapshot(
        string directory,
        int maximumCandidates = 10_000)
    {
        if (maximumCandidates is < 1 or > 50_000)
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates));

        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("OBS indicó una carpeta de grabaciones que no existe.");

        var snapshots = new List<ObsRecordingFileSnapshot>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsSupportedRecordingPath(path))
                continue;

            try
            {
                var file = new FileInfo(path);
                file.Refresh();
                if (file.Exists)
                    snapshots.Add(new ObsRecordingFileSnapshot(file.FullName, file.Length, file.LastWriteTimeUtc));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // A file can be moved/deleted while OBS updates its output directory; it is not a candidate.
            }
        }

        return snapshots
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .Take(maximumCandidates)
            .ToArray();
    }

    public static bool IsSupportedRecordingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return SupportedExtensions.Contains(Path.GetExtension(path));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static ObsRecordingFileSnapshot? FindNewRecording(
        IEnumerable<ObsRecordingFileSnapshot> before,
        IEnumerable<ObsRecordingFileSnapshot> after,
        DateTime startedAtUtc,
        long minimumBytes = 128)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (minimumBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumBytes));

        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in before)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.FullPath))
                continue;
            try { knownPaths.Add(Path.GetFullPath(item.FullPath)); }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException) { }
        }

        return after
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.FullPath))
            .Where(item => IsSupportedRecordingPath(item.FullPath))
            .Where(item => item.Length >= minimumBytes)
            .Where(item => item.LastWriteTimeUtc >= startedAtUtc.AddSeconds(-5))
            .Where(item =>
            {
                try { return !knownPaths.Contains(Path.GetFullPath(item.FullPath)); }
                catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException) { return false; }
            })
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .FirstOrDefault();
    }
}
