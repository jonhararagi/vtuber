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
    private readonly List<(TranslateTransform Transform, double Factor)> parallaxTransforms = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private bool parallaxEnabled = true;
    private double targetParallaxX;
    private double targetParallaxY;
    private double currentParallaxX;
    private double currentParallaxY;
    private double parallaxStrength = AvatarParallaxPolicy.MaximumOffsetPixels;
    private double phase;
    private string state = "Idle";
    private string? transientState;
    private string? baseImagePath;
    private IReadOnlyList<AvatarLayer> avatarLayers = Array.Empty<AvatarLayer>();
    private IReadOnlyDictionary<string, string> expressionImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset transientUntil;
    private DateTimeOffset nextAutomaticBlinkAt = DateTimeOffset.UtcNow.AddSeconds(Random.Shared.NextDouble() * 3 + 3);
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
        MouseMove += (_, e) =>
        {
            if (!parallaxEnabled || ReduceMotion)
                return;

            var position = e.GetPosition(this);
            targetParallaxX = AvatarParallaxPolicy.NormalizePointer(position.X, ActualWidth);
            targetParallaxY = AvatarParallaxPolicy.NormalizePointer(position.Y, ActualHeight);
        };
        MouseLeave += (_, _) =>
        {
            targetParallaxX = 0;
            targetParallaxY = 0;
        };
        timer.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (transientState is not null && now >= transientUntil)
            {
                transientState = null;
                RenderAvatar();
            }

            if (!ReduceMotion && IsVisible && transientState is null && now >= nextAutomaticBlinkAt)
            {
                // Blink is a facial sprite transition, not an opacity pulse over the entire avatar.
                if (expressionImages.TryGetValue("Blink", out var blinkPath) && File.Exists(blinkPath))
                {
                    transientState = "Blink";
                    transientUntil = now.AddMilliseconds(150);
                    nextAutomaticBlinkAt = now.AddSeconds(Random.Shared.NextDouble() * 4 + 3.5);
                    RenderAvatar();
                }
                else
                {
                    // Without a configured eyelid/closed-eye sprite, don't fake a blink by fading the whole character.
                    nextAutomaticBlinkAt = now.AddSeconds(Random.Shared.NextDouble() * 4 + 3.5);
                }
            }

            layerCanvas.Opacity = 1;
            alertCard.Visibility = now < alertUntil ? Visibility.Visible : Visibility.Collapsed;
            UpdateParallax();
            if (ReduceMotion) { breathing.ScaleX = 1; breathing.ScaleY = 1; return; }
            phase += state == "Talking" ? .12 : .035;
            breathing.ScaleY = state switch { "Talking" => 1 + Math.Sin(phase) * .012, "Happy" => 1.008, "Surprised" => 1.025, _ => 1 + Math.Sin(phase) * .008 };
            breathing.ScaleX = state switch { "Happy" => 1.008, "Surprised" => 1.025, _ => 1 + Math.Sin(phase + .6) * .004 };
        };
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    /// <summary>Snapshots the existing renderer for the private control-center preview; no second renderer is created.</summary>
    public BitmapSource CapturePreview()
    {
        Dispatcher.VerifyAccess();
        var width = Math.Clamp((int)Math.Ceiling(ActualWidth > 0 ? ActualWidth : Width), 160, 1920);
        var height = Math.Clamp((int)Math.Ceiling(ActualHeight > 0 ? ActualHeight : Height), 180, 1080);
        layerCanvas.Measure(new Size(width, height));
        layerCanvas.Arrange(new Rect(0, 0, width, height));
        layerCanvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(layerCanvas);
        bitmap.Freeze();
        return bitmap;
    }

    public void SetParallaxEnabled(bool enabled)
    {
        parallaxEnabled = enabled;
        if (!enabled)
        {
            targetParallaxX = 0;
            targetParallaxY = 0;
        }
    }

    public void SetParallaxStrength(double strength)
    {
        parallaxStrength = double.IsFinite(strength)
            ? Math.Clamp(strength, 0, AvatarParallaxPolicy.MaximumOffsetPixels)
            : 0;
    }

    public void SetImage(string path) => SetLayers([path]);

    public void SetState(string nextState)
    {
        if (string.Equals(nextState, "Blink", StringComparison.OrdinalIgnoreCase))
        {
            // A visible blink needs a closed-eye sprite assigned in Avatar Studio.
            if (!expressionImages.TryGetValue("Blink", out var blinkPath) || !File.Exists(blinkPath))
            {
                nextAutomaticBlinkAt = DateTimeOffset.UtcNow.AddSeconds(Random.Shared.NextDouble() * 4 + 3.5);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            transientState = "Blink";
            transientUntil = now.AddMilliseconds(160);
            nextAutomaticBlinkAt = now.AddSeconds(Random.Shared.NextDouble() * 4 + 3.5);
            RenderAvatar();
            return;
        }

        var isBuiltInState = nextState is "Idle" or "Talking" or "Happy" or "Surprised";
        var hasConfiguredExpression = expressionImages.TryGetValue(nextState, out var customPath) && File.Exists(customPath);
        state = isBuiltInState || hasConfiguredExpression ? nextState : "Idle";
        transientState = null;
        transientUntil = DateTimeOffset.MinValue;
        RenderAvatar();
    }

    public bool ShowReaction(string reactionState, TimeSpan? duration = null)
    {
        if (reactionState is not ("Happy" or "Surprised"))
            return false;

        var reactionDuration = duration ?? TimeSpan.FromSeconds(3);
        if (reactionDuration <= TimeSpan.Zero || reactionDuration > TimeSpan.FromSeconds(15))
            return false;

        // Reactions use the viewer's own configured expression art. Without that sprite,
        // keep the alert card but do not pretend that a meaningful expression was shown.
        if (!expressionImages.TryGetValue(reactionState, out var expressionPath) || !File.Exists(expressionPath))
            return false;

        var now = DateTimeOffset.UtcNow;
        transientState = reactionState;
        transientUntil = now.Add(reactionDuration);
        nextAutomaticBlinkAt = now.AddSeconds(Random.Shared.NextDouble() * 4 + 3.5);
        RenderAvatar();
        return true;
    }

    public void SetLayers(IEnumerable<string> paths)
    {
        var list = paths.Where(File.Exists).ToList();
        baseImagePath = list.FirstOrDefault();
        avatarLayers = Array.Empty<AvatarLayer>();
        expressionImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RenderAvatar(list.Skip(1));
    }

    public void SetAvatar(string basePath, IEnumerable<AvatarLayer> layers) =>
        SetAvatar(basePath, layers, null);

    public void SetAvatar(
        string basePath,
        IEnumerable<AvatarLayer> layers,
        IReadOnlyDictionary<string, string>? expressions)
    {
        baseImagePath = basePath;
        avatarLayers = layers.ToArray();
        expressionImages = expressions is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : expressions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        RenderAvatar();
    }

    private void RenderAvatar(IEnumerable<string>? additionalPaths = null)
    {
        layerCanvas.Children.Clear();
        parallaxTransforms.Clear();
        var visualState = transientState ?? state;
        var activeBasePath = expressionImages.TryGetValue(visualState, out var expressionPath) && File.Exists(expressionPath)
            ? expressionPath
            : baseImagePath;
        var visibleLayers = avatarLayers.Where(layer => layer.Visible && File.Exists(layer.Path)).ToArray();
        foreach (var layer in AvatarLayerOrdering.BackgroundLayers(visibleLayers))
            AddLayerImage(ResolveLayerArtwork(layer, visualState), layer);
        if (!string.IsNullOrWhiteSpace(activeBasePath) && File.Exists(activeBasePath))
            AddLayerImage(activeBasePath, null);
        foreach (var layer in AvatarLayerOrdering.ForegroundLayers(visibleLayers))
            AddLayerImage(ResolveLayerArtwork(layer, visualState), layer);
        if (additionalPaths is not null)
            foreach (var path in additionalPaths.Where(File.Exists))
                AddLayerImage(path, null);
        layerCanvas.Children.Add(alertCard);
    }

    private static string ResolveLayerArtwork(AvatarLayer layer, string visualState)
    {
        // Mouth art is an overlay layer: keep its saved transform and never replace the full character.
        if (string.Equals(visualState, "Talking", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(layer.OpenMouthPath) && File.Exists(layer.OpenMouthPath))
            return layer.OpenMouthPath;
        if (!string.IsNullOrWhiteSpace(layer.ClosedMouthPath) && File.Exists(layer.ClosedMouthPath))
            return layer.ClosedMouthPath;
        return layer.Path;
    }

    private void AddLayerImage(string path, AvatarLayer? layer)
    {
        var bitmap = AvatarImageLoader.Load(path, decodePixelWidth: 1000);
        var image = new Image
        {
            Source = bitmap, Stretch = AvatarLayerOrdering.IsBackground(layer) ? Stretch.UniformToFill : Stretch.Uniform,
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
            var parallax = new TranslateTransform();
            transform.Children.Add(parallax);
            parallaxTransforms.Add((parallax, AvatarParallaxPolicy.GetLayerFactor(layer)));
            image.RenderTransform = transform;
        }
        layerCanvas.Children.Add(image);
    }

    private void UpdateParallax()
    {
        if (ReduceMotion || !parallaxEnabled || !IsVisible)
        {
            targetParallaxX = 0;
            targetParallaxY = 0;
        }

        currentParallaxX += (targetParallaxX - currentParallaxX) * 0.18;
        currentParallaxY += (targetParallaxY - currentParallaxY) * 0.18;

        if (ReduceMotion)
        {
            currentParallaxX = 0;
            currentParallaxY = 0;
        }

        foreach (var (transform, factor) in parallaxTransforms)
        {
            transform.X = AvatarParallaxPolicy.GetOffset(currentParallaxX, factor, parallaxStrength);
            transform.Y = AvatarParallaxPolicy.GetOffset(currentParallaxY, factor, parallaxStrength);
        }
    }

    public void ShowAlert(string text)
    {
        alertText.Text = text;
        alertUntil = DateTimeOffset.UtcNow.AddSeconds(6);
        alertCard.Visibility = Visibility.Visible;
    }
}
