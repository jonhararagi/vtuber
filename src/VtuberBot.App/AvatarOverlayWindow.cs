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
    // Motion stays inside the two approved visual states (Idle/Talking); no blink or 2.5D animation.
    private readonly TranslateTransform idleFloat = new();
    private readonly TranslateTransform talkingBounce = new();
    private readonly TranslateTransform talkingShake = new();
    private bool speakingShakeEnabled = true;
    private bool talkingBounceEnabled = true;
    private bool idleFloatEnabled = true;
    private double speakingShakeStrength = 6;
    private double phase;
    private DateTimeOffset manualShakeUntil = DateTimeOffset.MinValue;
    private readonly List<(Image Image, BitmapSource Artwork)> intrinsicMouthImages = [];
    private BitmapSource? referenceAvatarBitmap;
    private readonly List<(TranslateTransform Transform, double Factor)> parallaxTransforms = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private bool parallaxEnabled;
    private double targetParallaxX;
    private double targetParallaxY;
    private double currentParallaxX;
    private double currentParallaxY;
    private double parallaxStrength = AvatarParallaxPolicy.MaximumOffsetPixels;
    private string state = "Idle";
    private string? baseImagePath;
    private IReadOnlyList<AvatarLayer> avatarLayers = Array.Empty<AvatarLayer>();
    private IReadOnlyDictionary<string, string> expressionImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset alertUntil;
    public bool ReduceMotion { get; set; }

    public AvatarOverlayWindow()
    {
        Title = "VTuberBot Avatar Overlay";
        Width = 420; Height = 520; MinWidth = 160; MinHeight = 180;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        layerCanvas.RenderTransformOrigin = new Point(.5,.5);
        var avatarMotion = new TransformGroup();
        avatarMotion.Children.Add(idleFloat);
        avatarMotion.Children.Add(talkingBounce);
        avatarMotion.Children.Add(talkingShake);
        layerCanvas.RenderTransform = avatarMotion;
        layerCanvas.SizeChanged += (_, _) => UpdateIntrinsicMouthSizes();
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
        // The avatar can move cartoonishly while preserving the two actual face states.
        // Reduced-motion mode also cancels the shake preview and every transform.
        timer.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow;
            alertCard.Visibility = now < alertUntil ? Visibility.Visible : Visibility.Collapsed;
            if (ReduceMotion)
            {
                idleFloat.Y = talkingBounce.Y = talkingShake.X = 0;
                return;
            }

            phase += state == "Talking" ? .12 : .035;
            idleFloat.Y = state == "Idle" && idleFloatEnabled
                ? AvatarMotionPolicy.GetIdleFloatOffset(phase, enabled: true)
                : 0;
            talkingBounce.Y = state == "Talking" && talkingBounceEnabled
                ? AvatarMotionPolicy.GetTalkingBounceOffset(phase, enabled: true)
                : 0;
            talkingShake.X = now < manualShakeUntil || (state == "Talking" && speakingShakeEnabled)
                ? AvatarMotionPolicy.GetShakeOffset(phase, speakingShakeStrength)
                : 0;
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

    /// <summary>Compatibility shim: parallax is intentionally disabled in the two-state PNGTuber workflow.</summary>
    public void SetParallaxEnabled(bool enabled)
    {
        parallaxEnabled = false;
        targetParallaxX = targetParallaxY = currentParallaxX = currentParallaxY = 0;
        foreach (var (transform, _) in parallaxTransforms)
        {
            transform.X = 0;
            transform.Y = 0;
        }
    }

    /// <summary>Compatibility shim: strength is fixed to zero because 2.5D/parallax is out of scope.</summary>
    public void SetParallaxStrength(double strength)
    {
        parallaxStrength = 0;
    }

    public void SetMotionEffects(bool speakingShake, double shakeStrength, bool enableTalkingBounce, bool enableIdleFloat = true)
    {
        speakingShakeEnabled = speakingShake;
        speakingShakeStrength = AvatarMotionPolicy.NormalizeShakeStrength(shakeStrength);
        talkingBounceEnabled = enableTalkingBounce;
        idleFloatEnabled = enableIdleFloat;
        if (!speakingShakeEnabled && DateTimeOffset.UtcNow >= manualShakeUntil)
            talkingShake.X = 0;
        if (!talkingBounceEnabled)
            talkingBounce.Y = 0;
    }

    /// <summary>Plays a short cartoon-like shake for a preview button.</summary>
    public void TriggerShake(TimeSpan? duration = null)
    {
        var requestedDuration = duration ?? TimeSpan.FromMilliseconds(650);
        if (requestedDuration <= TimeSpan.Zero || requestedDuration > TimeSpan.FromSeconds(3))
            return;
        manualShakeUntil = DateTimeOffset.UtcNow.Add(requestedDuration);
    }

    public void SetImage(string path) => SetLayers([path]);

    public void SetState(string nextState)
    {
        state = AvatarTalkStatePolicy.NormalizeVisualState(nextState);
        RenderAvatar();
    }

    /// <summary>Automatic reactions are outside the approved Idle/Talking-only scope.</summary>
    public bool ShowReaction(string reactionState, TimeSpan? duration = null) => false;

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
        intrinsicMouthImages.Clear();
        var visualState = AvatarTalkStatePolicy.NormalizeVisualState(state);
        var activeBasePath = expressionImages.TryGetValue(visualState, out var expressionPath) && File.Exists(expressionPath)
            ? expressionPath
            : baseImagePath;
        referenceAvatarBitmap = !string.IsNullOrWhiteSpace(activeBasePath) && File.Exists(activeBasePath)
            ? AvatarImageLoader.Load(activeBasePath, decodePixelWidth: 1000)
            : null;
        var visibleLayers = avatarLayers.Where(layer => layer.Visible && File.Exists(layer.Path)).ToArray();
        foreach (var layer in AvatarLayerOrdering.BackgroundLayers(visibleLayers))
            AddLayerImage(ResolveLayerArtwork(layer, visualState), layer, referenceAvatarBitmap);
        if (referenceAvatarBitmap is not null && !string.IsNullOrWhiteSpace(activeBasePath))
            AddLayerImage(activeBasePath, null, referenceAvatarBitmap, referenceAvatarBitmap);
        foreach (var layer in AvatarLayerOrdering.ForegroundLayers(visibleLayers))
            AddLayerImage(ResolveLayerArtwork(layer, visualState), layer, referenceAvatarBitmap);
        if (additionalPaths is not null)
            foreach (var path in additionalPaths.Where(File.Exists))
                AddLayerImage(path, null, referenceAvatarBitmap);
        layerCanvas.Children.Add(alertCard);
        UpdateIntrinsicMouthSizes();
    }

    private static string ResolveLayerArtwork(AvatarLayer layer, string visualState) =>
        AvatarMouthSpritePolicy.ResolvePath(layer, visualState);

    private void AddLayerImage(string path, AvatarLayer? layer, BitmapSource? referenceBitmap, BitmapSource? preloadedBitmap = null)
    {
        var bitmap = preloadedBitmap ?? AvatarImageLoader.Load(path, decodePixelWidth: 1000);
        var isBackground = AvatarLayerOrdering.IsBackground(layer);
        var isMouthArtwork = layer is not null &&
            ((!string.IsNullOrWhiteSpace(layer.ClosedMouthPath) && string.Equals(path, layer.ClosedMouthPath, StringComparison.OrdinalIgnoreCase)) ||
             (!string.IsNullOrWhiteSpace(layer.OpenMouthPath) && string.Equals(path, layer.OpenMouthPath, StringComparison.OrdinalIgnoreCase)));
        var intrinsicMouthPiece = isMouthArtwork && referenceBitmap is { PixelWidth: > 0, PixelHeight: > 0 };
        var image = new Image
        {
            Source = bitmap,
            Stretch = isBackground ? Stretch.UniformToFill : Stretch.Uniform,
            HorizontalAlignment = intrinsicMouthPiece ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = intrinsicMouthPiece ? VerticalAlignment.Center : VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };
        if (intrinsicMouthPiece)
            intrinsicMouthImages.Add((image, bitmap));
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

    private void UpdateIntrinsicMouthSizes()
    {
        if (referenceAvatarBitmap is null || referenceAvatarBitmap.PixelWidth <= 0 || referenceAvatarBitmap.PixelHeight <= 0)
            return;
        var viewportWidth = layerCanvas.ActualWidth > 0 ? layerCanvas.ActualWidth : Width;
        var viewportHeight = layerCanvas.ActualHeight > 0 ? layerCanvas.ActualHeight : Height;
        foreach (var (image, artwork) in intrinsicMouthImages)
        {
            var size = AvatarLayerSizingPolicy.GetStickerSize(
                artwork.PixelWidth, artwork.PixelHeight,
                referenceAvatarBitmap.PixelWidth, referenceAvatarBitmap.PixelHeight,
                viewportWidth, viewportHeight);
            image.Width = size.Width;
            image.Height = size.Height;
        }
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
