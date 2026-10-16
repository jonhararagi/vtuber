using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class BorderConnectedMatteProcessorTests
{
    [Fact]
    public void RemovesWhiteBackgroundConnectedToBorderAndPreservesEnclosedWhiteDetail()
    {
        const int width = 7;
        const int height = 7;
        var pixels = Solid(width, height, 255, 255, 255, 255);

        // A dark, opaque face patch disconnects the white eye highlight from the exterior.
        for (var y = 1; y <= 5; y++)
        for (var x = 1; x <= 5; x++)
            Set(pixels, width, x, y, 60, 45, 35, 255);
        Set(pixels, width, 3, 3, 255, 255, 255, 255);

        var removed = BorderConnectedMatteProcessor.RemoveNearWhiteBorder(pixels, width, height);

        Assert.True(removed > 0);
        Assert.Equal((byte)0, GetAlpha(pixels, width, 0, 0));
        Assert.Equal((byte)0, GetAlpha(pixels, width, 6, 4));
        Assert.Equal((byte)255, GetAlpha(pixels, width, 3, 3));
        Assert.Equal((byte)255, GetAlpha(pixels, width, 2, 2));
    }

    [Fact]
    public void SoftensNearWhiteAntiAliasedPixelsConnectedToMatte()
    {
        const int width = 3;
        const int height = 3;
        var pixels = Solid(width, height, 255, 255, 255, 255);
        Set(pixels, width, 1, 1, 230, 230, 230, 255);

        BorderConnectedMatteProcessor.RemoveNearWhiteBorder(pixels, width, height, tolerance: 72);

        Assert.Equal((byte)0, GetAlpha(pixels, width, 0, 0));
        Assert.InRange(GetAlpha(pixels, width, 1, 1), (byte)1, (byte)254);
    }

    [Fact]
    public void RejectsInconsistentPixelBuffer()
    {
        Assert.Throws<ArgumentException>(() =>
            BorderConnectedMatteProcessor.RemoveNearWhiteBorder(new byte[15], 2, 2));
    }

    [Fact]
    public void DoesNotChangeSolidNonWhiteImage()
    {
        const int width = 4;
        const int height = 4;
        var pixels = Solid(width, height, 35, 90, 140, 255);
        var original = (byte[])pixels.Clone();

        Assert.Equal(0, BorderConnectedMatteProcessor.RemoveNearWhiteBorder(pixels, width, height));
        Assert.Equal(original, pixels);
    }

    private static byte[] Solid(int width, int height, byte r, byte g, byte b, byte a)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
            Set(pixels, width, i % width, i / width, r, g, b, a);
        return pixels;
    }

    private static void Set(byte[] pixels, int width, int x, int y, byte r, byte g, byte b, byte a)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = a;
    }

    private static byte GetAlpha(byte[] pixels, int width, int x, int y) =>
        pixels[(y * width + x) * 4 + 3];
}
