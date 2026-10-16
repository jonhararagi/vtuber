namespace VtuberBot.Core;

/// <summary>Advisory thresholds for the VTuberBot process working set; this is not a hard memory limit.</summary>
public enum ProcessMemoryWarningLevel
{
    None,
    ApproachingLimit,
    LimitReached
}

public static class ProcessMemoryWarningPolicy
{
    public const long WarningThresholdBytes = 800L * 1024 * 1024;
    public const long LimitThresholdBytes = 1024L * 1024 * 1024;

    public static ProcessMemoryWarningLevel Evaluate(long? workingSetBytes)
    {
        if (workingSetBytes is null or < 0)
            return ProcessMemoryWarningLevel.None;

        if (workingSetBytes.Value >= LimitThresholdBytes)
            return ProcessMemoryWarningLevel.LimitReached;

        if (workingSetBytes.Value >= WarningThresholdBytes)
            return ProcessMemoryWarningLevel.ApproachingLimit;

        return ProcessMemoryWarningLevel.None;
    }
}
