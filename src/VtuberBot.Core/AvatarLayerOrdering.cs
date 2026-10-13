namespace VtuberBot.Core;

/// <summary>
/// Defines the visual order for avatar layers. A layer named "Fondo" or starting with
/// "Fondo ·" is rendered below the base character; all other layers are rendered above it.
/// </summary>
public static class AvatarLayerOrdering
{
    public static bool IsBackground(AvatarLayer? layer)
    {
        if (layer is null) return false;
        var name = layer.Name.Trim();
        return string.Equals(name, "Fondo", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Fondo ·", StringComparison.OrdinalIgnoreCase);
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
