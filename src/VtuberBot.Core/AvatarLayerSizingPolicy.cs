namespace VtuberBot.Core;

/// <summary>
/// Computes the rendered size of a tightly cropped image piece (for example, a mouth sticker)
/// relative to the reference avatar canvas instead of stretching that piece over the whole window.
/// </summary>
public static class AvatarLayerSizingPolicy
{
    public static (double Width, double Height) GetStickerSize(
        double artworkPixelWidth,
        double artworkPixelHeight,
        double canvasPixelWidth,
        double canvasPixelHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (!double.IsFinite(artworkPixelWidth) || artworkPixelWidth <= 0 ||
            !double.IsFinite(artworkPixelHeight) || artworkPixelHeight <= 0 ||
            !double.IsFinite(canvasPixelWidth) || canvasPixelWidth <= 0 ||
            !double.IsFinite(canvasPixelHeight) || canvasPixelHeight <= 0 ||
            !double.IsFinite(viewportWidth) || viewportWidth <= 0 ||
            !double.IsFinite(viewportHeight) || viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(artworkPixelWidth), "Las dimensiones de la pieza, del avatar y de la vista deben ser positivas y finitas.");

        // The avatar base uses Uniform inside the viewport; apply the exact same scale
        // to the cropped artwork so its real pixel dimensions stay relative to the face.
        var fitScale = GetFitScale(canvasPixelWidth, canvasPixelHeight, viewportWidth, viewportHeight);
        return (Math.Max(1, artworkPixelWidth * fitScale), Math.Max(1, artworkPixelHeight * fitScale));
    }

    public static double GetFitScale(
        double canvasPixelWidth,
        double canvasPixelHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (!double.IsFinite(canvasPixelWidth) || canvasPixelWidth <= 0 ||
            !double.IsFinite(canvasPixelHeight) || canvasPixelHeight <= 0 ||
            !double.IsFinite(viewportWidth) || viewportWidth <= 0 ||
            !double.IsFinite(viewportHeight) || viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(canvasPixelWidth), "Las dimensiones del avatar y de la vista deben ser positivas y finitas.");

        return Math.Min(viewportWidth / canvasPixelWidth, viewportHeight / canvasPixelHeight);
    }
}
