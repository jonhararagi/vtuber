using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VtuberBot.App;

public sealed class AvatarOverlayWindow : Window
{
    private readonly Grid layerCanvas = new();
    private readonly ScaleTransform breathing = new(1,1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private double phase;
    private string state = "Idle";
    private DateTimeOffset blinkUntil;
    public bool ReduceMotion { get; set; }

    public AvatarOverlayWindow()
    {
        Title = "VTuberBot Avatar Overlay";
        Width = 420; Height = 520; MinWidth = 160; MinHeight = 180;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        layerCanvas.RenderTransformOrigin = new Point(.5,.5);
        layerCanvas.RenderTransform = breathing;
        Content = layerCanvas;
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Topmost = !Topmost; else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        timer.Tick += (_, _) =>
        {
            if (DateTimeOffset.UtcNow < blinkUntil) layerCanvas.Opacity = .25;
            else layerCanvas.Opacity = 1;
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
        foreach (var path in paths.Where(File.Exists))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.DecodePixelWidth = 1000; bitmap.EndInit(); bitmap.Freeze();
            layerCanvas.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false });
        }
    }
}
