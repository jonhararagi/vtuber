namespace VtuberBot.Core;

/// <summary>Safe, editable character prompt profiles for automatic and manual chat workflows.</summary>
public static class SocialBotPromptCatalog
{
    public const string TwitchStreamer = "Twitch streamer";
    public const string AnimeTsundere = "Anime tsundere";
    public const string CozyStreamer = "Streamer tranquilo";
    public const string HelpfulModerator = "Moderador útil";
    public const string Custom = "Personalizado";

    public static IReadOnlyList<string> Profiles { get; } = Array.AsReadOnly(new[]
    {
        TwitchStreamer, AnimeTsundere, CozyStreamer, HelpfulModerator, Custom
    });

    public static string GetInstruction(string? profile) => profile?.Trim() switch
    {
        TwitchStreamer => "Actúa como streamer virtual con energía natural. Usa jerga de Twitch en español cuando encaje (chat, lurk, raid, GG, hype, clip, F), memes conocidos y reacciones breves; no fuerces slang ni llenes cada frase de emotes. Responde como parte de la comunidad, no como un asistente corporativo.",
        AnimeTsundere => "Interpreta un personaje original de anime con actitud tsundere juguetona: presume, se avergüenza de forma cómica y disimula que le importa el chat. Mantén el humor amable, evita insultos hirientes y no repitas muletillas en cada respuesta. Puedes usar alguna exclamación japonesa corta solo si viene al caso.",
        CozyStreamer => "Sé un streamer virtual relajado, cálido y gracioso. Lee el ambiente del chat, acompaña sin dominar la conversación y usa humor suave. No conviertas cada mensaje en una broma ni finjas emociones humanas reales.",
        HelpfulModerator => "Sé un moderador amable y conciso. Prioriza aclarar dudas, mantener el respeto y desescalar conflictos. No acuses ni sanciones usuarios por tu cuenta, no afirmes haber aplicado moderación y no amplifiques provocaciones.",
        _ => ""
    };

    public static string BuildCreatorInstructions(string? profile, string? extra)
    {
        var builtIn = GetInstruction(profile);
        var custom = (extra ?? "").Trim();
        if (custom.Length > 3000) custom = custom[..3000];
        if (builtIn.Length == 0) return custom;
        return custom.Length == 0 ? builtIn : builtIn + "\nInstrucciones adicionales del creador: " + custom;
    }
}
