using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarImageLimitsTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(7680, 4320)]
    [InlineData(10000, 8000)]
    public void AcceptsCommonStreamingArtworkDimensions(int width, int height) =>
        AvatarImageLimits.Validate(width, height);

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    [InlineData(16385, 1)]
    [InlineData(1, 16385)]
    [InlineData(10000, 10000)]
    public void RejectsInvalidOrExcessiveDimensions(int width, int height) =>
        Assert.Throws<InvalidDataException>(() => AvatarImageLimits.Validate(width, height));

    [Fact]
    public void AcceptsExactlyThePixelBudget()
    {
        AvatarImageLimits.Validate(10000, 8000);
    }
}
