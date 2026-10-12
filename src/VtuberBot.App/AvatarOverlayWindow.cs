using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VtuberBot.App;

public sealed class AvatarOverlayWindow : Window
{
    private readonly Image image = new() { Stretch = Stretch.Uniform, RenderTransformOrigin = new Point(.5,.5) };
    private readonly ScaleTransform breathing = new(1,1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private double phase;

    public AvatarOverlayWindow()
    {
        Title = "VTuberBot Avatar Overlay";
        Width = 420; Height = 520; MinWidth = 160; MinHeight = 180;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        image.RenderTransform = breathing;
        Content = image;
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Topmost = !Topmost; else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        timer.Tick += (_, _) => { phase += .035; breathing.ScaleY = 1 + Math.Sin(phase) * .008; breathing.ScaleX = 1 + Math.Sin(phase + .6) * .004; };
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    public void SetImage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
        image.Source = bitmap;
    }
}
