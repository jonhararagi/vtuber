namespace VtuberBot.Core;

/// <summary>Combines microphone activity and VTuberBot TTS into the overlay's coarse talk state.</summary>
public static class AvatarTalkStatePolicy
{
    public static string Resolve(bool microphoneActive, bool syntheticSpeechActive) =>
        microphoneActive || syntheticSpeechActive ? "Talking" : "Idle";

    /// <summary>Restricts the PNG avatar renderer to the two user-approved visual states.</summary>
    public static string NormalizeVisualState(string? state) =>
        string.Equals(state, "Talking", StringComparison.OrdinalIgnoreCase) ? "Talking" : "Idle";
}
