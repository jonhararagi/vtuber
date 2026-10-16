using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Wpf;
using NAudio.Wave;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private CancellationTokenSource? activeLocalAvatarSegmentation;

    private sealed record AvatarProfilePackageManifest(
        int FormatVersion,
        string Name,
        string? BaseAsset,
        List<AvatarProfilePackageLayer> Layers,
        Dictionary<string, string> Expressions,
        Dictionary<string, string>? Prompts = null);

    private sealed record AvatarProfilePackageLayer(
        string Name,
        string AssetEntry,
        int SortOrder,
        bool Visible,
        double OffsetX,
        double OffsetY,
        double Scale,
        double RotationDegrees);

    private sealed record ChibiAvatarPreset(
        string Id,
        string Name,
        string Description,
        string HairId,
        string OutfitId,
        string HeadAccessoryId,
        string FaceAccessoryId,
        string BackAccessoryId,
        string FaceShapeId,
        string EyeStyleId,
        string SkinId,
        string PoseId);

    private static readonly IReadOnlyList<ChibiAvatarPreset> ChibiAvatarPresets =
    [
        new("mocha-mel", "Miel de café", "Chibi de piel marrón medio-profunda, cabello castaño con inner hair miel claro, iris marrones y pupilas marrón oscuro.",
            "brown-inner-light", "comfy-hoodie", "double-bows", "heart-choker", "bunny-backpack",
            "heart", "brown", "brown", "wave"),
        new("neko-gamer", "Neko gamer", "Chibi con coletas, orejas de gato, streetwear y pose de paz.",
            "twin-tails", "tech-streetwear", "cat-ears", "cheek-stars", "mechanical-pack",
            "round", "rose", "warm", "peace"),
        new("lunar-witch", "Brujita lunar", "Maga de fantasía con rizos, sombrero y cristales.",
            "royal-curls", "mage-academy", "witch-hat", "round-glasses", "crystal-orbit",
            "elf", "violet", "moonlight", "microphone"),
        new("star-idol", "Idol estelar", "Cantante con coleta alta, lazos y alas luminosas.",
            "high-ponytail", "star-idol", "double-bows", "star-earrings", "celestial-wings",
            "heart", "ocean", "porcelain", "wave"),
        new("gothic-rose", "Gótica rosa", "Vestido gótico, cuernos y alas oscuras.",
            "long-waves", "gothic-rose", "small-horns", "star-eyepatch", "bat-wings",
            "sharp", "heterochromia", "cocoa", "crossed-arms"),
        new("cyber-neon", "Cyber neón", "Bob futurista, visor y conjunto tecnológico.",
            "soft-bob", "cyber-runner", "robo-visor", "mic-headset", "mechanical-pack",
            "sharp", "emerald", "moonlight", "hands-hips"),
        new("casual-streamer", "Streamer casual", "Pelo corto, hoodie, auriculares y mochila conejito.",
            "short-spiky", "comfy-hoodie", "gamer-headset", "heart-choker", "bunny-backpack",
            "round", "gold", "warm", "neutral"),
        new("fox-festival", "Zorrita de festival", "Yukata de verano, orejas de zorro y cola esponjosa.",
            "side-braid", "summer-festival", "fox-ears", "neck-ribbon", "fox-tail",
            "heart", "rose", "cocoa", "wave"),
        new("winter-oracle", "Oráculo invernal", "Abrigo de invierno, tiara lunar y capa corta.",
            "silver-cyan", "winter-parka", "crescent-tiara", "mask-pendant", "short-cape",
            "sharp", "sleepy", "porcelain", "neutral"),
        new("shrine-fox", "Guardiana kitsune", "Vestimenta ceremonial, trenza lisa y cola de zorro.",
            "long-straight", "shrine-ceremony", "fox-ears", "neck-ribbon", "fox-tail",
            "elf", "heterochromia", "cocoa", "hands-hips"),
        new("celestial-pop", "Pop celestial", "Vestido estelar, halo luminoso y alas de luz.",
            "long-waves", "celestial-gown", "star-halo", "star-earrings", "celestial-wings",
            "heart", "ocean", "moonlight", "wave"),
        new("pirate-captain", "Capitana pirata", "Abrigo vino, pluma, capa y pose desafiante.",
            "mint-braid", "night-pirate", "feather-pin", "mask-pendant", "short-cape",
            "sharp", "gold", "warm", "crossed-arms"),
        new("bunny-cafe", "Conejita de café", "Moños dobles, orejas largas y mochila conejito.",
            "double-buns", "comfy-hoodie", "bunny-ears", "heart-choker", "bunny-backpack",
            "round", "rose", "cocoa", "peace"),
        new("ocean-pop-inspiration", "Océano pop · referencia marina", "Inspiración general en VTubers de temática tiburón/mar; diseño propio, no copia de un modelo oficial.",
            "soft-bob", "sailor-academy", "shark-cap", "star-earrings", "fin-ornament",
            "round", "ocean", "warm", "peace"),
        new("reaper-rock-inspiration", "Reaper rock · referencia gótica", "Inspiración general en idols de estética reaper/rock; paleta y silueta editables, sin reutilizar arte ajeno.",
            "long-waves", "gothic-rose", "small-horns", "star-eyepatch", "bat-wings",
            "sharp", "rose", "cocoa", "microphone"),
        new("stellar-singer-inspiration", "Cantante estelar · referencia idol", "Inspiración en VTubers musicales y diseños celestes; combinación original para personalizar.",
            "high-ponytail", "star-idol", "star-halo", "star-earrings", "starlight-ribbons",
            "heart", "ocean", "morena", "wave"),
        new("cyber-dragon-inspiration", "Dragona cyber · referencia neón", "Inspiración general en VTubers dragón y cyberpunk; no reproduce un personaje existente.",
            "silver-cyan", "cyber-runner", "small-horns", "mic-headset", "mechanical-pack",
            "sharp", "emerald", "cocoa", "hands-hips"),
        new("demon-gamer-inspiration", "Demonio gamer · referencia streamer", "Inspiración en siluetas gamer demoníacas con colores propios y piezas intercambiables.",
            "short-spiky", "tech-streetwear", "small-horns", "cheek-stars", "short-cape",
            "round", "brown", "morena", "peace"),
        new("kitsune-idol-inspiration", "Kitsune idol · referencia japonesa", "Inspiración en folklore kitsune e idols japonesas; vestuario y rasgos originales.",
            "long-straight", "shrine-ceremony", "fox-ears", "neck-ribbon", "fox-tail",
            "heart", "brown", "morena", "wave")
    ];

    private UIElement BuildAvatarStudio()
    {
        var root = new StackPanel();

        // Keep the built-in chibi gallery visible as the quickest path to a usable avatar.
        var beginnerPanel = Panel("Crear y personalizar tu personaje");
        beginnerPanel.Margin = new Thickness(0, 0, 0, 14);
        beginnerPanel.Children.Add(Label(
            "Elegí uno de los personajes chibi de la galería de abajo o importá tu propia ilustración. El taller 3D integrado es otra opción para experimentar con un modelo paramétrico.",
            12, "#98A4BE"));

        beginnerPanel.Children.Add(Label(
            "El taller 3D genera un personaje paramétrico dentro de VTuberBot, permite cambiar silueta, piel, pelo e inner hair, ojos, ropa y accesorios, y guardar la apariencia en JSON o exportar un GLB estático. Requiere Internet para cargar Three.js. No es un editor VRM: el GLB no tiene rig ni animaciones y todavía no se puede usar como modelo animado en el overlay.",
            12, "#98A4BE"));

        Button startLocalSegmentation = null!;
        Button cancelLocalSegmentation = null!;
        var localSegmentationStatus = Label(
            "Listo. PNGTuber recomendado para empezar. Para 2.5D, el modelo local es experimental y separa tres clases aproximadas.",
            12, "#98A4BE");
        var localSegmentationProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            Margin = new Thickness(0, 8, 0, 8),
            IsIndeterminate = false,
            Visibility = Visibility.Collapsed
        };

        // Wayfinding for users who confuse the five avatar creation modes.
        var editorGuide = Panel("¿Qué editor tengo que abrir?");
        editorGuide.Margin = new Thickness(0, 0, 0, 10);
        editorGuide.Background = Brush("#1C2A3D");
        editorGuide.Children.Add(Label(
            "• EDITOR PNGTUBER · capas y piezas: el editor principal para importar PNG/GIF, combinar accesorios, mover capas y exportar el personaje.\n• ENCUADRE DE CÁMARA / OBS: herramienta complementaria para recortar rostro, busto o cuerpo y preparar un PNG recortado; su vista OBS es orientativa, no controla OBS en tiempo real.\n• Crear PNGTuber con mis imágenes: asistente rápido.\n• 2.5D experimental: separa una imagen con IA local aproximada.\n• Taller 3D / VRM / VRoid: otros flujos; no son necesarios para editar capas PNG.",
            12, "#D8E5F5"));
        beginnerPanel.Children.Add(editorGuide);

        var beginnerActions = new WrapPanel();
        beginnerActions.Children.Add(ActionButton("Crear PNGTuber con mis imágenes", (_, _) =>
            CreateSimplePngTuber(), true));
        beginnerActions.Children.Add(ActionButton("🎨 ABRIR EDITOR PNGTUBER · capas y piezas", (_, _) =>
        {
            var studio = new WaifuSpriteStudioWindow(avatarDirectory) { Owner = this };
            studio.CompositeExported += path =>
            {
                try
                {
                    var dataDirectory = Path.Combine(avatarDirectory, "waifu-composites");
                    Directory.CreateDirectory(dataDirectory);
                    var savedPath = Path.Combine(dataDirectory, Guid.NewGuid().ToString("N") + ".png");
                    File.Copy(path, savedPath, overwrite: false);
                    var rawName = Path.GetFileNameWithoutExtension(path).Trim();
                    if (string.IsNullOrWhiteSpace(rawName)) rawName = "Waifu";
                    var baseName = rawName + " (Waifu PNG)";
                    if (baseName.Length > 64) baseName = baseName[..64];
                    var profileNames = store.GetAvatarProfiles()
                        .Select(profile => profile.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var profileName = baseName;
                    for (var suffix = 2; profileNames.Contains(profileName); suffix++)
                    {
                        var extra = " (" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                        profileName = baseName[..Math.Min(baseName.Length, 64 - extra.Length)] + extra;
                    }

                    var profile = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
                    store.SetActiveAvatarProfile(profile.Id);
                    store.SetActiveAvatarProfileBasePath(savedPath);
                    store.SetAvatarExpression("Idle", savedPath);
                    avatarPath = savedPath;
                    store.SetSetting("avatar.state", "Idle");
                    store.Log("Avatar", "Perfil creado desde el Creador de waifu PNG: " + profileName + ".");
                    FooterStatus.Text = "PNGTuber compuesto creado y guardado como perfil: " + profileName + ".";
                    Navigate("Avatar Studio");
                    ShowOverlay();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "No se pudo crear el perfil PNGTuber: " + exception.GetBaseException().Message,
                        "Creador de waifu", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            studio.CompositeExportedFor2D += path =>
            {
                studio.Close();
                _ = StartLocalAvatarSegmentationAsync(path);
            };
            studio.ShowDialog();
        }, true));
        startLocalSegmentation = ActionButton("2 · 2.5D con IA local (experimental)", async (_, _) =>
            await StartLocalAvatarSegmentationAsync());
        beginnerActions.Children.Add(startLocalSegmentation);
        beginnerActions.Children.Add(ActionButton("Abrir mini estudio 3D integrado", (_, _) =>
            OpenAvatarWardrobeStudio(), true));
        beginnerActions.Children.Add(ActionButton("Cargar modelo VRM y abrir overlay 3D", (_, _) =>
            ImportVrmAndOpenOverlay(), true));
        beginnerActions.Children.Add(ActionButton("Abrir VRoid Studio (externo)", (_, _) =>
            OpenAvatarExternalTool("https://vroid.com/en/studio", "VRoid Studio")));
        cancelLocalSegmentation = ActionButton("Cancelar IA", (_, _) =>
            activeLocalAvatarSegmentation?.Cancel());
        cancelLocalSegmentation.IsEnabled = false;
        cancelLocalSegmentation.Visibility = Visibility.Collapsed;
        beginnerActions.Children.Add(cancelLocalSegmentation);
        beginnerPanel.Children.Add(beginnerActions);
        beginnerPanel.Children.Add(localSegmentationProgress);
        beginnerPanel.Children.Add(localSegmentationStatus);
        root.Children.Add(beginnerPanel);

        // Restore the built-in illustrated PNG layer gallery so a beginner can create a real
        // editable avatar profile without first sourcing their own artwork.
        var sampleGallery = Panel("Galería de personajes chibi · elegí uno para crear tu perfil");
        sampleGallery.Margin = new Thickness(0, 0, 0, 14);
        sampleGallery.Children.Add(Label(
            "Son trece diseños originales de muestra, generados como PNG transparentes por capas. Incluye una variante morena de cabello castaño con mechones interiores claros y ojos marrones. Al elegir uno se crea un perfil editable; después podés cambiar peinado, ropa, ojos, accesorios y expresiones, o reemplazar las capas con tus propias imágenes.",
            12, "#98A4BE"));
        var sampleGalleryItems = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var preset in ChibiAvatarPresets)
            sampleGalleryItems.Children.Add(BuildChibiPresetButton(preset));
        sampleGallery.Children.Add(sampleGalleryItems);
        root.Children.Add(sampleGallery);

        var referencePanel = Panel("Referencias públicas · buscá estilos para inspirarte");
        referencePanel.Margin = new Thickness(0, 0, 0, 14);
        referencePanel.Children.Add(Label(
            "Buscá un VTuber conocido para estudiar su paleta, silueta, peinado y accesorios. La búsqueda abre el navegador; no descarga ni copia automáticamente su arte. Después aplicá una plantilla de inspiración de la galería y personalizá cada pieza.",
            12, "#98A4BE"));
        var referenceSearchRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
        var referenceSearch = new TextBox
        {
            Width = 300,
            MinHeight = 34,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8, 5, 8, 5),
            Text = "Gawr Gura, Ironmouse, Suisei, Shylily..."
        };
        referenceSearch.GotKeyboardFocus += (_, _) =>
        {
            if (referenceSearch.Text == "Gawr Gura, Ironmouse, Suisei, Shylily...")
                referenceSearch.Clear();
        };
        referenceSearchRow.Children.Add(referenceSearch);
        referenceSearchRow.Children.Add(ActionButton("Buscar referencia en la web", (_, _) =>
        {
            var query = referenceSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query) || query == "Gawr Gura, Ironmouse, Suisei, Shylily...")
                query = "famous VTuber character design hair eyes accessories";
            OpenAvatarExternalTool(
                "https://www.google.com/search?q=" + Uri.EscapeDataString(query + " VTuber character design reference"),
                "Búsqueda de referencias VTuber");
        }, true));
        referencePanel.Children.Add(referenceSearchRow);
        referencePanel.Children.Add(Label(
            "Plantillas de inspiración originales: Océano pop, Reaper rock, Cantante estelar, Dragona cyber, Demonio gamer y Kitsune idol. No son modelos oficiales ni contienen ilustraciones descargadas de terceros.",
            12, "#98A4BE"));
        root.Children.Add(referencePanel);
        var waifuEditorButton = new Button
        {
            Content = "🎥 Encuadre de cámara y vista previa OBS (editor avanzado)",
            ToolTip = "Recorta rostro, busto o cuerpo, ajusta el encuadre y exporta un PNG transparente. La vista OBS es una guía visual; no mueve fuentes de OBS en tiempo real.",
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        };
        waifuEditorButton.Click += (_, _) => OpenWaifuObjectEditor();
        root.Children.Add(waifuEditorButton);
        root.Children.Add(BuildAvatarAssetLibraryPanel());

        var profilePanel = Panel("Perfiles de avatar");
        profilePanel.Margin = new Thickness(0, 0, 0, 14);
        profilePanel.Children.Add(Label("Cada perfil conserva su imagen base, capas, transformaciones y sprites de expresión.", 12, "#98A4BE"));
        var profileList = store.GetAvatarProfiles();
        var activeProfile = store.GetActiveAvatarProfile();
        MigrateLegacyAvatarArtPrompts(activeProfile.Id);
        var profilePicker = new ComboBox
        {
            ItemsSource = profileList,
            DisplayMemberPath = nameof(AvatarProfile.Name),
            SelectedItem = profileList.First(profile => profile.Id == activeProfile.Id),
            MinWidth = 240,
            Margin = new Thickness(0, 0, 0, 8)
        };
        profilePanel.Children.Add(profilePicker);

        void SwitchToProfile(AvatarProfile profile)
        {
            try
            {
                store.SetActiveAvatarProfile(profile.Id);
                var activated = store.GetActiveAvatarProfile();
                avatarPath = activated.BasePath;
                RefreshActiveAvatarOverlay();
                store.Log("Avatar", "Perfil activo: " + activated.Name + ".");
                Navigate("Avatar Studio");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo activar el perfil: " + exception.Message);
            }
        }

        string? PromptProfileName(string title, string initialName)
        {
            var dialog = new Window
            {
                Owner = this,
                Title = title,
                Width = 390,
                Height = 190,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brush("#161F33"),
                Foreground = Brush("#F8FAFC")
            };
            var body = new StackPanel { Margin = new Thickness(18) };
            body.Children.Add(Label("Nombre del perfil (1–64 caracteres)", 12, "#98A4BE"));
            var nameBox = new TextBox
            {
                Text = initialName,
                Background = Brush("#101525"),
                Foreground = Brush("#F8FAFC"),
                BorderBrush = Brush("#34415E"),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 6, 0, 12)
            };
            body.Children.Add(nameBox);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancelar", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 5, 10, 5) };
            cancel.Click += (_, _) => { dialog.DialogResult = false; };
            var save = new Button { Content = "Guardar", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5) };
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text) || nameBox.Text.Trim().Length > 64)
                {
                    MessageBox.Show(dialog, "El nombre debe tener entre 1 y 64 caracteres.");
                    return;
                }
                dialog.DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            body.Children.Add(buttons);
            dialog.Content = body;
            dialog.Loaded += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
            return dialog.ShowDialog() == true ? nameBox.Text.Trim() : null;
        }

        void CreateProfile(bool duplicate)
        {
            var suggestedName = duplicate ? activeProfile.Name + " (copia)" : "Nuevo avatar";
            var name = PromptProfileName(duplicate ? "Duplicar perfil de avatar" : "Crear perfil de avatar", suggestedName);
            if (name is null) return;
            try
            {
                var created = store.CreateAvatarProfile(name, duplicate);
                if (duplicate)
                {
                    foreach (var role in AvatarArtPromptCatalog.Roles)
                    {
                        var prompt = store.GetSetting(
                            AvatarArtPromptCatalog.GetSettingKey(activeProfile.Id, role), MissingAvatarArtPromptSetting);
                        if (prompt != MissingAvatarArtPromptSetting)
                            store.SetSetting(AvatarArtPromptCatalog.GetSettingKey(created.Id, role), prompt);
                    }
                }
                SwitchToProfile(created);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo crear el perfil: " + exception.Message);
            }
        }

        var profileActions = new WrapPanel();
        profileActions.Children.Add(ActionButton("＋ Nuevo perfil vacío", (_, _) => CreateProfile(false), true));
        profileActions.Children.Add(ActionButton("Duplicar perfil", (_, _) => CreateProfile(true)));
        profileActions.Children.Add(ActionButton("Exportar perfil (.vtuberavatar)", (_, _) => ExportActiveAvatarProfilePackage()));
        profileActions.Children.Add(ActionButton("Importar perfil", (_, _) => ImportAvatarProfilePackage()));
        profileActions.Children.Add(ActionButton("Eliminar perfil activo", (_, _) =>
        {
            if (store.GetAvatarProfiles().Count <= 1)
            {
                MessageBox.Show(this, "Debe quedar al menos un perfil de avatar.");
                return;
            }
            if (MessageBox.Show(this, "Se eliminará el perfil y sus capas registradas. Los archivos de imagen del disco no se borran. ¿Continuar?",
                "Eliminar perfil de avatar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                var next = store.DeleteAvatarProfile(store.GetActiveAvatarProfile().Id);
                avatarPath = next.BasePath;
                RefreshActiveAvatarOverlay();
                store.Log("Avatar", "Perfil eliminado; perfil activo: " + next.Name + ".");
                Navigate("Avatar Studio");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo eliminar el perfil: " + exception.Message);
            }
        }));
        profilePicker.SelectionChanged += (_, _) =>
        {
            if (profilePicker.SelectedItem is AvatarProfile profile && profile.Id != store.GetActiveAvatarProfile().Id)
                SwitchToProfile(profile);
        };
        var profileManagement = new StackPanel();
        profileManagement.Children.Add(profilePanel);
        profileManagement.Children.Add(profileActions);
        root.Children.Add(new Expander
        {
            Header = "Opciones avanzadas de perfil (duplicar, importar, exportar o eliminar)",
            Content = profileManagement,
            IsExpanded = false,
            Foreground = Brush("#F8FAFC"),
            Background = Brush("#161F33"),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 14)
        });

        var demoPanel = Panel("Más ejemplos incluidos");
        demoPanel.Margin = new Thickness(0, 0, 0, 14);
        demoPanel.Children.Add(Label(
            "Estos perfiles técnicos sirven para comprobar los estados del PNGTuber y el ejemplo 2D por capas. Para empezar con diseños artísticos realmente diferentes, usá la galería de personajes chibi de la parte superior.",
            12, "#98A4BE"));
        var demoActions = new WrapPanel();
        demoActions.Children.Add(ActionButton("Probar sprites técnicos de estados", (_, _) =>
            LoadDemoProfile("Ejemplo PNGTuber técnico", layered: false, themeId: "hikari"), true));
        demoActions.Children.Add(ActionButton("Probar ejemplo 2D por capas", (_, _) =>
            LoadDemoProfile("Ejemplo 2D por capas", layered: true, themeId: "hikari")));
        demoActions.Children.Add(ActionButton("Configurar PNGTuber Plus externo", (_, _) => Navigate("Avatar externo")));
        demoPanel.Children.Add(demoActions);


        var imageToAvatarPanel = Panel("Separación remota de mayor calidad (requiere subir la imagen)");
        imageToAvatarPanel.Margin = new Thickness(0, 0, 0, 14);
        imageToAvatarPanel.Children.Add(Label(
            "Alternativa más avanzada: intenta reconstruir partes ocultas usando un servicio web. Solo usala si aceptás subir la ilustración a un tercero. Para conservar la imagen en tu PC, elegí el modo local del panel principal.",
            12, "#98A4BE"));
        var imageToAvatarActions = new WrapPanel();
        imageToAvatarActions.Children.Add(ActionButton("Abrir separador online", (_, _) =>
            OpenAvatarExternalTool(
                "https://huggingface.co/spaces/24yearsold/see-through-demo",
                "Separador automático de imágenes"), true));
        imageToAvatarActions.Children.Add(ActionButton("Abrir animador 2.5D", (_, _) =>
            OpenAvatarExternalTool(
                "https://852wa.github.io/Anime2.5DRig/",
                "Animador de avatar 2.5D")));
        imageToAvatarPanel.Children.Add(imageToAvatarActions);
        imageToAvatarPanel.Children.Add(Label(
            "El PSD se descarga y se carga manualmente en el animador. Esta función no importa PSD automáticamente al overlay de VTuberBot.",
            12, "#98A4BE"));

        var advancedTools = new StackPanel();
        advancedTools.Children.Add(demoPanel);
        advancedTools.Children.Add(imageToAvatarPanel);
        root.Children.Add(new Expander
        {
            Header = "Personalización avanzada (capas, ropa, colores y herramientas externas)",
            Content = advancedTools,
            IsExpanded = false,
            Foreground = Brush("#F8FAFC"),
            Background = Brush("#161F33"),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 14)
        });

        void CreateSimplePngTuber()
        {
            var idleDialog = new OpenFileDialog
            {
                Title = "Elegí la imagen de reposo de tu PNGTuber",
                Filter = "Imágenes PNG/JPG/BMP | *.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = false
            };
            if (idleDialog.ShowDialog(this) != true)
                return;

            string ValidateAndCopy(string sourcePath, string targetFolder)
            {
                var info = new FileInfo(sourcePath);
                var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                if (!info.Exists || info.Length <= 0 || info.Length > 25 * 1024 * 1024)
                    throw new InvalidDataException("Cada imagen debe existir y ocupar menos de 25 MB.");
                if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp")
                    throw new InvalidDataException("Formato no admitido. Elegí PNG, JPG o BMP.");
                _ = LoadImage(sourcePath);
                var folder = Path.Combine(avatarDirectory, "art-library", targetFolder);
                Directory.CreateDirectory(folder);
                var destination = Path.Combine(folder,
                    "pngtuber-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N") + extension);
                File.Copy(sourcePath, destination, overwrite: false);
                return destination;
            }

            string? talkingSource = null;
            var talkingChoice = MessageBox.Show(
                this,
                "¿Tenés una segunda imagen con la boca abierta para cuando hablás?\n\nSí: elegir una segunda imagen.\nNo: usar la misma imagen en reposo y hablando.",
                "Imagen de hablando (opcional)",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (talkingChoice == MessageBoxResult.Cancel)
                return;
            if (talkingChoice == MessageBoxResult.Yes)
            {
                var talkingDialog = new OpenFileDialog
                {
                    Title = "Elegí la imagen de hablando (boca abierta)",
                    Filter = "Imágenes PNG/JPG/BMP | *.png;*.jpg;*.jpeg;*.bmp",
                    Multiselect = false
                };
                if (talkingDialog.ShowDialog(this) != true)
                    return;
                talkingSource = talkingDialog.FileName;
            }

            var suggestedName = Path.GetFileNameWithoutExtension(idleDialog.FileName);
            if (suggestedName.Length > 45) suggestedName = suggestedName[..45];
            var profileName = PromptProfileName("Crear mi primer PNGTuber", suggestedName + " PNGTuber");
            if (profileName is null)
                return;

            try
            {
                localSegmentationStatus.Text = "Preparando tu PNGTuber...";
                var idlePath = ValidateAndCopy(idleDialog.FileName, "base");
                var talkingPath = talkingSource is null
                    ? idlePath
                    : ValidateAndCopy(talkingSource, "expressions");

                var profile = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
                store.SetActiveAvatarProfile(profile.Id);
                store.SetActiveAvatarProfileBasePath(idlePath);
                store.SetAvatarExpression("Idle", idlePath);
                store.SetAvatarExpression("Talking", talkingPath);
                avatarPath = idlePath;
                store.SetSetting("avatar.state", "Idle");
                store.Log("Avatar", "Creado PNGTuber sencillo desde imágenes propias: " + profileName + ".");
                FooterStatus.Text = talkingSource is null
                    ? "PNGTuber creado. Para animar la boca, añadí después un sprite de hablando."
                    : "PNGTuber creado con sprites de reposo y hablando.";
                localSegmentationStatus.Text = talkingSource is null
                    ? "PNGTuber listo. Con una imagen es estático; añadí una imagen de boca abierta para alternar al hablar."
                    : "PNGTuber listo: las imágenes de reposo y hablando se alternan con el detector del micrófono.";
                Navigate("Avatar Studio");
                ShowOverlay();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                    "No se pudo crear el PNGTuber: " + exception.GetBaseException().Message,
                    "Crear PNGTuber",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        async Task StartLocalAvatarSegmentationAsync(string? inputPathOverride = null)
        {
            if (activeLocalAvatarSegmentation is not null)
                return;

            string inputPath;
            if (!string.IsNullOrWhiteSpace(inputPathOverride))
            {
                inputPath = Path.GetFullPath(inputPathOverride);
                if (!File.Exists(inputPath))
                {
                    MessageBox.Show(this, "El PNG exportado ya no existe. Exportalo nuevamente desde el Creador de waifu.",
                        "Separación local", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Elegí una ilustración para separar en capas",
                    Filter = "Imágenes | *.png;*.jpg;*.jpeg;*.bmp",
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) != true)
                    return;
                inputPath = dialog.FileName;
            }

            var outputDirectory = Path.Combine(
                avatarDirectory,
                "segmented-imports",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N"));

            using var cancellation = new CancellationTokenSource();
            activeLocalAvatarSegmentation = cancellation;
            startLocalSegmentation.IsEnabled = false;
            cancelLocalSegmentation.IsEnabled = true;
            cancelLocalSegmentation.Visibility = Visibility.Visible;
            localSegmentationProgress.Visibility = Visibility.Visible;
            localSegmentationProgress.IsIndeterminate = true;
            localSegmentationStatus.Text = "Iniciando trabajador de IA local...";
            try
            {
                var result = await LocalAvatarSegmentationRunner.RunAsync(
                    inputPath,
                    outputDirectory,
                    message => Dispatcher.BeginInvoke(new Action(() =>
                        localSegmentationStatus.Text = message)),
                    cancellation.Token);

                var usefulPixels = result.Manifest.SkinPixels
                    + result.Manifest.ClothesPixels
                    + result.Manifest.HairPixels;
                if (usefulPixels < 100)
                    throw new InvalidDataException("La IA no reconoció suficiente contenido del personaje.");

                var rawName = Path.GetFileNameWithoutExtension(inputPath).Trim();
                if (string.IsNullOrWhiteSpace(rawName)) rawName = "Avatar";
                var baseName = rawName + " (IA local)";
                if (baseName.Length > 64) baseName = baseName[..64];
                var profileNames = store.GetAvatarProfiles()
                    .Select(profile => profile.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var profileName = baseName;
                for (var suffix = 2; profileNames.Contains(profileName); suffix++)
                {
                    var extra = " (" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                    var allowedBaseLength = Math.Max(1, 64 - extra.Length);
                    profileName = baseName[..Math.Min(baseName.Length, allowedBaseLength)] + extra;
                }

                // Only create the profile after validating that the worker generated usable output.
                var profile = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
                store.SetActiveAvatarProfile(profile.Id);
                store.SetActiveAvatarProfileBasePath(result.DetailsPath);
                store.SetAvatarExpression("Idle", result.DetailsPath);
                if (result.Manifest.ClothesPixels > 0)
                    store.AddAvatarLayer("Ropa · IA local", result.ClothesPath);
                if (result.Manifest.SkinPixels > 0)
                    store.AddAvatarLayer("Piel y cuerpo · IA local", result.SkinPath);
                if (result.Manifest.HairPixels > 0)
                    store.AddAvatarLayer("Cabello · IA local", result.HairPath);

                avatarPath = result.DetailsPath;
                store.SetSetting("avatar.state", "Idle");
                store.Log("Avatar", "Separación local por IA completada: " + profileName + ".");
                FooterStatus.Text = $"Avatar creado en modo local: piel {result.Manifest.SkinPixels:N0}, ropa {result.Manifest.ClothesPixels:N0}, pelo {result.Manifest.HairPixels:N0} píxeles.";
                Navigate("Avatar Studio");
                ShowOverlay();
                MessageBox.Show(this,
                    "Se creó un perfil nuevo con capas aproximadas de piel/cuerpo, ropa, cabello y detalles. Revisá el orden y la alineación: el modelo es experimental y no reconstruye perfectamente todas las zonas ocultas.",
                    "Separación local terminada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                localSegmentationStatus.Text = "Procesamiento cancelado. VTuberBot sigue funcionando.";
                FooterStatus.Text = "Separación local cancelada.";
            }
            catch (TimeoutException exception)
            {
                localSegmentationStatus.Text = exception.Message;
                MessageBox.Show(this, exception.Message, "Límite de procesamiento", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception exception)
            {
                localSegmentationStatus.Text = "No se pudo separar la imagen: " + exception.GetBaseException().Message;
                MessageBox.Show(this,
                    "No se pudo crear el avatar por capas: " + exception.GetBaseException().Message,
                    "Separación local",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                activeLocalAvatarSegmentation = null;
                startLocalSegmentation.IsEnabled = true;
                cancelLocalSegmentation.IsEnabled = false;
                cancelLocalSegmentation.Visibility = Visibility.Collapsed;
                localSegmentationProgress.IsIndeterminate = false;
                localSegmentationProgress.Visibility = Visibility.Collapsed;
            }
        }

        void LoadDemoProfile(string profileName, bool layered, string themeId = "hikari")
        {
            try
            {
                var assets = DemoAvatarFactory.EnsureAssets(avatarDirectory, themeId);
                var existing = store.GetAvatarProfiles().FirstOrDefault(profile =>
                    string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    store.SetActiveAvatarProfile(existing.Id);
                    var activated = store.GetActiveAvatarProfile();
                    avatarPath = activated.BasePath;
                    if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                        throw new InvalidOperationException("El perfil de ejemplo existe, pero su imagen base ya no está disponible. Crea un perfil nuevo o importa otra imagen.");
                    FooterStatus.Text = "Se activó el perfil de ejemplo existente; tus cambios no se sobrescribieron.";
                    store.Log("Avatar", "Perfil de ejemplo activado: " + activated.Name + ".");
                }
                else
                {
                    var created = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
                    store.SetActiveAvatarProfile(created.Id);
                    var idle = layered ? assets.LayeredIdle : assets.Idle;
                    var talking = layered ? assets.LayeredTalking : assets.Talking;
                    var blink = layered ? assets.LayeredBlink : assets.Blink;
                    var happy = layered ? assets.LayeredHappy : assets.Happy;
                    var surprised = layered ? assets.LayeredSurprised : assets.Surprised;

                    store.SetActiveAvatarProfileBasePath(idle);
                    store.SetAvatarExpression("Idle", idle);
                    store.SetAvatarExpression("Talking", talking);
                    store.SetAvatarExpression("Blink", blink);
                    store.SetAvatarExpression("Happy", happy);
                    store.SetAvatarExpression("Surprised", surprised);
                    if (layered)
                    {
                        store.AddAvatarLayer("Pelo frontal (demo 2D)", assets.FrontHairLayer);
                        store.AddAvatarLayer("Destellos decorativos (demo 2D)", assets.SparklesLayer);
                    }

                    avatarPath = idle;
                    store.SetSetting("avatar.state", "Idle");
                    FooterStatus.Text = layered
                        ? "Ejemplo 2D creado: selecciona una capa y muévela en la previsualización."
                        : "Ejemplo PNGTuber creado: el micrófono puede alternar entre reposo y habla.";
                    store.Log("Avatar", "Creado perfil de demostración: " + created.Name + ".");
                }

                Navigate("Avatar Studio");
                ShowOverlay();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                    "No se pudo cargar el ejemplo de avatar: " + exception.Message,
                    "VTuberBot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var layerImages = new Dictionary<long, Image>();
        Grid? previewCanvas = null;
        Image? basePreviewImage = null;
        var previewState = store.GetSetting("avatar.state", "Idle");
        if (previewState == "Blink") previewState = "Idle";
        var left = Panel("Previsualización del avatar");
        left.MinHeight = 420;
        var preview = new Border { Background = Brush("#101525"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), MinHeight = 350 };
        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            previewCanvas = new Grid { Background = Brush("#101525"), Focusable = true };
            var visibleLayers = store.GetAvatarLayers().Where(layer => layer.Visible && File.Exists(layer.Path)).ToArray();
            foreach (var layer in AvatarLayerOrdering.BackgroundLayers(visibleLayers))
            {
                var image = new Image { Source = LoadImage(layer.Path), Stretch = Stretch.UniformToFill, MaxHeight = 360 };
                image.RenderTransformOrigin = new Point(.5, .5);
                image.RenderTransform = BuildLayerTransform(layer);
                previewCanvas.Children.Add(image);
                layerImages[layer.Id] = image;
            }
            var savedExpression = store.GetAvatarExpression(previewState) ?? string.Empty;
            var initialImagePath = !string.IsNullOrWhiteSpace(savedExpression) && File.Exists(savedExpression) ? savedExpression : avatarPath;
            basePreviewImage = new Image { Source = LoadImage(initialImagePath), Stretch = Stretch.Uniform, MaxHeight = 360 };
            previewCanvas.Children.Add(basePreviewImage);
            foreach (var layer in AvatarLayerOrdering.ForegroundLayers(visibleLayers))
            {
                var image = new Image { Source = LoadImage(layer.Path), Stretch = Stretch.Uniform, MaxHeight = 360 };
                image.RenderTransformOrigin = new Point(.5, .5);
                image.RenderTransform = BuildLayerTransform(layer);
                previewCanvas.Children.Add(image);
                layerImages[layer.Id] = image;
            }
            preview.Child = previewCanvas;
        }
        else preview.Child = Label("Todavía no hay arte importado.\nUsá un PNG propio para comenzar.", 16, "#98A4BE");
        left.Children.Add(preview);
        left.Children.Add(Label("Seleccioná una capa visible a la derecha, arrastrala en esta vista y guardá la transformación cuando te guste el resultado.", 12, "#98A4BE"));

        var right = Panel("Avatar y salida");
        right.Margin = new Thickness(18, 0, 0, 0);
        right.Children.Add(Label("Avatar activo", 14, "#F8FAFC", true));
        right.Children.Add(Label(string.IsNullOrWhiteSpace(avatarPath) ? "Ningún archivo seleccionado" : Path.GetFileName(avatarPath), 12, "#98A4BE"));
        right.Children.Add(ActionButton("＋  Importar PNG / imagen base", (_, _) => ImportAvatar(), true));
        right.Children.Add(Label("Capas adicionales — orden de abajo hacia arriba", 13, "#F8FAFC", true));

        var layerList = new ListBox { Height = 142 };
        long? SelectedLayerId()
        {
            if (layerList.SelectedItem is not string item) return null;
            return long.TryParse(item.Split('|')[0].Trim(), out var id) ? id : null;
        }
        void ReloadLayers(long? selectId = null)
        {
            var entries = store.GetAvatarLayers().Select(l => $"{l.Id} | {(l.Visible ? "●" : "○")} | {l.Name}").ToList();
            layerList.ItemsSource = entries;
            if (selectId.HasValue)
                layerList.SelectedItem = entries.FirstOrDefault(s => long.TryParse(s.Split('|')[0].Trim(), out var id) && id == selectId.Value);
        }
        ReloadLayers();
        right.Children.Add(layerList);

        var layerActions = new WrapPanel();
        layerActions.Children.Add(ActionButton("＋ Importar capa", (_, _) => ImportAvatarLayer()));
        layerActions.Children.Add(ActionButton("Mostrar / ocultar", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) return;
            var layer = store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            if (layer is null) return;
            store.SetAvatarLayerVisibility(id.Value, !layer.Visible);
            RefreshAvatarPresentation();
        }));
        layerActions.Children.Add(ActionButton("↑ Subir", (_, _) => MoveSelectedLayer(1)));
        layerActions.Children.Add(ActionButton("↓ Bajar", (_, _) => MoveSelectedLayer(-1)));
        layerActions.Children.Add(ActionButton("Eliminar capa", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) return;
            if (MessageBox.Show(this, "¿Eliminar esta capa del perfil? El archivo local no se borra.", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            store.DeleteAvatarLayer(id.Value);
            RefreshAvatarPresentation();
        }));
        right.Children.Add(layerActions);

        var transforms = Panel("Transformar capa seleccionada");
        transforms.Margin = new Thickness(0, 10, 0, 0);
        var xSlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 10 };
        var ySlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 10 };
        var scaleSlider = new Slider { Minimum = .1, Maximum = 4, Value = 1, TickFrequency = .1 };
        var rotationSlider = new Slider { Minimum = -180, Maximum = 180, TickFrequency = 5 };
        var xValue = Label("X: 0 px", 11, "#98A4BE");
        var yValue = Label("Y: 0 px", 11, "#98A4BE");
        var scaleValue = Label("Escala: 100%", 11, "#98A4BE");
        var rotationValue = Label("Rotación: 0°", 11, "#98A4BE");
        transforms.Children.Add(xValue); transforms.Children.Add(xSlider);
        transforms.Children.Add(yValue); transforms.Children.Add(ySlider);
        transforms.Children.Add(scaleValue); transforms.Children.Add(scaleSlider);
        transforms.Children.Add(rotationValue); transforms.Children.Add(rotationSlider);
        var loadingTransform = false;

        void PreviewSelectedTransform()
        {
            xValue.Text = $"X: {xSlider.Value:0} px";
            yValue.Text = $"Y: {ySlider.Value:0} px";
            scaleValue.Text = $"Escala: {scaleSlider.Value:P0}";
            rotationValue.Text = $"Rotación: {rotationSlider.Value:0}°";
            if (loadingTransform) return;
            var id = SelectedLayerId();
            if (id is null || !layerImages.TryGetValue(id.Value, out var image)) return;
            var layer = store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            if (layer is not null)
                image.RenderTransform = BuildLayerTransform(layer with { OffsetX = xSlider.Value, OffsetY = ySlider.Value, Scale = scaleSlider.Value, RotationDegrees = rotationSlider.Value });
        }

        void LoadSelectedTransform()
        {
            var id = SelectedLayerId();
            var layer = id is null ? null : store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            loadingTransform = true;
            xSlider.Value = layer?.OffsetX ?? 0;
            ySlider.Value = layer?.OffsetY ?? 0;
            scaleSlider.Value = layer?.Scale ?? 1;
            rotationSlider.Value = layer?.RotationDegrees ?? 0;
            loadingTransform = false;
            PreviewSelectedTransform();
        }
        layerList.SelectionChanged += (_, _) => LoadSelectedTransform();
        xSlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        ySlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        scaleSlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        rotationSlider.ValueChanged += (_, _) => PreviewSelectedTransform();

        // Dragging moves only the selected visible layer. The edit is preview-only until Save is pressed.
        var dragCanvas = previewCanvas;
        if (dragCanvas is not null)
        {
            System.Windows.Point dragOrigin = default;
            var dragStartX = 0d;
            var dragStartY = 0d;
            var isDraggingLayer = false;

            dragCanvas.MouseLeftButtonDown += (_, e) =>
            {
                var id = SelectedLayerId();
                if (id is null || !layerImages.ContainsKey(id.Value))
                    return;

                dragOrigin = e.GetPosition(dragCanvas);
                dragStartX = xSlider.Value;
                dragStartY = ySlider.Value;
                isDraggingLayer = dragCanvas.CaptureMouse();
                if (isDraggingLayer)
                    e.Handled = true;
            };
            dragCanvas.MouseMove += (_, e) =>
            {
                if (!isDraggingLayer || !dragCanvas.IsMouseCaptured)
                    return;

                var current = e.GetPosition(dragCanvas);
                xSlider.Value = Math.Clamp(dragStartX + current.X - dragOrigin.X, xSlider.Minimum, xSlider.Maximum);
                ySlider.Value = Math.Clamp(dragStartY + current.Y - dragOrigin.Y, ySlider.Minimum, ySlider.Maximum);
            };
            dragCanvas.MouseLeftButtonUp += (_, e) =>
            {
                if (!isDraggingLayer)
                    return;

                isDraggingLayer = false;
                dragCanvas.ReleaseMouseCapture();
                FooterStatus.Text = "Vista previa ajustada. Pulsá «Aplicar y guardar transformación» para conservar la posición.";
                e.Handled = true;
            };
        }

        transforms.Children.Add(ActionButton("Restablecer posición y giro", (_, _) =>
        {
            if (SelectedLayerId() is null)
            {
                MessageBox.Show(this, "Seleccioná una capa primero.");
                return;
            }
            xSlider.Value = 0;
            ySlider.Value = 0;
            scaleSlider.Value = 1;
            rotationSlider.Value = 0;
            PreviewSelectedTransform();
        }));

        transforms.Children.Add(ActionButton("Aplicar y guardar transformación", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) { MessageBox.Show(this, "Seleccioná una capa primero."); return; }
            try
            {
                store.UpdateAvatarLayerTransform(id.Value, xSlider.Value, ySlider.Value, scaleSlider.Value, rotationSlider.Value);
                if (overlay is { IsVisible: true } && !string.IsNullOrWhiteSpace(avatarPath))
                    overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths());
                FooterStatus.Text = "Transformación de capa guardada en SQLite.";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar la transformación: " + ex.Message); }
        }, true));
        right.Children.Add(transforms);

        right.Children.Add(ActionButton("Mostrar overlay transparente", (_, _) => ShowOverlay()));
        right.Children.Add(ActionButton("Ocultar overlay", (_, _) => { overlay?.Hide(); store.Log("Avatar", "Overlay ocultado."); }));
        right.Children.Add(Label("Estados disponibles", 14, "#F8FAFC", true));
        var states = new WrapPanel();
        foreach (var state in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised", "Angry", "LeftEyeClose" })
            states.Children.Add(ActionButton(state, (_, _) =>
            {
                store.SetSetting("avatar.state", state == "Blink" ? "Idle" : state);
                UpdatePreviewState(state);
                overlay?.SetState(state);
                store.Log("Avatar", $"Estado seleccionado: {state}.");
                FooterStatus.Text = $"Cue visual del avatar: {state}. Si existe un sprite asignado, se muestra ese estado.";
            }));
        right.Children.Add(states);

        void UpdatePreviewState(string selectedState)
        {
            if (basePreviewImage is null || string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath)) return;
            var expressionPath = store.GetAvatarExpression(selectedState) ?? string.Empty;
            basePreviewImage.Source = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath)
                ? LoadImage(expressionPath)
                : LoadImage(avatarPath);
        }

        var expressionPanel = Panel("Sprites por expresión");
        expressionPanel.Margin = new Thickness(0, 12, 0, 0);
        expressionPanel.Children.Add(Label("Asigná imágenes del mismo tamaño para cambiar la cara cuando el micrófono activa Talking o al elegir una expresión. Si no asignás una imagen, se usa la base.", 12, "#98A4BE"));
        foreach (var expressionName in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised", "Angry", "LeftEyeClose" })
        {
            var savedPath = store.GetAvatarExpression(expressionName) ?? string.Empty;
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(Label(expressionName + ": " + (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath) ? Path.GetFileName(savedPath) : "usa imagen base"), 12, "#F8FAFC"));
            row.Children.Add(ActionButton("Importar / cambiar", (_, _) => ImportAvatarExpression(expressionName)));
            row.Children.Add(ActionButton("Quitar", (_, _) =>
            {
                store.SetAvatarExpression(expressionName, null);
                RefreshActiveAvatarOverlay();
                store.Log("Avatar", "Sprite de expresión quitado: " + expressionName);
                Navigate("Avatar Studio");
            }));
            expressionPanel.Children.Add(row);
        }
        right.Children.Add(expressionPanel);


        var artPanel = Panel("Consola de arte — assets y prompts");
        artPanel.Margin = new Thickness(0, 12, 0, 0);
        artPanel.Children.Add(Label("Escribí o ajustá un prompt por tipo de asset. Los prompts quedan guardados localmente. La app no llama a un generador externo: importá el resultado de tu herramienta de imágenes y se asignará automáticamente al rol seleccionado.", 12, "#98A4BE"));
        var artRoles = AvatarArtPromptCatalog.Roles;
        var artRole = new ComboBox { ItemsSource = artRoles, SelectedIndex = 0, MinWidth = 220, Margin = new Thickness(0, 6, 0, 8) };
        artPanel.Children.Add(Label("Asignar resultado a", 12, "#98A4BE"));
        artPanel.Children.Add(artRole);
        var artPrompt = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 120, MaxHeight = 220,
            Padding = new Thickness(10), Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"), BorderBrush = Brush("#34415E")
        };
        string ArtPromptKey(string role) => AvatarArtPromptCatalog.GetSettingKey(store.GetActiveAvatarProfile().Id, role);
        string DefaultArtPrompt(string role) => role switch
        {
            "Personaje base" => "VTuber character sprite, full body, centered, clean silhouette, transparent background, front-facing, consistent proportions, no text, no watermark. Style and design: [describir personaje, ropa, paleta y estilo].",
            "Fondo" => "Streaming background plate, 16:9 composition, clear central safe area for a VTuber character, layered depth, no character, no text, no watermark. Scene and palette: [describir escenario].",
            "Objeto / capa" => "Isolated accessory or prop for a VTuber sprite, centered, transparent background, clean edges, matching the character art style, no text, no watermark. Object: [describir objeto].",
            "Idle" => "Expression sprite for the same VTuber character, neutral idle expression, same framing, pose, outfit, linework and palette as the base reference, transparent background, no text, no watermark. Character details: [describir o adjuntar referencia].",
            "Talking" => "Expression sprite for the same VTuber character, talking mouth open naturally, same framing, pose, outfit, linework and palette as the base reference, transparent background, no text, no watermark. Character details: [describir o adjuntar referencia].",
            "Blink" => "Expression sprite for the same VTuber character, eyes closed in a natural blink, same framing, pose, outfit, linework and palette as the base reference, transparent background, no text, no watermark. Character details: [describir o adjuntar referencia].",
            "Happy" => "Expression sprite for the same VTuber character, happy expression, same framing, pose, outfit, linework and palette as the base reference, transparent background, no text, no watermark. Character details: [describir o adjuntar referencia].",
            "Surprised" => "Expression sprite for the same VTuber character, surprised expression, same framing, pose, outfit, linework and palette as the base reference, transparent background, no text, no watermark. Character details: [describir o adjuntar referencia].",
            _ => string.Empty
        };
        void LoadArtPromptForRole()
        {
            if (artRole.SelectedItem is not string role) return;
            artPrompt.Text = store.GetSetting(ArtPromptKey(role), DefaultArtPrompt(role));
        }
        artRole.SelectionChanged += (_, _) => LoadArtPromptForRole();
        LoadArtPromptForRole();
        artPanel.Children.Add(artPrompt);
        var artActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        artActions.Children.Add(ActionButton("Guardar prompt", (_, _) =>
        {
            if (artRole.SelectedItem is not string role) return;
            if (artPrompt.Text.Length > 12000) { MessageBox.Show(this, "El prompt supera el límite de 12.000 caracteres."); return; }
            store.SetSetting(ArtPromptKey(role), artPrompt.Text);
            store.Log("Avatar", "Prompt de arte guardado para el rol " + role + ".");
            FooterStatus.Text = "Prompt guardado localmente para: " + role + ".";
        }, true));
        artActions.Children.Add(ActionButton("Copiar prompt", (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(artPrompt.Text)) return;
            try { Clipboard.SetText(artPrompt.Text); FooterStatus.Text = "Prompt copiado. Pegalo en tu generador de imágenes."; }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo copiar el prompt: " + exception.Message); }
        }));
        artActions.Children.Add(ActionButton("Restaurar plantilla", (_, _) =>
        {
            if (artRole.SelectedItem is string role) artPrompt.Text = DefaultArtPrompt(role);
        }));
        artActions.Children.Add(ActionButton("Importar resultado y asignar", (_, _) =>
        {
            if (artRole.SelectedItem is not string role) return;
            var dialog = new OpenFileDialog
            {
                Title = "Importar resultado para " + role,
                Filter = "Imágenes PNG/JPG/BMP/WebP|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ImportArtAssetForRole(role, dialog.FileName);
                FooterStatus.Text = "Asset importado y asignado a: " + role + ".";
                Navigate("Avatar Studio");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo asignar el asset: " + exception.Message, "Consola de arte", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, true));
        artPanel.Children.Add(artActions);
        right.Children.Add(artPanel);

        right.Children.Add(Label("X/Y, escala, rotación y orden se guardan localmente. El runtime admite sprites por estado; la física por huesos y el lip-sync por fonemas/visemas todavía requieren un rig más avanzado.", 12, "#98A4BE"));

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        columns.Children.Add(left);
        columns.Children.Add(right);
        root.Children.Add(columns);
        return root;

        void MoveSelectedLayer(int direction)
        {
            var id = SelectedLayerId();
            if (id is null) return;
            try { store.MoveAvatarLayer(id.Value, direction); RefreshAvatarPresentation(); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cambiar el orden: " + ex.Message); }
        }

        void RefreshAvatarPresentation()
        {
            if (overlay is { IsVisible: true } && !string.IsNullOrWhiteSpace(avatarPath))
                overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths());
            Navigate("Avatar Studio");
        }
    }


    private void OpenAvatarWardrobeStudio()
    {
        var window = new AvatarWardrobeStudioWindow { Owner = this };
        window.Show();
    }

    private void ImportVrmAndOpenOverlay()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí el modelo 3D VRM para tu VTuber",
            Filter = "Modelos VRM (*.vrm)|*.vrm",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var info = new FileInfo(dialog.FileName);
            if (!info.Exists || info.Length <= 0 || info.Length > 200L * 1024 * 1024)
                throw new InvalidDataException("El modelo debe existir y ocupar menos de 200 MB.");

            // Each imported model gets its own folder, so the renderer HTML can never
            // overwrite another user's model or a file with the same name.
            var modelFolder = Path.Combine(avatarDirectory, "models",
                "vrm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(modelFolder);
            var destination = Path.Combine(modelFolder, "avatar.vrm");
            File.Copy(dialog.FileName, destination, overwrite: false);
            store.SetSetting("avatar.vrm.path", destination);

            vrmOverlay?.Close();
            vrmOverlay = new VrmOverlayWindow(destination);
            vrmOverlay.Closed += (_, _) => vrmOverlay = null;
            vrmOverlay.Show();
            vrmOverlay.SetState(store.GetSetting("avatar.state", "Idle"));
            FooterStatus.Text = "Modelo VRM copiado a la biblioteca local. El overlay 3D se está iniciando.";
            store.Log("Avatar VRM", "Modelo local importado: " + Path.GetFileName(dialog.FileName) + ".");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo importar o abrir el modelo VRM: " + exception.GetBaseException().Message,
                "Importar modelo VRM", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenAvatarExternalTool(string url, string toolName)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            FooterStatus.Text = "Abierto en el navegador: " + toolName + ".";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo abrir " + toolName + ": " + exception.Message,
                "VTuberBot", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private UIElement BuildModularWardrobePanel()
    {
        var panel = Panel("Creador de personaje chibi — cambia cada parte");
        panel.Margin = new Thickness(0, 0, 0, 14);
        panel.Children.Add(Label(
            "Elegí por separado peinado, ropa, rostro, ojos, tono de piel, pose y accesorios. Las piezas se componen como PNG transparentes y se guardan en un perfil nuevo; no reemplazan tus dibujos ni otros avatares. Son personajes originales de VTuberBot, no copias de modelos oficiales.",
            12, "#98A4BE"));

        ComboBox Choice(IReadOnlyList<WardrobeOption> options)
        {
            var combo = new ComboBox
            {
                ItemsSource = options,
                DisplayMemberPath = nameof(WardrobeOption.Name),
                SelectedIndex = 0,
                MinWidth = 280,
                Margin = new Thickness(0, 4, 0, 8)
            };
            return combo;
        }

        var hair = Choice(ModularWardrobeFactory.HairOptions);
        var outfit = Choice(ModularWardrobeFactory.OutfitOptions);
        var faceShape = Choice(ModularWardrobeFactory.FaceShapeOptions);
        var eyes = Choice(ModularWardrobeFactory.EyeStyleOptions);
        var skin = Choice(ModularWardrobeFactory.SkinToneOptions);
        var pose = Choice(ModularWardrobeFactory.PoseOptions);
        var head = Choice(ModularWardrobeFactory.HeadAccessoryOptions);
        var faceAccessory = Choice(ModularWardrobeFactory.FaceAccessoryOptions);
        var back = Choice(ModularWardrobeFactory.BackAccessoryOptions);

        var previewPanel = Panel("Vista previa — se actualiza al cambiar opciones");
        previewPanel.Margin = new Thickness(0, 8, 0, 12);
        var previewCanvas = new Grid
        {
            Width = 176,
            Height = 250,
            ClipToBounds = true,
            Background = Brush("#101525"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        previewPanel.Children.Add(previewCanvas);
        panel.Children.Add(previewPanel);

        panel.Children.Add(Label("1 · Peinado", 12, "#F8FAFC", true));
        panel.Children.Add(hair);
        panel.Children.Add(Label("2 · Ropa / conjunto", 12, "#F8FAFC", true));
        panel.Children.Add(outfit);
        panel.Children.Add(Label("3 · Forma del rostro", 12, "#F8FAFC", true));
        panel.Children.Add(faceShape);
        panel.Children.Add(Label("4 · Ojos", 12, "#F8FAFC", true));
        panel.Children.Add(eyes);
        panel.Children.Add(Label("5 · Tono de piel", 12, "#F8FAFC", true));
        panel.Children.Add(skin);
        panel.Children.Add(Label("6 · Pose / gesto", 12, "#F8FAFC", true));
        panel.Children.Add(pose);
        panel.Children.Add(Label("7 · Accesorio de cabeza", 12, "#F8FAFC", true));
        panel.Children.Add(head);
        panel.Children.Add(Label("8 · Accesorio facial / joyería", 12, "#F8FAFC", true));
        panel.Children.Add(faceAccessory);
        panel.Children.Add(Label("9 · Espalda / capa / alas / mochila", 12, "#F8FAFC", true));
        panel.Children.Add(back);

        var colorPanel = Panel("Configurar colores por zona");
        colorPanel.Margin = new Thickness(0, 10, 0, 12);
        colorPanel.Children.Add(Label(
            "Escribí un color hexadecimal (#RRGGBB) o elegí una muestra. Cada zona se recolorea por separado; dejala vacía o pulsa «Original» para conservar sus colores. El contorno oscuro y los brillos claros se protegen.",
            12, "#98A4BE"));

        var colorInputs = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        var summary = Label("", 12, "#69E4D0", true);
        void AddColorRow(string area, string title)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 5, 0, 5), VerticalAlignment = VerticalAlignment.Center };
            var label = Label(title, 12, "#F8FAFC", true);
            label.Width = 150;
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);

            var settingKey = "avatar.wardrobe.custom-tint." + area;
            var input = new TextBox
            {
                Text = store.GetSetting(settingKey, string.Empty),
                Width = 94,
                Height = 28,
                Padding = new Thickness(5, 3, 5, 3),
                Margin = new Thickness(0, 0, 8, 0),
                Background = Brush("#101525"),
                Foreground = Brush("#F8FAFC"),
                BorderBrush = Brush("#34415E"),
                ToolTip = "Escribí #RRGGBB o dejalo vacío para usar el color original."
            };
            colorInputs[area] = input;
            input.LostFocus += (_, _) => UpdateSummary();
            row.Children.Add(input);

            foreach (var hex in new[] { "#E65E78", "#D85CA8", "#9576DF", "#3B82F6", "#42B9B0", "#E8C35D" })
            {
                var swatch = new Button
                {
                    Content = " ",
                    Width = 22,
                    Height = 22,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2, 2, 2, 2),
                    Background = Brush(hex),
                    BorderBrush = Brush("#C7D2E5"),
                    BorderThickness = new Thickness(1),
                    ToolTip = hex
                };
                swatch.Click += (_, _) =>
                {
                    input.Text = hex;
                    UpdateSummary();
                };
                row.Children.Add(swatch);
            }

            row.Children.Add(ActionButton("Original", (_, _) =>
            {
                input.Clear();
                UpdateSummary();
            }));
            colorPanel.Children.Add(row);
        }

        AddColorRow("hair", "Cabello");
        AddColorRow("outfit", "Ropa / conjunto");
        AddColorRow("head", "Accesorio de cabeza");
        AddColorRow("face", "Cara / joyería");
        AddColorRow("back", "Espalda / alas / capa");
        panel.Children.Add(colorPanel);
        panel.Children.Add(summary);

        string TintPreview(string area, string sourcePath)
        {
            if (!colorInputs.TryGetValue(area, out var input))
                return sourcePath;
            var raw = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return sourcePath;
            var hex = raw.StartsWith('#') ? raw[1..] : raw;
            if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                return sourcePath;
            var tint = Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
            var tintDirectory = Path.Combine(avatarDirectory, "builtin-demos", "wardrobe-custom-colors");
            return ModularWardrobeFactory.TintPng(sourcePath, tintDirectory, tint);
        }

        void UpdateLivePreview()
        {
            if (hair.SelectedItem is not WardrobeOption selectedHair ||
                outfit.SelectedItem is not WardrobeOption selectedOutfit ||
                faceShape.SelectedItem is not WardrobeOption selectedFaceShape ||
                eyes.SelectedItem is not WardrobeOption selectedEyes ||
                skin.SelectedItem is not WardrobeOption selectedSkin ||
                pose.SelectedItem is not WardrobeOption selectedPose ||
                head.SelectedItem is not WardrobeOption selectedHead ||
                faceAccessory.SelectedItem is not WardrobeOption selectedFaceAccessory ||
                back.SelectedItem is not WardrobeOption selectedBack)
            {
                previewCanvas.Children.Clear();
                return;
            }

            var assets = ModularWardrobeFactory.EnsureAssets(
                avatarDirectory, selectedFaceShape.Id, selectedEyes.Id, selectedSkin.Id, selectedOutfit.Id);
            var paths = new List<string> { assets.BaseIdle };
            if (selectedBack.Id != "none-back" && assets.BackAccessories.TryGetValue(selectedBack.Id, out var backPath))
                paths.Add(TintPreview("back", backPath));
            paths.Add(TintPreview("outfit", assets.Outfits[selectedOutfit.Id]));
            paths.Add(TintPreview("outfit", assets.PoseLayers[selectedPose.Id]));
            paths.Add(TintPreview("hair", assets.Hair[selectedHair.Id]));
            if (assets.HeadAccessories.TryGetValue(selectedHead.Id, out var headPath))
                paths.Add(TintPreview("head", headPath));
            if (assets.FaceAccessories.TryGetValue(selectedFaceAccessory.Id, out var facePath))
                paths.Add(TintPreview("face", facePath));

            previewCanvas.Children.Clear();
            foreach (var path in paths)
            {
                previewCanvas.Children.Add(new Image
                {
                    Source = LoadChibiPreviewBitmap(path),
                    Width = 176,
                    Height = 250,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                });
            }
        }

        void UpdateSummary()
        {
            summary.Text =
                "Combinación: " +
                (hair.SelectedItem as WardrobeOption)?.Name + " · " +
                (outfit.SelectedItem as WardrobeOption)?.Name + " · " +
                (faceShape.SelectedItem as WardrobeOption)?.Name + " · " +
                (eyes.SelectedItem as WardrobeOption)?.Name + " · " +
                (skin.SelectedItem as WardrobeOption)?.Name + " · " +
                (pose.SelectedItem as WardrobeOption)?.Name + " · " +
                (head.SelectedItem as WardrobeOption)?.Name + " · " +
                (faceAccessory.SelectedItem as WardrobeOption)?.Name + " · " +
                (back.SelectedItem as WardrobeOption)?.Name;
            UpdateLivePreview();
        }

        hair.SelectionChanged += (_, _) => UpdateSummary();
        outfit.SelectionChanged += (_, _) => UpdateSummary();
        faceShape.SelectionChanged += (_, _) => UpdateSummary();
        eyes.SelectionChanged += (_, _) => UpdateSummary();
        skin.SelectionChanged += (_, _) => UpdateSummary();
        pose.SelectionChanged += (_, _) => UpdateSummary();
        head.SelectionChanged += (_, _) => UpdateSummary();
        faceAccessory.SelectionChanged += (_, _) => UpdateSummary();
        back.SelectionChanged += (_, _) => UpdateSummary();
        UpdateSummary();

        panel.Children.Add(ActionButton("Crear perfil con esta combinación", (_, _) =>
        {
            if (hair.SelectedItem is not WardrobeOption selectedHair ||
                outfit.SelectedItem is not WardrobeOption selectedOutfit ||
                faceShape.SelectedItem is not WardrobeOption selectedFaceShape ||
                eyes.SelectedItem is not WardrobeOption selectedEyes ||
                skin.SelectedItem is not WardrobeOption selectedSkin ||
                pose.SelectedItem is not WardrobeOption selectedPose ||
                head.SelectedItem is not WardrobeOption selectedHead ||
                faceAccessory.SelectedItem is not WardrobeOption selectedFaceAccessory ||
                back.SelectedItem is not WardrobeOption selectedBack)
            {
                MessageBox.Show(this, "Elegí una opción en cada categoría antes de crear el personaje.");
                return;
            }

            var customTints = new Dictionary<string, Color?>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var pair in colorInputs)
                {
                    var raw = pair.Value.Text.Trim();
                    Color? parsed = null;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var hex = raw.StartsWith('#') ? raw[1..] : raw;
                        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                            throw new FormatException($"El color de «{pair.Key}» debe tener formato #RRGGBB, por ejemplo #E65E78.");

                        parsed = Color.FromRgb(
                            Convert.ToByte(hex[..2], 16),
                            Convert.ToByte(hex.Substring(2, 2), 16),
                            Convert.ToByte(hex.Substring(4, 2), 16));
                        pair.Value.Text = $"#{parsed.Value.R:X2}{parsed.Value.G:X2}{parsed.Value.B:X2}";
                    }

                    customTints[pair.Key] = parsed;
                    store.SetSetting("avatar.wardrobe.custom-tint." + pair.Key, pair.Value.Text.Trim());
                }
            }
            catch (FormatException exception)
            {
                MessageBox.Show(this, exception.Message, "Color no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreateModularWardrobeProfile(
                selectedHair, selectedOutfit, selectedHead, selectedFaceAccessory, selectedBack, customTints,
                selectedFaceShape.Id, selectedEyes.Id, selectedSkin.Id, selectedPose.Id);
        }, true));

        panel.Children.Add(Label("Más estilos ilustrados desde la web", 14, "#F8FAFC", true));
        panel.Children.Add(Label(
            "Estos creadores tienen estilos dibujados distintos del catálogo vectorial básico. Armá tu personaje, exportá la imagen y después importala en un perfil; VTuberBot no sube tus imágenes ni los convierte automáticamente en un rig.",
            12, "#98A4BE"));
        var webCreatorActions = new WrapPanel();
        webCreatorActions.Children.Add(ActionButton("Humation · kawaii dibujado (PNG/SVG)", (_, _) =>
            OpenAvatarExternalTool("https://apps.humation.app/humation", "Humation — creador kawaii"), true));
        webCreatorActions.Children.Add(ActionButton("Open Peeps Studio · cuerpo y poses", (_, _) =>
            OpenAvatarExternalTool("https://peepstudio.msgenie.art/", "Open Peeps Studio")));
        webCreatorActions.Children.Add(ActionButton("Chibi Maker · peinados y ropa", (_, _) =>
            OpenAvatarExternalTool("https://squarefaceicongenerator.co/pricing/chibi-maker", "Chibi Maker web")));
        panel.Children.Add(webCreatorActions);
        panel.Children.Add(Label(
            "Humation exporta avatares ilustrados; Open Peeps ofrece poses de cuerpo entero con un estilo distinto al anime; Chibi Maker se orienta a iconos cuadrados. Para que una imagen hable, asigná sprites distintos de reposo y habla. Ninguna de estas páginas entrega un rig Live2D listo.",
            12, "#98A4BE"));
        return panel;
    }

    private static BitmapImage LoadChibiPreviewBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private Button BuildChibiPresetButton(ChibiAvatarPreset preset)
    {
        var assets = ModularWardrobeFactory.EnsureAssets(
            avatarDirectory, preset.FaceShapeId, preset.EyeStyleId, preset.SkinId, preset.OutfitId);
        var preview = new Grid
        {
            Width = 92,
            Height = 124,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true
        };

        void AddPreviewLayer(string path)
        {
            preview.Children.Add(new Image
            {
                Source = LoadChibiPreviewBitmap(path),
                Width = 92,
                Height = 124,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });
        }

        AddPreviewLayer(assets.BaseIdle);
        if (preset.BackAccessoryId != "none-back")
            AddPreviewLayer(assets.BackAccessories[preset.BackAccessoryId]);
        AddPreviewLayer(assets.Outfits[preset.OutfitId]);
        AddPreviewLayer(assets.PoseLayers[preset.PoseId]);
        AddPreviewLayer(assets.Hair[preset.HairId]);
        if (assets.HeadAccessories.TryGetValue(preset.HeadAccessoryId, out var headPath))
            AddPreviewLayer(headPath);
        if (assets.FaceAccessories.TryGetValue(preset.FaceAccessoryId, out var facePath))
            AddPreviewLayer(facePath);

        // This badge is a UI-only overlay: it never edits or bakes into the underlying PNG.
        preview.Children.Add(new TextBlock
        {
            Text = "VTUBERBOT · REFERENCIA",
            FontSize = 7,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(205, 22, 28, 45)),
            Padding = new Thickness(3, 2, 3, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(1, 0, 1, 2),
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false
        });

        var card = new StackPanel { Width = 112 };
        card.Children.Add(preview);
        card.Children.Add(new TextBlock
        {
            Text = preset.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F8FAFC"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 2)
        });
        card.Children.Add(new TextBlock
        {
            Text = preset.Description,
            FontSize = 10,
            Foreground = Brush("#98A4BE"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 44
        });
        var originalAssetLink = new TextBlock
        {
            Text = "Ver PNG original sin marca ↗",
            FontSize = 9,
            Foreground = Brush("#8BC5FF"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Abre el archivo PNG local sin la marca superpuesta de la vista previa."
        };
        originalAssetLink.MouseLeftButtonUp += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(Path.GetFullPath(assets.BaseIdle)) { UseShellExecute = true });
                FooterStatus.Text = "PNG original abierto; la marca solo existe en la vista previa.";
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo abrir el PNG original: " + exception.Message;
            }
        };
        card.Children.Add(originalAssetLink);

        var button = new Button
        {
            Content = card,
            Width = 132,
            MinHeight = 192,
            Margin = new Thickness(3),
            Padding = new Thickness(5),
            Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#39476A"),
            BorderThickness = new Thickness(1),
            ToolTip = preset.Description
        };
        button.Click += (_, _) => CreateChibiPresetProfile(preset);
        return button;
    }

    private void CreateChibiPresetProfile(ChibiAvatarPreset preset)
    {
        static WardrobeOption Get(IReadOnlyList<WardrobeOption> options, string id) =>
            options.First(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase));

        try
        {
            CreateModularWardrobeProfile(
                Get(ModularWardrobeFactory.HairOptions, preset.HairId),
                Get(ModularWardrobeFactory.OutfitOptions, preset.OutfitId),
                Get(ModularWardrobeFactory.HeadAccessoryOptions, preset.HeadAccessoryId),
                Get(ModularWardrobeFactory.FaceAccessoryOptions, preset.FaceAccessoryId),
                Get(ModularWardrobeFactory.BackAccessoryOptions, preset.BackAccessoryId),
                new Dictionary<string, Color?>(),
                preset.FaceShapeId,
                preset.EyeStyleId,
                preset.SkinId,
                preset.PoseId,
                preset.Name);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo crear el personaje «" + preset.Name + "»: " + exception.GetBaseException().Message,
                "Crear personaje chibi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateModularWardrobeProfile(
        WardrobeOption hair,
        WardrobeOption outfit,
        WardrobeOption head,
        WardrobeOption face,
        WardrobeOption back,
        IReadOnlyDictionary<string, Color?> customTints,
        string faceShapeId = "round",
        string eyeStyleId = "ocean",
        string skinId = "porcelain",
        string poseId = "neutral",
        string? requestedProfileName = null)
    {
        try
        {
            var assets = ModularWardrobeFactory.EnsureAssets(avatarDirectory, faceShapeId, eyeStyleId, skinId, outfit.Id);
            var customColorDirectory = Path.Combine(avatarDirectory, "builtin-demos", "wardrobe-custom-colors");
            string ApplyTint(string area, string sourcePath) =>
                customTints.TryGetValue(area, out var tint) && tint.HasValue
                    ? ModularWardrobeFactory.TintPng(sourcePath, customColorDirectory, tint.Value)
                    : sourcePath;
            var hasCustomColors = customTints.Values.Any(tint => tint.HasValue);
            var defaultProfileName = "Armario - " + outfit.Name + (hasCustomColors ? " personalizado" : string.Empty);
            var baseName = string.IsNullOrWhiteSpace(requestedProfileName) ? defaultProfileName : requestedProfileName.Trim();
            var profileName = baseName;
            var existingNames = store.GetAvatarProfiles()
                .Select(profile => profile.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var suffix = 2; existingNames.Contains(profileName); suffix++)
            {
                var extra = " (" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                var allowedBaseLength = Math.Max(1, 64 - extra.Length);
                profileName = baseName[..Math.Min(baseName.Length, allowedBaseLength)] + extra;
            }

            var profile = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
            store.SetActiveAvatarProfile(profile.Id);
            store.SetActiveAvatarProfileBasePath(assets.BaseIdle);
            store.SetAvatarExpression("Idle", assets.BaseIdle);
            store.SetAvatarExpression("Talking", assets.BaseTalking);
            store.SetAvatarExpression("Blink", assets.BaseBlink);
            store.SetAvatarExpression("Happy", assets.BaseHappy);
            store.SetAvatarExpression("Surprised", assets.BaseSurprised);

            if (back.Id != "none-back")
                store.AddAvatarLayer("Fondo · " + back.Name, ApplyTint("back", assets.BackAccessories[back.Id]));
            store.AddAvatarLayer("Ropa · " + outfit.Name, ApplyTint("outfit", assets.Outfits[outfit.Id]));
            var selectedPose = ModularWardrobeFactory.PoseOptions.First(option => option.Id == poseId);
            store.AddAvatarLayer("Pose · " + selectedPose.Name, ApplyTint("outfit", assets.PoseLayers[poseId]));
            store.AddAvatarLayer("Cabello · " + hair.Name, ApplyTint("hair", assets.Hair[hair.Id]));
            if (head.Id != "star-halo")
            {
                // A halo is also a head layer, and uses the same common canvas as every other item.
                if (assets.HeadAccessories.TryGetValue(head.Id, out var headPath))
                    store.AddAvatarLayer("Cabeza · " + head.Name, ApplyTint("head", headPath));
            }
            else if (assets.HeadAccessories.TryGetValue(head.Id, out var haloPath))
            {
                store.AddAvatarLayer("Cabeza · " + head.Name, haloPath);
            }
            if (face.Id != "none-face" && assets.FaceAccessories.TryGetValue(face.Id, out var facePath))
                store.AddAvatarLayer("Accesorio · " + face.Name, ApplyTint("face", facePath));

            avatarPath = assets.BaseIdle;
            store.SetSetting("avatar.state", "Idle");
            store.Log("Avatar", "Creado perfil modular con piezas originales: " + profileName + ".");
            Navigate("Avatar Studio");
            ShowOverlay();
            FooterStatus.Text = "Perfil modular creado. Seleccioná las capas para moverlas o cambiar su escala y rotación.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo crear el perfil modular: " + exception.Message,
                "VTuberBot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private const string MissingAvatarArtPromptSetting = "\u001fVTUBERBOT_MISSING_PROMPT\u001f";

    private void MigrateLegacyAvatarArtPrompts(long profileId)
    {
        foreach (var role in AvatarArtPromptCatalog.Roles)
        {
            var profileKey = AvatarArtPromptCatalog.GetSettingKey(profileId, role);
            if (store.GetSetting(profileKey, MissingAvatarArtPromptSetting) != MissingAvatarArtPromptSetting)
                continue;

            var legacyKey = AvatarArtPromptCatalog.GetLegacySettingKey(role);
            var legacyPrompt = store.GetSetting(legacyKey, MissingAvatarArtPromptSetting);
            if (legacyPrompt == MissingAvatarArtPromptSetting || string.IsNullOrWhiteSpace(legacyPrompt))
                continue;

            store.SetSetting(profileKey, legacyPrompt);
            store.SetSetting(legacyKey, string.Empty);
        }
    }

    private void ImportArtAssetForRole(string role, string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        if (!info.Exists || info.Length is <= 0 or > 25 * 1024 * 1024)
            throw new InvalidDataException("La imagen debe existir y ocupar entre 1 byte y 25 MB.");
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!IsSupportedAvatarImageExtension(extension))
            throw new InvalidDataException("Formato de imagen no admitido: " + extension + ".");
        _ = LoadImage(sourcePath);

        var folder = role is "Idle" or "Talking" or "Blink" or "Happy" or "Surprised"
            ? "expressions" : role == "Personaje base" ? "base" : "layers";
        var destinationFolder = Path.Combine(avatarDirectory, "art-library", folder);
        Directory.CreateDirectory(destinationFolder);
        var destination = Path.Combine(destinationFolder,
            Regex.Replace(role.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') + "-" +
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + extension);
        File.Copy(sourcePath, destination, false);

        switch (role)
        {
            case "Personaje base":
                avatarPath = destination;
                store.SetActiveAvatarProfileBasePath(destination);
                break;
            case "Idle":
            case "Talking":
            case "Blink":
            case "Happy":
            case "Surprised":
                store.SetAvatarExpression(role, destination);
                break;
            case "Fondo":
            case "Objeto / capa":
                store.AddAvatarLayer(role == "Fondo" ? "Fondo" : Path.GetFileNameWithoutExtension(sourcePath), destination);
                break;
            default:
                throw new InvalidOperationException("Rol de arte desconocido: " + role);
        }
        RefreshActiveAvatarOverlay();
        store.Log("Avatar", "Asset asignado desde la consola de arte al rol " + role + ".");
    }

    private void ImportAvatar()
    {
        var dialog=new OpenFileDialog{Title="Importar arte del avatar",Filter="Imágenes|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        var info=new FileInfo(dialog.FileName);
        if(info.Length>25*1024*1024){MessageBox.Show(this,"El archivo supera el límite de 25 MB.","VTuberBot",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if(!AvatarImageFormatPolicy.IsSupportedExtension(extension))return;
        try
        {
            _=LoadImage(dialog.FileName);
            var destination=Path.Combine(avatarDirectory,$"avatar-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{extension}");
            File.Copy(dialog.FileName,destination,false);avatarPath=destination;store.SetActiveAvatarProfileBasePath(destination);RefreshActiveAvatarOverlay();store.Log("Avatar",$"Asset importado: {Path.GetFileName(destination)}");
            Navigate("Avatar Studio");
        }
        catch(Exception ex){MessageBox.Show(this,$"No se pudo importar la imagen: {ex.Message}","VTuberBot",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    private void ImportAvatarLayer()
    {
        var dialog=new OpenFileDialog{Title="Importar capa del avatar",Filter="Imágenes PNG/JPG/BMP|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            var info=new FileInfo(dialog.FileName);if(info.Length>25*1024*1024)throw new InvalidOperationException("El archivo supera 25 MB.");
            _=LoadImage(dialog.FileName);
            var destination=Path.Combine(avatarDirectory,"layer-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName).ToLowerInvariant());
            File.Copy(dialog.FileName,destination,false);store.AddAvatarLayer(Path.GetFileNameWithoutExtension(dialog.FileName),destination);store.Log("Avatar","Capa importada: "+Path.GetFileName(destination));
            Navigate("Avatar Studio");
        }
        catch(Exception ex){MessageBox.Show(this,"No se pudo importar la capa: "+ex.Message);}
    }

    private void ImportAvatarExpression(string expressionName)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar sprite para " + expressionName,
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 25 * 1024 * 1024)
                throw new InvalidOperationException("El archivo supera el límite de 25 MB.");
            _ = LoadImage(dialog.FileName);
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var destination = Path.Combine(avatarDirectory,
                "expression-" + expressionName.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N") + extension);
            File.Copy(dialog.FileName, destination, false);
            store.SetAvatarExpression(expressionName, destination);
            RefreshActiveAvatarOverlay();
            store.Log("Avatar", "Sprite de expresión importado: " + expressionName);
            Navigate("Avatar Studio");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo importar el sprite de expresión: " + ex.Message);
        }
    }

    private IReadOnlyDictionary<string, string> GetAvatarExpressionPaths() =>
        store.GetAvatarExpressions()
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && File.Exists(item.Value))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private void RefreshActiveAvatarOverlay()
    {
        if (overlay is { IsVisible: true })
            overlay.SetAvatar(avatarPath ?? string.Empty, store.GetAvatarLayers(), GetAvatarExpressionPaths());
    }

    private void ExportActiveAvatarProfilePackage()
    {
        var profile = store.GetActiveAvatarProfile();
        var dialog = new SaveFileDialog
        {
            Title = "Exportar perfil de avatar",
            FileName = new string(profile.Name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray()) + ".vtuberavatar",
            Filter = "Perfil de avatar VTuberBot (*.vtuberavatar)|*.vtuberavatar|Archivo ZIP (*.zip)|*.zip",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var temporaryPath = dialog.FileName + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            long totalAssetBytes = 0;
            var layers = store.GetAvatarLayers().OrderBy(layer => layer.SortOrder).ToList();
            if (layers.Count > 100)
                throw new InvalidOperationException("El paquete admite hasta 100 capas por perfil.");

            var expressions = store.GetAvatarExpressions();
            if (expressions.Count > 7)
                throw new InvalidOperationException("El perfil contiene más de 7 expresiones admitidas por este formato.");

            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            {
                string AddAsset(string sourcePath, string archivePath)
                {
                    if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                        throw new FileNotFoundException("Falta un asset utilizado por el perfil.", sourcePath);
                    var info = new FileInfo(sourcePath);
                    if (info.Length is <= 0 or > 25 * 1024 * 1024)
                        throw new InvalidDataException("Cada imagen del paquete debe ocupar entre 1 byte y 25 MB: " + Path.GetFileName(sourcePath));
                    var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                    if (!IsSupportedAvatarImageExtension(extension))
                        throw new InvalidDataException("Formato de imagen no admitido: " + extension);
                    totalAssetBytes = checked(totalAssetBytes + info.Length);
                    if (totalAssetBytes > 250 * 1024 * 1024)
                        throw new InvalidDataException("Los assets del perfil superan el límite total de 250 MB.");
                    var entry = archive.CreateEntry(archivePath, CompressionLevel.Optimal);
                    using var source = File.OpenRead(sourcePath);
                    using var target = entry.Open();
                    source.CopyTo(target);
                    return archivePath;
                }

                var baseAsset = string.IsNullOrWhiteSpace(profile.BasePath)
                    ? null
                    : AddAsset(profile.BasePath, "base/avatar" + Path.GetExtension(profile.BasePath).ToLowerInvariant());

                var exportedLayers = new List<AvatarProfilePackageLayer>(layers.Count);
                for (var index = 0; index < layers.Count; index++)
                {
                    var layer = layers[index];
                    var extension = Path.GetExtension(layer.Path).ToLowerInvariant();
                    var entryPath = AddAsset(layer.Path, $"layers/{index + 1:000}{extension}");
                    exportedLayers.Add(new AvatarProfilePackageLayer(
                        layer.Name, entryPath, index, layer.Visible,
                        layer.OffsetX, layer.OffsetY, layer.Scale, layer.RotationDegrees));
                }

                var exportedExpressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var expression in expressions)
                {
                    ValidatePackageExpressionState(expression.Key);
                    var extension = Path.GetExtension(expression.Value).ToLowerInvariant();
                    exportedExpressions[expression.Key] = AddAsset(
                        expression.Value, $"expressions/{expression.Key.ToLowerInvariant()}{extension}");
                }

                var exportedPrompts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var role in AvatarArtPromptCatalog.Roles)
                {
                    var prompt = store.GetSetting(
                        AvatarArtPromptCatalog.GetSettingKey(profile.Id, role), MissingAvatarArtPromptSetting);
                    if (prompt == MissingAvatarArtPromptSetting)
                        continue;
                    if (prompt.Length > 12000)
                        throw new InvalidDataException("El prompt de arte supera el límite de 12.000 caracteres: " + role);
                    exportedPrompts[role] = prompt;
                }

                var manifest = new AvatarProfilePackageManifest(
                    1, profile.Name, baseAsset, exportedLayers, exportedExpressions, exportedPrompts);
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var manifestStream = manifestEntry.Open();
                JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
            }

            var packageInfo = new FileInfo(temporaryPath);
            if (packageInfo.Length > 300 * 1024 * 1024)
                throw new InvalidDataException("El paquete supera el límite de 300 MB.");
            File.Move(temporaryPath, dialog.FileName, overwrite: true);
            store.Log("Avatar", "Perfil exportado como paquete portable: " + Path.GetFileName(dialog.FileName) + ".");
            FooterStatus.Text = "Perfil exportado con base, capas, expresiones y prompts guardados.";
            MessageBox.Show(this, "El paquete incluye los assets, la composición y los prompts personalizados de este perfil. No contiene credenciales ni el resto de la base de datos.",
                "Perfil de avatar exportado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            store.Log("Avatar", "Falló la exportación de perfil (" + exception.GetType().Name + ").");
            MessageBox.Show(this, "No se pudo exportar el perfil: " + exception.Message,
                "Exportar avatar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportAvatarProfilePackage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar perfil de avatar",
            Filter = "Perfil de avatar VTuberBot (*.vtuberavatar;*.zip)|*.vtuberavatar;*.zip",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        var packageDirectory = Path.Combine(avatarDirectory, "profile-packages", Guid.NewGuid().ToString("N"));
        AvatarProfile? createdProfile = null;
        var previousProfile = store.GetActiveAvatarProfile();
        try
        {
            var packageInfo = new FileInfo(dialog.FileName);
            if (packageInfo.Length is <= 0 or > 300 * 1024 * 1024)
                throw new InvalidDataException("El paquete debe ocupar entre 1 byte y 300 MB.");

            AvatarProfilePackageManifest manifest;
            Dictionary<string, ZipArchiveEntry> archiveEntries;
            using var archive = ZipFile.OpenRead(dialog.FileName);
            if (archive.Entries.Count is < 1 or > 120)
                    throw new InvalidDataException("El paquete debe contener como máximo 120 archivos.");

            long uncompressedTotal = 0;
            archiveEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                uncompressedTotal = checked(uncompressedTotal + entry.Length);
                if (uncompressedTotal > 300 * 1024 * 1024)
                    throw new InvalidDataException("El contenido descomprimido supera el límite de 300 MB.");
                if (!archiveEntries.TryAdd(entry.FullName, entry))
                    throw new InvalidDataException("El paquete contiene nombres de archivo duplicados.");
            }

            if (!archiveEntries.TryGetValue("manifest.json", out var manifestEntry) ||
                manifestEntry.Length is <= 0 or > 1024 * 1024)
                throw new InvalidDataException("No se encontró un manifest.json válido en el paquete.");

            using var manifestStream = manifestEntry.Open();
            manifest = JsonSerializer.Deserialize<AvatarProfilePackageManifest>(manifestStream)
                ?? throw new InvalidDataException("No se pudo leer el manifiesto del perfil.");

            if (manifest.FormatVersion != 1)
                throw new InvalidDataException("Versión de paquete no admitida: " + manifest.FormatVersion + ".");
            if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Trim().Length > 64)
                throw new InvalidDataException("El nombre de perfil del manifiesto no es válido.");
            if (manifest.Layers is null || manifest.Layers.Count > 100)
                throw new InvalidDataException("El paquete puede incluir hasta 100 capas.");
            if (manifest.Expressions is null || manifest.Expressions.Count > 5)
                throw new InvalidDataException("El paquete puede incluir hasta cinco expresiones.");

            if (manifest.Prompts is { Count: > 8 })
                throw new InvalidDataException("El paquete puede incluir hasta ocho prompts de arte.");
            if (manifest.Prompts is not null)
            {
                foreach (var prompt in manifest.Prompts)
                {
                    if (!AvatarArtPromptCatalog.Roles.Contains(prompt.Key, StringComparer.OrdinalIgnoreCase))
                        throw new InvalidDataException("El paquete contiene un rol de prompt desconocido: " + prompt.Key);
                    if (prompt.Value is null || prompt.Value.Length > 12000)
                        throw new InvalidDataException("Cada prompt de arte debe tener como máximo 12.000 caracteres.");
                }
            }

            Directory.CreateDirectory(packageDirectory);
            string ExtractAsset(string entryName, string requiredPrefix, string destinationFolder, string destinationStem)
            {
                if (!IsSafeAvatarPackageEntry(entryName) ||
                    !entryName.StartsWith(requiredPrefix + "/", StringComparison.Ordinal))
                    throw new InvalidDataException("Ruta de asset no válida dentro del paquete: " + entryName);
                if (!archiveEntries.TryGetValue(entryName, out var entry))
                    throw new InvalidDataException("No se encontró el asset " + entryName + " dentro del paquete.");
                if (entry.Length is <= 0 or > 25 * 1024 * 1024)
                    throw new InvalidDataException("Cada imagen debe ocupar entre 1 byte y 25 MB.");

                var extension = Path.GetExtension(entryName).ToLowerInvariant();
                if (!IsSupportedAvatarImageExtension(extension))
                    throw new InvalidDataException("Formato de imagen no admitido: " + extension);
                var destinationPath = Path.Combine(packageDirectory, destinationFolder, destinationStem + extension);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                using (var input = entry.Open())
                using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long copied = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        copied += read;
                        if (copied > 25 * 1024 * 1024)
                            throw new InvalidDataException("El asset descomprimido supera el límite de 25 MB.");
                        output.Write(buffer, 0, read);
                    }
                    if (copied == 0)
                        throw new InvalidDataException("El asset está vacío.");
                }

                _ = LoadImage(destinationPath);
                return destinationPath;
            }

            string? basePath = null;
            if (!string.IsNullOrWhiteSpace(manifest.BaseAsset))
                basePath = ExtractAsset(manifest.BaseAsset, "base", "base", "avatar");

            var normalizedLayers = new List<(AvatarProfilePackageLayer Layer, string Path)>();
            foreach (var layer in manifest.Layers.OrderBy(layer => layer.SortOrder))
            {
                if (string.IsNullOrWhiteSpace(layer.Name) || layer.Name.Trim().Length > 100)
                    throw new InvalidDataException("Una capa tiene un nombre vacío o demasiado largo.");
                if (!double.IsFinite(layer.OffsetX) || layer.OffsetX is < -500 or > 500 ||
                    !double.IsFinite(layer.OffsetY) || layer.OffsetY is < -500 or > 500 ||
                    !double.IsFinite(layer.Scale) || layer.Scale is < 0.1 or > 4 ||
                    !double.IsFinite(layer.RotationDegrees) || layer.RotationDegrees is < -180 or > 180)
                    throw new InvalidDataException("Una capa contiene transformaciones fuera de los límites permitidos.");
                var assetPath = ExtractAsset(
                    layer.AssetEntry, "layers", "layers", "layer-" + normalizedLayers.Count.ToString("000"));
                normalizedLayers.Add((layer, assetPath));
            }

            var normalizedExpressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var expression in manifest.Expressions)
            {
                ValidatePackageExpressionState(expression.Key);
                var assetPath = ExtractAsset(
                    expression.Value, "expressions", "expressions", expression.Key.ToLowerInvariant());
                normalizedExpressions[expression.Key] = assetPath;
            }

            var usedNames = store.GetAvatarProfiles()
                .Select(profile => profile.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var importRootName = manifest.Name.Trim();
            if (importRootName.Length > 48) importRootName = importRootName[..48].TrimEnd();
            var profileName = importRootName + " (importado)";
            var suffix = 2;
            while (usedNames.Contains(profileName))
            {
                var suffixText = " (" + suffix++ + ")";
                profileName = importRootName[..Math.Min(importRootName.Length, 63 - suffixText.Length)].TrimEnd() + suffixText;
            }

            createdProfile = store.CreateAvatarProfile(profileName, duplicateActiveProfile: false);
            store.SetActiveAvatarProfile(createdProfile.Id);
            if (basePath is not null)
                store.SetActiveAvatarProfileBasePath(basePath);

            foreach (var item in normalizedLayers)
            {
                var layerId = store.AddAvatarLayer(item.Layer.Name, item.Path);
                store.UpdateAvatarLayerTransform(layerId, item.Layer.OffsetX, item.Layer.OffsetY,
                    item.Layer.Scale, item.Layer.RotationDegrees);
                if (!item.Layer.Visible)
                    store.SetAvatarLayerVisibility(layerId, false);
            }
            foreach (var expression in normalizedExpressions)
                store.SetAvatarExpression(expression.Key, expression.Value);
            if (manifest.Prompts is not null)
            {
                foreach (var prompt in manifest.Prompts)
                    store.SetSetting(AvatarArtPromptCatalog.GetSettingKey(createdProfile.Id, prompt.Key), prompt.Value);
            }

            avatarPath = store.GetActiveAvatarProfile().BasePath;
            RefreshActiveAvatarOverlay();
            store.Log("Avatar", "Paquete de avatar importado: " + profileName + ".");
            Navigate("Avatar Studio");
            MessageBox.Show(this, "El perfil se importó como «" + profileName + "». La composición y las imágenes quedaron guardadas localmente.",
                "Importación completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            if (createdProfile is not null)
            {
                try
                {
                    store.SetActiveAvatarProfile(previousProfile.Id);
                    store.DeleteAvatarProfile(createdProfile.Id);
                }
                catch (Exception rollbackException)
                {
                    store.Log("Avatar", "No se pudo retirar completamente el perfil parcial (" + rollbackException.GetType().Name + ").");
                }
            }
            avatarPath = store.GetActiveAvatarProfile().BasePath;
            try { RefreshActiveAvatarOverlay(); } catch { }
            try { if (Directory.Exists(packageDirectory)) Directory.Delete(packageDirectory, recursive: true); } catch { }
            store.Log("Avatar", "Falló la importación del paquete (" + exception.GetType().Name + ").");
            MessageBox.Show(this, "No se importó el paquete. El perfil activo anterior se conserva.\n\nDetalle: " + exception.Message,
                "Importar avatar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsSupportedAvatarImageExtension(string extension) =>
        AvatarImageFormatPolicy.IsSupportedExtension(extension);

    private static bool IsSafeAvatarPackageEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName) ||
            entryName.Contains('\\') || entryName.StartsWith("/", StringComparison.Ordinal))
            return false;
        return entryName.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not ".." &&
            !segment.Contains(':'));
    }

    private static void ValidatePackageExpressionState(string state)
    {
        if (state is null || !(state.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Talking", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Blink", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Happy", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Surprised", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Angry", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("LeftEyeClose", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Estado de expresión no admitido: " + (state ?? "(vacío)") + ".");
    }

    private void ShowOverlay()
    {
        if(string.IsNullOrWhiteSpace(avatarPath)||!File.Exists(avatarPath)){MessageBox.Show(this,"Primero importá una imagen en Avatar Studio.");return;}
        try { overlay??=new AvatarOverlayWindow(); overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths()); overlay.ReduceMotion=store.GetSetting("ui.reduceMotion","false")=="true"; overlay.SetParallaxEnabled(store.GetSetting("avatar.parallax.enabled","true")=="true"); overlay.SetParallaxStrength(double.TryParse(store.GetSetting("avatar.parallax.strength","3"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var parallaxStrength)?Math.Clamp(parallaxStrength,0,AvatarParallaxPolicy.MaximumOffsetPixels):3); overlay.SetState(store.GetSetting("avatar.state","Idle")); if(!overlay.IsVisible)overlay.Show();overlay.Activate();store.Log("Avatar","Overlay mostrado."); }
        catch(Exception ex){MessageBox.Show(this,$"No se pudo abrir el overlay: {ex.Message}");}
    }
}
