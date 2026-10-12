namespace VtuberBot.Core;

/// <summary>Shared bounded exponential backoff for optional external connections.</summary>
public static class ReconnectBackoff
{
    /// <summary>Returns 1, 2, 4, 8, 16, then 30 seconds for attempts one and above.</summary>
    public static TimeSpan GetDelay(int failedAttempt)
    {
        if (failedAttempt < 1)
            throw new ArgumentOutOfRangeException(nameof(failedAttempt), "El intento fallido debe empezar en 1.");
        var seconds = failedAttempt >= 6 ? 30 : 1 << (failedAttempt - 1);
        return TimeSpan.FromSeconds(seconds);
    }
}
