using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VtuberBot.Core;

namespace VtuberBot.App;

public sealed class AvatarOverlayWindow : Window
{
    private readonly Grid layerCanvas = new();
    private readonly Border alertCard = new() { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12), Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromArgb(230, 16, 21, 37)), CornerRadius = new CornerRadius(12), MaxWidth = 390 };
    private readonly TextBlock alertText = new() { Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly ScaleTransform breathing = new(1,1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private double phase;
    private string state = "Idle";
    private DateTimeOffset blinkUntil;
    private DateTimeOffset alertUntil;
    public bool ReduceMotion { get; set; }

    public AvatarOverlayWindow()
    {
        Title = "VTuberBot Avatar Overlay";
        Width = 420; Height = 520; MinWidth = 160; MinHeight = 180;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        layerCanvas.RenderTransformOrigin = new Point(.5,.5);
        layerCanvas.RenderTransform = breathing;
        alertCard.Child = alertText;
        layerCanvas.Children.Add(alertCard);
        Content = layerCanvas;
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Topmost = !Topmost; else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        timer.Tick += (_, _) =>
        {
            if (DateTimeOffset.UtcNow < blinkUntil) layerCanvas.Opacity = .25;
            else layerCanvas.Opacity = 1;
            alertCard.Visibility = DateTimeOffset.UtcNow < alertUntil ? Visibility.Visible : Visibility.Collapsed;
            if (ReduceMotion) { breathing.ScaleX = 1; breathing.ScaleY = 1; return; }
            phase += state == "Talking" ? .12 : .035;
            breathing.ScaleY = state switch { "Talking" => 1 + Math.Sin(phase) * .012, "Happy" => 1.008, "Surprised" => 1.025, _ => 1 + Math.Sin(phase) * .008 };
            breathing.ScaleX = state switch { "Happy" => 1.008, "Surprised" => 1.025, _ => 1 + Math.Sin(phase + .6) * .004 };
        };
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    public void SetImage(string path) => SetLayers([path]);

    public void SetState(string nextState)
    {
        if (string.Equals(nextState, "Blink", StringComparison.OrdinalIgnoreCase)) { blinkUntil = DateTimeOffset.UtcNow.AddMilliseconds(160); return; }
        state = nextState is "Talking" or "Happy" or "Surprised" ? nextState : "Idle";
    }

    public void SetLayers(IEnumerable<string> paths)
    {
        layerCanvas.Children.Clear();
        foreach (var path in paths.Where(File.Exists)) AddLayerImage(path, null);
        layerCanvas.Children.Add(alertCard);
    }

    public void SetAvatar(string basePath, IEnumerable<AvatarLayer> layers)
    {
        layerCanvas.Children.Clear();
        if (File.Exists(basePath)) AddLayerImage(basePath, null);
        foreach (var layer in layers.Where(layer => layer.Visible && File.Exists(layer.Path)).OrderBy(layer => layer.SortOrder))
            AddLayerImage(layer.Path, layer);
        layerCanvas.Children.Add(alertCard);
    }

    private void AddLayerImage(string path, AvatarLayer? layer)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.DecodePixelWidth = 1000;
        bitmap.EndInit(); bitmap.Freeze();
        var image = new Image
        {
            Source = bitmap, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };
        if (layer is not null)
        {
            image.RenderTransformOrigin = new Point(.5, .5);
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(layer.Scale, layer.Scale));
            transform.Children.Add(new RotateTransform(layer.RotationDegrees));
            transform.Children.Add(new TranslateTransform(layer.OffsetX, layer.OffsetY));
            image.RenderTransform = transform;
        }
        layerCanvas.Children.Add(image);
    }

    public void ShowAlert(string text)
    {
        alertText.Text = text;
        alertUntil = DateTimeOffset.UtcNow.AddSeconds(6);
        alertCard.Visibility = Visibility.Visible;
    }
}
