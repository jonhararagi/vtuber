namespace VtuberBot.Core;

/// <summary>Validates and normalizes Twitch login names before they affect commands or rewards.</summary>
public static class TwitchLoginPolicy
{
    public const int MaximumLength = 25;

    public static bool TryNormalize(string? login, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(login) || login.Length > MaximumLength)
            return false;

        foreach (var character in login)
        {
            if (!(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))
                return false;
        }

        normalized = login.ToLowerInvariant();
        return true;
    }
}
