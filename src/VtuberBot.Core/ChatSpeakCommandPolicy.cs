namespace VtuberBot.Core;

public sealed record AvatarActionCommand(string Command, string Expression);
public sealed record ParsedChatSpeakOutput(string SpeechText, IReadOnlyList<AvatarActionCommand> Commands);

/// <summary>Allowlist of avatar-only directives; it intentionally is not an application command router.</summary>
public static class ChatSpeakCommandPolicy
{
    private static readonly Dictionary<string, (string Command, string Expression)> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["angri"] = ("/angri", "Angry"),
        ["angry"] = ("/angri", "Angry"),
        ["happy"] = ("/happy", "Happy"),
        ["surprised"] = ("/surprised", "Surprised"),
        ["idle"] = ("/idle", "Idle"),
        ["neutral"] = ("/idle", "Idle"),
        ["talking"] = ("/talking", "Talking"),
        ["blink"] = ("/blink", "Blink"),
        ["left eye close"] = ("/left eye close", "LeftEyeClose"),
        ["left-eye-close"] = ("/left eye close", "LeftEyeClose"),
        ["left_eye_close"] = ("/left eye close", "LeftEyeClose")
    };

    public static bool TryParseAvatarCommand(string? input, out AvatarActionCommand command)
    {
        command = new("", "");
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();
        if (!text.StartsWith('/') || text.Length < 2 || text.StartsWith("//", StringComparison.Ordinal)) return false;
        var name = string.Join(' ', text[1..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (!Commands.TryGetValue(name, out var value))
        {
            name = string.Join(' ', text[1..].Split([' ', '\t', '-', '_'], StringSplitOptions.RemoveEmptyEntries));
            if (!Commands.TryGetValue(name, out value)) return false;
        }
        command = new(value.Command, value.Expression);
        return true;
    }

    /// <summary>Only exact, whole-line allowlisted slash directives can reach the avatar.</summary>
    public static ParsedChatSpeakOutput ParseAssistantOutput(string? output)
    {
        var speech = new List<string>();
        var commands = new List<AvatarActionCommand>();
        foreach (var rawLine in (output ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('/'))
            {
                if (commands.Count < 4 && TryParseAvatarCommand(line, out var action)) commands.Add(action);
                // Never speak arbitrary slash commands and never execute creator-only commands from AI output.
                continue;
            }
            speech.Add(line);
        }
        return new ParsedChatSpeakOutput(string.Join(" ", speech).Trim(), commands);
    }

    public static string GetPromptInstructions() =>
        "Podés solicitar cambios visuales del avatar colocando una directiva exacta en una línea separada: /angri, /happy, /surprised, /idle, /talking, /blink o /left eye close. No pongas esas directivas en el texto hablado ni emitas otros comandos. Nunca podés cambiar de chat, reiniciar conversaciones, activar/desactivar el bot ni controlar archivos, navegador o servicios. Esas operaciones son exclusivas del creador. Los sprites Angry y LeftEyeClose deben estar configurados en Avatar Studio.";
}
