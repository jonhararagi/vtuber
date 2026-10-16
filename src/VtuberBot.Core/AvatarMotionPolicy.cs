namespace VtuberBot.Core;

/// <summary>Small, bounded motion policies for a lightweight 2D PNGTuber overlay.</summary>
public static class AvatarMotionPolicy
{
    public const double MaximumShakePixels = 18;
    public const double MaximumBouncePixels = 12;

    public static double NormalizeShakeStrength(double strength) =>
        double.IsFinite(strength) ? Math.Clamp(strength, 0, MaximumShakePixels) : 0;

    public static double GetShakeOffset(double phase, double strength)
    {
        if (!double.IsFinite(phase))
            return 0;

        var normalizedStrength = NormalizeShakeStrength(strength);
        return Math.Sin(phase * 2.6) * normalizedStrength;
    }

    public static double GetTalkingBounceOffset(double phase, bool enabled, double strength = 4.5)
    {
        if (!enabled || !double.IsFinite(phase) || !double.IsFinite(strength))
            return 0;

        return Math.Sin(phase * 1.15) * Math.Clamp(strength, 0, MaximumBouncePixels);
    }

    public static double GetIdleFloatOffset(double phase, bool enabled, double strength = 2)
    {
        if (!enabled || !double.IsFinite(phase) || !double.IsFinite(strength))
            return 0;

        return Math.Sin(phase * 0.72) * Math.Clamp(strength, 0, 6);
    }
}
