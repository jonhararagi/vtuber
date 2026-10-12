namespace VtuberBot.Core;

/// <summary>
/// Portable image formats guaranteed by the application's built-in WPF/WIC decoding path.
/// Optional Windows codecs (such as WebP) are not required by the distributable.
/// </summary>
public static class AvatarImageFormatPolicy
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };

    public static bool IsSupportedExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) &&
        SupportedExtensions.Contains(extension.Trim());
}
