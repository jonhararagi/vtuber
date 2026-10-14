using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VtuberBot.App;

internal sealed record WardrobeOption(string Id, string Name, string Description);

internal sealed record ModularWardrobeAssets(
    string BaseIdle,
    string BaseTalking,
    string BaseBlink,
    string BaseHappy,
    string BaseSurprised,
    IReadOnlyDictionary<string, string> Hair,
    IReadOnlyDictionary<string, string> Outfits,
    IReadOnlyDictionary<string, string> HeadAccessories,
    IReadOnlyDictionary<string, string> FaceAccessories,
    IReadOnlyDictionary<string, string> BackAccessories,
    IReadOnlyDictionary<string, string> PoseLayers);

/// <summary>
/// Generates original, mix-and-match VTuber wardrobe parts on transparent PNG canvases.
/// The catalog uses broad genre conventions rather than reproducing any named VTuber's model.
/// </summary>
internal static class ModularWardrobeFactory
{
    private const int CanvasWidth = 512;
    private const int CanvasHeight = 768;
    private static readonly Pen Outline = new(Brush("#352445"), 7) { LineJoin = PenLineJoin.Round };
    private static readonly Pen FineOutline = new(Brush("#352445"), 3.5) { LineJoin = PenLineJoin.Round };

    public static IReadOnlyList<WardrobeOption> HairOptions { get; } =
    [
        new("high-ponytail", "Coleta alta carmesí", "Coleta dinámica con mechones largos."),
        new("twin-tails", "Coletas dobles violetas", "Dos coletas laterales con volumen."),
        new("soft-bob", "Bob azul noche", "Corte corto con puntas curvas."),
        new("long-waves", "Ondas largas rosa pastel", "Cabello largo con ondas suaves."),
        new("silver-cyan", "Plata con mechón cian", "Corte plateado con un acento luminoso."),
        new("mint-braid", "Trenza verde marina", "Trenza lateral y flequillo suelto."),
        new("short-spiky", "Corto despeinado", "Silueta corta para un estilo gamer."),
        new("royal-curls", "Rizos dorados", "Volumen y rizos de fantasía."),
        new("long-straight", "Largo liso índigo", "Mechones largos y rectos a ambos lados."),
        new("side-braid", "Trenza lateral coral", "Trenza marcada sobre un hombro."),
        new("double-buns", "Dos moños redondos", "Dos moños altos y mechones cortos.")
    ];

    public static IReadOnlyList<WardrobeOption> OutfitOptions { get; } =
    [
        new("star-idol", "Conjunto idol estelar", "Chaqueta escénica azul y detalles de estrella."),
        new("sailor-academy", "Uniforme marinero", "Cuello marinero, lazo y falda plisada."),
        new("gothic-rose", "Vestido gótico romántico", "Negro, ciruela y encajes ilustrados."),
        new("shrine-ceremony", "Conjunto ceremonial rojo", "Capas rojas e ivory con mangas amplias."),
        new("tech-streetwear", "Streetwear tecnológico", "Chaqueta oversize, paneles cian y falda corta."),
        new("night-pirate", "Capitana nocturna", "Abrigo vino, detalles dorados y camisa clara."),
        new("fantasy-knight", "Armadura fantástica ligera", "Hombreras suaves, túnica blanca y ribetes oro."),
        new("comfy-hoodie", "Hoodie pastel", "Sudadera amplia y cómoda con puños grandes."),
        new("celestial-gown", "Vestido celestial", "Azul profundo con líneas lunares y estrellas."),
        new("cyber-runner", "Traje cyber runner", "Paneles deportivos morado/cian y cinturón técnico."),
        new("idol-casual", "Chaqueta idol casual", "Chaqueta corta, cuello contrastado y falda plisada."),
        new("summer-festival", "Yukata de festival", "Kimono ligero con faja y mangas amplias."),
        new("mage-academy", "Túnica de maga", "Túnica larga con paneles y ribetes arcanos."),
        new("royal-ballgown", "Vestido de baile real", "Corpiño ajustado y falda amplia."),
        new("winter-parka", "Abrigo invernal", "Abrigo acolchado con cuello y bolsillos.")
    ];

    public static IReadOnlyList<WardrobeOption> HeadAccessoryOptions { get; } =
    [
        new("double-bows", "Lazos dobles", "Dos lazos decorativos."),
        new("cat-ears", "Diadema gatuna", "Orejas de gato originales."),
        new("bunny-ears", "Orejas de conejo", "Orejas largas con interior rosa."),
        new("small-horns", "Cuernos pequeños", "Cuernos decorativos curvos."),
        new("star-halo", "Aro estelar", "Halo fino con pequeñas estrellas."),
        new("shark-cap", "Gorra tiburón", "Gorro azul con una cara de tiburón simpática."),
        new("mini-crown", "Mini corona", "Corona dorada con gemas."),
        new("gamer-headset", "Auriculares gamer", "Auriculares grandes con luces cian."),
        new("flower-clip", "Flores laterales", "Broche floral de tres pétalos."),
        new("feather-pin", "Pluma ornamental", "Adorno de pluma azul y oro."),
        new("fox-ears", "Orejas de zorro", "Diadema de orejas triangulares con interior coral."),
        new("witch-hat", "Sombrero de bruja", "Sombrero cónico con hebilla decorativa."),
        new("crescent-tiara", "Tiara lunar", "Media luna con pequeñas estrellas."),
        new("robo-visor", "Visor tecnológico", "Visor futurista sobre la frente.")
    ];

    public static IReadOnlyList<WardrobeOption> FaceAccessoryOptions { get; } =
    [
        new("round-glasses", "Gafas redondas", "Montura fina y lentes transparentes."),
        new("star-earrings", "Pendientes de estrella", "Pendientes dorados a ambos lados."),
        new("heart-choker", "Choker con corazón", "Collar fino con corazón."),
        new("star-eyepatch", "Parche estelar", "Parche decorativo sobre un ojo."),
        new("mic-headset", "Micrófono de diadema", "Micrófono pequeño de streamer."),
        new("mask-pendant", "Colgante técnico", "Pequeño colgante geométrico."),
        new("face-mask", "Mascarilla urbana", "Mascarilla con contorno suave."),
        new("cheek-stars", "Estrellitas en mejillas", "Pequeños adornos simétricos."),
        new("neck-ribbon", "Lazo de cuello", "Lazo decorativo debajo del cuello."),
        new("none-face", "Sin accesorio facial", "Dejar libre la cara.")
    ];

    public static IReadOnlyList<WardrobeOption> BackAccessoryOptions { get; } =
    [
        new("short-cape", "Capa corta", "Capa que cae por detrás de los hombros."),
        new("bat-wings", "Alas demoníacas", "Alas pequeñas estilo fantasía."),
        new("celestial-wings", "Alas de luz", "Alas pequeñas con líneas blancas/doradas."),
        new("bunny-backpack", "Mochila conejito", "Mochila redondeada con orejas."),
        new("fin-ornament", "Aletas acuáticas", "Aletas laterales estilizadas."),
        new("starlight-ribbons", "Cintas estelares", "Cintas largas con acentos brillantes."),
        new("fox-tail", "Cola esponjosa", "Cola curva grande detrás de la cintura."),
        new("mechanical-pack", "Mochila mecánica", "Mochila tecnológica con antenas."),
        new("crystal-orbit", "Cristales flotantes", "Cristales mágicos alrededor de la espalda."),
        new("none-back", "Sin accesorio trasero", "Dejar libre la silueta.")
    ];

    public static IReadOnlyList<WardrobeOption> FaceShapeOptions { get; } =
    [
        new("round", "Rostro redondeado", "Mejillas suaves y forma clásica chibi."),
        new("heart", "Rostro corazón", "Mejillas amplias y mentón más fino."),
        new("sharp", "Rostro anguloso", "Mandíbula más estrecha y rasgos definidos."),
        new("elf", "Rostro élfico", "Contorno fino y orejas puntiagudas.")
    ];

    public static IReadOnlyList<WardrobeOption> EyeStyleOptions { get; } =
    [
        new("ocean", "Azul océano", "Iris azul brillante."),
        new("violet", "Violeta mágico", "Iris violeta."),
        new("rose", "Rosa rubí", "Iris rosa."),
        new("emerald", "Verde esmeralda", "Iris verde."),
        new("gold", "Dorado solar", "Iris dorado."),
        new("heterochromia", "Heterocromía", "Un ojo turquesa y el otro rosa."),
        new("sleepy", "Ojos soñolientos", "Párpado más caído y expresión relajada.")
    ];

    public static IReadOnlyList<WardrobeOption> SkinToneOptions { get; } =
    [
        new("porcelain", "Porcelana", "Piel clara con subtono cálido."),
        new("warm", "Durazno", "Piel media cálida."),
        new("cocoa", "Cacao", "Piel marrón cálida."),
        new("moonlight", "Luz lunar", "Piel fantástica de tono lavanda.")
    ];

    public static IReadOnlyList<WardrobeOption> PoseOptions { get; } =
    [
        new("neutral", "De pie", "Pose tranquila."),
        new("wave", "Saludando", "Un brazo levantado para saludar."),
        new("peace", "Señal de paz", "Una mano en V junto al rostro."),
        new("hands-hips", "Manos en la cintura", "Pose confiada."),
        new("crossed-arms", "Brazos cruzados", "Pose seria y segura."),
        new("microphone", "Cantando al micro", "Brazo elevado con micrófono.")
    ];

    public static ModularWardrobeAssets EnsureAssets(
        string avatarDirectory,
        string faceShapeId = "round",
        string eyeStyleId = "ocean",
        string skinId = "porcelain",
        string outfitId = "comfy-hoodie")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(avatarDirectory);
        var directory = Path.Combine(Path.GetFullPath(avatarDirectory), "builtin-demos", "wardrobe-v2");
        var variantKey = string.Join("-", new[] { skinId, faceShapeId, eyeStyleId }
            .Select(value => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()));
        var variantDirectory = Path.Combine(directory, "variants", variantKey);
        var poseKey = string.Join("-", new[] { skinId, outfitId }
            .Select(value => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()));
        var poseDirectory = Path.Combine(directory, "poses", poseKey);
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(variantDirectory);
        Directory.CreateDirectory(poseDirectory);

        string Asset(string fileName, Action<DrawingContext> draw)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                RenderPng(path, draw);
            return path;
        }

        string VariantAsset(string fileName, Action<DrawingContext> draw)
        {
            var path = Path.Combine(variantDirectory, fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                RenderPng(path, draw);
            return path;
        }

        IReadOnlyDictionary<string, string> BuildAssets(
            IEnumerable<WardrobeOption> options,
            Action<DrawingContext, string> draw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                var captured = option.Id;
                result[option.Id] = Asset(option.Id + ".png", dc => draw(dc, captured));
            }
            return result;
        }

        string PoseAsset(string fileName, Action<DrawingContext> draw)
        {
            var path = Path.Combine(poseDirectory, fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                RenderPng(path, draw);
            return path;
        }

        IReadOnlyDictionary<string, string> BuildPoseAssets()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in PoseOptions)
            {
                var captured = option.Id;
                result[option.Id] = PoseAsset("pose-" + skinId + "-" + outfitId + "-" + captured + ".png", dc => DrawPose(dc, captured, skinId, outfitId));
            }
            return result;
        }

        return new ModularWardrobeAssets(
            VariantAsset("base-idle.png", dc => DrawBase(dc, "Idle", faceShapeId, eyeStyleId, skinId)),
            VariantAsset("base-talking.png", dc => DrawBase(dc, "Talking", faceShapeId, eyeStyleId, skinId)),
            VariantAsset("base-blink.png", dc => DrawBase(dc, "Blink", faceShapeId, eyeStyleId, skinId)),
            VariantAsset("base-happy.png", dc => DrawBase(dc, "Happy", faceShapeId, eyeStyleId, skinId)),
            VariantAsset("base-surprised.png", dc => DrawBase(dc, "Surprised", faceShapeId, eyeStyleId, skinId)),
            BuildAssets(HairOptions, DrawHair),
            BuildAssets(OutfitOptions, DrawOutfit),
            BuildAssets(HeadAccessoryOptions, DrawHeadAccessory),
            BuildAssets(FaceAccessoryOptions.Where(x => x.Id != "none-face"), DrawFaceAccessory),
            BuildAssets(BackAccessoryOptions.Where(x => x.Id != "none-back"), DrawBackAccessory),
            BuildPoseAssets());
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

    private static void DrawBase(DrawingContext dc, string expression, string faceShapeId, string eyeStyleId, string skinId)
    {
        var skinPalette = skinId switch
        {
            "warm" => ("#F7CDAA", "#D89D82"),
            "cocoa" => ("#B97965", "#7E4D48"),
            "moonlight" => ("#D9D0FF", "#A99BE5"),
            _ => ("#FFE0C8", "#EFB8A7")
        };
        var skin = Brush(skinPalette.Item1);
        var skinShadow = Brush(skinPalette.Item2);
        var bodysuit = Brush("#F5F0F8");
        var suitShadow = Brush("#C7C0DC");
        var dark = Brush("#352445");
        var teal = Brush("#69E4D0");

        // Modest neutral bodysuit, arms and legs are intentionally clothing-covered.
        dc.DrawEllipse(Brush("#330F172A"), null, new Point(256, 720), 116, 12);
        dc.DrawGeometry(skin, Outline, Geometry.Parse(
            "M 192,585 C 185,614 182,664 184,708 C 184,724 205,730 219,719 L 245,617 Z"));
        dc.DrawGeometry(skin, Outline, Geometry.Parse(
            "M 320,585 C 327,614 330,664 328,708 C 328,724 307,730 293,719 L 267,617 Z"));

        dc.DrawGeometry(bodysuit, Outline, Geometry.Parse(
            "M 197,378 C 172,387 156,411 159,452 L 171,596 C 174,626 195,642 256,642 C 317,642 338,626 341,596 L 353,452 C 356,411 340,387 315,378 L 256,414 Z"));
        dc.DrawGeometry(suitShadow, null, Geometry.Parse(
            "M 196,423 L 222,433 L 211,594 L 183,598 Z"));
        dc.DrawGeometry(suitShadow, null, Geometry.Parse(
            "M 316,423 L 290,433 L 301,594 L 329,598 Z"));
        dc.DrawGeometry(Brush("#E8E1F1"), FineOutline, Geometry.Parse(
            "M 221,410 L 256,439 L 291,410 L 283,482 L 256,514 L 229,482 Z"));
        dc.DrawEllipse(teal, FineOutline, new Point(256, 473), 9, 9);

        dc.DrawRoundedRectangle(skinShadow, null, new Rect(222, 338, 68, 76), 20, 20);
        if (faceShapeId == "elf")
        {
            dc.DrawGeometry(skin, Outline, Geometry.Parse("M 173,262 L 115,220 L 141,298 Z"));
            dc.DrawGeometry(skin, Outline, Geometry.Parse("M 339,262 L 397,220 L 371,298 Z"));
        }
        else
        {
            dc.DrawEllipse(skin, Outline, new Point(151, 248), 20, 31);
            dc.DrawEllipse(skin, FineOutline, new Point(361, 248), 20, 31);
        }

        switch (faceShapeId)
        {
            case "heart":
                dc.DrawGeometry(skin, Outline, Geometry.Parse("M 256,121 C 316,121 357,169 359,226 C 362,279 324,344 275,374 C 263,382 249,382 237,374 C 188,344 150,279 153,226 C 155,169 196,121 256,121 Z"));
                break;
            case "sharp":
                dc.DrawGeometry(skin, Outline, Geometry.Parse("M 256,119 C 317,119 359,164 354,232 C 350,294 313,347 256,379 C 199,347 162,294 158,232 C 153,164 195,119 256,119 Z"));
                break;
            case "elf":
                dc.DrawGeometry(skin, Outline, Geometry.Parse("M 256,119 C 316,119 354,165 350,231 C 348,294 306,355 256,382 C 206,355 164,294 162,231 C 158,165 196,119 256,119 Z"));
                break;
            default:
                dc.DrawEllipse(skin, Outline, new Point(256, 253), 105, 132);
                break;
        }

        dc.DrawEllipse(Brush("#FFFFEBDD"), null, new Point(256, 256), 84, 113);
        dc.DrawEllipse(Brush("#55F08AA6"), null, new Point(198, 289), 21, 9);
        dc.DrawEllipse(Brush("#55F08AA6"), null, new Point(314, 289), 21, 9);

        DrawEyes(dc, expression, eyeStyleId);
        dc.DrawEllipse(Brush("#D58B83"), null, new Point(256, 281), 4, 2.5);
        switch (expression)
        {
            case "Talking":
                dc.DrawEllipse(Brush("#572944"), FineOutline, new Point(256, 309), 17, 20);
                dc.DrawEllipse(Brush("#F08AAB"), null, new Point(256, 320), 9, 5);
                break;
            case "Surprised":
                dc.DrawEllipse(Brush("#572944"), FineOutline, new Point(256, 307), 13, 19);
                break;
            case "Happy":
                dc.DrawGeometry(Brush("#572944"), FineOutline, Geometry.Parse(
                    "M 229,302 C 242,297 270,297 283,302 C 272,330 240,330 229,302 Z"));
                dc.DrawGeometry(Brush("#FFFFFF"), null, Geometry.Parse(
                    "M 237,304 L 275,304 L 269,312 L 243,312 Z"));
                break;
            default:
                dc.DrawGeometry(null, new Pen(dark, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    Geometry.Parse("M 232,307 C 246,318 266,318 280,307"));
                break;
        }
    }

    private static void DrawEyes(DrawingContext dc, string expression, string eyeStyleId)
    {
        if (expression is "Blink" or "Happy")
        {
            var pen = new Pen(Brush("#352445"), 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (expression == "Blink")
            {
                dc.DrawGeometry(null, pen, Geometry.Parse("M 191,249 C 204,262 219,262 232,249"));
                dc.DrawGeometry(null, pen, Geometry.Parse("M 280,249 C 293,262 308,262 321,249"));
            }
            else
            {
                dc.DrawGeometry(null, pen, Geometry.Parse("M 191,250 C 204,232 219,232 232,250"));
                dc.DrawGeometry(null, pen, Geometry.Parse("M 280,250 C 293,232 308,232 321,250"));
            }
            return;
        }

        var eyes = eyeStyleId switch
        {
            "violet" => ("#9B68F1", "#9B68F1"),
            "rose" => ("#E45E9C", "#E45E9C"),
            "emerald" => ("#3EBB9A", "#3EBB9A"),
            "gold" => ("#D7A63F", "#D7A63F"),
            "heterochromia" => ("#41C8DB", "#D56AB8"),
            "sleepy" => ("#70A0C8", "#70A0C8"),
            _ => ("#5A87E8", "#5A87E8")
        };
        var eyeHeight = expression == "Surprised" ? 52 : eyeStyleId == "sleepy" ? 30 : 44;
        dc.DrawEllipse(Brush("#FFFFFF"), FineOutline, new Point(211, 247), 22, eyeHeight / 2);
        dc.DrawEllipse(Brush(eyes.Item1), null, new Point(211, 254), 12, eyeHeight / 2 - 7);
        dc.DrawEllipse(Brush("#2F234B"), null, new Point(215, 256), 7, eyeHeight / 2 - 11);
        dc.DrawEllipse(Brush("#FFFFFF"), null, new Point(207, 244), 5, 6);
        dc.DrawEllipse(Brush("#FFFFFF"), FineOutline, new Point(301, 247), 22, eyeHeight / 2);
        dc.DrawEllipse(Brush(eyes.Item2), null, new Point(301, 254), 12, eyeHeight / 2 - 7);
        dc.DrawEllipse(Brush("#2F234B"), null, new Point(305, 256), 7, eyeHeight / 2 - 11);
        dc.DrawEllipse(Brush("#FFFFFF"), null, new Point(297, 244), 5, 6);
        var brow = new Pen(Brush("#352445"), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, brow, Geometry.Parse("M 188,213 Q 209,202 231,212"));
        dc.DrawGeometry(null, brow, Geometry.Parse("M 280,212 Q 302,202 324,213"));
    }

    private static void DrawHair(DrawingContext dc, string id)
    {
        var colors = id switch
        {
            "high-ponytail" => ("#9D294F", "#E65E78", "#FFB0C2"),
            "twin-tails" => ("#6041A1", "#9576DF", "#D3B9FF"),
            "soft-bob" => ("#172D5E", "#365FB8", "#79A5FF"),
            "long-waves" => ("#D76E9E", "#F2A9CB", "#FFE1F0"),
            "silver-cyan" => ("#BFC9DA", "#EAF3FC", "#53E9F2"),
            "mint-braid" => ("#247F78", "#5AC5A8", "#BBF9D9"),
            "short-spiky" => ("#302A47", "#5D526F", "#B4A7D8"),
            "long-straight" => ("#292750", "#504BA1", "#B7B9F7"),
            "side-braid" => ("#9D4C43", "#E68776", "#FFD0B9"),
            "double-buns" => ("#7B355F", "#C46DA8", "#F8C5E7"),
            _ => ("#A87924", "#E8C35D", "#FFE9A1")
        };

        var dark = Brush(colors.Item1);
        var mid = Brush(colors.Item2);
        var highlight = Brush(colors.Item3);

        // Each hairstyle is an independent transparent overlay: side locks plus its own fringe/cap.
        switch (id)
        {
            case "high-ponytail":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 286,134 C 333,85 401,103 398,157 C 393,194 352,203 328,183 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 352,131 C 394,127 398,155 378,177 L 350,170 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 172,211 C 144,251 157,318 175,365 L 190,402 L 208,385 L 198,253 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 340,206 C 372,249 361,321 344,365 L 328,399 L 310,381 L 321,250 Z"));
                break;
            case "twin-tails":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 165,186 C 119,160 113,211 133,262 L 159,318 L 181,291 L 179,216 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 346,186 C 393,160 399,211 379,262 L 353,318 L 331,291 L 333,216 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 135,223 C 130,257 151,284 160,294 L 167,275 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 377,223 C 382,257 361,284 352,294 L 345,275 Z"));
                break;
            case "soft-bob":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 147,211 C 135,140 186,109 254,116 C 327,107 372,151 363,218 L 348,295 L 324,306 L 329,222 L 291,203 L 262,245 L 235,209 L 190,236 L 183,303 L 160,292 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 179,157 C 211,127 244,127 274,138 L 224,153 Z"));
                break;
            case "long-waves":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 163,210 C 124,249 141,309 133,343 C 126,378 156,396 179,368 L 198,231 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 349,210 C 388,249 371,309 379,343 C 386,378 356,396 333,368 L 314,231 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 158,260 C 151,304 160,342 163,362 L 177,354 L 177,253 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 354,260 C 361,304 352,342 349,362 L 335,354 L 335,253 Z"));
                break;
            case "silver-cyan":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 151,210 C 139,141 196,105 268,119 C 330,119 370,166 356,218 L 342,274 L 322,288 L 327,217 L 279,204 L 256,236 L 235,211 L 185,239 L 179,282 L 158,280 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 286,133 C 324,143 340,170 339,195 L 309,177 L 287,154 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 234,143 L 214,195 L 233,215 L 251,154 Z"));
                break;
            case "mint-braid":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 151,210 C 140,142 194,111 258,117 C 330,115 370,160 360,217 L 338,252 L 319,216 L 282,202 L 256,239 L 231,207 L 190,232 L 178,270 L 160,267 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 345,224 C 374,246 375,278 354,296 C 372,318 364,342 343,352 C 357,372 341,391 325,398 L 313,375 C 331,362 331,348 317,334 C 341,320 339,305 323,290 C 341,273 332,247 319,239 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 286,133 C 319,144 334,165 338,185 L 311,166 Z"));
                break;
            case "short-spiky":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 147,220 L 158,157 L 193,176 L 214,113 L 252,147 L 285,105 L 304,154 L 346,138 L 367,200 L 343,236 L 321,211 L 289,230 L 258,204 L 227,233 L 195,210 L 173,242 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 174,174 L 205,185 L 218,140 L 239,178 L 280,133 L 290,183 L 327,164 L 344,204 L 309,190 L 283,204 L 255,179 L 225,205 L 196,186 Z"));
                break;
            case "long-straight":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 159,205 C 140,252 146,337 137,383 L 171,401 L 195,231 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 353,205 C 372,252 366,337 375,383 L 341,401 L 317,231 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 157,277 L 159,367 L 173,377 L 179,253 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 355,277 L 353,367 L 339,377 L 333,253 Z"));
                break;
            case "side-braid":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 165,211 C 143,252 155,302 169,330 L 184,352 L 205,336 L 194,245 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 344,223 C 378,245 375,278 354,296 C 373,317 365,342 343,353 C 357,371 342,391 326,401 L 312,378 C 331,363 330,349 316,334 C 340,319 339,304 322,290 C 341,271 332,246 318,238 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 338,257 L 354,278 L 338,295 L 324,278 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 343,312 L 357,331 L 341,346 L 328,330 Z"));
                break;
            case "double-buns":
                dc.DrawEllipse(dark, Outline, new Point(179, 143), 31, 29);
                dc.DrawEllipse(mid, FineOutline, new Point(179, 143), 18, 17);
                dc.DrawEllipse(dark, Outline, new Point(333, 143), 31, 29);
                dc.DrawEllipse(mid, FineOutline, new Point(333, 143), 18, 17);
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 171,204 C 145,241 154,289 173,323 L 196,256 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 341,204 C 367,241 358,289 339,323 L 316,256 Z"));
                break;
            default:
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 146,213 C 134,144 192,105 258,117 C 329,107 374,159 361,219 L 340,255 L 322,215 L 292,209 L 278,238 L 254,205 L 227,240 L 205,211 L 181,241 L 160,267 Z"));
                dc.DrawGeometry(mid, null, Geometry.Parse("M 184,157 C 221,118 267,124 295,139 C 259,133 225,156 204,184 Z"));
                dc.DrawGeometry(highlight, null, Geometry.Parse("M 296,136 C 330,144 344,166 345,186 L 322,167 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 347,190 C 375,240 365,300 346,324 L 329,307 L 333,233 Z"));
                break;
        }

        // Shared upper cap/front fringe keeps the face readable while distinguishing the selected cut.
        dc.DrawGeometry(dark, Outline, Geometry.Parse("M 151,215 C 139,153 185,108 256,112 C 326,108 370,153 361,215 L 331,226 L 311,201 L 282,235 L 259,203 L 234,237 L 211,208 L 181,229 Z"));
        dc.DrawGeometry(highlight, null, Geometry.Parse("M 183,153 C 207,123 248,121 273,130 C 234,137 214,160 197,181 Z"));
    }

    private static void DrawOutfit(DrawingContext dc, string id)
    {
        var palette = id switch
        {
            "star-idol" => ("#233A82", "#638EF1", "#62E3DE", "#FFF0C7"),
            "sailor-academy" => ("#233956", "#547BB9", "#F9F4FF", "#E99C6D"),
            "gothic-rose" => ("#25202F", "#4A2F59", "#D99DCB", "#F3E8F4"),
            "shrine-ceremony" => ("#AC3356", "#D65E79", "#FFF0DF", "#D9AF67"),
            "tech-streetwear" => ("#382966", "#6B4DBC", "#52E9E2", "#23233A"),
            "night-pirate" => ("#54253B", "#8C3C56", "#F8EEE2", "#E8C05A"),
            "fantasy-knight" => ("#C7D4E5", "#667FA7", "#FBFAF2", "#D6B65D"),
            "comfy-hoodie" => ("#74BDB8", "#B1E4DD", "#FFF4F0", "#D77C9F"),
            "celestial-gown" => ("#1B2C65", "#3D61A5", "#A6DDF6", "#F5D98F"),
            "idol-casual" => ("#7C2D5C", "#D75A8A", "#F8E9F0", "#F6CA62"),
            "summer-festival" => ("#B52E52", "#EAA0AE", "#FFF0D8", "#D5A447"),
            "mage-academy" => ("#2F285A", "#6655AC", "#B2D8FA", "#E8C96C"),
            "royal-ballgown" => ("#542A68", "#A66CC2", "#F6DFF6", "#EFC66C"),
            "winter-parka" => ("#266B75", "#55B5B6", "#E6FAF9", "#D9829C"),
            _ => ("#292A46", "#6345A9", "#4DE7E2", "#E65A8C")
        };
        var main = Brush(palette.Item1);
        var light = Brush(palette.Item2);
        var accent = Brush(palette.Item3);
        var trim = Brush(palette.Item4);

        switch (id)
        {
            case "sailor-academy":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 186,391 L 226,380 L 256,412 L 286,380 L 326,391 L 347,592 L 319,622 L 193,622 L 165,592 Z"));
                dc.DrawGeometry(light, FineOutline, Geometry.Parse("M 187,398 L 225,396 L 256,436 L 287,396 L 325,398 L 300,474 L 256,495 L 212,474 Z"));
                dc.DrawGeometry(trim, FineOutline, Geometry.Parse("M 237,428 L 275,428 L 292,495 L 256,528 L 220,495 Z"));
                dc.DrawGeometry(accent, null, Geometry.Parse("M 224,512 L 288,512 L 306,596 L 206,596 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 194,586 L 318,586 L 356,692 L 156,692 Z"));
                for (var i = 0; i < 5; i++) dc.DrawLine(new Pen(accent, 3), new Point(190 + i * 22, 600), new Point(174 + i * 32, 686));
                DrawSleeves(dc, main, light);
                break;
            case "gothic-rose":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,390 L 224,380 L 256,414 L 288,380 L 328,390 L 345,606 L 314,628 L 198,628 L 167,606 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 190,414 L 222,405 L 256,438 L 290,405 L 322,414 L 304,475 L 256,508 L 208,475 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 243,429 L 269,429 L 284,492 L 256,520 L 228,492 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 196,594 L 316,594 L 375,700 L 137,700 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 213,608 L 299,608 L 327,681 L 185,681 Z"));
                for (var i = 0; i < 7; i++) dc.DrawLine(new Pen(accent, 3), new Point(177 + i * 27, 678), new Point(199 + i * 21, 693));
                DrawSleeves(dc, main, light);
                dc.DrawGeometry(trim, null, Geometry.Parse("M 256,514 L 268,529 L 256,546 L 244,529 Z"));
                break;
            case "shrine-ceremony":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,389 L 220,380 L 256,421 L 292,380 L 328,389 L 344,594 L 309,623 L 203,623 L 168,594 Z"));
                dc.DrawGeometry(trim, FineOutline, Geometry.Parse("M 225,401 L 256,431 L 287,401 L 268,496 L 256,512 L 244,496 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 203,436 L 224,422 L 242,601 L 215,608 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 309,436 L 288,422 L 270,601 L 297,608 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 197,583 L 315,583 L 368,694 L 144,694 Z"));
                dc.DrawGeometry(trim, null, Geometry.Parse("M 153,680 L 359,680 L 368,694 L 144,694 Z"));
                DrawWideSleeves(dc, main, light);
                dc.DrawEllipse(trim, FineOutline, new Point(256, 542), 17, 17);
                break;
            case "tech-streetwear":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,388 L 220,380 L 256,417 L 292,380 L 328,388 L 350,603 L 324,632 L 188,632 L 162,603 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 190,404 L 223,397 L 256,438 L 289,397 L 322,404 L 310,477 L 256,519 L 202,477 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 239,429 L 273,429 L 284,482 L 256,508 L 228,482 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 192,596 L 320,596 L 353,692 L 159,692 Z"));
                dc.DrawLine(new Pen(accent, 8), new Point(186, 514), new Point(202, 610));
                dc.DrawLine(new Pen(accent, 8), new Point(326, 514), new Point(310, 610));
                dc.DrawRoundedRectangle(trim, FineOutline, new Rect(294, 546, 33, 45), 5, 5);
                DrawSleeves(dc, main, light);
                break;
            case "night-pirate":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 182,387 L 222,378 L 256,418 L 290,378 L 330,387 L 350,605 L 320,631 L 192,631 L 162,605 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 204,401 L 229,397 L 256,431 L 283,397 L 308,401 L 296,484 L 256,513 L 216,484 Z"));
                dc.DrawGeometry(trim, FineOutline, Geometry.Parse("M 243,426 L 269,426 L 286,493 L 256,521 L 226,493 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 195,595 L 317,595 L 354,693 L 158,693 Z"));
                dc.DrawLine(new Pen(trim, 5), new Point(194, 417), new Point(215, 597));
                dc.DrawLine(new Pen(trim, 5), new Point(318, 417), new Point(297, 597));
                for (var i = 0; i < 3; i++) dc.DrawEllipse(trim, null, new Point(216 + i * 40, 548), 4, 4);
                DrawWideSleeves(dc, main, light);
                break;
            case "fantasy-knight":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 185,391 L 224,378 L 256,415 L 288,378 L 327,391 L 343,589 L 313,618 L 199,618 L 169,589 Z"));
                dc.DrawGeometry(light, FineOutline, Geometry.Parse("M 207,414 L 228,402 L 256,431 L 284,402 L 305,414 L 286,498 L 256,521 L 226,498 Z"));
                dc.DrawGeometry(trim, null, Geometry.Parse("M 244,427 L 268,427 L 279,485 L 256,505 L 233,485 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 196,583 L 316,583 L 352,695 L 160,695 Z"));
                dc.DrawLine(new Pen(trim, 7), new Point(205, 609), new Point(304, 609));
                DrawSleeves(dc, main, light);
                break;
            case "comfy-hoodie":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 180,390 L 220,379 L 256,419 L 292,379 L 332,390 L 356,604 L 328,632 L 184,632 L 156,604 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 205,409 L 232,407 L 256,432 L 280,407 L 307,409 L 291,484 L 256,505 L 221,484 Z"));
                dc.DrawRoundedRectangle(trim, FineOutline, new Rect(216, 546, 80, 44), 12, 12);
                dc.DrawEllipse(accent, null, new Point(243, 569), 5, 5);
                dc.DrawEllipse(accent, null, new Point(269, 569), 5, 5);
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 193,598 L 319,598 L 348,692 L 164,692 Z"));
                DrawSleeves(dc, main, light);
                break;
            case "celestial-gown":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 183,390 L 220,379 L 256,417 L 292,379 L 329,390 L 348,604 L 320,631 L 192,631 L 164,604 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 205,405 L 232,400 L 256,433 L 280,400 L 307,405 L 293,489 L 256,519 L 219,489 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 244,427 L 268,427 L 280,487 L 256,512 L 232,487 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 195,596 L 317,596 L 383,704 L 129,704 Z"));
                dc.DrawGeometry(trim, null, Geometry.Parse("M 146,673 L 366,673 L 383,704 L 129,704 Z"));
                DrawWideSleeves(dc, main, light);
                for (var i = 0; i < 3; i++) DrawStar(dc, new Point(207 + i * 48, 555 + (i % 2) * 21), 10, trim);
                break;
            case "cyber-runner":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 183,389 L 221,379 L 256,416 L 291,379 L 329,389 L 350,604 L 322,632 L 190,632 L 162,604 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 206,405 L 231,401 L 256,432 L 281,401 L 306,405 L 291,482 L 256,505 L 221,482 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 242,427 L 270,427 L 280,481 L 256,505 L 232,481 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 193,596 L 319,596 L 350,691 L 162,691 Z"));
                dc.DrawRoundedRectangle(trim, FineOutline, new Rect(187, 546, 42, 47), 7, 7);
                dc.DrawLine(new Pen(accent, 7), new Point(306, 515), new Point(326, 586));
                DrawSleeves(dc, main, light);
                break;
            case "idol-casual":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 183,389 L 220,379 L 256,413 L 292,379 L 329,389 L 343,590 L 314,621 L 198,621 L 169,590 Z"));
                dc.DrawGeometry(light, FineOutline, Geometry.Parse("M 201,403 L 230,397 L 256,429 L 282,397 L 311,403 L 294,474 L 256,504 L 218,474 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 234,424 L 278,424 L 287,490 L 256,516 L 225,490 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 196,586 L 316,586 L 353,692 L 159,692 Z"));
                for (var i = 0; i < 5; i++) dc.DrawLine(new Pen(trim, 4), new Point(183 + i * 34, 625), new Point(174 + i * 35, 679));
                DrawSleeves(dc, main, light);
                break;
            case "summer-festival":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 183,389 L 220,379 L 256,417 L 292,379 L 329,389 L 345,600 L 315,628 L 197,628 L 167,600 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 193,405 L 223,399 L 256,432 L 289,399 L 319,405 L 301,473 L 256,507 L 211,473 Z"));
                dc.DrawGeometry(trim, FineOutline, Geometry.Parse("M 240,424 L 272,424 L 280,489 L 256,514 L 232,489 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 195,590 L 317,590 L 378,699 L 134,699 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 227,608 L 285,608 L 312,685 L 200,685 Z"));
                dc.DrawLine(new Pen(trim, 10), new Point(201, 555), new Point(311, 577));
                DrawWideSleeves(dc, main, light);
                DrawStar(dc, new Point(256, 556), 10, accent);
                break;
            case "mage-academy":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,389 L 221,378 L 256,416 L 291,378 L 328,389 L 346,593 L 317,620 L 195,620 L 166,593 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 203,405 L 231,401 L 256,433 L 281,401 L 309,405 L 292,487 L 256,516 L 220,487 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 193,592 L 319,592 L 395,706 L 117,706 Z"));
                dc.DrawGeometry(trim, null, Geometry.Parse("M 246,426 L 266,426 L 281,486 L 256,508 L 231,486 Z"));
                dc.DrawLine(new Pen(trim, 6), new Point(201, 609), new Point(311, 609));
                dc.DrawLine(new Pen(trim, 6), new Point(185, 652), new Point(327, 652));
                DrawWideSleeves(dc, main, light);
                for (var i = 0; i < 3; i++) DrawStar(dc, new Point(224 + i * 32, 672), 7, accent);
                break;
            case "royal-ballgown":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,389 L 221,378 L 256,415 L 291,378 L 328,389 L 341,581 L 311,612 L 201,612 L 171,581 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 204,405 L 232,401 L 256,432 L 280,401 L 308,405 L 292,484 L 256,512 L 220,484 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 198,580 L 314,580 L 402,706 L 110,706 Z"));
                dc.DrawGeometry(trim, FineOutline, Geometry.Parse("M 240,426 L 272,426 L 283,487 L 256,511 L 229,487 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 152,666 L 360,666 L 384,695 L 128,695 Z"));
                DrawWideSleeves(dc, main, light);
                DrawStar(dc, new Point(256, 548), 12, trim);
                break;
            case "winter-parka":
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 182,390 L 220,378 L 256,416 L 292,378 L 330,390 L 350,610 L 320,635 L 192,635 L 162,610 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 203,401 L 232,398 L 256,429 L 280,398 L 309,401 L 291,487 L 256,514 L 221,487 Z"));
                dc.DrawRoundedRectangle(accent, FineOutline, new Rect(198, 549, 50, 38), 9, 9);
                dc.DrawRoundedRectangle(accent, FineOutline, new Rect(264, 549, 50, 38), 9, 9);
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 194,599 L 318,599 L 355,695 L 157,695 Z"));
                DrawWideSleeves(dc, main, light);
                dc.DrawLine(new Pen(trim, 7), new Point(256, 429), new Point(256, 615));
                break;
            default: // star idol
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 184,390 L 224,380 L 256,414 L 288,380 L 328,390 L 345,600 L 316,626 L 196,626 L 167,600 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 193,407 L 225,400 L 256,432 L 287,400 L 319,407 L 303,485 L 256,511 L 209,485 Z"));
                dc.DrawGeometry(accent, FineOutline, Geometry.Parse("M 242,426 L 270,426 L 284,492 L 256,519 L 228,492 Z"));
                dc.DrawGeometry(main, Outline, Geometry.Parse("M 195,592 L 317,592 L 365,699 L 147,699 Z"));
                dc.DrawGeometry(light, null, Geometry.Parse("M 214,605 L 298,605 L 319,673 L 193,673 Z"));
                DrawSleeves(dc, main, light);
                DrawStar(dc, new Point(256, 557), 16, trim);
                break;
        }
    }

    private static void DrawSleeves(DrawingContext dc, Brush main, Brush light)
    {
        dc.DrawGeometry(main, Outline, Geometry.Parse("M 188,391 C 158,395 144,418 136,456 L 122,548 C 119,570 132,584 151,579 L 177,563 L 203,449 Z"));
        dc.DrawGeometry(main, Outline, Geometry.Parse("M 324,391 C 354,395 368,418 376,456 L 390,548 C 393,570 380,584 361,579 L 335,563 L 309,449 Z"));
        dc.DrawGeometry(light, null, Geometry.Parse("M 133,539 L 173,545 L 170,568 L 143,575 Z"));
        dc.DrawGeometry(light, null, Geometry.Parse("M 379,539 L 339,545 L 342,568 L 369,575 Z"));
    }

    private static void DrawWideSleeves(DrawingContext dc, Brush main, Brush light)
    {
        dc.DrawGeometry(main, Outline, Geometry.Parse("M 189,390 L 163,403 L 118,518 L 110,573 L 148,581 L 202,473 Z"));
        dc.DrawGeometry(main, Outline, Geometry.Parse("M 323,390 L 349,403 L 394,518 L 402,573 L 364,581 L 310,473 Z"));
        dc.DrawGeometry(light, null, Geometry.Parse("M 116,539 L 151,548 L 148,575 L 111,565 Z"));
        dc.DrawGeometry(light, null, Geometry.Parse("M 396,539 L 361,548 L 364,575 L 401,565 Z"));
    }

    private static void DrawHeadAccessory(DrawingContext dc, string id)
    {
        var pink = Brush("#F078AB");
        var purple = Brush("#6A4AA8");
        var dark = Brush("#352445");
        var gold = Brush("#F6D477");
        switch (id)
        {
            case "double-bows":
                dc.DrawGeometry(pink, FineOutline, Geometry.Parse("M 174,164 C 143,133 143,112 175,129 L 197,144 L 174,164 Z"));
                dc.DrawGeometry(pink, FineOutline, Geometry.Parse("M 198,145 C 232,113 235,139 207,163 L 198,145 Z"));
                dc.DrawEllipse(gold, FineOutline, new Point(193, 149), 7, 7);
                dc.DrawGeometry(pink, FineOutline, Geometry.Parse("M 316,150 C 288,119 289,104 319,127 L 337,141 L 316,150 Z"));
                dc.DrawGeometry(pink, FineOutline, Geometry.Parse("M 335,140 C 365,112 374,136 345,160 L 335,140 Z"));
                break;
            case "cat-ears":
                dc.DrawGeometry(purple, Outline, Geometry.Parse("M 157,200 L 148,143 L 207,180 Z"));
                dc.DrawGeometry(purple, Outline, Geometry.Parse("M 355,200 L 364,143 L 305,180 Z"));
                dc.DrawGeometry(pink, null, Geometry.Parse("M 162,184 L 159,157 L 190,181 Z"));
                dc.DrawGeometry(pink, null, Geometry.Parse("M 350,184 L 353,157 L 322,181 Z"));
                break;
            case "bunny-ears":
                dc.DrawGeometry(Brush("#F5F0F8"), Outline, Geometry.Parse("M 185,185 C 159,143 150,82 178,72 C 211,75 218,138 213,177 Z"));
                dc.DrawGeometry(Brush("#F078AB"), null, Geometry.Parse("M 184,160 C 169,125 164,92 178,88 C 195,100 201,139 204,162 Z"));
                dc.DrawGeometry(Brush("#F5F0F8"), Outline, Geometry.Parse("M 302,178 C 299,129 312,72 340,71 C 370,86 350,146 327,185 Z"));
                dc.DrawGeometry(Brush("#F078AB"), null, Geometry.Parse("M 315,158 C 319,121 329,91 341,88 C 351,103 336,142 324,164 Z"));
                break;
            case "small-horns":
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 181,187 C 163,151 175,117 201,115 C 196,145 211,163 222,174 Z"));
                dc.DrawGeometry(dark, Outline, Geometry.Parse("M 331,187 C 349,151 337,117 311,115 C 316,145 301,163 290,174 Z"));
                dc.DrawEllipse(pink, null, new Point(198, 142), 5, 12);
                dc.DrawEllipse(pink, null, new Point(314, 142), 5, 12);
                break;
            case "star-halo":
                dc.DrawEllipse(null, new Pen(gold, 6), new Point(256, 83), 83, 18);
                DrawStar(dc, new Point(184, 79), 11, gold);
                DrawStar(dc, new Point(330, 80), 9, Brush("#76E8E1"));
                break;
            case "shark-cap":
                dc.DrawGeometry(Brush("#419BCB"), Outline, Geometry.Parse("M 149,196 C 132,148 170,116 226,114 L 292,120 C 343,129 370,159 363,199 L 331,208 L 180,208 Z"));
                dc.DrawGeometry(Brush("#D8F5FF"), FineOutline, Geometry.Parse("M 182,173 C 202,148 229,143 256,151 C 283,143 310,148 330,173 L 303,182 L 285,177 L 272,188 L 256,181 L 240,188 L 227,177 L 209,182 Z"));
                dc.DrawEllipse(Brush("#2F234B"), null, new Point(222, 169), 6, 7);
                dc.DrawEllipse(Brush("#2F234B"), null, new Point(290, 169), 6, 7);
                dc.DrawGeometry(Brush("#FFFFFF"), null, Geometry.Parse("M 241,179 L 248,192 L 255,180 Z"));
                dc.DrawGeometry(Brush("#FFFFFF"), null, Geometry.Parse("M 257,180 L 264,192 L 271,179 Z"));
                break;
            case "mini-crown":
                dc.DrawGeometry(gold, FineOutline, Geometry.Parse("M 181,139 L 203,162 L 229,128 L 255,160 L 285,126 L 309,162 L 333,139 L 322,184 L 192,184 Z"));
                dc.DrawEllipse(pink, null, new Point(225, 157), 5, 5);
                dc.DrawEllipse(Brush("#72E9E2"), null, new Point(286, 156), 5, 5);
                break;
            case "gamer-headset":
                dc.DrawGeometry(null, new Pen(dark, 15), Geometry.Parse("M 150,237 C 135,147 183,104 256,108 C 329,104 377,147 362,237"));
                dc.DrawRoundedRectangle(purple, FineOutline, new Rect(135, 217, 33, 71), 12, 12);
                dc.DrawRoundedRectangle(purple, FineOutline, new Rect(344, 217, 33, 71), 12, 12);
                dc.DrawEllipse(Brush("#54E4E1"), null, new Point(151, 252), 6, 12);
                dc.DrawEllipse(Brush("#54E4E1"), null, new Point(360, 252), 6, 12);
                break;
            case "flower-clip":
                dc.DrawEllipse(Brush("#F078AB"), FineOutline, new Point(327, 167), 12, 12);
                dc.DrawEllipse(Brush("#F078AB"), FineOutline, new Point(342, 180), 12, 12);
                dc.DrawEllipse(Brush("#F078AB"), FineOutline, new Point(326, 191), 12, 12);
                dc.DrawEllipse(gold, null, new Point(333, 179), 7, 7);
                dc.DrawGeometry(Brush("#4EAB7F"), null, Geometry.Parse("M 325,190 L 305,210 L 327,204 Z"));
                break;
            case "fox-ears":
                dc.DrawGeometry(Brush("#C96752"), Outline, Geometry.Parse("M 155,192 L 148,116 L 213,174 Z"));
                dc.DrawGeometry(Brush("#C96752"), Outline, Geometry.Parse("M 357,192 L 364,116 L 299,174 Z"));
                dc.DrawGeometry(Brush("#FFE0C9"), null, Geometry.Parse("M 162,174 L 158,134 L 194,173 Z"));
                dc.DrawGeometry(Brush("#FFE0C9"), null, Geometry.Parse("M 350,174 L 354,134 L 318,173 Z"));
                break;
            case "witch-hat":
                dc.DrawGeometry(Brush("#393052"), Outline, Geometry.Parse("M 176,185 L 226,78 L 267,66 L 306,186 Z"));
                dc.DrawEllipse(Brush("#252039"), FineOutline, new Point(256, 184), 111, 17);
                dc.DrawRoundedRectangle(Brush("#E9C66A"), null, new Rect(222, 142, 66, 13), 3, 3);
                break;
            case "crescent-tiara":
                dc.DrawGeometry(Brush("#EACB72"), FineOutline, Geometry.Parse("M 175,188 C 186,126 222,111 256,128 C 290,111 326,126 337,188 L 318,186 L 306,150 L 281,173 L 256,134 L 231,173 L 206,150 L 194,186 Z"));
                dc.DrawGeometry(Brush("#EACB72"), null, Geometry.Parse("M 267,90 C 242,104 245,132 264,141 C 285,148 303,127 295,108 C 287,126 268,128 259,115 C 254,105 260,96 267,90 Z"));
                DrawStar(dc, new Point(202, 174), 7, Brush("#7AE6E7"));
                DrawStar(dc, new Point(311, 174), 7, Brush("#E68BB7"));
                break;
            case "robo-visor":
                dc.DrawRoundedRectangle(Brush("#24314F"), Outline, new Rect(168, 190, 176, 47), 16, 16);
                dc.DrawRoundedRectangle(Brush("#59E4E2"), FineOutline, new Rect(184, 199, 144, 28), 10, 10);
                dc.DrawLine(new Pen(Brush("#F7FCFF"), 3), new Point(202, 204), new Point(225, 218));
                break;
            default: // feather pin
                dc.DrawGeometry(Brush("#4A8FE0"), Outline, Geometry.Parse("M 328,176 C 291,155 306,116 342,102 C 359,132 357,160 328,176 Z"));
                dc.DrawLine(new Pen(gold, 3), new Point(323, 167), new Point(343, 119));
                dc.DrawLine(new Pen(Brush("#BCEEF6"), 4), new Point(334, 158), new Point(353, 145));
                break;
        }
    }

    private static void DrawFaceAccessory(DrawingContext dc, string id)
    {
        var gold = Brush("#EBC86E");
        var dark = Brush("#38283F");
        switch (id)
        {
            case "round-glasses":
                dc.DrawEllipse(null, new Pen(dark, 4), new Point(211, 249), 25, 27);
                dc.DrawEllipse(null, new Pen(dark, 4), new Point(301, 249), 25, 27);
                dc.DrawLine(new Pen(dark, 4), new Point(236, 247), new Point(276, 247));
                dc.DrawLine(new Pen(dark, 3), new Point(185, 243), new Point(176, 239));
                dc.DrawLine(new Pen(dark, 3), new Point(327, 243), new Point(336, 239));
                break;
            case "star-earrings":
                DrawStar(dc, new Point(151, 282), 10, gold);
                DrawStar(dc, new Point(361, 282), 10, gold);
                dc.DrawLine(new Pen(gold, 3), new Point(151, 264), new Point(151, 272));
                dc.DrawLine(new Pen(gold, 3), new Point(361, 264), new Point(361, 272));
                break;
            case "heart-choker":
                dc.DrawRoundedRectangle(dark, null, new Rect(226, 365, 60, 13), 5, 5);
                dc.DrawGeometry(Brush("#F078AB"), FineOutline, Geometry.Parse(
                    "M 256,389 C 238,377 242,362 256,372 C 270,362 274,377 256,389 Z"));
                break;
            case "star-eyepatch":
                dc.DrawGeometry(dark, FineOutline, Geometry.Parse("M 182,223 L 235,222 L 229,271 L 187,270 Z"));
                DrawStar(dc, new Point(208, 246), 11, gold);
                dc.DrawLine(new Pen(dark, 4), new Point(184, 225), new Point(169, 209));
                break;
            case "mic-headset":
                dc.DrawGeometry(null, new Pen(dark, 5), Geometry.Parse("M 353,221 C 367,237 370,258 359,275"));
                dc.DrawGeometry(null, new Pen(Brush("#5D526F"), 4), Geometry.Parse("M 359,275 C 338,299 320,306 296,307"));
                dc.DrawEllipse(Brush("#53E6DE"), FineOutline, new Point(294, 307), 7, 7);
                break;
            case "face-mask":
                dc.DrawRoundedRectangle(Brush("#D8DAE8"), FineOutline, new Rect(198, 267, 116, 42), 12, 12);
                dc.DrawLine(new Pen(Brush("#A7B0CA"), 3), new Point(198, 276), new Point(184, 267));
                dc.DrawLine(new Pen(Brush("#A7B0CA"), 3), new Point(314, 276), new Point(328, 267));
                dc.DrawLine(new Pen(Brush("#A7B0CA"), 2), new Point(212, 282), new Point(300, 282));
                dc.DrawLine(new Pen(Brush("#A7B0CA"), 2), new Point(212, 292), new Point(300, 292));
                break;
            case "cheek-stars":
                DrawStar(dc, new Point(177, 289), 8, gold);
                DrawStar(dc, new Point(335, 289), 8, gold);
                dc.DrawEllipse(Brush("#E68BB7"), null, new Point(189, 302), 4, 4);
                dc.DrawEllipse(Brush("#E68BB7"), null, new Point(323, 302), 4, 4);
                break;
            case "neck-ribbon":
                dc.DrawGeometry(Brush("#D85CA8"), FineOutline, Geometry.Parse("M 226,368 L 246,378 L 256,366 L 266,378 L 286,368 L 276,393 L 256,402 L 236,393 Z"));
                dc.DrawEllipse(gold, FineOutline, new Point(256, 384), 5, 5);
                break;
            default: // geometric pendant
                dc.DrawLine(new Pen(gold, 3), new Point(256, 361), new Point(256, 386));
                dc.DrawGeometry(Brush("#53E6DE"), FineOutline, Geometry.Parse("M 256,384 L 269,397 L 256,411 L 243,397 Z"));
                break;
        }
    }

    private static void DrawBackAccessory(DrawingContext dc, string id)
    {
        switch (id)
        {
            case "short-cape":
                dc.DrawGeometry(Brush("#8B314F"), Outline, Geometry.Parse("M 190,383 L 222,379 L 256,407 L 290,379 L 322,383 L 352,577 L 320,614 L 192,614 L 160,577 Z"));
                dc.DrawGeometry(Brush("#D4A653"), null, Geometry.Parse("M 221,402 L 256,422 L 291,402 L 281,433 L 256,449 L 231,433 Z"));
                break;
            case "bat-wings":
                dc.DrawGeometry(Brush("#44304F"), Outline, Geometry.Parse("M 200,409 C 151,374 110,386 89,432 L 130,430 L 102,477 L 162,459 L 184,505 L 222,461 Z"));
                dc.DrawGeometry(Brush("#44304F"), Outline, Geometry.Parse("M 312,409 C 361,374 402,386 423,432 L 382,430 L 410,477 L 350,459 L 328,505 L 290,461 Z"));
                break;
            case "celestial-wings":
                dc.DrawGeometry(Brush("#F7F4FF"), FineOutline, Geometry.Parse("M 209,414 C 164,368 121,388 112,432 C 143,419 155,451 174,470 C 178,439 198,453 219,467 Z"));
                dc.DrawGeometry(Brush("#F7F4FF"), FineOutline, Geometry.Parse("M 303,414 C 348,368 391,388 400,432 C 369,419 357,451 338,470 C 334,439 314,453 293,467 Z"));
                dc.DrawLine(new Pen(Brush("#D6B65D"), 4), new Point(210, 420), new Point(169, 441));
                dc.DrawLine(new Pen(Brush("#D6B65D"), 4), new Point(302, 420), new Point(343, 441));
                break;
            case "bunny-backpack":
                dc.DrawRoundedRectangle(Brush("#F4F0F6"), Outline, new Rect(321, 444, 65, 87), 20, 20);
                dc.DrawGeometry(Brush("#F4F0F6"), FineOutline, Geometry.Parse("M 333,448 C 317,418 320,397 337,398 C 354,404 352,431 350,450 Z"));
                dc.DrawGeometry(Brush("#F4F0F6"), FineOutline, Geometry.Parse("M 359,448 C 374,418 371,397 354,398 C 337,404 340,431 342,450 Z"));
                dc.DrawEllipse(Brush("#F078AB"), null, new Point(342, 472), 4, 4);
                dc.DrawEllipse(Brush("#F078AB"), null, new Point(364, 472), 4, 4);
                break;
            case "fin-ornament":
                dc.DrawGeometry(Brush("#68C8E8"), Outline, Geometry.Parse("M 173,400 C 134,395 111,426 117,459 C 141,445 160,451 187,464 Z"));
                dc.DrawGeometry(Brush("#68C8E8"), Outline, Geometry.Parse("M 339,400 C 378,395 401,426 395,459 C 371,445 352,451 325,464 Z"));
                dc.DrawLine(new Pen(Brush("#D9FAFF"), 4), new Point(144, 414), new Point(163, 450));
                dc.DrawLine(new Pen(Brush("#D9FAFF"), 4), new Point(368, 414), new Point(349, 450));
                break;
            case "fox-tail":
                dc.DrawGeometry(Brush("#D57955"), Outline, Geometry.Parse("M 321,521 C 372,514 397,480 389,444 C 426,454 444,492 426,525 C 408,563 369,584 329,568 Z"));
                dc.DrawGeometry(Brush("#FFF0DB"), FineOutline, Geometry.Parse("M 391,452 C 418,468 424,489 413,507 C 401,522 385,528 371,527 C 390,511 402,486 391,452 Z"));
                break;
            case "mechanical-pack":
                dc.DrawRoundedRectangle(Brush("#37445E"), Outline, new Rect(323, 434, 70, 93), 12, 12);
                dc.DrawRoundedRectangle(Brush("#55E5DC"), FineOutline, new Rect(335, 447, 46, 19), 5, 5);
                dc.DrawLine(new Pen(Brush("#D4E7FF"), 4), new Point(343, 485), new Point(373, 485));
                dc.DrawLine(new Pen(Brush("#D4E7FF"), 4), new Point(343, 498), new Point(373, 498));
                dc.DrawLine(new Pen(Brush("#E9C66A"), 5), new Point(335, 434), new Point(323, 412));
                dc.DrawLine(new Pen(Brush("#E9C66A"), 5), new Point(381, 434), new Point(393, 412));
                break;
            case "crystal-orbit":
                dc.DrawGeometry(Brush("#83DCEB"), FineOutline, Geometry.Parse("M 157,438 L 174,402 L 191,438 L 174,466 Z"));
                dc.DrawGeometry(Brush("#B69BFA"), FineOutline, Geometry.Parse("M 356,490 L 375,447 L 394,490 L 375,520 Z"));
                dc.DrawGeometry(Brush("#EAD1F5"), FineOutline, Geometry.Parse("M 338,392 L 349,365 L 360,392 L 349,413 Z"));
                dc.DrawGeometry(null, new Pen(Brush("#D9C5F2"), 3), Geometry.Parse("M 161,421 C 215,371 308,375 369,445"));
                break;
            default: // starlight ribbons
                dc.DrawGeometry(Brush("#8C7AE6"), null, Geometry.Parse("M 222,410 C 190,465 208,503 181,556 C 159,600 183,640 162,683 L 188,676 C 208,633 190,604 215,563 C 240,523 223,467 238,420 Z"));
                dc.DrawGeometry(Brush("#E66DAA"), null, Geometry.Parse("M 288,410 C 320,465 302,503 329,556 C 351,600 327,640 348,683 L 322,676 C 302,633 320,604 295,563 C 270,523 287,467 272,420 Z"));
                DrawStar(dc, new Point(183, 585), 9, Brush("#F6D477"));
                DrawStar(dc, new Point(329, 585), 9, Brush("#F6D477"));
                break;
        }
    }


    private static void DrawPose(DrawingContext dc, string poseId, string skinId, string outfitId)
    {
        var skinHex = skinId switch
        {
            "warm" => "#F7CDAA",
            "cocoa" => "#B97965",
            "moonlight" => "#D9D0FF",
            _ => "#FFE0C8"
        };
        var skin = Brush(skinHex);
        var sleeveHex = outfitId switch
        {
            "star-idol" => "#233A82",
            "sailor-academy" => "#233956",
            "gothic-rose" => "#25202F",
            "shrine-ceremony" => "#AC3356",
            "tech-streetwear" => "#382966",
            "night-pirate" => "#54253B",
            "fantasy-knight" => "#667FA7",
            "comfy-hoodie" => "#74BDB8",
            "celestial-gown" => "#1B2C65",
            "cyber-runner" => "#382966",
            "idol-casual" => "#7C2D5C",
            "summer-festival" => "#B52E52",
            "mage-academy" => "#2F285A",
            "royal-ballgown" => "#542A68",
            "winter-parka" => "#266B75",
            _ => "#7660B5"
        };
        var armPen = new Pen(Brush("#352445"), 7) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var sleeve = Brush(sleeveHex);
        var cuff = Brush("#F4C96B");

        void Arm(string geometry) => dc.DrawGeometry(sleeve, armPen, Geometry.Parse(geometry));
        void Cuff(Point center) => dc.DrawEllipse(cuff, armPen, center, 13, 9);
        void Hand(Point center, double rx = 13, double ry = 15) => dc.DrawEllipse(skin, armPen, center, rx, ry);
        void NeutralLeft() =>
            Arm("M 193,384 C 164,390 148,420 144,457 L 132,562 C 130,585 141,600 156,597 C 169,594 174,579 175,562 L 199,461 Z");
        void NeutralRight() =>
            Arm("M 319,384 C 348,390 364,420 368,457 L 380,562 C 382,585 371,600 356,597 C 343,594 338,579 337,562 L 313,461 Z");

        switch (poseId)
        {
            case "wave":
                NeutralLeft();
                Arm("M 319,390 C 342,374 356,344 365,315 L 374,288 C 378,273 392,273 398,286 C 403,297 396,320 391,337 L 381,371 C 371,409 349,438 326,457 Z");
                Cuff(new Point(358, 342));
                Hand(new Point(393, 276), 14, 17);
                dc.DrawLine(armPen, new Point(385, 263), new Point(380, 244));
                dc.DrawLine(armPen, new Point(394, 262), new Point(397, 241));
                dc.DrawLine(armPen, new Point(402, 267), new Point(413, 250));
                break;
            case "peace":
                NeutralLeft();
                Arm("M 319,390 C 341,378 348,354 348,333 L 349,310 C 349,296 361,291 370,302 C 378,312 374,331 370,346 L 366,371 C 362,399 345,419 326,442 Z");
                Cuff(new Point(349, 342));
                Hand(new Point(359, 296), 16, 17);
                dc.DrawLine(armPen, new Point(352, 289), new Point(343, 265));
                dc.DrawLine(armPen, new Point(362, 288), new Point(365, 261));
                dc.DrawLine(armPen, new Point(373, 294), new Point(383, 277));
                break;
            case "hands-hips":
                Arm("M 193,384 C 167,400 163,425 184,449 L 222,480 L 238,463 L 208,425 Z");
                Arm("M 319,384 C 345,400 349,425 328,449 L 290,480 L 274,463 L 304,425 Z");
                Cuff(new Point(220, 474));
                Cuff(new Point(292, 474));
                Hand(new Point(230, 484), 12, 12);
                Hand(new Point(282, 484), 12, 12);
                break;
            case "crossed-arms":
                Arm("M 193,387 C 172,405 169,434 192,451 L 274,486 C 287,491 301,484 300,471 C 299,460 288,455 276,450 L 215,414 Z");
                Arm("M 319,387 C 340,405 343,434 320,451 L 238,486 C 225,491 211,484 212,471 C 213,460 224,455 236,450 L 297,414 Z");
                Hand(new Point(297, 451), 12, 12);
                Hand(new Point(215, 451), 12, 12);
                break;
            case "microphone":
                NeutralLeft();
                Arm("M 319,388 C 341,375 346,353 335,335 L 304,317 C 294,310 296,296 308,290 C 318,285 330,293 340,302 L 370,330 C 391,350 370,390 329,433 Z");
                Cuff(new Point(337, 349));
                Hand(new Point(303, 304), 12, 13);
                dc.DrawRoundedRectangle(Brush("#2D254A"), armPen, new Rect(289, 282, 16, 28), 7, 7);
                dc.DrawEllipse(Brush("#E86AA6"), null, new Point(297, 286), 5, 4);
                break;
            default:
                NeutralLeft();
                NeutralRight();
                Cuff(new Point(155, 567));
                Cuff(new Point(357, 567));
                Hand(new Point(153, 588), 12, 14);
                Hand(new Point(359, 588), 12, 14);
                break;
        }

        // Small sleeve accents make pose layers read as clothing rather than disconnected skin shapes.
        dc.DrawEllipse(sleeve, null, new Point(191, 414), 10, 17);
        dc.DrawEllipse(sleeve, null, new Point(321, 414), 10, 17);
    }

    /// <summary>
    /// Recolors the mid-tones of a transparent PNG while preserving its alpha, dark outlines and light highlights.
    /// Assets are written to a separate local folder so the original catalog PNGs are never modified.
    /// </summary>
    public static string TintPng(string sourcePath, string destinationDirectory, Color tint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("No se encontró la pieza que se desea recolorear.", sourcePath);

        Directory.CreateDirectory(destinationDirectory);
        var suffix = $"{tint.R:X2}{tint.G:X2}{tint.B:X2}";
        var destinationPath = Path.Combine(
            Path.GetFullPath(destinationDirectory),
            Path.GetFileNameWithoutExtension(sourcePath) + "-" + suffix + ".png");

        var input = new BitmapImage();
        input.BeginInit();
        input.CacheOption = BitmapCacheOption.OnLoad;
        input.UriSource = new Uri(Path.GetFullPath(sourcePath), UriKind.Absolute);
        input.EndInit();
        input.Freeze();

        BitmapSource pixelsSource = new FormatConvertedBitmap(input, PixelFormats.Bgra32, null, 0);
        var width = pixelsSource.PixelWidth;
        var height = pixelsSource.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        pixelsSource.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == 0)
                continue;

            var blue = pixels[i];
            var green = pixels[i + 1];
            var red = pixels[i + 2];
            var luminance = (red * 0.2126) + (green * 0.7152) + (blue * 0.0722);

            // Protect dark linework and near-white shine so recolors keep the illustrated style.
            if (luminance < 42 || luminance > 244)
                continue;

            var brightness = 0.34 + 0.66 * (luminance / 255.0);
            var targetRed = tint.R * brightness;
            var targetGreen = tint.G * brightness;
            var targetBlue = tint.B * brightness;
            var mix = luminance < 82 ? 0.42 : luminance > 218 ? 0.48 : 0.84;

            pixels[i] = ToByte(blue * (1 - mix) + targetBlue * mix);
            pixels[i + 1] = ToByte(green * (1 - mix) + targetGreen * mix);
            pixels[i + 2] = ToByte(red * (1 - mix) + targetRed * mix);
        }

        var outputBitmap = BitmapSource.Create(
            width, height, pixelsSource.DpiX, pixelsSource.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        outputBitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(outputBitmap));
        using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            encoder.Save(output);

        return destinationPath;

        static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private static void DrawStar(DrawingContext dc, Point center, double radius, Brush fill)
    {
        var points = new Point[10];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var distance = i % 2 == 0 ? radius : radius * .43;
            points[i] = new Point(center.X + Math.Cos(angle) * distance, center.Y + Math.Sin(angle) * distance);
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true, true);
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(fill, new Pen(Brush("#55352445"), 2), geometry);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var value = hex.TrimStart('#');
        var color = value.Length switch
        {
            6 => Color.FromRgb(Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16)),
            8 => Color.FromArgb(Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16), Convert.ToByte(value.Substring(6, 2), 16)),
            _ => throw new ArgumentException("Color hexadecimal no válido.", nameof(hex))
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
