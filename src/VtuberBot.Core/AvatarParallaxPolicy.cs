namespace VtuberBot.Core;

/// <summary>Small, bounded cursor-driven offsets for layered PNG avatar compositions.</summary>
public static class AvatarParallaxPolicy
{
    public const double MaximumOffsetPixels = 5;
    public const double BackgroundLayerFactor = -0.35;

    public static double NormalizePointer(double coordinate, double extent)
    {
        if (!double.IsFinite(coordinate) || !double.IsFinite(extent) || extent <= 0)
            return 0;

        return Math.Clamp((coordinate / extent - 0.5) * 2, -1, 1);
    }

    public static double GetLayerFactor(AvatarLayer? layer)
    {
        if (layer is null)
            return 0;
        if (AvatarLayerOrdering.IsBackground(layer))
            return BackgroundLayerFactor;

        return 0.55 + Math.Clamp(layer.SortOrder, 0, 10) * 0.05;
    }

    public static double GetOffset(double normalizedPosition, double layerFactor)
    {
        if (!double.IsFinite(normalizedPosition) || !double.IsFinite(layerFactor))
            return 0;

        return Math.Clamp(normalizedPosition, -1, 1) *
            Math.Clamp(layerFactor, -1, 1.25) *
            MaximumOffsetPixels;
    }
}
