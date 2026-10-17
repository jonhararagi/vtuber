namespace VtuberBot.Core;

/// <summary>Chooses a mouth layer sprite for the current visual state, falling back safely when art is missing.</summary>
public static class AvatarMouthSpritePolicy
{
    public static string ResolvePath(AvatarLayer layer, string visualState)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (string.Equals(visualState, "Talking", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(layer.OpenMouthPath) && File.Exists(layer.OpenMouthPath))
            return layer.OpenMouthPath;

        if (!string.IsNullOrWhiteSpace(layer.ClosedMouthPath) && File.Exists(layer.ClosedMouthPath))
            return layer.ClosedMouthPath;

        return layer.Path;
    }
}
