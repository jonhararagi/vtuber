using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace VtuberBot.App;

public partial class MainWindow
{
    private void OpenWaifuObjectEditor()
    {
        var editor = new Window
        {
            Title = "Creador de waifu PNG por objetos",
            Width = 1180,
            Height = 780,
            MinWidth = 900,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = new SolidColorBrush(Color.FromRgb(17, 23, 39))
        };

        var layers = new List<WaifuLayer>();
        var selected = -1;
        var gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        gifTimer.Tick += (_, _) =>
        {
            foreach (var layer in layers)
            {
                if (layer.AnimationFrames.Count < 2 || layer.PreviewImage is null) continue;
                layer.FrameElapsedMs += 50;
                var duration = layer.FrameDurationsMs[Math.Clamp(layer.FrameIndex, 0, layer.FrameDurationsMs.Count - 1)];
                if (layer.FrameElapsedMs < duration) continue;
                layer.FrameElapsedMs = 0;
                layer.FrameIndex = (layer.FrameIndex + 1) % layer.AnimationFrames.Count;
                layer.Source = layer.AnimationFrames[layer.FrameIndex];
                layer.PreviewImage.Source = layer.Source;
            }
        };
        // Encuadre configurable de cámara; es una guía visual guardada en el proyecto.
        double cameraX = 80, cameraY = 30, cameraWidth = 480, cameraHeight = 580;
        double obsX = 1420, obsY = 360, obsWidth = 420, obsHeight = 630;
        var cameraFrame = new System.Windows.Shapes.Rectangle
        {
            Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(20, 40, 170, 255)),
            IsHitTestVisible = false
        };
        var canvas = new Canvas { Width = 640, Height = 640, ClipToBounds = true, Background = Brushes.Transparent };
        var checker = new Grid { Width = 640, Height = 640, Background = new SolidColorBrush(Color.FromRgb(37, 45, 63)) };
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
            checker.Children.Add(new Border
            {
                Width = 40, Height = 40,
                Background = new SolidColorBrush((x + y) % 2 == 0 ? Color.FromRgb(48, 57, 76) : Color.FromRgb(37, 45, 63)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(x * 40, y * 40, 0, 0)
            });

        var preview = new Grid { Width = 640, Height = 640, ClipToBounds = true };
        preview.Children.Add(checker);
        preview.Children.Add(canvas);
        var list = new ListBox { MinWidth = 220, Height = 250, Background = new SolidColorBrush(Color.FromRgb(25, 33, 51)), Foreground = Brushes.White };
        var opacity = new Slider { Minimum = 0, Maximum = 1, Value = 1, Width = 180, IsEnabled = false };
        var scale = new Slider { Minimum = 0.1, Maximum = 3, Value = 1, Width = 180, IsEnabled = false };
        var status = new TextBlock { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
        var cameraSummary = new TextBlock { Foreground = Brushes.LightSkyBlue, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
        var obsPreview = new Canvas { Width = 220, Height = 124, Background = new SolidColorBrush(Color.FromRgb(8, 12, 20)), ClipToBounds = true };
        var obsAvatarRect = new System.Windows.Shapes.Rectangle { Fill = new SolidColorBrush(Color.FromArgb(95, 40, 170, 255)), Stroke = Brushes.DeepSkyBlue, StrokeThickness = 1 };
        obsPreview.Children.Add(obsAvatarRect);
        void RefreshCameraPreview()
        {
            Canvas.SetLeft(cameraFrame, cameraX); Canvas.SetTop(cameraFrame, cameraY);
            cameraFrame.Width = cameraWidth; cameraFrame.Height = cameraHeight;
            Canvas.SetLeft(obsAvatarRect, obsX / 1920d * obsPreview.Width);
            Canvas.SetTop(obsAvatarRect, obsY / 1080d * obsPreview.Height);
            obsAvatarRect.Width = Math.Max(1, obsWidth / 1920d * obsPreview.Width);
            obsAvatarRect.Height = Math.Max(1, obsHeight / 1080d * obsPreview.Height);
            var area = Math.Clamp(obsWidth, 0, 1920) * Math.Clamp(obsHeight, 0, 1080) / (1920d * 1080d) * 100;
            cameraSummary.Text = $"Encuadre: X {cameraX:0}, Y {cameraY:0}, {cameraWidth:0} × {cameraHeight:0} px\nOBS 1920 × 1080: X {obsX:0}, Y {obsY:0}, {obsWidth:0} × {obsHeight:0} px. Ocupa {area:0.0}% del lienzo (ancho {obsWidth / 1920d * 100:0.0}%, alto {obsHeight / 1080d * 100:0.0}%).";
        }

        void RefreshList()
        {
            var desiredSelection = selected;
            list.ItemsSource = null;
            list.ItemsSource = layers.Select((layer, index) => $"{(index == desiredSelection ? "▶ " : "")}{index + 1}. {layer.Name}").Reverse().ToArray();
            // Reassign after ItemsSource reset because SelectionChanged can temporarily set -1.
            selected = desiredSelection;
            list.SelectedIndex = selected >= 0 && selected < layers.Count ? layers.Count - 1 - selected : -1;
            RenderLayers();
        }

        void RenderLayers()
        {
            canvas.Children.Clear();
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var image = new Image
                {
                    Source = layer.Source,
                    Width = layer.Width,
                    Height = layer.Height,
                    Opacity = layer.Opacity,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = true,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(layer.Scale, layer.Scale)
                };
                Canvas.SetLeft(image, layer.X);
                Canvas.SetTop(image, layer.Y);
                var index = i;
                image.MouseLeftButtonDown += (_, e) =>
                {
                    selected = index;
                    list.SelectedIndex = layers.Count - 1 - selected;
                    image.CaptureMouse();
                    var start = e.GetPosition(canvas);
                    var originalX = layers[index].X;
                    var originalY = layers[index].Y;
                    image.MouseMove += Drag;
                    void Drag(object sender, MouseEventArgs args)
                    {
                        if (args.LeftButton != MouseButtonState.Pressed) { image.MouseMove -= Drag; return; }
                        var point = args.GetPosition(canvas);
                        layers[index].X = Math.Clamp(originalX + point.X - start.X, -640, 640);
                        layers[index].Y = Math.Clamp(originalY + point.Y - start.Y, -640, 640);
                        Canvas.SetLeft(image, layers[index].X);
                        Canvas.SetTop(image, layers[index].Y);
                    }
                    image.MouseLeftButtonUp += (_, _) => { image.ReleaseMouseCapture(); image.MouseMove -= Drag; RefreshList(); };
                    e.Handled = true;
                };
                layer.PreviewImage = image;
                canvas.Children.Add(image);
            }
            Canvas.SetLeft(cameraFrame, cameraX);
            Canvas.SetTop(cameraFrame, cameraY);
            cameraFrame.Width = cameraWidth;
            cameraFrame.Height = cameraHeight;
            canvas.Children.Add(cameraFrame);
        }

        void SelectLayer()
        {
            selected = list.SelectedIndex < 0 ? -1 : layers.Count - 1 - list.SelectedIndex;
            var active = selected >= 0 && selected < layers.Count ? layers[selected] : null;
            opacity.IsEnabled = active is not null;
            scale.IsEnabled = active is not null;
            if (active is not null) { opacity.Value = active.Opacity; scale.Value = active.Scale; }
            RenderLayers();
        }

        void AddImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Importar objeto para la waifu",
                Filter = "Imágenes compatibles|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog(editor) != true) return;
            foreach (var path in dialog.FileNames)
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var animationFrames = Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                        ? decoder.Frames.Select(frame => (BitmapSource)new WriteableBitmap(frame)).ToList()
                        : new List<BitmapSource>();
                    var bitmap = animationFrames.Count > 0
                        ? animationFrames[0]
                        : new WriteableBitmap(decoder.Frames[0]);
                    var frameDurations = animationFrames.Select(frame => ReadGifFrameDuration(decoder.Frames[animationFrames.IndexOf(frame)])).ToList();
                    if (frameDurations.Count == 0) frameDurations.Add(100);
                    layers.Add(new WaifuLayer
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Source = bitmap,
                        AnimationFrames = animationFrames,
                        FrameDurationsMs = frameDurations,
                        PixelWidth = bitmap.PixelWidth,
                        PixelHeight = bitmap.PixelHeight,
                        Width = Math.Min(420, bitmap.PixelWidth),
                        Height = Math.Min(420, bitmap.PixelHeight),
                        X = (640 - Math.Min(420, bitmap.PixelWidth)) / 2,
                        Y = (640 - Math.Min(420, bitmap.PixelHeight)) / 2,
                        OriginalPath = path
                    });
                    selected = layers.Count - 1;
                }
                catch (Exception ex) { status.Text = $"No se pudo cargar {Path.GetFileName(path)}: {ex.Message}"; }
            }
            RefreshList();
            SelectLayer();
            status.Text = "Imágenes importadas. Seleccioná una capa y arrastrala en la vista previa. Los GIF animados se reproducen en la vista previa; la exportación usa el fotograma visible.";
        }

        var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        Button ButtonFor(string text, Action action)
        {
            var button = new Button { Content = text, Margin = new Thickness(3), Padding = new Thickness(10, 7, 10, 7), MinWidth = 90 };
            button.Click += (_, _) => action();
            buttons.Children.Add(button);
            return button;
        }

        ButtonFor("＋ Importar objetos", AddImage);
        ButtonFor("Quitar fondo blanco (bordes)", () =>
        {
            if (selected < 0 || selected >= layers.Count)
            {
                status.Text = "Seleccioná primero la capa de imagen a procesar.";
                return;
            }

            try
            {
                var layer = layers[selected];
                layer.Source = RemoveWhiteBackgroundConnectedToEdges(layer.Source);
                // El recorte transforma el fotograma actual en una imagen estática; no dejar que
                // el temporizador restaure después los fotogramas GIF sin procesar.
                layer.AnimationFrames.Clear();
                layer.FrameDurationsMs.Clear();
                layer.FrameIndex = 0;
                layer.FrameElapsedMs = 0;
                layer.PixelWidth = layer.Source.PixelWidth;
                layer.PixelHeight = layer.Source.PixelHeight;
                var assetDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VTuberBot", "WaifuAssets");
                Directory.CreateDirectory(assetDirectory);
                var assetPath = Path.Combine(assetDirectory, $"cutout-{Guid.NewGuid():N}.png");
                SaveBitmapAsPng(layer.Source, assetPath);
                layer.OriginalPath = assetPath;
                RenderLayers();
                status.Text = "Fondo blanco conectado al borde eliminado. Los blancos encerrados dentro de la ilustración se conservan; revisá el contorno antes de exportar.";
            }
            catch (Exception ex)
            {
                status.Text = "No se pudo quitar el fondo: " + ex.Message;
            }
        });
        ButtonFor("↑ Subir capa", () =>
        {
            if (selected <= 0 || selected >= layers.Count) return;
            (layers[selected - 1], layers[selected]) = (layers[selected], layers[selected - 1]);
            selected--; RefreshList();
        });
        ButtonFor("↓ Bajar capa", () =>
        {
            if (selected < 0 || selected >= layers.Count - 1) return;
            (layers[selected + 1], layers[selected]) = (layers[selected], layers[selected + 1]);
            selected++; RefreshList();
        });
        ButtonFor("Eliminar", () =>
        {
            if (selected < 0 || selected >= layers.Count) return;
            layers.RemoveAt(selected); selected = Math.Min(selected, layers.Count - 1); RefreshList(); SelectLayer();
        });
        ButtonFor("Abrir proyecto", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Proyecto waifu (*.waifu.json)|*.waifu.json|JSON|*.json" };
            if (dialog.ShowDialog(editor) != true) return;
            try
            {
                var project = JsonSerializer.Deserialize<WaifuProject>(File.ReadAllText(dialog.FileName))
                    ?? throw new InvalidDataException("El proyecto está vacío o no es válido.");
                var loaded = new List<WaifuLayer>();
                foreach (var item in project.Layers)
                {
                    var resolvedPath = string.IsNullOrWhiteSpace(item.Path)
                        ? string.Empty
                        : Path.IsPathRooted(item.Path)
                            ? item.Path
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dialog.FileName)!, item.Path));
                    if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                        throw new FileNotFoundException("No se encuentra el recurso de una capa: " + item.Path);
                    using var stream = File.OpenRead(resolvedPath);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var isGif = Path.GetExtension(resolvedPath).Equals(".gif", StringComparison.OrdinalIgnoreCase);
                    var animationFrames = isGif
                        ? decoder.Frames.Select(frame => (BitmapSource)new WriteableBitmap(frame)).ToList()
                        : new List<BitmapSource>();
                    var bitmap = animationFrames.Count > 0 ? animationFrames[0] : new WriteableBitmap(decoder.Frames[0]);
                    var frameDurations = isGif
                        ? decoder.Frames.Cast<BitmapFrame>().Select(ReadGifFrameDuration).ToList()
                        : new List<int>();
                    if (frameDurations.Count == 0) frameDurations.Add(100);
                    loaded.Add(new WaifuLayer
                    {
                        Name = item.Name, Source = bitmap, PixelWidth = bitmap.PixelWidth, PixelHeight = bitmap.PixelHeight,
                        AnimationFrames = animationFrames, FrameDurationsMs = frameDurations,
                        OriginalPath = resolvedPath, X = item.X, Y = item.Y, Width = item.Width, Height = item.Height,
                        Scale = Math.Clamp(item.Scale, 0.1, 3), Opacity = Math.Clamp(item.Opacity, 0, 1)
                    });
                }
                layers.Clear();
                layers.AddRange(loaded);
                cameraX = Math.Clamp(project.CameraX, 0, 640); cameraY = Math.Clamp(project.CameraY, 0, 640);
                cameraWidth = Math.Clamp(project.CameraWidth <= 0 ? 480 : project.CameraWidth, 80, 640);
                cameraHeight = Math.Clamp(project.CameraHeight <= 0 ? 580 : project.CameraHeight, 80, 640);
                obsX = Math.Clamp(project.ObsX, 0, 1920); obsY = Math.Clamp(project.ObsY, 0, 1080);
                obsWidth = Math.Clamp(project.ObsWidth <= 0 ? 420 : project.ObsWidth, 50, 1920);
                obsHeight = Math.Clamp(project.ObsHeight <= 0 ? 630 : project.ObsHeight, 50, 1080);
                cropX.Value = cameraX; cropY.Value = cameraY; cropW.Value = cameraWidth; cropH.Value = cameraHeight;
                RefreshCameraPreview();
                selected = layers.Count - 1;
                RefreshList();
                SelectLayer();
                status.Text = "Proyecto abierto. Se recuperaron las capas y sus transformaciones.";
            }
            catch (Exception ex) { status.Text = "No se pudo abrir el proyecto: " + ex.Message; }
        });
        ButtonFor("Guardar proyecto", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Proyecto waifu (*.waifu.json)|*.waifu.json", FileName = "mi-waifu.waifu.json" };
            if (dialog.ShowDialog(editor) != true) return;
            try
            {
                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))
                    ?? throw new InvalidOperationException("No se pudo determinar la carpeta del proyecto.");
                var projectName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dialog.FileName));
                var assetFolderName = (string.IsNullOrWhiteSpace(projectName) ? "waifu" : projectName) + "_assets";
                var assetDirectory = Path.Combine(projectDirectory, assetFolderName);
                Directory.CreateDirectory(assetDirectory);
                var savedLayers = new List<WaifuLayerData>();
                for (var index = 0; index < layers.Count; index++)
                {
                    var layer = layers[index];
                    var keepAnimation = layer.AnimationFrames.Count > 1 &&
                        Path.GetExtension(layer.OriginalPath).Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(layer.OriginalPath);
                    var extension = keepAnimation ? ".gif" : ".png";
                    var assetName = $"{index + 1:D3}-{MakeSafeFileName(layer.Name)}{extension}";
                    var assetPath = Path.Combine(assetDirectory, assetName);
                    if (keepAnimation) File.Copy(layer.OriginalPath, assetPath, true);
                    else SaveBitmapAsPng(layer.Source, assetPath);
                    layer.OriginalPath = assetPath;
                    savedLayers.Add(new WaifuLayerData
                    {
                        Name = layer.Name, Path = Path.Combine(assetFolderName, assetName), X = layer.X, Y = layer.Y,
                        Width = layer.Width, Height = layer.Height, Scale = layer.Scale, Opacity = layer.Opacity
                    });
                }
                var project = new WaifuProject { Width = 640, Height = 640, CameraX = cameraX, CameraY = cameraY, CameraWidth = cameraWidth, CameraHeight = cameraHeight, ObsX = obsX, ObsY = obsY, ObsWidth = obsWidth, ObsHeight = obsHeight, Layers = savedLayers };
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
                status.Text = "Proyecto guardado con una copia PNG de cada capa en una carpeta de recursos junto al JSON. Podés mover el proyecto completo sin depender de las rutas originales.";
            }
            catch (Exception ex) { status.Text = "No se pudo guardar el proyecto: " + ex.Message; }
        });
        ButtonFor("Exportar encuadre de cámara", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Recorte PNG transparente (*.png)|*.png", FileName = "avatar-encuadre-obs.png" };
            if (dialog.ShowDialog(editor) != true) return;
            try
            {
                var width = Math.Clamp((int)Math.Round(cameraWidth), 1, 640);
                var height = Math.Clamp((int)Math.Round(cameraHeight), 1, 640);
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
                    dc.PushTransform(new TranslateTransform(-cameraX, -cameraY));
                    foreach (var layer in layers)
                    {
                        var rect = new Rect(layer.X + layer.Width * (1 - layer.Scale) / 2, layer.Y + layer.Height * (1 - layer.Scale) / 2, layer.Width * layer.Scale, layer.Height * layer.Scale);
                        dc.PushOpacity(layer.Opacity); dc.DrawImage(layer.Source, rect); dc.Pop();
                    }
                    dc.Pop(); dc.Pop();
                }
                var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                render.Render(visual); SaveBitmapAsPng(render, dialog.FileName);
                status.Text = $"Encuadre exportado en PNG transparente ({width} × {height}). Importalo como fuente de imagen en OBS; la previsualización de posición no mueve fuentes dentro de OBS automáticamente.";
            }
            catch (Exception ex) { status.Text = "No se pudo exportar el encuadre: " + ex.Message; }
        });
        ButtonFor("Exportar PNG", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Imagen PNG transparente (*.png)|*.png", FileName = "mi-waifu.png" };
            if (dialog.ShowDialog(editor) != true) return;
            try
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    foreach (var layer in layers)
                    {
                        var bitmap = layer.Source;
                        var rect = new Rect(layer.X + layer.Width * (1 - layer.Scale) / 2, layer.Y + layer.Height * (1 - layer.Scale) / 2, layer.Width * layer.Scale, layer.Height * layer.Scale);
                        dc.PushOpacity(layer.Opacity);
                        dc.DrawImage(bitmap, rect);
                        dc.Pop();
                    }
                }
                var render = new RenderTargetBitmap(640, 640, 96, 96, PixelFormats.Pbgra32);
                render.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(render));
                using var output = File.Create(dialog.FileName);
                encoder.Save(output);
                status.Text = "PNG exportado con transparencia fuera de los objetos.";
            }
            catch (Exception ex) { status.Text = "No se pudo exportar: " + ex.Message; }
        });

        opacity.ValueChanged += (_, _) =>
        {
            if (selected < 0 || selected >= layers.Count) return;
            layers[selected].Opacity = opacity.Value; RenderLayers();
        };
        scale.ValueChanged += (_, _) =>
        {
            if (selected < 0 || selected >= layers.Count) return;
            layers[selected].Scale = scale.Value; RenderLayers();
        };
        list.SelectionChanged += (_, _) => SelectLayer();

        var controls = new StackPanel { Margin = new Thickness(12) };
        controls.Children.Add(new TextBlock { Text = "Capas / objetos (arriba = primer plano)", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4) });
        controls.Children.Add(list);

        var catalogStack = new StackPanel();
        void AddCatalogCategory(string category, string title, string[] names)
        {
            catalogStack.Children.Add(new TextBlock
            {
                Text = title, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 9, 4, 3)
            });
            var items = new WrapPanel();
            for (var itemIndex = 0; itemIndex < names.Length; itemIndex++)
            {
                var index = itemIndex;
                var button = new Button
                {
                    Content = names[index], Width = 104, MinHeight = 30, Margin = new Thickness(2),
                    Padding = new Thickness(3), ToolTip = "Añadir " + names[index] + " como capa editable"
                };
                button.Click += (_, _) => AddCatalogLayer(layers, RefreshList, SelectLayer, value => selected = value, value => status.Text = value, category, index, names[index]);
                items.Children.Add(button);
            }
            catalogStack.Children.Add(items);
        }
        AddCatalogCategory("head", "Accesorios de cabeza · 10", ["Orejas de gato", "Orejas de conejo", "Moño doble", "Corona", "Sombrero bruja", "Halo", "Cuernos", "Flor lateral", "Auriculares", "Hebilla estrella"]);
        AddCatalogCategory("eyes", "Ojos anime · 9", ["Marrones", "Azules", "Verdes", "Violetas", "Rosa", "Dorado", "Adormilados", "Estrellados", "Heterocromía"]);
        AddCatalogCategory("mouth", "Bocas · 7", ["Sonrisa", "Abierta", "Gatita", "Pequeña", "Sorprendida", "Puchero", "Risa"]);
        AddCatalogCategory("effect", "Efectos · 10", ["Corazones", "Estrellas", "Brillos", "Rubor", "Lágrimas", "Enojo", "Aura rosa", "Aura azul", "Notas musicales", "Confeti"]);
        AddCatalogCategory("background", "Fondos HD · 4", ["Atardecer", "Noche estelar", "Café acogedor", "Cielo pastel"]);
        var catalogScroll = new ScrollViewer { Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = catalogStack, Margin = new Thickness(0, 4, 0, 8) };
        controls.Children.Add(new TextBlock { Text = "Catálogo local de piezas vectoriales", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 10, 4, 2) });
        controls.Children.Add(new TextBlock { Text = "Piezas originales de muestra generadas localmente; cada una se agrega como capa que podés mover, escalar y ordenar.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 4) });
        controls.Children.Add(catalogScroll);
        controls.Children.Add(new TextBlock { Text = "Encuadre de cámara / recorte visible", Foreground = Brushes.DeepSkyBlue, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 12, 4, 3) });
        controls.Children.Add(new TextBlock { Text = "El marco azul indica qué parte del personaje mostrar. Elegí rostro, busto o cuerpo completo y ajustá el recorte.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
        Slider cropX = null!, cropY = null!, cropW = null!, cropH = null!;
        var framingPreset = new ComboBox { Margin = new Thickness(4), ItemsSource = new[] { "Personalizado", "Rostro", "Busto", "Cuerpo completo" }, SelectedIndex = 2 };
        framingPreset.SelectionChanged += (_, _) =>
        {
            switch (framingPreset.SelectedItem as string)
            {
                case "Rostro": cameraWidth = 280; cameraHeight = 300; cameraX = 180; cameraY = 25; break;
                case "Busto": cameraWidth = 420; cameraHeight = 430; cameraX = 110; cameraY = 30; break;
                case "Cuerpo completo": cameraWidth = 600; cameraHeight = 620; cameraX = 20; cameraY = 10; break;
                default: return;
            }
            cropX.Value = cameraX; cropY.Value = cameraY; cropW.Value = cameraWidth; cropH.Value = cameraHeight;
            RefreshCameraPreview(); RenderLayers();
        };
        controls.Children.Add(framingPreset);
        Slider CropSlider(string title, double min, double max, double value, Action<double> set)
        {
            controls.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, Margin = new Thickness(4, 5, 4, 0) });
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 210, Margin = new Thickness(4) };
            slider.ValueChanged += (_, _) => { set(slider.Value); RefreshCameraPreview(); RenderLayers(); };
            controls.Children.Add(slider); return slider;
        }
        cropX = CropSlider("Recorte X", 0, 640, cameraX, v => cameraX = v);
        cropY = CropSlider("Recorte Y", 0, 640, cameraY, v => cameraY = v);
        cropW = CropSlider("Ancho del recorte", 80, 640, cameraWidth, v => cameraWidth = v);
        cropH = CropSlider("Alto del recorte", 80, 640, cameraHeight, v => cameraHeight = v);
        controls.Children.Add(new TextBlock { Text = "Lienzo completo de OBS (1920 × 1080)", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 10, 4, 3) });
        controls.Children.Add(obsPreview);
        controls.Children.Add(cameraSummary);
        Slider ObsSlider(string title, double min, double max, double value, Action<double> set)
        {
            controls.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, Margin = new Thickness(4, 4, 4, 0) });
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 210, Margin = new Thickness(4) };
            slider.ValueChanged += (_, _) => { set(slider.Value); RefreshCameraPreview(); };
            controls.Children.Add(slider); return slider;
        }
        ObsSlider("Posición en OBS X", 0, 1920, obsX, v => obsX = v);
        ObsSlider("Posición en OBS Y", 0, 1080, obsY, v => obsY = v);
        ObsSlider("Ancho en OBS", 50, 1920, obsWidth, v => obsWidth = v);
        ObsSlider("Alto en OBS", 50, 1080, obsHeight, v => obsHeight = v);
        RefreshCameraPreview();
        controls.Children.Add(new TextBlock { Text = "Opacidad", Foreground = Brushes.White, Margin = new Thickness(4, 10, 4, 0) });
        controls.Children.Add(opacity);
        controls.Children.Add(new TextBlock { Text = "Escala", Foreground = Brushes.White, Margin = new Thickness(4, 10, 4, 0) });
        controls.Children.Add(scale);
        controls.Children.Add(status);
        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Top);
        layout.Children.Add(buttons);
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        body.Children.Add(new Border { Child = preview, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(Color.FromRgb(70, 82, 110)), BorderThickness = new Thickness(1) });
        var controlsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = controls };
        Grid.SetColumn(controlsScroll, 1); body.Children.Add(controlsScroll);
        layout.Children.Add(body);
        editor.Content = layout;
        gifTimer.Start();
        editor.Closed += (_, _) => gifTimer.Stop();
        editor.ShowDialog();
    }


    private void AddCatalogLayer(List<WaifuLayer> layers, Action refreshList, Action selectLayer, Action<int> setSelected, Action<string> setStatus, string category, int index, string name)
    {
        // El catálogo se dibuja con primitivas vectoriales locales: no requiere descargas,
        // cuentas ni API externa. Los fondos se crean en resolución 1280x720.
        var source = CreateWaifuCatalogAsset(category, index);
        var isBackground = category == "background";
        var width = isBackground ? 640d : 256d;
        var height = isBackground ? 360d : 256d;
        var layer = new WaifuLayer
        {
            Name = name,
            Source = source,
            PixelWidth = source.PixelWidth,
            PixelHeight = source.PixelHeight,
            Width = width,
            Height = height,
            X = isBackground ? 0 : (640 - width) / 2,
            Y = isBackground ? 140 : (640 - height) / 2,
            OriginalPath = SaveCatalogAsset(source, name)
        };
        // New layers are inserted in front so the user sees the result immediately.
        if (isBackground)
        {
            // El índice 0 se dibuja primero, por detrás de los demás objetos.
            layers.Insert(0, layer);
            setSelected(0);
        }
        else
        {
            layers.Add(layer);
            setSelected(layers.Count - 1);
        }
        refreshList();
        selectLayer();
        setStatus($"Se añadió «{name}» como una capa independiente. El catálogo usa arte vectorial de muestra, editable por posición, escala, opacidad y orden.");
    }

    private static string SaveCatalogAsset(BitmapSource source, string name)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VTuberBot", "WaifuAssets");
        Directory.CreateDirectory(directory);
        var safe = MakeSafeFileName(name);
        var path = Path.Combine(directory, $"catalog-{safe}-{Guid.NewGuid():N}.png");
        SaveBitmapAsPng(source, path);
        return path;
    }

    private static BitmapSource CreateWaifuCatalogAsset(string category, int index)
    {
        var isBackground = category == "background";
        var width = isBackground ? 1280 : 256;
        var height = isBackground ? 720 : 256;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, width, height);
            if (isBackground)
            {
                var palettes = new[]
                {
                    new[] { Color.FromRgb(255, 169, 126), Color.FromRgb(113, 87, 159) },
                    new[] { Color.FromRgb(12, 22, 56), Color.FromRgb(65, 76, 145) },
                    new[] { Color.FromRgb(95, 64, 68), Color.FromRgb(241, 189, 137) },
                    new[] { Color.FromRgb(250, 211, 229), Color.FromRgb(153, 208, 239) }
                };
                var colors = palettes[index % palettes.Length];
                dc.DrawRectangle(new LinearGradientBrush(colors[0], colors[1], 90), null, bounds);
                if (index == 0)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 231, 177)), null, new Point(1010, 180), 100, 100);
                    for (var i = 0; i < 9; i++)
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 255, 230, 211)), null, new Point(90 + i * 145, 590 - (i % 3) * 35), 100, 40);
                }
                else if (index == 1)
                {
                    var random = new Random(713);
                    for (var i = 0; i < 100; i++)
                    {
                        var x = random.Next(20, width - 20); var y = random.Next(15, 490);
                        dc.DrawEllipse(Brushes.White, null, new Point(x, y), random.Next(2, 5), random.Next(2, 5));
                    }
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 244, 207)), null, new Point(1030, 150), 75, 75);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(49, 59, 113)), null, new Point(1060, 125), 72, 72);
                }
                else if (index == 2)
                {
                    for (var i = 0; i < 12; i++)
                    {
                        var x = i * 115;
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(190, 59, 46, 66)), null, new Rect(x, 350 + (i % 4) * 25, 85, 370), 8, 8);
                        for (var row = 0; row < 5; row++)
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 255, 218, 158)), null, new Rect(x + 14, 375 + (i % 4) * 25 + row * 42, 13, 20));
                    }
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var x = 70 + i * 160;
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), null, new Point(x, 150 + (i % 2) * 70), 75, 38);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), null, new Point(x + 40, 135 + (i % 2) * 70), 65, 48);
                    }
                }
            }
            else if (category == "eyes")
            {
                var irisColors = new[] { "#865334", "#3298D4", "#3BAA78", "#9560D5", "#EA719F", "#D6A52D", "#805E9E", "#4F92C6", "#865334" };
                var irisBrush = (Brush)new BrushConverter().ConvertFromString(irisColors[index % irisColors.Length])!;
                for (var side = 0; side < 2; side++)
                {
                    var x = side == 0 ? 72d : 184d;
                    if (index == 6) { dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(65, 55, 84)), null, new Point(x, 128), 35, 19); continue; }
                    dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(64, 45, 63)), 5), new Point(x, 126), 39, index == 7 ? 33 : 42);
                    dc.DrawEllipse(irisBrush, null, new Point(x, 132), 27, 31);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(30, 24, 43)), null, new Point(x, 136), index == 7 ? 11 : 15, 21);
                    dc.DrawEllipse(Brushes.White, null, new Point(x - 9, 119), 8, 10);
                    dc.DrawEllipse(Brushes.White, null, new Point(x + 10, 143), 4, 5);
                }
                if (index == 8)
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(85, 164, 219)), null, new Point(184, 132), 27, 31);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(70, 42, 51)), 6), new Point(32, 80), new Point(105, 73));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(70, 42, 51)), 6), new Point(151, 73), new Point(224, 80));
            }
            else if (category == "mouth")
            {
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(119, 43, 65)), 7);
                switch (index)
                {
                    case 0: dc.DrawGeometry(null, pen, MakePath(new Point(77, 124), new Point(105, 153), new Point(151, 153), new Point(179, 124))); break;
                    case 1: dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(139, 42, 72)), new Pen(new SolidColorBrush(Color.FromRgb(93, 32, 53)), 4), new Point(128, 139), 25, 34); dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(245, 132, 160)), null, new Point(128, 158), 14, 7); break;
                    case 2: dc.DrawGeometry(null, pen, MakePath(new Point(96, 134), new Point(112, 122), new Point(128, 134), new Point(144, 122), new Point(160, 134))); break;
                    case 3: dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(139, 42, 72)), null, new Point(128, 140), 10, 7); break;
                    case 4: dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(139, 42, 72)), new Pen(new SolidColorBrush(Color.FromRgb(93, 32, 53)), 4), new Point(128, 142), 24, 29); break;
                    case 5: dc.DrawGeometry(null, pen, MakePath(new Point(90, 143), new Point(108, 132), new Point(128, 142), new Point(148, 132), new Point(166, 143))); break;
                    default: dc.DrawGeometry(null, pen, MakePath(new Point(83, 121), new Point(101, 146), new Point(128, 155), new Point(155, 146), new Point(173, 121))); break;
                }
            }
            else if (category == "head")
            {
                var main = new SolidColorBrush(new[] { Color.FromRgb(244, 112, 164), Color.FromRgb(255, 195, 220), Color.FromRgb(205, 142, 255), Color.FromRgb(244, 197, 94) }[index % 4]);
                var dark = new SolidColorBrush(Color.FromRgb(80, 47, 91));
                switch (index)
                {
                    case 0:
                        dc.DrawGeometry(main, new Pen(dark, 5), MakePath(new Point(38, 95), new Point(51, 16), new Point(105, 71)));
                        dc.DrawGeometry(main, new Pen(dark, 5), MakePath(new Point(151, 71), new Point(205, 16), new Point(218, 95)));
                        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 228, 166)), null, new Point(52, 88), 10, 8); break;
                    case 1:
                        dc.DrawRoundedRectangle(main, new Pen(dark, 4), new Rect(44, 18, 42, 110), 22, 22);
                        dc.DrawRoundedRectangle(main, new Pen(dark, 4), new Rect(170, 18, 42, 110), 22, 22); break;
                    case 2:
                        dc.DrawEllipse(main, new Pen(dark, 4), new Point(98, 90), 42, 30);
                        dc.DrawEllipse(main, new Pen(dark, 4), new Point(158, 90), 42, 30);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 231, 165)), null, new Point(128, 90), 18, 18); break;
                    case 3:
                        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 218, 104)), new Pen(dark, 4), MakePath(new Point(47, 100), new Point(56, 42), new Point(78, 67), new Point(99, 35), new Point(121, 68), new Point(145, 36), new Point(169, 67), new Point(194, 42), new Point(208, 100))); break;
                    case 4:
                        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(70, 55, 99)), new Pen(dark, 5), MakePath(new Point(30, 100), new Point(75, 22), new Point(184, 22), new Point(226, 100)));
                        dc.DrawEllipse(main, null, new Point(128, 96), 98, 15); break;
                    case 5:
                        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(255, 228, 139)), 12), new Point(128, 66), 48, 30);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(65, 255, 239, 166)), null, new Point(128, 66), 64, 43); break;
                    case 6:
                        dc.DrawGeometry(main, new Pen(dark, 4), MakePath(new Point(52, 90), new Point(45, 37), new Point(87, 62), new Point(104, 91)));
                        dc.DrawGeometry(main, new Pen(dark, 4), MakePath(new Point(152, 91), new Point(169, 62), new Point(211, 37), new Point(204, 90))); break;
                    case 7:
                        dc.DrawEllipse(main, new Pen(dark, 4), new Point(70, 76), 30, 30);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 223, 102)), null, new Point(70, 76), 12, 12); break;
                    case 8:
                        dc.DrawEllipse(null, new Pen(dark, 9), new Point(78, 85), 43, 43);
                        dc.DrawEllipse(null, new Pen(dark, 9), new Point(178, 85), 43, 43);
                        dc.DrawLine(new Pen(dark, 10), new Point(119, 85), new Point(137, 85));
                        dc.DrawLine(new Pen(dark, 7), new Point(48, 55), new Point(102, 54));
                        dc.DrawLine(new Pen(dark, 7), new Point(154, 54), new Point(208, 55)); break;
                    default:
                        dc.DrawEllipse(main, new Pen(dark, 3), new Point(186, 64), 20, 20);
                        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 223, 102)), null, MakePath(new Point(186, 35), new Point(192, 55), new Point(214, 56), new Point(197, 69), new Point(203, 91), new Point(186, 78), new Point(168, 91), new Point(175, 69), new Point(158, 56), new Point(180, 55))); break;
                }
            }
            else if (category == "effect")
            {
                var colors = new[] { Color.FromRgb(255, 102, 158), Color.FromRgb(255, 217, 95), Color.FromRgb(151, 220, 255), Color.FromRgb(255, 130, 170) };
                var brush = new SolidColorBrush(colors[index % colors.Length]);
                if (index == 3)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 97, 144)), null, new Point(78, 138), 34, 18);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 255, 97, 144)), null, new Point(178, 138), 34, 18);
                }
                else if (index == 4)
                {
                    dc.DrawGeometry(brush, null, MakePath(new Point(75, 105), new Point(100, 138), new Point(82, 163)));
                    dc.DrawGeometry(brush, null, MakePath(new Point(175, 105), new Point(150, 138), new Point(168, 163)));
                }
                else if (index == 5)
                {
                    dc.DrawLine(new Pen(brush, 12), new Point(50, 60), new Point(92, 82));
                    dc.DrawLine(new Pen(brush, 12), new Point(164, 82), new Point(206, 60));
                    dc.DrawLine(new Pen(brush, 12), new Point(40, 86), new Point(78, 99));
                    dc.DrawLine(new Pen(brush, 12), new Point(178, 99), new Point(216, 86));
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = (Math.PI * 2 * i / 8) + index * .22;
                        var x = 128 + Math.Cos(angle) * (index is 6 or 7 ? 78 : 62);
                        var y = 128 + Math.Sin(angle) * (index is 6 or 7 ? 78 : 62);
                        if (index == 0 || index == 6 || index == 7)
                        {
                            var heart = MakePath(new Point(x, y - 14), new Point(x - 13, y - 25), new Point(x - 24, y - 13), new Point(x, y + 20), new Point(x + 24, y - 13), new Point(x + 13, y - 25));
                            dc.DrawGeometry(brush, null, heart);
                        }
                        else
                        {
                            dc.DrawGeometry(brush, null, MakePath(new Point(x, y - 17), new Point(x + 5, y - 5), new Point(x + 17, y), new Point(x + 5, y + 5), new Point(x, y + 17), new Point(x - 5, y + 5), new Point(x - 17, y), new Point(x - 5, y - 5)));
                        }
                    }
                }
            }
        }
        var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze();
        return render;
    }

    private static StreamGeometry MakePath(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (points.Length < 2) return geometry;
            context.BeginFigure(points[0], true, true);
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static int ReadGifFrameDuration(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery("/grctlext/Delay"))
            {
                var delay = metadata.GetQuery("/grctlext/Delay");
                if (delay is ushort hundredths && hundredths > 0)
                    return Math.Clamp(hundredths * 10, 20, 2000);
                if (delay is short signedHundredths && signedHundredths > 0)
                    return Math.Clamp(signedHundredths * 10, 20, 2000);
            }
        }
        catch (Exception) { /* Algunos GIF no incluyen metadatos de duración. */ }
        return 100;
    }

    private static BitmapSource RemoveWhiteBackgroundConnectedToEdges(BitmapSource source)
    {
        const byte threshold = 242;
        const int maxPixels = 20_000_000;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var pixelCount = checked(width * height);
        if (pixelCount > maxPixels)
            throw new InvalidOperationException($"La imagen tiene {pixelCount:N0} píxeles. Para evitar un uso excesivo de memoria, el límite de esta operación es {maxPixels:N0} píxeles.");

        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);
        // Reutilizamos el canal alfa para marcar los píxeles visitados y evitar una segunda
        // matriz booleana. Solo se eliminan blancos casi puros conectados al borde.
        var queue = new int[pixelCount];
        var head = 0;
        var tail = 0;

        void EnqueueIfBackground(int x, int y)
        {
            var index = y * width + x;
            var offset = index * 4;
            if (pixels[offset + 3] == 0 ||
                pixels[offset] < threshold || pixels[offset + 1] < threshold || pixels[offset + 2] < threshold)
                return;
            pixels[offset + 3] = 0;
            queue[tail++] = index;
        }

        for (var x = 0; x < width; x++)
        {
            EnqueueIfBackground(x, 0);
            if (height > 1) EnqueueIfBackground(x, height - 1);
        }
        for (var y = 1; y < height - 1; y++)
        {
            EnqueueIfBackground(0, y);
            if (width > 1) EnqueueIfBackground(width - 1, y);
        }

        while (head < tail)
        {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;
            if (x > 0) EnqueueIfBackground(x - 1, y);
            if (x + 1 < width) EnqueueIfBackground(x + 1, y);
            if (y > 0) EnqueueIfBackground(x, y - 1);
            if (y + 1 < height) EnqueueIfBackground(x, y + 1);
        }

        return BitmapSource.Create(width, height, converted.DpiX, converted.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
    }

    private static void SaveBitmapAsPng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "capa" : safe;
    }

    private sealed class WaifuLayer
    {
        public required string Name { get; init; }
        public required BitmapSource Source { get; set; }
        public required int PixelWidth { get; set; }
        public required int PixelHeight { get; set; }
        public required string OriginalPath { get; set; }
        public List<BitmapSource> AnimationFrames { get; set; } = [];
        public List<int> FrameDurationsMs { get; set; } = [];
        public int FrameIndex { get; set; }
        public int FrameElapsedMs { get; set; }
        public Image? PreviewImage { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Scale { get; set; } = 1;
        public double Opacity { get; set; } = 1;
    }

    private sealed class WaifuProject
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double CameraX { get; set; } = 80;
        public double CameraY { get; set; } = 30;
        public double CameraWidth { get; set; } = 480;
        public double CameraHeight { get; set; } = 580;
        public double ObsX { get; set; } = 1420;
        public double ObsY { get; set; } = 360;
        public double ObsWidth { get; set; } = 420;
        public double ObsHeight { get; set; } = 630;
        public List<WaifuLayerData> Layers { get; set; } = [];
    }

    private sealed class WaifuLayerData
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Scale { get; set; }
        public double Opacity { get; set; }
    }
}
