namespace VtuberBot.Core;

/// <summary>Shared wall-clock retention rule for short-lived in-memory cooldown entries.</summary>
public static class CooldownRetentionPolicy
{
    public static bool IsExpired(DateTimeOffset recordedAt, DateTimeOffset now, TimeSpan retention)
    {
        if (retention < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention), "La retención no puede ser negativa.");

        // A system clock adjustment into the past must never expire an active cooldown early.
        return now >= recordedAt && now - recordedAt > retention;
    }
}
