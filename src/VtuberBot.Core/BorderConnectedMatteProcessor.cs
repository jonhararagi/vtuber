namespace VtuberBot.Core;

/// <summary>
/// Removes a near-white matte only when it is connected to the image border.
/// Enclosed white details such as eye highlights, teeth and clothing accents are preserved.
/// Input pixels must be tightly packed BGRA32.
/// </summary>
public static class BorderConnectedMatteProcessor
{
    public static int RemoveNearWhiteBorder(byte[] bgraPixels, int width, int height, byte tolerance = 72)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");

        var pixelCount64 = checked((long)width * height);
        if (pixelCount64 > int.MaxValue || bgraPixels.Length != checked((int)pixelCount64 * 4))
            throw new ArgumentException("The BGRA32 buffer length does not match the image dimensions.", nameof(bgraPixels));
        if (tolerance is < 8 or > 120)
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be between 8 and 120.");

        var pixelCount = (int)pixelCount64;
        var visited = new byte[pixelCount];
        var queue = new int[pixelCount];
        var head = 0;
        var tail = 0;

        bool IsMattePixel(int pixelIndex)
        {
            var offset = pixelIndex * 4;
            if (bgraPixels[offset + 3] == 0)
                return false;

            var blueDistance = 255 - bgraPixels[offset];
            var greenDistance = 255 - bgraPixels[offset + 1];
            var redDistance = 255 - bgraPixels[offset + 2];
            return Math.Max(redDistance, Math.Max(greenDistance, blueDistance)) <= tolerance;
        }

        void TryEnqueue(int x, int y)
        {
            var index = y * width + x;
            if (visited[index] != 0 || !IsMattePixel(index))
                return;

            visited[index] = 1;
            queue[tail++] = index;
        }

        for (var x = 0; x < width; x++)
        {
            TryEnqueue(x, 0);
            if (height > 1) TryEnqueue(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            TryEnqueue(0, y);
            if (width > 1) TryEnqueue(width - 1, y);
        }

        while (head < tail)
        {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;
            if (x > 0) TryEnqueue(x - 1, y);
            if (x + 1 < width) TryEnqueue(x + 1, y);
            if (y > 0) TryEnqueue(x, y - 1);
            if (y + 1 < height) TryEnqueue(x, y + 1);
        }

        var hardCutoff = Math.Max(4, tolerance / 5);
        var featherSpan = Math.Max(1, tolerance - hardCutoff);
        var removedPixels = 0;
        for (var i = 0; i < tail; i++)
        {
            var index = queue[i];
            var offset = index * 4;
            var distance = Math.Max(
                255 - bgraPixels[offset + 2],
                Math.Max(255 - bgraPixels[offset + 1], 255 - bgraPixels[offset]));

            if (distance <= hardCutoff)
            {
                bgraPixels[offset + 3] = 0;
            }
            else
            {
                var originalAlpha = bgraPixels[offset + 3];
                var factor = Math.Clamp((distance - hardCutoff) / (double)featherSpan, 0d, 1d);
                bgraPixels[offset + 3] = (byte)Math.Round(originalAlpha * factor);
            }

            removedPixels++;
        }

        return removedPixels;
    }
}
