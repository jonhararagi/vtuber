namespace VtuberBot.Core;

/// <summary>Identifies transport/subscription diagnostics that deserve priority over viewer alerts.</summary>
public static class EventSubQueuePolicy
{
    public static bool IsCritical(string? type) =>
        string.Equals(type, "subscription_error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "revocation", StringComparison.OrdinalIgnoreCase);
}
