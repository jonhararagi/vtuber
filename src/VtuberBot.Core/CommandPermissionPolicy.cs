namespace VtuberBot.Core;

/// <summary>
/// Enforces the minimum Twitch role configured for a custom command.
/// Role signals must come from parsed server-supplied IRCv3 tags, not from chat text.
/// </summary>
public static class CommandPermissionPolicy
{
    private static readonly HashSet<string> SupportedScopes =
        new(StringComparer.OrdinalIgnoreCase) { "everyone", "subscriber", "vip", "moderator", "broadcaster" };

    public static bool IsRecognizedScope(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && SupportedScopes.Contains(scope.Trim());

    public static bool IsAllowed(string? requiredScope, IEnumerable<string>? userRoles)
    {
        if (!IsRecognizedScope(requiredScope))
            return false;

        var roles = userRoles is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : userRoles.Select(role => role.Trim()).Where(role => role.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Contains("broadcaster"))
            return true;

        return requiredScope!.Trim().ToLowerInvariant() switch
        {
            "everyone" => true,
            "subscriber" => roles.Contains("subscriber") || roles.Contains("moderator"),
            "vip" => roles.Contains("vip") || roles.Contains("moderator"),
            "moderator" => roles.Contains("moderator"),
            "broadcaster" => false,
            _ => false
        };
    }

    public static IReadOnlySet<string> GetTwitchRoles(TwitchIrcMessage message, string channelLogin)
    {
        ArgumentNullException.ThrowIfNull(message);
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (message.Tags.TryGetValue("mod", out var moderatorTag) && moderatorTag == "1")
            roles.Add("moderator");
        if (message.Tags.TryGetValue("subscriber", out var subscriberTag) && subscriberTag == "1")
            roles.Add("subscriber");
        if (message.Tags.TryGetValue("vip", out var vipTag) && vipTag == "1")
            roles.Add("vip");

        if (message.Tags.TryGetValue("badges", out var badges))
        {
            foreach (var badge in badges.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = badge.Split('/', 2)[0].Trim().ToLowerInvariant();
                if (name is "broadcaster" or "moderator" or "vip" or "subscriber")
                    roles.Add(name);
            }
        }

        var login = (message.Prefix ?? string.Empty).Split('!', 2)[0];
        if (!string.IsNullOrWhiteSpace(channelLogin) &&
            string.Equals(login, channelLogin.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase))
            roles.Add("broadcaster");

        return roles;
    }
}
