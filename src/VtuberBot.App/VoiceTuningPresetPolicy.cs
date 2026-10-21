namespace VtuberBot.App;

public sealed record VoiceTuningProfile(double PitchScale, double IntonationScale, int Rate, int Volume);

public sealed record VoiceTuningPreset(string Id, string Name, double PitchScale, double IntonationScale, int Rate);

/// <summary>
/// Presets and validation for per-VOICEVOX-style tuning. Values are parameters to the
/// neural synthesizer, not post-processing that would add a metallic Windows-SAPI effect.
/// </summary>
public static class VoiceTuningPresetPolicy
{
    private static readonly VoiceTuningPreset[] presets =
    [
        new("natural", "Natural / equilibrada", 0.00, 1.00, 0),
        new("anime-high", "Anime aguda", 0.18, 1.28, 1),
        new("anime-low", "Anime grave", -0.14, 0.90, -1),
        new("anime-energy", "Anime enérgica", 0.22, 1.42, 2)
    ];

    public static IReadOnlyList<VoiceTuningPreset> Presets => presets;

    public static VoiceTuningPreset GetPreset(string? id) =>
        presets.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? presets[0];

    public static VoiceTuningProfile FromPreset(VoiceTuningPreset preset, int volume = 100)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return Normalize(new VoiceTuningProfile(preset.PitchScale, preset.IntonationScale, preset.Rate, volume));
    }

    public static VoiceTuningProfile Normalize(VoiceTuningProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var pitch = double.IsFinite(profile.PitchScale) ? Math.Clamp(profile.PitchScale, -0.3, 0.3) : 0d;
        var intonation = double.IsFinite(profile.IntonationScale) ? Math.Clamp(profile.IntonationScale, 0.5, 1.5) : 1d;
        return new VoiceTuningProfile(pitch, intonation, Math.Clamp(profile.Rate, -10, 10), Math.Clamp(profile.Volume, 0, 100));
    }

    public static string GetSettingKey(string? voiceId)
    {
        if (!VoicevoxSpeechSession.IsVoiceId(voiceId) ||
            !int.TryParse(voiceId![VoicevoxSpeechSession.VoiceIdPrefix.Length..],
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var speakerId))
            throw new ArgumentException("Se necesita un ID de estilo VOICEVOX válido.", nameof(voiceId));

        return "audio.voicevox.profile." + speakerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static VoiceTuningProfile? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var profile = System.Text.Json.JsonSerializer.Deserialize<VoiceTuningProfile>(json);
            return profile is null ? null : Normalize(profile);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static string Serialize(VoiceTuningProfile profile) =>
        System.Text.Json.JsonSerializer.Serialize(Normalize(profile));
}
