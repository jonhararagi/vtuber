namespace VtuberBot.Core;

/// <summary>Advisory thresholds for CPU consumed by VTuberBot, normalized to total machine capacity.</summary>
public enum ProcessCpuWarningLevel
{
    Unavailable,
    Normal,
    HighLoad,
    Critical
}

/// <summary>
/// Warns when the VTuberBot process is consuming a large share of all logical CPU capacity.
/// This is an advisory signal, not a scheduler/CPU cap and excludes OBS and other processes.
/// </summary>
public static class ProcessCpuWarningPolicy
{
    public const double WarningThresholdPercent = 80d;
    public const double CriticalThresholdPercent = 95d;

    public static ProcessCpuWarningLevel Evaluate(double? cpuPercent)
    {
        if (cpuPercent is not double value || !double.IsFinite(value) || value < 0)
            return ProcessCpuWarningLevel.Unavailable;

        var normalized = Math.Clamp(value, 0d, 100d);
        if (normalized >= CriticalThresholdPercent)
            return ProcessCpuWarningLevel.Critical;
        if (normalized >= WarningThresholdPercent)
            return ProcessCpuWarningLevel.HighLoad;
        return ProcessCpuWarningLevel.Normal;
    }
}
