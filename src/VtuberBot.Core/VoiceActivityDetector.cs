namespace VtuberBot.Core;

/// <summary>A low-cost acoustic activity detector for normalized microphone level samples.</summary>
/// <remarks>
/// This is a signal-level gate, not speech recognition. It smooths the input and retains the active
/// state briefly after the signal falls below the threshold to avoid flicker in avatar cues.
/// </remarks>
public sealed class VoiceActivityDetector
{
    private readonly object gate = new();
    private readonly double smoothingFactor;
    private double thresholdPercent;
    private TimeSpan releaseHold;
    private double smoothedLevelPercent;
    private DateTimeOffset? lastAboveThreshold;
    private bool isActive;

    public VoiceActivityDetector(
        double thresholdPercent = 8,
        TimeSpan? releaseHold = null,
        double smoothingFactor = 0.35)
    {
        if (!double.IsFinite(smoothingFactor) || smoothingFactor is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(smoothingFactor));
        this.smoothingFactor = smoothingFactor;
        SetThreshold(thresholdPercent);
        SetReleaseHold(releaseHold ?? TimeSpan.FromMilliseconds(350));
    }

    public double ThresholdPercent
    {
        get { lock (gate) return thresholdPercent; }
    }

    public TimeSpan ReleaseHold
    {
        get { lock (gate) return releaseHold; }
    }

    public bool IsActive
    {
        get { lock (gate) return isActive; }
    }

    public void SetThreshold(double value)
    {
        if (!double.IsFinite(value) || value is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "El umbral debe estar entre 1 y 100.");
        lock (gate) thresholdPercent = value;
    }

    public void SetReleaseHold(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(3))
            throw new ArgumentOutOfRangeException(nameof(value), "La retención debe estar entre 0 y 3 segundos.");
        lock (gate) releaseHold = value;
    }

    public VoiceActivitySnapshot Process(double rawLevelPercent, DateTimeOffset timestamp)
    {
        if (!double.IsFinite(rawLevelPercent) || rawLevelPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(rawLevelPercent), "El nivel debe estar entre 0 y 100.");
        lock (gate)
        {
            var previousActive = isActive;
            smoothedLevelPercent += smoothingFactor * (rawLevelPercent - smoothedLevelPercent);
            if (smoothedLevelPercent >= thresholdPercent)
                lastAboveThreshold = timestamp;

            isActive = lastAboveThreshold.HasValue && timestamp - lastAboveThreshold.Value <= releaseHold;
            return new VoiceActivitySnapshot(smoothedLevelPercent, isActive, isActive != previousActive);
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            smoothedLevelPercent = 0;
            lastAboveThreshold = null;
            isActive = false;
        }
    }
}

public sealed record VoiceActivitySnapshot(double SmoothedLevelPercent, bool IsActive, bool StateChanged);
