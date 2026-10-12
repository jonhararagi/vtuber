namespace VtuberBot.Core;

/// <summary>Resource limits applied before an imported image is decoded for Avatar Studio or the overlay.</summary>
public static class AvatarImageLimits
{
    public const int MaximumDimension = 16_384;
    public const long MaximumPixelCount = 80_000_000;

    public static void Validate(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            throw new InvalidDataException("La imagen no tiene dimensiones válidas.");

        if (pixelWidth > MaximumDimension || pixelHeight > MaximumDimension)
            throw new InvalidDataException(
                $"La imagen supera el límite de {MaximumDimension} píxeles por dimensión.");

        var pixelCount = (long)pixelWidth * pixelHeight;
        if (pixelCount > MaximumPixelCount)
            throw new InvalidDataException(
                $"La imagen contiene {pixelCount:N0} píxeles; el límite es {MaximumPixelCount:N0}.");
    }
}
