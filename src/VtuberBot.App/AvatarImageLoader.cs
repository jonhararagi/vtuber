using System.IO;
using System.Windows.Media.Imaging;
using VtuberBot.Core;

namespace VtuberBot.App;

/// <summary>Validates image metadata before WPF allocates the decoded bitmap.</summary>
internal static class AvatarImageLoader
{
    public static BitmapImage Load(string path, int decodePixelWidth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (decodePixelWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(decodePixelWidth));

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No existe el archivo de imagen.", fullPath);

        using (var stream = File.OpenRead(fullPath))
        {
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("La imagen no contiene ningún fotograma válido.");

            var frame = decoder.Frames[0];
            AvatarImageLimits.Validate(frame.PixelWidth, frame.PixelHeight);
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = decodePixelWidth;
        image.UriSource = new Uri(fullPath);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
