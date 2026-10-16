using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private const string PngtuberShakeEnabledKey = "avatar.pngtuber.motion.shake-enabled";
    private const string PngtuberShakeStrengthKey = "avatar.pngtuber.motion.shake-strength";
    private const string PngtuberBounceEnabledKey = "avatar.pngtuber.motion.bounce-enabled";

    private UIElement BuildPngtuber2DWorkspace()
    {
        var root = new StackPanel();
        var intro = Panel("Tu PNGTuber 2D · modo simple");
        intro.Children.Add(Label(
            "Acá solo necesitás el PNG de tu personaje, opcionalmente dos stickers de boca dibujada y los efectos de movimiento. No se genera una boca realista: se muestran exactamente los dibujos que elijas. OBS y Twitch se configuran en sus botones separados.",
            13, "#C4CDE0"));
        var quickSteps = new UniformGrid { Columns = 3, Margin = new Thickness(0, 10, 0, 0) };
        quickSteps.Children.Add(StatusRow("1 · PERSONAJE", string.IsNullOrWhiteSpace(avatarPath) ? "Importar PNG" : "Imagen base lista", !string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath)));
        var currentMouth = FindPngtuberMouthLayer();
        quickSteps.Children.Add(StatusRow("2 · BOCA", currentMouth?.ClosedMouthPath is not null ? "Capa creada" : "Opcional", currentMouth?.ClosedMouthPath is not null));
        quickSteps.Children.Add(StatusRow("3 · MOVIMIENTO", "Vibración y rebote", true));
        intro.Children.Add(quickSteps);
        root.Children.Add(intro);

        var workspace = new Grid();
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var previewPanel = Panel("Vista previa · arrastrá la boca para ubicarla");
        previewPanel.Margin = new Thickness(0, 0, 14, 0);
        var previewFrame = new Border
        {
            Background = Brush("#101525"), BorderBrush = Brush("#34415E"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8), MinHeight = 430
        };
        Grid? previewCanvas = null;
        Image? basePreview = null;
        var layerImages = new Dictionary<long, Image>();
        var previewState = store.GetSetting("avatar.state", "Idle");
        if (previewState is not ("Idle" or "Talking")) previewState = "Idle";

        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            previewCanvas = new Grid { Background = Brush("#101525"), Height = 460, ClipToBounds = true };
            var layers = store.GetAvatarLayers().Where(layer => layer.Visible && File.Exists(layer.Path)).ToArray();
            var expressionPath = store.GetAvatarExpression(previewState);
            var activeBasePath = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath) ? expressionPath : avatarPath;
            basePreview = new Image
            {
                Source = LoadImage(activeBasePath), Stretch = Stretch.Uniform,
                MaxHeight = 440, IsHitTestVisible = false
            };

            foreach (var layer in AvatarLayerOrdering.BackgroundLayers(layers))
            {
                var artworkPath = AvatarMouthSpritePolicy.ResolvePath(layer, previewState);
                var image = new Image { Source = LoadImage(artworkPath), Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
                ApplyPngtuberPreviewTransform(image, layer);
                previewCanvas.Children.Add(image);
                layerImages[layer.Id] = image;
            }
            previewCanvas.Children.Add(basePreview);
            foreach (var layer in AvatarLayerOrdering.ForegroundLayers(layers))
            {
                var artworkPath = AvatarMouthSpritePolicy.ResolvePath(layer, previewState);
                var isMouthPiece = IsPngtuberMouthArtwork(layer, artworkPath);
                var image = new Image
                {
                    Source = LoadImage(artworkPath),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = isMouthPiece ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                    VerticalAlignment = isMouthPiece ? VerticalAlignment.Center : VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                };
                ApplyPngtuberPreviewTransform(image, layer);
                previewCanvas.Children.Add(image);
                layerImages[layer.Id] = image;
            }
            previewFrame.Child = previewCanvas;
        }
        else
        {
            previewFrame.Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    Label("Tu personaje aparecerá acá", 20, "#F8FAFC", true),
                    Label("Pulsa «Importar mi personaje PNG» para empezar.", 13, "#98A4BE")
                }
            };
        }
        previewPanel.Children.Add(previewFrame);
        previewPanel.Children.Add(Label("Probá el cambio de boca sin hablar. Para que el sticker quede bien, usá PNG transparente con solo la boca recortada.", 11, "#98A4BE"));
        var stateActions = new WrapPanel();
        stateActions.Children.Add(ActionButton("🙂 Reposo", (_, _) => SetPreviewState("Idle")));
        stateActions.Children.Add(ActionButton("🗣 Boca abierta", (_, _) => SetPreviewState("Talking")));
        previewPanel.Children.Add(stateActions);
        Grid.SetColumn(previewPanel, 0);
        workspace.Children.Add(previewPanel);

        var controlsPanel = Panel("Configuración rápida");
        controlsPanel.Children.Add(ActionButton("＋ Importar mi personaje PNG", (_, _) => ImportPngtuberBase(), true));
        controlsPanel.Children.Add(Label("BOCA ANIME / STICKER", 13, "#F8FAFC", true));
        controlsPanel.Children.Add(Label("1. Elegí el dibujo de boca cerrada. 2. Elegí otro dibujo para cuando hablás. Son stickers 2D: no hace falta un modelo 3D.", 12, "#98A4BE"));
        var mouthActions = new WrapPanel();
        mouthActions.Children.Add(ActionButton("Elegir boca cerrada", (_, _) => ImportPngtuberMouth(open: false)));
        mouthActions.Children.Add(ActionButton("Elegir boca abierta", (_, _) => ImportPngtuberMouth(open: true)));
        controlsPanel.Children.Add(mouthActions);

        var moveMouth = false;
        var moveButton = ActionButton("🎯 Activar movimiento de boca con mouse", (_, _) =>
        {
            if (FindPngtuberMouthLayer() is null)
            {
                MessageBox.Show(this, "Primero elegí el PNG de boca cerrada.", "Ubicar la boca", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            moveMouth = !moveMouth;
            moveButtonPlaceholder(moveButton: null, active: moveMouth);
        });
        void moveButtonPlaceholder(Button? moveButton, bool active) { }
        // Replace the generic button content while keeping its action obvious.
        moveButton.Click -= null;
        controlsPanel.Children.Add(moveButton);

        var xSlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 5, IsSnapToTickEnabled = false };
        var ySlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 5, IsSnapToTickEnabled = false };
        var scaleSlider = new Slider { Minimum = .1, Maximum = 4, Value = 1, TickFrequency = .1 };
        var xLabel = Label("Mover a izquierda/derecha: 0", 11, "#98A4BE");
        var yLabel = Label("Mover arriba/abajo: 0", 11, "#98A4BE");
        var scaleLabel = Label("Tamaño de boca: 100%", 11, "#98A4BE");
        controlsPanel.Children.Add(xLabel); controlsPanel.Children.Add(xSlider);
        controlsPanel.Children.Add(yLabel); controlsPanel.Children.Add(ySlider);
        controlsPanel.Children.Add(scaleLabel); controlsPanel.Children.Add(scaleSlider);
        controlsPanel.Children.Add(ActionButton("💾 Guardar posición de boca", (_, _) => SaveMouthPosition(), true));
        controlsPanel.Children.Add(ActionButton("La boca vieja sigue debajo · limpiarla", (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
            {
                MessageBox.Show(this, "Primero importá el personaje base.");
                return;
            }
            OpenMouthCutoutEditor();
            Navigate("PNGTuber 2D");
        }));
        controlsPanel.Children.Add(Label("Si ves dos bocas superpuestas, usá la última herramienta para quitar la anterior. El original no se sobrescribe: se crea una copia.", 11, "#98A4BE"));

        controlsPanel.Children.Add(new Divider { Margin = new Thickness(0, 12, 0, 10), Background = Brush("#34415E"), Height = 1 });
        controlsPanel.Children.Add(Label("EFECTOS DE DIBUJO · SIN IA", 13, "#F8FAFC", true));
        var shakeEnabled = new CheckBox
        {
            Content = "Vibrar/sacudir al hablar",
            IsChecked = store.GetSetting(PngtuberShakeEnabledKey, "true") == "true",
            Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 6)
        };
        var bounceEnabled = new CheckBox
        {
            Content = "Rebotar suavemente al hablar",
            IsChecked = store.GetSetting(PngtuberBounceEnabledKey, "true") == "true",
            Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 0, 0, 8)
        };
        var shakeStrength = double.TryParse(store.GetSetting(PngtuberShakeStrengthKey, "6"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStrength)
            ? AvatarMotionPolicy.NormalizeShakeStrength(parsedStrength) : 6;
        var strengthSlider = new Slider { Minimum = 0, Maximum = AvatarMotionPolicy.MaximumShakePixels, Value = shakeStrength, TickFrequency = 1 };
        var strengthLabel = Label($"Intensidad de la vibración: {shakeStrength:0}/18", 11, "#98A4BE");
        void SaveMotionSettings()
        {
            store.SetSetting(PngtuberShakeEnabledKey, shakeEnabled.IsChecked == true ? "true" : "false");
            store.SetSetting(PngtuberBounceEnabledKey, bounceEnabled.IsChecked == true ? "true" : "false");
            store.SetSetting(PngtuberShakeStrengthKey, strengthSlider.Value.ToString(CultureInfo.InvariantCulture));
            strengthLabel.Text = $"Intensidad de la vibración: {strengthSlider.Value:0}/18";
            ApplyPngtuberMotionSettings();
        }
        shakeEnabled.Checked += (_, _) => SaveMotionSettings();
        shakeEnabled.Unchecked += (_, _) => SaveMotionSettings();
        bounceEnabled.Checked += (_, _) => SaveMotionSettings();
        bounceEnabled.Unchecked += (_, _) => SaveMotionSettings();
        strengthSlider.ValueChanged += (_, _) => SaveMotionSettings();
        controlsPanel.Children.Add(shakeEnabled);
        controlsPanel.Children.Add(bounceEnabled);
        controlsPanel.Children.Add(strengthLabel);
        controlsPanel.Children.Add(strengthSlider);
        controlsPanel.Children.Add(ActionButton("⚡ Probar vibración ahora", (_, _) =>
        {
            ShowOverlay();
            ApplyPngtuberMotionSettings();
            overlay?.TriggerShake();
        }, true));
        controlsPanel.Children.Add(ActionButton("Mostrar mi avatar transparente", (_, _) =>
        {
            ShowOverlay();
            ApplyPngtuberMotionSettings();
        }));
        controlsPanel.Children.Add(Label("Para usarlo en OBS, agregá la ventana «VTuberBot Avatar Overlay» como una captura de ventana. El overlay no inicia una transmisión por sí solo.", 11, "#98A4BE"));
        Grid.SetColumn(controlsPanel, 1);
        workspace.Children.Add(controlsPanel);
        root.Children.Add(workspace);

        var serviceActions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        serviceActions.Children.Add(ActionButton("⚙ Configurar OBS paso a paso", (_, _) => Navigate("OBS"), true));
        serviceActions.Children.Add(ActionButton("Conectar el chat de Twitch", (_, _) => Navigate("Twitch y chat")));
        serviceActions.Children.Add(ActionButton("Configurar micrófono", (_, _) => Navigate("Voz y audio")));
        root.Children.Add(serviceActions);

        if (previewCanvas is not null)
        {
            void UpdateLayerSource(AvatarLayer layer, string state)
            {
                if (!layerImages.TryGetValue(layer.Id, out var image)) return;
                var path = AvatarMouthSpritePolicy.ResolvePath(layer, state);
                if (File.Exists(path)) image.Source = LoadImage(path);
            }

            void UpdateMouthPreviewSizing()
            {
                if (previewCanvas is null || basePreview?.Source is not BitmapSource reference)
                    return;
                var viewportWidth = previewCanvas.ActualWidth > 0 ? previewCanvas.ActualWidth : 560;
                var viewportHeight = previewCanvas.ActualHeight > 0 ? previewCanvas.ActualHeight : 460;
                foreach (var layer in store.GetAvatarLayers())
                {
                    if (!layerImages.TryGetValue(layer.Id, out var image) || image.Source is not BitmapSource artwork)
                        continue;
                    var currentPath = AvatarMouthSpritePolicy.ResolvePath(layer, previewState);
                    if (IsPngtuberMouthArtwork(layer, currentPath))
                    {
                        var size = AvatarLayerSizingPolicy.GetStickerSize(artwork.PixelWidth, artwork.PixelHeight,
                            reference.PixelWidth, reference.PixelHeight, viewportWidth, viewportHeight);
                        image.Width = size.Width;
                        image.Height = size.Height;
                        image.HorizontalAlignment = HorizontalAlignment.Center;
                        image.VerticalAlignment = VerticalAlignment.Center;
                    }
                }
            }

            void SetPreviewState(string state)
            {
                previewState = state;
                store.SetSetting("avatar.state", state);
                var savedExpression = store.GetAvatarExpression(state);
                if (basePreview is not null && !string.IsNullOrWhiteSpace(savedExpression) && File.Exists(savedExpression))
                    basePreview.Source = LoadImage(savedExpression);
                else if (basePreview is not null && !string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
                    basePreview.Source = LoadImage(avatarPath);
                foreach (var layer in store.GetAvatarLayers())
                    UpdateLayerSource(layer, state);
                UpdateMouthPreviewSizing();
                overlay?.SetState(state);
            }

            void RefreshMouthTransform()
            {
                var mouth = FindPngtuberMouthLayer();
                xLabel.Text = $"Mover a izquierda/derecha: {xSlider.Value:0}";
                yLabel.Text = $"Mover arriba/abajo: {ySlider.Value:0}";
                scaleLabel.Text = $"Tamaño de boca: {scaleSlider.Value:P0}";
                if (mouth is not null && layerImages.TryGetValue(mouth.Id, out var image))
                    ApplyPngtuberPreviewTransform(image, mouth with { OffsetX = xSlider.Value, OffsetY = ySlider.Value, Scale = scaleSlider.Value });
            }

            void SaveMouthPosition()
            {
                var mouth = FindPngtuberMouthLayer();
                if (mouth is null)
                {
                    MessageBox.Show(this, "Elegí primero la boca cerrada.", "Guardar posición", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    store.UpdateAvatarLayerTransform(mouth.Id, xSlider.Value, ySlider.Value, scaleSlider.Value, mouth.RotationDegrees);
                    if (overlay is { IsVisible: true } && !string.IsNullOrWhiteSpace(avatarPath))
                        overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths());
                    FooterStatus.Text = $"Posición de boca guardada (X {xSlider.Value:0}, Y {ySlider.Value:0}, tamaño {scaleSlider.Value:P0}).";
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "No se pudo guardar la posición: " + exception.Message, "Posición de boca", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            void LoadMouthTransform()
            {
                var mouth = FindPngtuberMouthLayer();
                xSlider.Value = mouth?.OffsetX ?? 0;
                ySlider.Value = mouth?.OffsetY ?? 0;
                scaleSlider.Value = mouth?.Scale ?? 1;
                RefreshMouthTransform();
                UpdateMouthPreviewSizing();
            }

            xSlider.ValueChanged += (_, _) => RefreshMouthTransform();
            ySlider.ValueChanged += (_, _) => RefreshMouthTransform();
            scaleSlider.ValueChanged += (_, _) => RefreshMouthTransform();
            previewCanvas.SizeChanged += (_, _) => UpdateMouthPreviewSizing();
            previewCanvas.Loaded += (_, _) => { LoadMouthTransform(); UpdateMouthPreviewSizing(); };

            System.Windows.Point dragStart = default;
            double dragX = 0, dragY = 0, dragScale = 1;
            var dragging = false;
            previewCanvas.MouseLeftButtonDown += (_, e) =>
            {
                if (!moveMouth) return;
                var mouth = FindPngtuberMouthLayer();
                if (mouth is null || !layerImages.ContainsKey(mouth.Id)) return;
                dragStart = e.GetPosition(previewCanvas);
                dragX = xSlider.Value; dragY = ySlider.Value;
                var reference = basePreview?.Source as BitmapSource;
                var overlayWidth = overlay is { ActualWidth: > 0 } ? overlay.ActualWidth : 420d;
                var overlayHeight = overlay is { ActualHeight: > 0 } ? overlay.ActualHeight : 520d;
                if (reference is not null)
                {
                    var previewFit = AvatarLayerSizingPolicy.GetFitScale(reference.PixelWidth, reference.PixelHeight,
                        Math.Max(1, previewCanvas.ActualWidth), Math.Max(1, previewCanvas.ActualHeight));
                    var overlayFit = AvatarLayerSizingPolicy.GetFitScale(reference.PixelWidth, reference.PixelHeight,
                        overlayWidth, overlayHeight);
                    dragScale = overlayFit / Math.Max(.0001, previewFit);
                }
                dragging = previewCanvas.CaptureMouse();
                if (dragging) e.Handled = true;
            };
            previewCanvas.MouseMove += (_, e) =>
            {
                if (!dragging || !previewCanvas.IsMouseCaptured) return;
                var point = e.GetPosition(previewCanvas);
                xSlider.Value = Math.Clamp(dragX + (point.X - dragStart.X) * dragScale, xSlider.Minimum, xSlider.Maximum);
                ySlider.Value = Math.Clamp(dragY + (point.Y - dragStart.Y) * dragScale, ySlider.Minimum, ySlider.Maximum);
            };
            previewCanvas.MouseLeftButtonUp += (_, e) =>
            {
                if (!dragging) return;
                dragging = false;
                previewCanvas.ReleaseMouseCapture();
                e.Handled = true;
                FooterStatus.Text = "Vista previa ajustada. Pulsá «Guardar posición de boca».";
            };
        }

        root.Children.Add(Label("Herramientas avanzadas (el editor 2.5D/3D, las capas complejas y los paquetes de perfil quedan fuera del recorrido básico).", 11, "#7F8AA5"));
        return root;
    }

    private void ApplyPngtuberMotionSettings()
    {
        if (overlay is null) return;
        var shake = store.GetSetting(PngtuberShakeEnabledKey, "true") == "true";
        var bounce = store.GetSetting(PngtuberBounceEnabledKey, "true") == "true";
        var intensity = double.TryParse(store.GetSetting(PngtuberShakeStrengthKey, "6"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) ? AvatarMotionPolicy.NormalizeShakeStrength(parsed) : 6;
        overlay.SetMotionEffects(shake, intensity, bounce);
        overlay.ReduceMotion = store.GetSetting("ui.reduceMotion", "false") == "true";
    }

    private void ImportPngtuberBase()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí el PNG de tu personaje",
            Filter = "Imagen del personaje|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length is <= 0 or > 25 * 1024 * 1024)
                throw new InvalidOperationException("La imagen debe ocupar entre 1 byte y 25 MB.");
            var bitmap = LoadImage(dialog.FileName);
            if (bitmap.PixelWidth > 16384 || bitmap.PixelHeight > 16384 || (long)bitmap.PixelWidth * bitmap.PixelHeight > 80_000_000)
                throw new InvalidOperationException("La imagen supera las dimensiones admitidas.");
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var destination = Path.Combine(avatarDirectory, "pngtuber-base-" + Guid.NewGuid().ToString("N") + extension);
            File.Copy(dialog.FileName, destination, false);
            avatarPath = destination;
            store.SetActiveAvatarProfileBasePath(destination);
            RefreshActiveAvatarOverlay();
            store.Log("Avatar", "Imagen base importada desde el modo PNGTuber 2D.");
            FooterStatus.Text = "Personaje importado. Ahora podés añadir la boca animada y probar los efectos.";
            Navigate("PNGTuber 2D");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo importar el personaje: " + exception.Message, "PNGTuber 2D", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportPngtuberMouth(bool open)
    {
        var dialog = new OpenFileDialog
        {
            Title = open ? "Elegí el dibujo de boca abierta" : "Elegí el dibujo de boca cerrada",
            Filter = "PNG transparente recomendado (*.png)|*.png|Todas las imágenes compatibles (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        string? destination = null;
        try
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                throw new InvalidOperationException("Primero importá la imagen base de tu personaje.");
            var info = new FileInfo(dialog.FileName);
            if (info.Length is <= 0 or > 25 * 1024 * 1024)
                throw new InvalidOperationException("El dibujo de la boca debe ocupar entre 1 byte y 25 MB.");
            var bitmap = LoadImage(dialog.FileName);
            if (bitmap.PixelWidth > 16384 || bitmap.PixelHeight > 16384 || (long)bitmap.PixelWidth * bitmap.PixelHeight > 80_000_000)
                throw new InvalidOperationException("El dibujo de la boca supera las dimensiones admitidas.");
            if (open && FindPngtuberMouthLayer()?.ClosedMouthPath is null)
                throw new InvalidOperationException("Primero elegí la boca cerrada para crear la capa.");
            var mouth = FindPngtuberMouthLayer();
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            destination = Path.Combine(avatarDirectory, "pngtuber-mouth-" + Guid.NewGuid().ToString("N") + extension);
            File.Copy(dialog.FileName, destination, false);
            if (mouth is null)
            {
                var mouthId = store.AddAvatarLayer("Boca animada", destination);
                store.SetAvatarLayerMouthSprites(mouthId, destination, null);
            }
            else if (open)
            {
                store.SetAvatarLayerMouthSprites(mouth.Id, mouth.ClosedMouthPath ?? mouth.Path, destination);
            }
            else
            {
                store.SetAvatarLayerMouthSprites(mouth.Id, destination, mouth.OpenMouthPath);
            }
            RefreshActiveAvatarOverlay();
            store.Log("Avatar", $"PNG de boca {(open ? "abierta" : "cerrada")} asignado en modo simple.");
            FooterStatus.Text = open
                ? "Boca abierta asignada. Se mostrará cuando el micrófono active Talking."
                : "Boca cerrada asignada. Ahora podés elegir la boca abierta y acomodar el sticker.";
            Navigate("PNGTuber 2D");
        }
        catch (Exception exception)
        {
            if (destination is not null) { try { File.Delete(destination); } catch { } }
            MessageBox.Show(this, "No se pudo asignar la boca: " + exception.Message, "PNGTuber 2D", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private AvatarLayer? FindPngtuberMouthLayer()
    {
        var layers = store.GetAvatarLayers();
        return layers.FirstOrDefault(layer => !string.IsNullOrWhiteSpace(layer.ClosedMouthPath) || !string.IsNullOrWhiteSpace(layer.OpenMouthPath))
            ?? layers.FirstOrDefault(layer => string.Equals(layer.Name, "Boca animada", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPngtuberMouthArtwork(AvatarLayer layer, string path) =>
        (!string.IsNullOrWhiteSpace(layer.ClosedMouthPath) && string.Equals(path, layer.ClosedMouthPath, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(layer.OpenMouthPath) && string.Equals(path, layer.OpenMouthPath, StringComparison.OrdinalIgnoreCase));

    private static void ApplyPngtuberPreviewTransform(Image image, AvatarLayer layer)
    {
        image.RenderTransformOrigin = new Point(.5, .5);
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(layer.Scale, layer.Scale));
        group.Children.Add(new RotateTransform(layer.RotationDegrees));
        group.Children.Add(new TranslateTransform(layer.OffsetX, layer.OffsetY));
        image.RenderTransform = group;
    }
}
