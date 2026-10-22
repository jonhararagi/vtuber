using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VtuberBot.Core;

namespace VtuberBot.App;

public sealed record AvatarArtworkFileDiagnostic(
    string FileName,
    long FileSizeBytes,
    int SourceWidth,
    int SourceHeight,
    AvatarEdgeAnalysis BorderAnalysis)
{
    public string ToDisplayText()
    {
        var sizeKb = Math.Max(1, (long)Math.Ceiling(FileSizeBytes / 1024d));
        var lines = new List<string>
        {
            FileName + " — " + SourceWidth + "×" + SourceHeight + " px, " + sizeKb + " KB",
            BorderAnalysis.HasSampledTransparentPixels
                ? "Borde: hay píxeles transparentes en las muestras."
                : "Borde: no se encontraron píxeles transparentes en las muestras."
        };

        if (BorderAnalysis.HasLikelyOpaqueWhiteBorder)
            lines.Add("AVISO: muchos píxeles blancos opacos en el borde; puede aparecer un rectángulo blanco en OBS.");
        else if (!BorderAnalysis.HasSampledTransparentPixels)
            lines.Add("Revisá visualmente el fondo: el borde muestreado es opaco, pero esto no demuestra que toda la imagen tenga fondo blanco.");

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Read-only local diagnostics for avatar artwork. Decodes a bounded preview and samples
/// the edges; it never alters the source file or attempts unsafe automatic background removal.
/// </summary>
public static class AvatarArtworkDiagnostics
{
    public const long MaximumFileBytes = 25L * 1024 * 1024;
    private const int MaximumDecodeWidth = 1024;
    private const int SamplesPerEdge = 64;

    public static AvatarArtworkFileDiagnostic InspectFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No existe el archivo de arte.", Path.GetFileName(fullPath));

        var extension = Path.GetExtension(fullPath);
        if (!AvatarImageFormatPolicy.IsSupportedExtension(extension))
            throw new InvalidDataException("Formato no admitido: " + extension + ". Usá PNG, JPG o BMP.");

        var info = new FileInfo(fullPath);
        if (info.Length is <= 0 or > MaximumFileBytes)
            throw new InvalidDataException("El archivo debe ocupar entre 1 byte y 25 MB.");

        int sourceWidth;
        int sourceHeight;
        using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("La imagen no contiene un fotograma válido.");

            var frame = decoder.Frames[0];
            sourceWidth = frame.PixelWidth;
            sourceHeight = frame.PixelHeight;
            AvatarImageLimits.Validate(sourceWidth, sourceHeight);
        }

        // Keep the inspection allocation small even when the original artwork is very large.
        var preview = AvatarImageLoader.Load(fullPath, Math.Min(sourceWidth, MaximumDecodeWidth));
        var bgra = new FormatConvertedBitmap(preview, PixelFormats.Bgra32, null, 0);
        bgra.Freeze();
        var pixels = SampleBorder(bgra);
        var analysis = AvatarArtworkDiagnosticsPolicy.AnalyzeBorder(pixels);

        return new AvatarArtworkFileDiagnostic(Path.GetFileName(fullPath), info.Length, sourceWidth, sourceHeight, analysis);
    }

    private static IReadOnlyList<AvatarEdgePixel> SampleBorder(BitmapSource image)
    {
        var width = image.PixelWidth;
        var height = image.PixelHeight;
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("El bitmap decodificado no tiene dimensiones válidas.");

        var points = new HashSet<(int X, int Y)>();
        static int Coordinate(int index, int count, int maximum)
        {
            if (maximum <= 1 || count <= 1) return 0;
            return (int)Math.Round(index * (maximum - 1d) / (count - 1d));
        }

        var countX = Math.Min(SamplesPerEdge, width);
        var countY = Math.Min(SamplesPerEdge, height);
        for (var i = 0; i < countX; i++)
        {
            var x = Coordinate(i, countX, width);
            points.Add((x, 0));
            points.Add((x, height - 1));
        }
        for (var i = 0; i < countY; i++)
        {
            var y = Coordinate(i, countY, height);
            points.Add((0, y));
            points.Add((width - 1, y));
        }

        var output = new List<AvatarEdgePixel>(points.Count);
        var pixel = new byte[4];
        foreach (var (x, y) in points)
        {
            image.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            output.Add(new AvatarEdgePixel(pixel[2], pixel[1], pixel[0], pixel[3]));
        }
        return output;
    }
}
