namespace VtuberBot.Core;

public readonly record struct AvatarEdgePixel(byte Red, byte Green, byte Blue, byte Alpha);

public sealed record AvatarEdgeAnalysis(
    int SampleCount,
    int TransparentSampleCount,
    int OpaqueWhiteSampleCount)
{
    public bool HasSampledTransparentPixels => TransparentSampleCount > 0;
    public bool HasLikelyOpaqueWhiteBorder =>
        SampleCount >= 20 && OpaqueWhiteSampleCount >= Math.Max(6, (int)Math.Ceiling(SampleCount * 0.2));
}

/// <summary>
/// Pure analysis for sampled border pixels of avatar artwork. It never edits or removes
/// background pixels: white-border findings are warnings for the creator to review.
/// </summary>
public static class AvatarArtworkDiagnosticsPolicy
{
    public const int MaximumSamples = 1024;

    public static AvatarEdgeAnalysis AnalyzeBorder(IEnumerable<AvatarEdgePixel> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var count = 0;
        var transparent = 0;
        var opaqueWhite = 0;
        foreach (var pixel in samples.Take(MaximumSamples))
        {
            count++;
            if (pixel.Alpha <= 16)
                transparent++;

            if (pixel.Alpha >= 240 &&
                pixel.Red >= 245 &&
                pixel.Green >= 245 &&
                pixel.Blue >= 245)
                opaqueWhite++;
        }

        return new AvatarEdgeAnalysis(count, transparent, opaqueWhite);
    }
}
