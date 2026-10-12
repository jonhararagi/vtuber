using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarParallaxPolicyTests
{
    [Fact]
    public void PointerPositionIsNormalizedAndClamped()
    {
        Assert.Equal(-1, AvatarParallaxPolicy.NormalizePointer(0, 100));
        Assert.Equal(0, AvatarParallaxPolicy.NormalizePointer(50, 100));
        Assert.Equal(1, AvatarParallaxPolicy.NormalizePointer(100, 100));
        Assert.Equal(-1, AvatarParallaxPolicy.NormalizePointer(-500, 100));
        Assert.Equal(1, AvatarParallaxPolicy.NormalizePointer(500, 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(double.NaN, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(100, double.NaN)]
    [InlineData(100, double.PositiveInfinity)]
    public void InvalidViewportProducesNoParallax(double coordinate, double extent) =>
        Assert.Equal(0, AvatarParallaxPolicy.NormalizePointer(coordinate, extent));

    [Fact]
    public void BackgroundAndForegroundMoveInOppositeDirections()
    {
        var background = new AvatarLayer(1, "Fondo", "background.png", 0, true);
        var foreground = new AvatarLayer(2, "Pelo", "hair.png", 3, true);
        var backgroundOffset = AvatarParallaxPolicy.GetOffset(1, AvatarParallaxPolicy.GetLayerFactor(background));
        var foregroundOffset = AvatarParallaxPolicy.GetOffset(1, AvatarParallaxPolicy.GetLayerFactor(foreground));

        Assert.True(backgroundOffset < 0);
        Assert.True(foregroundOffset > 0);
        Assert.True(Math.Abs(backgroundOffset) < Math.Abs(foregroundOffset));
        Assert.Equal(0, AvatarParallaxPolicy.GetLayerFactor(null));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.5, 2.5)]
    [InlineData(5, 5)]
    [InlineData(20, 5)]
    [InlineData(-2, 0)]
    public void ConfigurableStrengthIsBounded(double requestedStrength, double expectedStrength) =>
        Assert.Equal(expectedStrength,
            AvatarParallaxPolicy.GetOffset(1, 1, requestedStrength));

    [Fact]
    public void OffsetsNeverExceedTheBoundedRange()
    {
        Assert.Equal(AvatarParallaxPolicy.MaximumOffsetPixels,
            AvatarParallaxPolicy.GetOffset(10, 10));
        Assert.Equal(-AvatarParallaxPolicy.MaximumOffsetPixels,
            AvatarParallaxPolicy.GetOffset(-10, 10));
        Assert.Equal(0, AvatarParallaxPolicy.GetOffset(double.NaN, 1));
    }
}
