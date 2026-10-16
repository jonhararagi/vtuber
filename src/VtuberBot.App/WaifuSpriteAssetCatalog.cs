using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VtuberBot.App;

internal sealed record WaifuSpriteAsset(
    string Id,
    string Name,
    string Category,
    string Description,
    string Path,
    int Width,
    int Height)
{
    public override string ToString() => Name + " · " + Category;
}

/// <summary>
/// Built-in accessory art is drawn from original vector primitives on first use.
/// No web request, account, or third-party artwork is required to populate the starter library.
/// </summary>
internal static class WaifuSpriteAssetCatalog
{
    private sealed record AssetSpec(string Id, string Name, string Category, string Description, int Width, int Height);

    private static readonly AssetSpec[] Specs =
    [
        new("hat-cat", "Orejas neko", "Sombreros", "Diadema con orejas de gato y centro rosado.", 512, 350),
        new("hat-bunny", "Orejas de conejita", "Sombreros", "Orejas largas con interior rosa.", 512, 430),
        new("hat-witch", "Sombrero de bruja", "Sombreros", "Sombrero violeta con hebilla.", 512, 390),
        new("hat-beret", "Boina pastel", "Sombreros", "Boina rosa con broche.", 512, 300),
        new("hat-cap", "Gorra de streamer", "Sombreros", "Gorra de visera con estrella.", 512, 300),
        new("hat-crown", "Mini corona", "Sombreros", "Corona dorada decorativa.", 512, 290),
        new("hat-bow", "Lazo grande", "Sombreros", "Lazo doble para el pelo.", 512, 300),
        new("hat-halo", "Aro celestial", "Sombreros", "Halo luminoso con estrellas.", 512, 300),
        new("hat-flower", "Corona de flores", "Sombreros", "Flores y hojas pequeñas.", 512, 300),
        new("hat-sailor", "Gorra marinera", "Sombreros", "Gorra azul con insignia.", 512, 300),

        new("eyes-open", "Ojos anime abiertos", "Ojos", "Iris grandes con brillo y pestañas.", 512, 220),
        new("eyes-happy", "Ojos felices cerrados", "Ojos", "Curvas de sonrisa para expresión feliz.", 512, 180),
        new("eyes-angry", "Ojos enojados", "Ojos", "Cejas inclinadas y mirada intensa.", 512, 220),
        new("eyes-sad", "Ojos tristes", "Ojos", "Mirada inclinada hacia arriba y pestañas caídas.", 512, 220),
        new("eyes-teary", "Ojos llorosos", "Ojos", "Ojos brillantes con lágrimas.", 512, 260),
        new("eyes-heart", "Ojos de corazón", "Ojos", "Iris en forma de corazón.", 512, 220),
        new("eyes-wink", "Guiño", "Ojos", "Un ojo cerrado y otro abierto.", 512, 220),
        new("eyes-sleepy", "Ojos somnolientos", "Ojos", "Párpados semicerrados.", 512, 220),
        new("eyes-surprised", "Ojos sorprendidos", "Ojos", "Ojos redondos y cejas levantadas.", 512, 240),

        new("mouth-smile", "Sonrisa cerrada", "Bocas", "Sonrisa suave.", 260, 170),
        new("mouth-open", "Boca abierta feliz", "Bocas", "Boca abierta con lengua rosada.", 260, 190),
        new("mouth-laugh", "Risa grande", "Bocas", "Boca abierta con dientes y lengua.", 280, 220),
        new("mouth-o", "Boca de sorpresa O", "Bocas", "Boca redonda de sorpresa.", 220, 220),
        new("mouth-sad", "Boca triste", "Bocas", "Comisuras caídas.", 260, 150),
        new("mouth-angry", "Grito enojado", "Bocas", "Grito con dientes.", 280, 230),
        new("mouth-pout", "Puchero", "Bocas", "Labios fruncidos.", 230, 170),

        new("fx-tears", "Lágrimas grandes", "Emociones", "Dos lágrimas azules transparentes.", 330, 360),
        new("fx-sweat", "Gota de sudor", "Emociones", "Gota de sudor caricaturesca.", 240, 300),
        new("fx-blush", "Rubor", "Emociones", "Rubor rosado suave en ambas mejillas.", 420, 180),
        new("fx-sad-lines", "Líneas de tristeza", "Emociones", "Líneas azules sobre la frente.", 430, 170),
        new("fx-anger", "Símbolo de enfado", "Emociones", "Marca roja de enojo estilo manga.", 220, 220),
        new("fx-hearts", "Corazones flotantes", "Emociones", "Corazones rosados para alegría/amor.", 360, 340),
        new("fx-sparkles", "Destellos", "Emociones", "Destellos blancos y amarillos.", 360, 360),
        new("fx-confusion", "Confusión", "Emociones", "Signos de interrogación decorativos.", 260, 310),
        new("fx-stars", "Estrellitas manga", "Emociones", "Estrellas de impacto.", 360, 300),
        new("fx-shadow", "Sombra dramática", "Emociones", "Sombreado dramático translúcido.", 460, 180),

        new("bg-sakura", "Atardecer sakura · HD", "Fondos", "Fondo original 1920×1080 con pétalos.", 1920, 1080),
        new("bg-galaxy", "Galaxia soñada · HD", "Fondos", "Fondo original 1920×1080 estrellado.", 1920, 1080),
        new("bg-room", "Habitación pastel · HD", "Fondos", "Fondo original 1920×1080 para streaming.", 1920, 1080),
        new("bg-night", "Noche neón · HD", "Fondos", "Fondo original 1920×1080 violeta y cian.", 1920, 1080)
    ];

    public static IReadOnlyList<WaifuSpriteAsset> EnsureAssets(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var assets = new List<WaifuSpriteAsset>(Specs.Length);
        foreach (var spec in Specs)
        {
            var path = Path.Combine(directory, spec.Id + ".png");
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                RenderAsset(path, spec);
            assets.Add(new WaifuSpriteAsset(spec.Id, spec.Name, spec.Category, spec.Description, path, spec.Width, spec.Height));
        }

        return assets;
    }

    private static void RenderAsset(string path, AssetSpec spec)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (spec.Category == "Fondos")
                DrawBackground(dc, spec.Id, spec.Width, spec.Height);
            else
                DrawPart(dc, spec.Id, spec.Width, spec.Height);
        }

        var bitmap = new RenderTargetBitmap(spec.Width, spec.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(output);
    }

    private static void DrawPart(DrawingContext dc, string id, int width, int height)
    {
        var outline = new Pen(B("#342342"), 13) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var fine = new Pen(B("#342342"), 8) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var pink = B("#FF8FC9");
        var palePink = B("#FFD8EC");
        var cream = B("#FFF7FF");
        var gold = B("#FFD36A");
        var purple = B("#8F73ED");
        var blue = B("#56C9FF");
        var dark = B("#36233F");
        var red = B("#FF4C78");

        switch (id)
        {
            case "hat-cat":
                dc.DrawGeometry(purple, outline, Geometry.Parse("M 75,300 L 120,40 Q 135,15 157,53 L 247,190 L 280,190 L 365,53 Q 388,15 403,40 L 447,300 Z"));
                dc.DrawGeometry(pink, fine, Geometry.Parse("M 119,222 L 145,83 L 222,220 Z M 290,220 L 374,83 L 397,222 Z"));
                dc.DrawGeometry(B("#F7CBFF"), null, Geometry.Parse("M 110,266 Q 256,305 402,266 L 410,312 Q 256,350 102,312 Z"));
                break;
            case "hat-bunny":
                dc.DrawRoundedRectangle(B("#F7EAF6"), outline, new Rect(110, 10, 108, 305), 55, 55);
                dc.DrawRoundedRectangle(B("#F7EAF6"), outline, new Rect(294, 10, 108, 305), 55, 55);
                dc.DrawRoundedRectangle(pink, null, new Rect(137, 47, 54, 230), 27, 27);
                dc.DrawRoundedRectangle(pink, null, new Rect(321, 47, 54, 230), 27, 27);
                dc.DrawEllipse(purple, outline, new Point(256, 312), 170, 66);
                dc.DrawEllipse(palePink, null, new Point(256, 300), 102, 30);
                break;
            case "hat-witch":
                dc.DrawGeometry(purple, outline, Geometry.Parse("M 70,342 Q 250,280 442,342 Q 350,315 256,60 Z"));
                dc.DrawGeometry(B("#B6A2FF"), fine, Geometry.Parse("M 150,320 Q 256,292 360,320"));
                dc.DrawRoundedRectangle(gold, fine, new Rect(219, 280, 74, 38), 9, 9);
                dc.DrawEllipse(pink, null, new Point(256, 110), 22, 22);
                break;
            case "hat-beret":
                dc.DrawGeometry(B("#FF84BA"), outline, Geometry.Parse("M 95,238 C 82,112 183,49 288,78 C 374,99 414,163 395,240 Z"));
                dc.DrawEllipse(B("#FFC5DF"), fine, new Point(256, 74), 28, 21);
                dc.DrawGeometry(B("#D74486"), null, Geometry.Parse("M 120,222 Q 250,258 392,218 L 380,265 Q 246,299 132,263 Z"));
                break;
            case "hat-cap":
                dc.DrawGeometry(blue, outline, Geometry.Parse("M 70,220 C 77,100 200,44 314,95 Q 381,120 398,216 Z"));
                dc.DrawGeometry(B("#2B7ECA"), outline, Geometry.Parse("M 210,215 Q 385,195 469,260 Q 353,308 199,257 Z"));
                dc.DrawGeometry(gold, null, Geometry.Parse("M 247,146 L 264,183 L 304,188 L 274,214 L 281,253 L 247,232 L 213,253 L 220,214 L 190,188 L 231,183 Z"));
                break;
            case "hat-crown":
                dc.DrawGeometry(gold, outline, Geometry.Parse("M 78,257 L 65,85 L 178,181 L 254,40 L 333,181 L 445,85 L 431,257 Z"));
                dc.DrawRoundedRectangle(B("#FFF0A8"), fine, new Rect(78, 235, 353, 62), 16, 16);
                dc.DrawEllipse(red, null, new Point(256, 260), 27, 23);
                dc.DrawEllipse(blue, null, new Point(151, 258), 15, 15);
                dc.DrawEllipse(blue, null, new Point(362, 258), 15, 15);
                break;
            case "hat-bow":
                dc.DrawGeometry(pink, outline, Geometry.Parse("M 250,165 C 161,63 63,82 64,170 C 65,235 150,278 252,207 Z"));
                dc.DrawGeometry(B("#FFB8D8"), outline, Geometry.Parse("M 262,165 C 351,63 449,82 448,170 C 447,235 362,278 260,207 Z"));
                dc.DrawRoundedRectangle(purple, outline, new Rect(213, 145, 86, 89), 26, 26);
                break;
            case "hat-halo":
                dc.DrawEllipse(null, new Pen(B("#FFD968"), 18), new Point(256, 151), 168, 66);
                DrawStar(dc, new Point(122, 141), 32, gold, fine);
                DrawStar(dc, new Point(394, 141), 31, pink, fine);
                DrawStar(dc, new Point(256, 68), 26, cream, fine);
                break;
            case "hat-flower":
                for (var i = 0; i < 9; i++)
                {
                    var x = 70 + i * 47;
                    var y = 175 - Math.Sin(i / 8d * Math.PI) * 70;
                    dc.DrawEllipse(B(i % 2 == 0 ? "#FF92CE" : "#B9A4FF"), fine, new Point(x, y), 29, 32);
                    dc.DrawEllipse(gold, null, new Point(x, y), 11, 11);
                }
                dc.DrawGeometry(B("#70C99A"), null, Geometry.Parse("M 74,202 Q 220,288 450,193 L 441,218 Q 220,312 82,226 Z"));
                break;
            case "hat-sailor":
                dc.DrawGeometry(cream, outline, Geometry.Parse("M 93,215 Q 125,68 260,73 Q 371,75 419,215 Z"));
                dc.DrawGeometry(B("#3576DA"), outline, Geometry.Parse("M 82,209 Q 250,256 432,209 L 414,267 Q 256,313 102,266 Z"));
                dc.DrawEllipse(gold, fine, new Point(256, 172), 40, 30);
                DrawStar(dc, new Point(256, 172), 22, gold, null);
                break;

            case "eyes-open":
            case "eyes-teary":
            case "eyes-heart":
            case "eyes-wink":
            case "eyes-surprised":
                DrawOpenEyes(dc, id, outline, fine, cream, purple, pink, blue, gold, dark);
                break;
            case "eyes-happy":
                dc.DrawGeometry(null, outline, Geometry.Parse("M 48,137 Q 115,47 194,137 M 318,137 Q 397,47 464,137"));
                dc.DrawGeometry(null, fine, Geometry.Parse("M 66,142 Q 122,84 178,142 M 335,142 Q 391,84 448,142"));
                break;
            case "eyes-angry":
                dc.DrawGeometry(cream, outline, Geometry.Parse("M 47,130 Q 120,70 204,143 Q 143,203 73,164 Z M 308,143 Q 392,70 465,130 L 439,164 Q 369,203 308,143 Z"));
                dc.DrawGeometry(null, outline, Geometry.Parse("M 45,46 L 198,106 M 467,46 L 314,106"));
                dc.DrawEllipse(purple, null, new Point(135, 149), 26, 33);
                dc.DrawEllipse(purple, null, new Point(377, 149), 26, 33);
                dc.DrawEllipse(dark, null, new Point(139, 156), 12, 20);
                dc.DrawEllipse(dark, null, new Point(373, 156), 12, 20);
                break;
            case "eyes-sad":
                dc.DrawGeometry(cream, outline, Geometry.Parse("M 45,85 Q 120,15 206,111 Q 143,163 66,130 Z M 306,111 Q 392,15 467,85 L 446,130 Q 369,163 306,111 Z"));
                dc.DrawEllipse(blue, null, new Point(135, 116), 25, 31);
                dc.DrawEllipse(blue, null, new Point(377, 116), 25, 31);
                dc.DrawGeometry(null, outline, Geometry.Parse("M 50,47 L 115,63 M 462,47 L 397,63"));
                break;
            case "eyes-sleepy":
                dc.DrawGeometry(cream, outline, Geometry.Parse("M 50,107 Q 120,64 202,113 L 202,143 L 50,143 Z M 310,113 Q 392,64 462,107 L 462,143 L 310,143 Z"));
                dc.DrawEllipse(purple, null, new Point(132, 130), 29, 18);
                dc.DrawEllipse(purple, null, new Point(380, 130), 29, 18);
                dc.DrawGeometry(null, outline, Geometry.Parse("M 48,98 Q 122,35 204,95 M 308,95 Q 390,35 464,98"));
                break;

            case "mouth-smile":
                dc.DrawGeometry(null, new Pen(B("#AD466F"), 15) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse("M 35,54 Q 130,160 225,54"));
                dc.DrawGeometry(null, new Pen(B("#FFD3E5"), 6), Geometry.Parse("M 64,72 Q 130,136 196,72"));
                break;
            case "mouth-open":
                dc.DrawEllipse(B("#69234E"), outline, new Point(130, 96), 74, 68);
                dc.DrawGeometry(pink, null, Geometry.Parse("M 72,114 Q 130,164 188,114 Q 168,151 130,151 Q 91,151 72,114 Z"));
                break;
            case "mouth-laugh":
                dc.DrawGeometry(B("#69234E"), outline, Geometry.Parse("M 24,36 Q 140,10 256,36 L 234,153 Q 140,220 46,153 Z"));
                dc.DrawRoundedRectangle(cream, null, new Rect(48, 39, 184, 45), 13, 13);
                dc.DrawGeometry(pink, null, Geometry.Parse("M 48,130 Q 140,177 232,130 Q 196,202 140,198 Q 80,195 48,130 Z"));
                break;
            case "mouth-o":
                dc.DrawEllipse(B("#69234E"), outline, new Point(110, 110), 54, 72);
                dc.DrawEllipse(pink, null, new Point(110, 143), 29, 25);
                break;
            case "mouth-sad":
                dc.DrawGeometry(null, new Pen(B("#AD466F"), 15) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse("M 35,115 Q 130,10 225,115"));
                break;
            case "mouth-angry":
                dc.DrawGeometry(B("#69234E"), outline, Geometry.Parse("M 23,33 L 257,33 L 223,178 L 140,218 L 57,178 Z"));
                dc.DrawGeometry(cream, fine, Geometry.Parse("M 51,42 L 229,42 L 212,92 L 68,92 Z"));
                dc.DrawGeometry(pink, null, Geometry.Parse("M 66,142 L 214,142 Q 140,205 66,142 Z"));
                break;
            case "mouth-pout":
                dc.DrawGeometry(null, new Pen(B("#AD466F"), 17) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse("M 45,94 Q 85,50 129,82 Q 173,50 215,94 Q 130,153 45,94 Z"));
                dc.DrawEllipse(B("#F58CBB"), null, new Point(130, 93), 25, 12);
                break;

            case "fx-tears":
                dc.DrawGeometry(B("#59D7FF"), new Pen(B("#2698D8"), 7), Geometry.Parse("M 83,23 C 65,83 20,116 34,183 C 44,240 139,245 150,180 C 160,125 103,75 83,23 Z"));
                dc.DrawGeometry(B("#59D7FF"), new Pen(B("#2698D8"), 7), Geometry.Parse("M 245,75 C 227,135 183,164 194,225 C 205,276 286,276 300,225 C 312,173 266,126 245,75 Z"));
                dc.DrawGeometry(B("#C7F5FF"), null, Geometry.Parse("M 69,99 Q 49,136 59,165"));
                dc.DrawGeometry(B("#C7F5FF"), null, Geometry.Parse("M 231,151 Q 214,178 224,201"));
                break;
            case "fx-sweat":
                dc.DrawGeometry(B("#59D7FF"), new Pen(B("#2698D8"), 8), Geometry.Parse("M 121,13 C 94,89 38,143 48,205 C 57,270 171,278 190,208 C 205,152 145,88 121,13 Z"));
                dc.DrawEllipse(B("#DFFAFF"), null, new Point(97, 176), 16, 28);
                break;
            case "fx-blush":
                var blush = new SolidColorBrush(Color.FromArgb(115, 255, 93, 165));
                dc.DrawEllipse(blush, null, new Point(105, 95), 88, 34);
                dc.DrawEllipse(blush, null, new Point(315, 95), 88, 34);
                for (var i = 0; i < 5; i++)
                {
                    dc.DrawLine(new Pen(B("#FF70A8"), 5), new Point(40 + i * 27, 123), new Point(58 + i * 27, 145));
                    dc.DrawLine(new Pen(B("#FF70A8"), 5), new Point(274 + i * 24, 123), new Point(292 + i * 24, 145));
                }
                break;
            case "fx-sad-lines":
                for (var i = 0; i < 7; i++)
                    dc.DrawLine(new Pen(B("#66BCEB"), 9), new Point(40 + i * 58, 10), new Point(82 + i * 48, 160));
                break;
            case "fx-anger":
                dc.DrawGeometry(red, new Pen(B("#9A193F"), 7), Geometry.Parse("M 103,16 L 146,82 L 205,46 L 196,112 L 218,165 L 157,161 L 123,204 L 102,153 L 43,159 L 66,106 L 34,55 L 91,75 Z"));
                dc.DrawGeometry(B("#FFF6A4"), null, Geometry.Parse("M 115,49 L 140,91 L 178,72 L 169,114 L 185,144 L 145,143 L 126,173 L 111,138 L 70,141 L 89,107 L 67,79 L 106,92 Z"));
                break;
            case "fx-hearts":
                DrawHeart(dc, new Point(88, 100), 52, B("#FF5DAD"), outline);
                DrawHeart(dc, new Point(248, 62), 35, B("#FF92CE"), fine);
                DrawHeart(dc, new Point(278, 234), 61, B("#FF5DAD"), outline);
                DrawHeart(dc, new Point(101, 260), 30, B("#FFC6E5"), fine);
                break;
            case "fx-sparkles":
                DrawStar(dc, new Point(78, 92), 42, gold, fine);
                DrawStar(dc, new Point(247, 77), 58, cream, fine);
                DrawStar(dc, new Point(288, 268), 47, B("#B6A2FF"), fine);
                DrawStar(dc, new Point(106, 275), 28, pink, fine);
                break;
            case "fx-confusion":
                dc.DrawGeometry(null, new Pen(B("#B6A2FF"), 22), Geometry.Parse("M 38,117 C 10,30 173,11 169,94 C 166,132 112,138 111,181"));
                dc.DrawEllipse(B("#B6A2FF"), null, new Point(111, 245), 14, 14);
                dc.DrawGeometry(null, new Pen(B("#FFD86A"), 20), Geometry.Parse("M 218,73 C 203,28 274,15 278,62 C 281,82 250,87 250,116"));
                dc.DrawEllipse(B("#FFD86A"), null, new Point(250, 156), 11, 11);
                break;
            case "fx-stars":
                DrawStar(dc, new Point(83, 170), 52, gold, outline);
                DrawStar(dc, new Point(256, 102), 80, B("#FFF3A5"), outline);
                DrawStar(dc, new Point(287, 252), 48, pink, fine);
                break;
            case "fx-shadow":
                var shade = new LinearGradientBrush(Color.FromArgb(0, 10, 8, 32), Color.FromArgb(224, 10, 8, 32), new Point(0, 0), new Point(0, 1));
                dc.DrawRectangle(shade, null, new Rect(0, 0, width, height));
                dc.DrawGeometry(null, new Pen(B("#7D6B9A"), 8), Geometry.Parse("M 15,140 Q 220,75 445,140"));
                break;
        }
    }

    private static void DrawOpenEyes(DrawingContext dc, string kind, Pen outline, Pen fine, Brush cream, Brush purple, Brush pink, Brush blue, Brush gold, Brush dark)
    {
        if (kind == "eyes-wink")
        {
            dc.DrawGeometry(null, outline, Geometry.Parse("M 42,130 Q 125,33 210,130"));
            dc.DrawGeometry(cream, outline, Geometry.Parse("M 309,96 Q 386,31 469,96 Q 426,204 350,183 Z"));
            dc.DrawEllipse(purple, null, new Point(400, 123), 34, 48);
            dc.DrawEllipse(dark, null, new Point(405, 134), 15, 25);
            dc.DrawEllipse(cream, null, new Point(415, 108), 10, 12);
            return;
        }

        var left = kind == "eyes-surprised" ? new Rect(52, 30, 155, 180) : new Rect(50, 48, 160, 152);
        var right = kind == "eyes-surprised" ? new Rect(304, 30, 155, 180) : new Rect(302, 48, 160, 152);
        dc.DrawEllipse(cream, outline, new Point(left.X + left.Width / 2, left.Y + left.Height / 2), left.Width / 2, left.Height / 2);
        dc.DrawEllipse(cream, outline, new Point(right.X + right.Width / 2, right.Y + right.Height / 2), right.Width / 2, right.Height / 2);

        if (kind == "eyes-heart")
        {
            DrawHeart(dc, new Point(130, 122), 63, pink, fine);
            DrawHeart(dc, new Point(382, 122), 63, pink, fine);
        }
        else
        {
            var irisBrush = kind == "eyes-teary" ? blue : kind == "eyes-surprised" ? gold : purple;
            dc.DrawEllipse(irisBrush, fine, new Point(132, 126), 49, 61);
            dc.DrawEllipse(irisBrush, fine, new Point(380, 126), 49, 61);
            dc.DrawEllipse(dark, null, new Point(137, 139), 22, 35);
            dc.DrawEllipse(dark, null, new Point(375, 139), 22, 35);
            dc.DrawEllipse(cream, null, new Point(152, 105), 15, 20);
            dc.DrawEllipse(cream, null, new Point(390, 105), 15, 20);
            dc.DrawEllipse(B("#D9F6FF"), null, new Point(116, 158), 9, 12);
            dc.DrawEllipse(B("#D9F6FF"), null, new Point(364, 158), 9, 12);
            if (kind == "eyes-teary")
            {
                dc.DrawGeometry(blue, null, Geometry.Parse("M 76,172 C 58,210 59,234 79,237 C 98,234 99,211 76,172 Z"));
                dc.DrawGeometry(blue, null, Geometry.Parse("M 442,172 C 423,210 425,234 444,237 C 462,234 464,211 442,172 Z"));
            }
        }

        dc.DrawGeometry(null, outline, Geometry.Parse("M 35,61 Q 115,5 209,57 M 303,57 Q 398,5 478,61"));
        if (kind == "eyes-teary")
            dc.DrawEllipse(pink, null, new Point(256, 24), 15, 10);
    }

    private static void DrawBackground(DrawingContext dc, string id, int width, int height)
    {
        var (top, bottom, accent, seed) = id switch
        {
            "bg-galaxy" => ("#17133E", "#663A91", "#B8A7FF", 11),
            "bg-room" => ("#FFCFDE", "#AFCDFE", "#FFF4D9", 24),
            "bg-night" => ("#0D102D", "#27245E", "#49E6FF", 31),
            _ => ("#FFAFCA", "#704D9B", "#FFE7F3", 7)
        };
        var gradient = new LinearGradientBrush(ParseColor(top), ParseColor(bottom), new Point(0, 0), new Point(1, 1));
        dc.DrawRectangle(gradient, null, new Rect(0, 0, width, height));
        var random = new Random(seed);
        if (id == "bg-room")
        {
            dc.DrawRoundedRectangle(B("#F9D8E2"), new Pen(B("#FFFFFF"), 10), new Rect(width * .09, height * .14, width * .82, height * .72), 38, 38);
            dc.DrawRoundedRectangle(B("#BDE3F8"), null, new Rect(width * .15, height * .22, width * .29, height * .36), 24, 24);
            dc.DrawLine(new Pen(B("#FFFFFF"), 15), new Point(width * .295, height * .22), new Point(width * .295, height * .58));
            dc.DrawLine(new Pen(B("#FFFFFF"), 15), new Point(width * .15, height * .40), new Point(width * .44, height * .40));
            dc.DrawRoundedRectangle(B("#E49DBF"), null, new Rect(width * .58, height * .48, width * .24, height * .24), 40, 40);
            dc.DrawRectangle(B("#F8B6D0"), null, new Rect(0, height * .84, width, height * .16));
            dc.DrawEllipse(B("#FFF5F8"), null, new Point(width * .70, height * .27), width * .045, height * .06);
            dc.DrawEllipse(B("#FFF5F8"), null, new Point(width * .78, height * .27), width * .045, height * .06);
        }
        else
        {
            for (var i = 0; i < 230; i++)
            {
                var x = random.NextDouble() * width;
                var y = random.NextDouble() * height;
                var radius = random.NextDouble() * (id == "bg-galaxy" ? 4.5 : 2.4) + .7;
                var star = B(i % 5 == 0 ? accent : "#FFFFFF");
                star.Opacity = .25 + random.NextDouble() * .65;
                dc.DrawEllipse(star, null, new Point(x, y), radius, radius);
            }
            for (var i = 0; i < 16; i++)
            {
                var x = random.NextDouble() * width;
                var y = random.NextDouble() * height;
                if (id == "bg-sakura")
                {
                    var petal = B(i % 2 == 0 ? "#FFD9EA" : "#FFF0F7");
                    petal.Opacity = .7;
                    dc.DrawEllipse(petal, null, new Point(x, y), 18 + random.Next(18), 7 + random.Next(10));
                }
                else if (id == "bg-galaxy")
                {
                    var ring = new Pen(B(accent), random.Next(2, 7)) { };
                    dc.DrawEllipse(null, ring, new Point(x, y), 40 + random.Next(170), 12 + random.Next(40));
                }
                else
                {
                    dc.DrawLine(new Pen(B(accent), 3), new Point(x, y), new Point(x + random.Next(-45, 46), y + random.Next(20, 80)));
                }
            }
        }

        var glow = new RadialGradientBrush(ParseColor("#66FFFFFF"), Color.FromArgb(0, 255, 255, 255));
        dc.DrawEllipse(glow, null, new Point(width * .5, height * .35), width * .42, height * .48);
        dc.DrawRectangle(null, new Pen(B(accent), 4), new Rect(12, 12, width - 24, height - 24));
    }

    private static void DrawStar(DrawingContext dc, Point center, double radius, Brush fill, Pen? pen)
    {
        var points = new List<Point>(10);
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var r = i % 2 == 0 ? radius : radius * .44;
            points.Add(new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r));
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true, true);
            ctx.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawHeart(DrawingContext dc, Point center, double size, Brush fill, Pen? pen)
    {
        var x = center.X;
        var y = center.Y;
        var half = size / 2;
        var geometry = Geometry.Parse(
            $"M {x},{y + half * .8} C {x - size * 1.25},{y - half * .2} {x - size * .75},{y - half * 1.15} {x},{y - half * .48} C {x + size * .75},{y - half * 1.15} {x + size * 1.25},{y - half * .2} {x},{y + half * .8} Z");
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        return value.Length switch
        {
            6 => Color.FromRgb(
                byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            8 => Color.FromArgb(
                byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            _ => Colors.White
        };
    }

    private static SolidColorBrush B(string hex) => new(ParseColor(hex));
}
