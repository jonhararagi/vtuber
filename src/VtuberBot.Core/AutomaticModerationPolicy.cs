namespace VtuberBot.Core;

/// <summary>Explains whether a matched automatic moderation rule is allowed to take effect.</summary>
public enum AutomaticModerationDecision
{
    Execute,
    LogOnly,
    Disabled,
    ProtectedUser,
    MissingMessageId,
    Cooldown,
    UnsupportedAction
}

/// <summary>
/// Safety gate shared by live moderation handling and unit tests. It intentionally protects
/// broadcasters and moderators from automatic sanctions, even when global automation is enabled.
/// </summary>
public static class AutomaticModerationPolicy
{
    private static readonly HashSet<string> SupportedActions =
        new(StringComparer.OrdinalIgnoreCase) { "flag", "warn", "delete", "timeout", "ban" };

    public static bool IsSupportedAction(string? action) =>
        !string.IsNullOrWhiteSpace(action) && SupportedActions.Contains(action.Trim());

    public static bool IsProtectedUser(IEnumerable<string>? roles)
    {
        if (roles is null) return false;
        return roles.Any(role =>
            string.Equals(role.Trim(), "broadcaster", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role.Trim(), "moderator", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ShouldSuppressMessageSideEffects(AutomaticModerationDecision decision) =>
        decision is AutomaticModerationDecision.Execute
            or AutomaticModerationDecision.MissingMessageId
            or AutomaticModerationDecision.Cooldown;

    public static AutomaticModerationDecision Evaluate(
        string? action,
        bool globallyEnabled,
        IEnumerable<string>? userRoles,
        string? messageId,
        DateTimeOffset? lastSuccessfulActionUtc,
        DateTimeOffset nowUtc,
        TimeSpan cooldown)
    {
        if (!IsSupportedAction(action))
            return AutomaticModerationDecision.UnsupportedAction;
        if (string.Equals(action!.Trim(), "flag", StringComparison.OrdinalIgnoreCase))
            return AutomaticModerationDecision.LogOnly;
        if (!globallyEnabled)
            return AutomaticModerationDecision.Disabled;
        if (IsProtectedUser(userRoles))
            return AutomaticModerationDecision.ProtectedUser;
        if (string.Equals(action.Trim(), "delete", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(messageId))
            return AutomaticModerationDecision.MissingMessageId;
        if (cooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        if (lastSuccessfulActionUtc.HasValue &&
            nowUtc - lastSuccessfulActionUtc.Value < cooldown)
            return AutomaticModerationDecision.Cooldown;

        return AutomaticModerationDecision.Execute;
    }
}
