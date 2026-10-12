namespace VtuberBot.Core;

/// <summary>Portable audio formats supported by the local sound library.</summary>
public static class LocalSoundFormatPolicy
{
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);
    }
}
