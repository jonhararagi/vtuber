namespace VtuberBot.Core;

/// <summary>Deterministic scheduling rule for periodic stream messages.</summary>
public static class AutomationSchedulePolicy
{
    public static bool IsDue(DateTimeOffset nowUtc, DateTimeOffset? lastRunUtc, DateTimeOffset firstObservedUtc, int intervalMinutes)
    {
        if (intervalMinutes is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(intervalMinutes), "El intervalo debe estar entre 1 y 1.440 minutos.");

        var baseline = lastRunUtc ?? firstObservedUtc;
        if (nowUtc < baseline)
            return false;

        return nowUtc - baseline >= TimeSpan.FromMinutes(intervalMinutes);
    }
}
