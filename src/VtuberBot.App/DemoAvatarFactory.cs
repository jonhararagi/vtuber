using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VtuberBot.App;

internal sealed record DemoAvatarAssets(
    string Idle,
    string Talking,
    string Blink,
    string Happy,
    string Surprised,
    string LayeredIdle,
    string LayeredTalking,
    string LayeredBlink,
    string LayeredHappy,
    string LayeredSurprised,
    string FrontHairLayer,
    string SparklesLayer);

/// <summary>
/// Palette for an original, local-only sample PNGTuber. Samples share the same base drawing,
/// but have distinct palettes and accents; they are generated locally without downloads.
/// </summary>
internal sealed record DemoAvatarTheme(
    string Id,
    string Hair,
    string HairLight,
    string Coat,
    string CoatLight,
    string CoatDeep,
    string Eye,
    string Accent,
    string Secondary,
    string Outline,
    string Dark);

/// <summary>
/// Creates original, local-only demonstration sprites so a fresh install can preview an avatar
/// without downloading art or requiring the user to supply files first.
/// </summary>
internal static class DemoAvatarFactory
{
    private const int CanvasWidth = 512;
    private const int CanvasHeight = 768;

    private static DemoAvatarTheme ResolveTheme(string? themeId) =>
        (themeId ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "aoi" => new DemoAvatarTheme("aoi", "#15345D", "#42B8F2", "#184B70", "#2E8EAD",
                "#163D5C", "#1FA6E0", "#53E0FF", "#B0F6F0", "#13253D", "#10223B"),
            "mio" => new DemoAvatarTheme("mio", "#55233B", "#C44D72", "#64213F", "#A93D66",
                "#4B1932", "#C43F74", "#F5B95F", "#FFD4E0", "#371528", "#2A1224"),
            "nagi" => new DemoAvatarTheme("nagi", "#25483C", "#80B78D", "#355E53", "#68A898",
                "#29463B", "#5CA89E", "#F1D37C", "#D1FFE2", "#20362F", "#1F302B"),
            _ => new DemoAvatarTheme("hikari", "#483078", "#9576E8", "#453B8F", "#7568C8",
                "#5C4AA7", "#8060D6", "#FF78C8", "#69E4D0", "#352445", "#2F234B")
        };

    public static DemoAvatarAssets EnsureAssets(string avatarDirectory, string? themeId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(avatarDirectory);
        var theme = ResolveTheme(themeId);
        var directory = Path.Combine(Path.GetFullPath(avatarDirectory), "builtin-demos", theme.Id);
        Directory.CreateDirectory(directory);

        string Asset(string fileName, Action<DrawingContext> draw)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                RenderPng(path, draw);
            return path;
        }

        return new DemoAvatarAssets(
            Asset("pngtuber-idle.png", dc => DrawCharacter(dc, "Idle", includeFrontHair: true, theme: theme)),
            Asset("pngtuber-talking.png", dc => DrawCharacter(dc, "Talking", includeFrontHair: true, theme: theme)),
            Asset("pngtuber-blink.png", dc => DrawCharacter(dc, "Blink", includeFrontHair: true, theme: theme)),
            Asset("pngtuber-happy.png", dc => DrawCharacter(dc, "Happy", includeFrontHair: true, theme: theme)),
            Asset("pngtuber-surprised.png", dc => DrawCharacter(dc, "Surprised", includeFrontHair: true, theme: theme)),
            Asset("layered-idle.png", dc => DrawCharacter(dc, "Idle", includeFrontHair: false, theme: theme)),
            Asset("layered-talking.png", dc => DrawCharacter(dc, "Talking", includeFrontHair: false, theme: theme)),
            Asset("layered-blink.png", dc => DrawCharacter(dc, "Blink", includeFrontHair: false, theme: theme)),
            Asset("layered-happy.png", dc => DrawCharacter(dc, "Happy", includeFrontHair: false, theme: theme)),
            Asset("layered-surprised.png", dc => DrawCharacter(dc, "Surprised", includeFrontHair: false, theme: theme)),
            Asset("layer-front-hair.png", dc => DrawFrontHairLayer(dc, theme)),
            Asset("layer-sparkles.png", dc => DrawSparklesLayer(dc, theme)));
    }

    private static void RenderPng(string path, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            draw(context);

        var bitmap = new RenderTargetBitmap(CanvasWidth, CanvasHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(output);
    }

    private static void DrawEllipse(DrawingContext context, Brush? fill, Pen? pen, Rect bounds) =>
        context.DrawEllipse(
            fill,
            pen,
            new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
            bounds.Width / 2,
            bounds.Height / 2);

    private static void DrawCharacter(DrawingContext dc, string expression, bool includeFrontHair, DemoAvatarTheme theme)
    {
        var outline = new Pen(Brush(theme.Outline), 7) { LineJoin = PenLineJoin.Round };
        var fineOutline = new Pen(Brush(theme.Outline), 3.5) { LineJoin = PenLineJoin.Round };
        var hair = Brush(theme.Hair);
        var hairLight = Brush(theme.HairLight);
        var skin = Brush("#FFE0C8");
        var skinShadow = Brush("#F3BFAF");
        var coat = Brush(theme.Coat);
        var coatLight = Brush(theme.CoatLight);
        var cream = Brush("#FFF4E8");
        var teal = Brush(theme.Secondary);

        // Soft ground shadow; the rest of the canvas remains transparent.
        DrawEllipse(dc, Brush("#440F172A"), null, new Rect(135, 710, 242, 28));

        // Long hair behind the shoulders.
        dc.DrawGeometry(hair, outline, Geometry.Parse(
            "M 164,158 C 126,164 112,220 125,303 L 139,467 C 143,514 173,536 198,481 L 216,319 Z"));
        dc.DrawGeometry(hair, outline, Geometry.Parse(
            "M 344,157 C 385,169 401,224 387,313 L 373,466 C 370,512 338,535 315,480 L 294,319 Z"));
        dc.DrawGeometry(hairLight, null, Geometry.Parse(
            "M 143,235 C 132,302 150,393 157,446 C 161,473 173,480 179,461 C 158,374 160,289 168,218 Z"));
        dc.DrawGeometry(hairLight, null, Geometry.Parse(
            "M 366,234 C 378,310 359,393 354,445 C 351,469 339,481 333,460 C 353,368 349,285 341,218 Z"));

        // Jacket, shirt and sleeves.
        dc.DrawGeometry(coat, outline, Geometry.Parse(
            "M 181,382 C 151,389 127,407 112,448 L 79,638 C 73,669 88,688 117,688 L 394,688 C 423,688 438,669 432,638 L 399,448 C 385,407 360,389 331,382 Z"));
        dc.DrawGeometry(coatLight, null, Geometry.Parse(
            "M 151,430 C 133,451 119,540 108,637 L 145,646 L 180,459 Z"));
        dc.DrawGeometry(coatLight, null, Geometry.Parse(
            "M 361,430 C 380,451 393,540 403,637 L 366,646 L 332,459 Z"));
        dc.DrawGeometry(cream, fineOutline, Geometry.Parse(
            "M 196,383 L 256,432 L 316,383 L 300,507 L 256,551 L 212,507 Z"));
        dc.DrawGeometry(teal, fineOutline, Geometry.Parse(
            "M 242,426 L 270,426 L 281,485 L 256,523 L 231,485 Z"));
        dc.DrawGeometry(Brush(theme.CoatDeep), null, Geometry.Parse(
            "M 181,552 C 215,577 295,577 331,552 L 352,688 L 160,688 Z"));
        dc.DrawRoundedRectangle(Brush(theme.Dark), fineOutline, new Rect(222, 608, 68, 47), 8, 8);
        DrawEllipse(dc, teal, fineOutline, new Rect(244, 619, 24, 24));

        // Neck and face.
        dc.DrawRoundedRectangle(skinShadow, null, new Rect(222, 342, 68, 82), 20, 20);
        DrawEllipse(dc, skinShadow, null, new Rect(132, 222, 40, 62));
        DrawEllipse(dc, skin, fineOutline, new Rect(134, 216, 35, 57));
        DrawEllipse(dc, skinShadow, null, new Rect(341, 222, 40, 62));
        DrawEllipse(dc, skin, fineOutline, new Rect(343, 216, 35, 57));
        DrawEllipse(dc, skin, outline, new Rect(151, 120, 210, 267));
        DrawEllipse(dc, Brush("#FFEBDD"), null, new Rect(172, 139, 170, 227));

        // Soft cheek blush.
        DrawEllipse(dc, Brush("#55F08AA6"), null, new Rect(174, 278, 45, 20));
        DrawEllipse(dc, Brush("#55F08AA6"), null, new Rect(292, 278, 45, 20));

        DrawEyes(dc, expression, fineOutline, theme);

        // Nose and mouth; each generated PNG has the same canvas and character alignment.
        DrawEllipse(dc, Brush("#D58B83"), null, new Rect(251, 277, 8, 5));
        switch (expression)
        {
            case "Talking":
                DrawEllipse(dc, Brush("#572944"), fineOutline, new Rect(239, 299, 36, 44));
                DrawEllipse(dc, Brush("#F08AAB"), null, new Rect(247, 324, 20, 12));
                break;
            case "Surprised":
                DrawEllipse(dc, Brush("#572944"), fineOutline, new Rect(243, 298, 29, 43));
                DrawEllipse(dc, Brush("#F08AAB"), null, new Rect(249, 323, 17, 10));
                break;
            case "Happy":
                dc.DrawGeometry(Brush("#572944"), fineOutline, Geometry.Parse(
                    "M 230,303 C 245,295 267,295 282,303 C 275,333 238,333 230,303 Z"));
                dc.DrawGeometry(Brush("#FFFFFF"), null, Geometry.Parse(
                    "M 237,304 C 250,300 264,300 275,304 L 270,312 L 242,312 Z"));
                break;
            default:
                dc.DrawGeometry(null, new Pen(Brush("#572944"), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    Geometry.Parse("M 231,308 C 245,322 267,322 281,308"));
                break;
        }

        // Front hair is a separate transparent layer in the layered demo and baked into the
        // full PNGTuber sprites. This makes the two examples behave differently in the editor.
        if (includeFrontHair)
            DrawFrontHairLayer(dc, theme);

        // Hair clip and outfit accents.
        dc.DrawGeometry(Brush(theme.Accent), fineOutline, Geometry.Parse(
            "M 319,151 C 293,126 306,104 323,115 C 337,87 361,108 348,132 C 367,145 347,165 333,154 Z"));
        DrawEllipse(dc, Brush("#FFF1AA"), null, new Rect(326, 128, 9, 9));
        dc.DrawLine(new Pen(Brush(theme.HairLight), 4) { StartLineCap = PenLineCap.Round },
            new Point(182, 179), new Point(197, 158));
        dc.DrawLine(new Pen(Brush(theme.HairLight), 4) { StartLineCap = PenLineCap.Round },
            new Point(335, 191), new Point(322, 171));
    }

    private static void DrawEyes(DrawingContext dc, string expression, Pen outline, DemoAvatarTheme theme)
    {
        if (expression is "Blink" or "Happy")
        {
            var pen = new Pen(Brush(theme.Outline), 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (expression == "Blink")
            {
                dc.DrawGeometry(null, pen, Geometry.Parse("M 188,247 C 202,260 218,260 231,246"));
                dc.DrawGeometry(null, pen, Geometry.Parse("M 281,246 C 294,260 310,260 324,247"));
            }
            else
            {
                dc.DrawGeometry(null, pen, Geometry.Parse("M 188,251 C 201,232 217,232 231,251"));
                dc.DrawGeometry(null, pen, Geometry.Parse("M 281,251 C 294,232 310,232 324,251"));
            }
            return;
        }

        var eyeHeight = expression == "Surprised" ? 54 : 47;
        DrawEllipse(dc, Brush("#FFFFFF"), outline, new Rect(184, 220, 48, eyeHeight));
        DrawEllipse(dc, Brush(theme.Eye), null, new Rect(195, 228, 28, eyeHeight - 12));
        DrawEllipse(dc, Brush(theme.Dark), null, new Rect(202, 234, 15, eyeHeight - 23));
        DrawEllipse(dc, Brush("#FFFFFF"), null, new Rect(199, 231, 10, 11));
        DrawEllipse(dc, Brush("#FFFFFF"), null, new Rect(213, 250, 5, 6));

        DrawEllipse(dc, Brush("#FFFFFF"), outline, new Rect(280, 220, 48, eyeHeight));
        DrawEllipse(dc, Brush(theme.Eye), null, new Rect(290, 228, 28, eyeHeight - 12));
        DrawEllipse(dc, Brush(theme.Dark), null, new Rect(297, 234, 15, eyeHeight - 23));
        DrawEllipse(dc, Brush("#FFFFFF"), null, new Rect(294, 231, 10, 11));
        DrawEllipse(dc, Brush("#FFFFFF"), null, new Rect(308, 250, 5, 6));

        var brow = new Pen(Brush(theme.Outline), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, brow, Geometry.Parse("M 186,211 Q 207,198 228,211"));
        dc.DrawGeometry(null, brow, Geometry.Parse("M 283,211 Q 304,198 326,211"));
    }

    private static void DrawFrontHairLayer(DrawingContext dc, DemoAvatarTheme theme)
    {
        var hair = Brush(theme.Hair);
        var outline = new Pen(Brush(theme.Outline), 7) { LineJoin = PenLineJoin.Round };
        var highlight = Brush(theme.HairLight);
        dc.DrawGeometry(hair, outline, Geometry.Parse(
            "M 151,207 C 143,151 174,107 226,105 C 282,86 343,118 362,166 C 369,183 367,203 361,224 C 341,203 323,194 304,203 L 282,237 L 270,201 L 244,247 L 221,213 L 196,237 L 181,211 L 157,232 Z"));
        dc.DrawGeometry(hair, null, Geometry.Parse(
            "M 181,157 C 205,124 243,118 268,124 C 227,129 208,153 195,184 Z"));
        dc.DrawGeometry(highlight, null, Geometry.Parse(
            "M 284,119 C 318,127 339,147 345,171 C 326,150 310,141 292,139 Z"));
        dc.DrawGeometry(hair, outline, Geometry.Parse(
            "M 155,185 C 137,229 150,286 165,320 C 169,333 177,333 181,321 L 190,232 Z"));
        dc.DrawGeometry(hair, outline, Geometry.Parse(
            "M 353,184 C 372,226 359,286 344,319 C 340,331 332,331 328,319 L 319,230 Z"));
    }

    private static void DrawSparklesLayer(DrawingContext dc, DemoAvatarTheme theme)
    {
        DrawStar(dc, new Point(106, 280), 27, Brush(theme.Accent));
        DrawStar(dc, new Point(405, 206), 23, Brush(theme.Secondary));
        DrawStar(dc, new Point(394, 463), 16, Brush(theme.HairLight));
        DrawStar(dc, new Point(115, 507), 13, Brush(theme.Eye));
        DrawStar(dc, new Point(365, 590), 11, Brush(theme.Accent));
    }

    private static void DrawStar(DrawingContext dc, Point center, double radius, Brush fill)
    {
        var points = new Point[10];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var distance = i % 2 == 0 ? radius : radius * 0.43;
            points[i] = new Point(center.X + Math.Cos(angle) * distance, center.Y + Math.Sin(angle) * distance);
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: true, isClosed: true);
            context.PolyLineTo(points.Skip(1).ToArray(), isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        dc.DrawGeometry(fill, new Pen(Brush("#55352445"), 2), geometry);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var value = (hex ?? string.Empty).TrimStart('#');
        var color = value.Length switch
        {
            6 => Color.FromRgb(
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16)),
            8 => Color.FromArgb(
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16),
                Convert.ToByte(value.Substring(6, 2), 16)),
            _ => throw new ArgumentException("Color hexadecimal no válido.", nameof(hex))
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
