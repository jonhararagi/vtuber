namespace VtuberBot.Core;

/// <summary>Validates user-supplied values before they are written into Twitch IRC protocol lines.</summary>
public static class TwitchIrcValidation
{
    public static string NormalizeChannelLogin(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Ingresá el nombre del canal de Twitch.", nameof(channel));

        var normalized = channel.Trim().TrimStart('#').ToLowerInvariant();
        if (normalized.Length is < 1 or > 25 ||
            normalized.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
        {
            throw new ArgumentException(
                "El canal debe tener entre 1 y 25 letras ASCII, números o guiones bajos, sin espacios ni saltos de línea.",
                nameof(channel));
        }

        return normalized;
    }

    public static string NormalizeOAuthToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Ingresá un token OAuth de Twitch.", nameof(token));

        var normalized = token.Trim();
        if (normalized.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[6..];

        if (normalized.Length == 0 ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                "El token OAuth está vacío o contiene espacios/caracteres de control no permitidos.",
                nameof(token));
        }

        return normalized;
    }
}
