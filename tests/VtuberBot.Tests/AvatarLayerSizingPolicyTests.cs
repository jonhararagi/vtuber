using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarLayerSizingPolicyTests
{
    [Fact]
    public void CroppedMouthUsesScaleOfAvatarCanvasInsteadOfFillingWholeViewport()
    {
        var (width, height) = AvatarLayerSizingPolicy.GetStickerSize(
            artworkPixelWidth: 240,
            artworkPixelHeight: 80,
            canvasPixelWidth: 1000,
            canvasPixelHeight: 2000,
            viewportWidth: 400,
            viewportHeight: 500);

        Assert.Equal(60, width, precision: 6);
        Assert.Equal(20, height, precision: 6);
    }

    [Fact]
    public void DragScaleUsesUniformFitScaleNotIndependentWindowWidthAndHeight()
    {
        const double avatarWidth = 1000;
        const double avatarHeight = 2000;
        var previewScale = AvatarLayerSizingPolicy.GetFitScale(avatarWidth, avatarHeight, 400, 360);
        var overlayScale = AvatarLayerSizingPolicy.GetFitScale(avatarWidth, avatarHeight, 420, 520);

        Assert.Equal(0.18, previewScale, precision: 6);
        Assert.Equal(0.26, overlayScale, precision: 6);
        Assert.Equal(0.26 / 0.18, overlayScale / previewScale, precision: 6);
        Assert.NotEqual(420d / 400d, overlayScale / previewScale);
    }

    [Fact]
    public void PortraitBaseUsesTheUniformScaleThatFitsItsHeight()
    {
        var (width, height) = AvatarLayerSizingPolicy.GetStickerSize(
            artworkPixelWidth: 500,
            artworkPixelHeight: 250,
            canvasPixelWidth: 1000,
            canvasPixelHeight: 2000,
            viewportWidth: 600,
            viewportHeight: 500);

        Assert.Equal(125, width, precision: 6);
        Assert.Equal(62.5, height, precision: 6);
    }

    [Theory]
    [InlineData(0, 20, 100, 100, 400, 400)]
    [InlineData(20, 0, 100, 100, 400, 400)]
    [InlineData(20, 20, 0, 100, 400, 400)]
    [InlineData(20, 20, 100, 100, 0, 400)]
    public void RejectsInvalidDimensions(
        double artworkWidth,
        double artworkHeight,
        double canvasWidth,
        double canvasHeight,
        double viewportWidth,
        double viewportHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AvatarLayerSizingPolicy.GetStickerSize(
                artworkWidth, artworkHeight, canvasWidth, canvasHeight, viewportWidth, viewportHeight));
    }
}
