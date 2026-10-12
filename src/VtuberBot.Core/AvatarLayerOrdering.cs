namespace VtuberBot.Core;

/// <summary>
/// Defines the visual order for avatar layers. A layer named "Fondo" is rendered below
/// the base character; all other layers are rendered above it.
/// </summary>
public static class AvatarLayerOrdering
{
    public static bool IsBackground(AvatarLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return string.Equals(layer.Name.Trim(), "Fondo", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<AvatarLayer> BackgroundLayers(IEnumerable<AvatarLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        return layers.Where(IsBackground).OrderBy(layer => layer.SortOrder).ToArray();
    }

    public static IReadOnlyList<AvatarLayer> ForegroundLayers(IEnumerable<AvatarLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        return layers.Where(layer => !IsBackground(layer)).OrderBy(layer => layer.SortOrder).ToArray();
    }
}
