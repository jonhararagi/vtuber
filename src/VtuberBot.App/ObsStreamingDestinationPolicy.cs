namespace VtuberBot.App;

/// <summary>Validated, non-secret presets for the primary OBS RTMPS destination.</summary>
public static class ObsStreamingDestinationPolicy
{
    public const int MaximumStreamKeyCharacters = 8192;

    public static string NormalizePlatform(string platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        return platform.Trim().ToLowerInvariant() switch
        {
            "twitch" => "Twitch",
            "youtube" => "YouTube",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), "Elegí Twitch o YouTube.")
        };
    }

    public static string GetServer(string platform) => NormalizePlatform(platform) switch
    {
        "Twitch" => "rtmps://live.twitch.tv:443/app",
        "YouTube" => "rtmps://a.rtmp.youtube.com:443/live2",
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static string GetSecretName(string platform) => NormalizePlatform(platform) switch
    {
        "Twitch" => "obs-twitch-stream-key",
        "YouTube" => "obs-youtube-stream-key",
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static string ValidateAndNormalizeStreamKey(string streamKey)
    {
        ArgumentNullException.ThrowIfNull(streamKey);
        var normalized = streamKey.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumStreamKeyCharacters)
            throw new ArgumentException("Pegá una clave de transmisión válida.", nameof(streamKey));
        return normalized;
    }
}
