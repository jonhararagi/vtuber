using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        void RefreshList()
        {
            list.ItemsSource = null;
            list.ItemsSource = layers.Select((layer, index) => $"{(index == selected ? "▶ " : "")}{index + 1}. {layer.Name}").Reverse().ToArray();
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
                canvas.Children.Add(image);
            }
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
                    var frame = decoder.Frames[0];
                    var bitmap = new WriteableBitmap(frame);
                    layers.Add(new WaifuLayer
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Source = bitmap,
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
            status.Text = "Imágenes importadas. Seleccioná una capa y arrastrala en la vista previa. Los GIF se importan como fotograma estático.";
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
                    var bitmap = new WriteableBitmap(decoder.Frames[0]);
                    loaded.Add(new WaifuLayer
                    {
                        Name = item.Name, Source = bitmap, PixelWidth = bitmap.PixelWidth, PixelHeight = bitmap.PixelHeight,
                        OriginalPath = resolvedPath, X = item.X, Y = item.Y, Width = item.Width, Height = item.Height,
                        Scale = Math.Clamp(item.Scale, 0.1, 3), Opacity = Math.Clamp(item.Opacity, 0, 1)
                    });
                }
                layers.Clear();
                layers.AddRange(loaded);
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
                    var assetName = $"{index + 1:D3}-{MakeSafeFileName(layer.Name)}.png";
                    var assetPath = Path.Combine(assetDirectory, assetName);
                    SaveBitmapAsPng(layer.Source, assetPath);
                    layer.OriginalPath = assetPath;
                    savedLayers.Add(new WaifuLayerData
                    {
                        Name = layer.Name, Path = Path.Combine(assetFolderName, assetName), X = layer.X, Y = layer.Y,
                        Width = layer.Width, Height = layer.Height, Scale = layer.Scale, Opacity = layer.Opacity
                    });
                }
                var project = new WaifuProject { Width = 640, Height = 640, Layers = savedLayers };
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
                status.Text = "Proyecto guardado con una copia PNG de cada capa en una carpeta de recursos junto al JSON. Podés mover el proyecto completo sin depender de las rutas originales.";
            }
            catch (Exception ex) { status.Text = "No se pudo guardar el proyecto: " + ex.Message; }
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
        Grid.SetColumn(controls, 1); body.Children.Add(controls);
        layout.Children.Add(body);
        editor.Content = layout;
        editor.ShowDialog();
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
