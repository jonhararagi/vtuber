namespace VtuberBot.Core;

/// <summary>Combines microphone activity and VTuberBot TTS into the overlay's coarse talk state.</summary>
public static class AvatarTalkStatePolicy
{
    public static string Resolve(bool microphoneActive, bool syntheticSpeechActive) =>
        microphoneActive || syntheticSpeechActive ? "Talking" : "Idle";
}
