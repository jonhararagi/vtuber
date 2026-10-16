namespace VtuberBot.App;

/// <summary>Conservative input-kind filter for sources likely to expose an OBS audio mixer.</summary>
public static class ObsInputKindPolicy
{
    public static bool IsPotentialAudioInput(string? inputKind)
    {
        if (string.IsNullOrWhiteSpace(inputKind))
            return false;

        var kind = inputKind.Trim();
        return kind.Contains("audio", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("wasapi", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("pulse", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("coreaudio", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("alsa", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("jack", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("ffmpeg_source", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("vlc_source", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("browser_source", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("dshow_input", StringComparison.OrdinalIgnoreCase);
    }
}
