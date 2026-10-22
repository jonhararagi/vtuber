using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarArtworkDiagnosticsPolicyTests
{
    [Fact]
    public void DetectsTransparentPixelsOnSampledBorder()
    {
        var pixels = Enumerable.Repeat(new AvatarEdgePixel(0, 0, 0, 0), 40);
        var result = AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(pixels);

        Assert.Equal(40, result.SampleCount);
        Assert.Equal(40, result.TransparentSampleCount);
        Assert.True(result.HasSampledTransparentPixels);
        Assert.False(result.HasLikelyOpaqueWhiteBorder);
    }

    [Fact]
    public void FlagsLikelyOpaqueWhiteBackgroundWithoutEditingArtwork()
    {
        var pixels = Enumerable.Repeat(new AvatarEdgePixel(255, 255, 255, 255), 100);
        var result = AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(pixels);

        Assert.Equal(100, result.SampleCount);
        Assert.Equal(100, result.OpaqueWhiteSampleCount);
        Assert.True(result.HasLikelyOpaqueWhiteBorder);
    }

    [Fact]
    public void DoesNotFlagAUsualColoredOrTransparentBorderAsWhite()
    {
        var pixels = Enumerable.Repeat(new AvatarEdgePixel(40, 80, 150, 255), 100)
            .Concat(Enumerable.Repeat(new AvatarEdgePixel(255, 255, 255, 10), 15));
        var result = AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(pixels);

        Assert.Equal(0, result.OpaqueWhiteSampleCount);
        Assert.True(result.HasSampledTransparentPixels);
        Assert.False(result.HasLikelyOpaqueWhiteBorder);
    }

    [Fact]
    public void BoundsAnalysisWhenGivenAnUnboundedSampleSequence()
    {
        var result = AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(
            Enumerable.Repeat(new AvatarEdgePixel(0, 0, 0, 255), 10000));

        Assert.Equal(AvatarArtworkDiagnosticsPolicy.MaximumSamples, result.SampleCount);
    }

    [Fact]
    public void RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(null!));
    }
}
