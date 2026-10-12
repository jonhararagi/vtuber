using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

public partial class MainWindow : Window
{
    private readonly VtuberApplication application;
    private readonly ProductStore store;
    private readonly string dataDirectory;
    private readonly string avatarDirectory;
    private readonly string secretDirectory;
    private readonly TwitchChatClient twitch = new();
    private readonly TwitchEventSubClient eventSub = new();
    private readonly ObsWebSocketClient obs = new();
    private readonly LocalSpeechQueue speechQueue = new();
    private AvatarOverlayWindow? overlay;
    private string? avatarPath;
    private WaveInEvent? microphone;
    private readonly VoiceActivityDetector voiceActivity = new(thresholdPercent: 8, releaseHold: TimeSpan.FromMilliseconds(350), smoothingFactor: 0.35);
    private WebView2? telegramView;
    private readonly ObservableCollection<string> chatLines = [];
    private readonly DispatcherTimer automationTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Dictionary<long, DateTimeOffset> automationLastRun = [];
    private readonly Dictionary<string, DateTimeOffset> lastPointAward = new(StringComparer.OrdinalIgnoreCase);
    private string currentPage = "Inicio";

    private static readonly string[] Pages =
    [
        "Inicio","Avatar Studio","Twitch y chat","Comandos","Comunidad y puntos","Sorteos",
        "Minijuegos","Moderación","Voz y audio","OBS","Telegram Web","Actividad y diagnóstico","Ajustes"
    ];

    public MainWindow(VtuberApplication application)
    {
        InitializeComponent();
        this.application = application;
        store = new ProductStore(application.Database.Path);
        dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VtuberBot");
        avatarDirectory = Path.Combine(dataDirectory, "avatars");
        secretDirectory = Path.Combine(dataDirectory, "secrets");
        Directory.CreateDirectory(avatarDirectory);
        Directory.CreateDirectory(secretDirectory);
        avatarPath = store.GetSetting("avatar.path");
        twitch.StatusChanged += status => Dispatcher.Invoke(() => { FooterStatus.Text = status; RuntimeStatus.Text = twitch.IsConnected ? "●  TWITCH CONECTADO" : "●  LOCAL ACTIVO"; });
        twitch.MessageReceived += HandleTwitchMessage;
        eventSub.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() => FooterStatus.Text = status));
        speechQueue.StatusChanged += (status, pending) => Dispatcher.BeginInvoke(new Action(() =>
            FooterStatus.Text = $"Voz local: {status} · pendientes: {pending}"));
        eventSub.EventReceived += (type,summary) => Dispatcher.BeginInvoke(new Action(() =>
        {
            chatLines.Insert(0, "[EventSub] " + type + ": " + summary);
            while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
            store.Log("Twitch EventSub", type + ": " + summary);
            if (type != "subscription_error" && type != "revocation")
            {
                if (store.GetSetting("twitch.speak_alerts", "false") == "true")
                {
                    if (!speechQueue.Enqueue("Alerta de Twitch. " + summary, SpeechPriority.High))
                        store.Log("Audio", "Alerta EventSub no encolada: la cola de voz está llena o cerrada.");
                }
                if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
                {
                    if (overlay is null || !overlay.IsVisible) ShowOverlay();
                    overlay?.ShowAlert(type + ": " + summary);
                }
            }
        }));
        obs.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() => FooterStatus.Text = status));
        Closed += async (_, _) =>
        {
            automationTimer.Stop();
            StopMicrophone();
            speechQueue.Dispose();
            if (overlay is not null) overlay.Close();
            await twitch.DisposeAsync();
            await obs.DisposeAsync();
            await eventSub.DisposeAsync();
        };
        automationTimer.Tick += async (_, _) => await ProcessAutomationsAsync();
        automationTimer.Start();
        Navigate("Inicio");
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) Navigate(page);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Navigate(currentPage);

    private void Navigate(string page)
    {
        currentPage = Pages.Contains(page, StringComparer.Ordinal) ? page : "Inicio";
        PageTitle.Text = currentPage;
        PageSubtitle.Text = currentPage switch
        {
            "Inicio" => "Todo lo importante del stream, en un solo lugar.",
            "Avatar Studio" => "Importá tu arte, prepará estados y controlá la salida transparente.",
            "Twitch y chat" => "Conexión oficial por IRC/TLS; la cuenta solo aparece conectada tras autenticación real.",
            "Comandos" => "Un registro de comandos compartido entre pruebas locales y chat.",
            "Comunidad y puntos" => "Perfiles, puntos e historial persistidos en SQLite local.",
            "Sorteos" => "Participantes deduplicados y ganador guardado en el historial.",
            "Minijuegos" => "Juegos locales con resultados y recompensas registradas.",
            "Moderación" => "Reglas configurables; el modo actual marca coincidencias sin castigar automáticamente.",
            "Voz y audio" => "Micrófono real, medidor local y síntesis de voz de Windows.",
            "OBS" => "Diagnóstico local del puerto de OBS WebSocket; no se afirma control remoto sin autenticar.",
            "Telegram Web" => "Telegram Web integrado cuando WebView2 está disponible.",
            "Actividad y diagnóstico" => "Registro local de acciones y exportación sin tokens ni cookies.",
            "Ajustes" => "Preferencias locales, seguridad y perfil de rendimiento.",
            _ => ""
        };
        Workspace.Content = currentPage switch
        {
            "Inicio" => BuildDashboard(),
            "Avatar Studio" => BuildAvatarStudio(),
            "Twitch y chat" => BuildTwitchPage(),
            "Comandos" => BuildCommandsPage(),
            "Comunidad y puntos" => BuildCommunityPage(),
            "Sorteos" => BuildGiveawaysPage(),
            "Minijuegos" => BuildMinigamesPage(),
            "Moderación" => BuildModerationPage(),
            "Voz y audio" => BuildAudioPage(),
            "OBS" => BuildObsPage(),
            "Telegram Web" => BuildTelegramPage(),
            "Actividad y diagnóstico" => BuildActivityPage(),
            "Ajustes" => BuildSettingsPage(),
            _ => BuildDashboard()
        };
    }

    private UIElement BuildDashboard()
    {
        var root = new StackPanel();
        var hero = new Border { Background = Brush("#171D30"), BorderBrush = Brush("#293650"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(24), Margin = new Thickness(0,0,0,18) };
        var heroGrid = new Grid(); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) }); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label("Tu stream, bajo control.", 28, "#F8FAFC", true));
        text.Children.Add(Label("Configurá el avatar y probá la lógica local antes de conectar servicios externos.", 14, "#A8B3CC"));
        var buttons = new WrapPanel { Margin = new Thickness(0,18,0,0) };
        buttons.Children.Add(ActionButton("Abrir Avatar Studio", (_,_)=>Navigate("Avatar Studio"), true));
        buttons.Children.Add(ActionButton("Probar comandos", (_,_)=>Navigate("Comandos")));
        text.Children.Add(buttons); Grid.SetColumn(text,0); heroGrid.Children.Add(text);
        var preview = new Border { Background = Brush("#101525"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Margin = new Thickness(18,0,0,0), MinHeight = 220 };
        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            try { var image = new Image { Source = LoadImage(avatarPath), Stretch = Stretch.Uniform, MaxHeight = 240 }; preview.Child = image; }
            catch { preview.Child = Label("No se pudo cargar el avatar. Volvé a importarlo.",13,"#FCA5A5"); }
        }
        else
        {
            var empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(Label("✧",48,"#69D8F2",true)); empty.Children.Add(Label("Tu avatar va acá",17,"#F8FAFC",true));
            empty.Children.Add(Label("Importá un PNG en Avatar Studio",12,"#98A4BE")); preview.Child = empty;
        }
        Grid.SetColumn(preview,1); heroGrid.Children.Add(preview); hero.Child=heroGrid; root.Children.Add(hero);
        var cards = new UniformGrid { Columns = 4, Margin = new Thickness(0,0,0,18) };
        cards.Children.Add(MetricCard("AVATAR", string.IsNullOrWhiteSpace(avatarPath) ? "Sin importar" : "Configurado", "PNG local"));
        cards.Children.Add(MetricCard("TWITCH", twitch.IsConnected ? "Conectado" : "Desconectado", "IRC / TLS"));
        cards.Children.Add(MetricCard("COMANDOS", store.GetCommands().Count.ToString(), "Configurables"));
        cards.Children.Add(MetricCard("COMUNIDAD", store.GetLeaderboard(500).Count.ToString(), "Usuarios guardados"));
        root.Children.Add(cards);
        var lower = new Grid(); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) }); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        var quick = Panel("Accesos rápidos"); var quickWrap = new WrapPanel();
        foreach(var item in new[] {("Sorteos","Sorteos"),("Comunidad","Comunidad y puntos"),("Voz y audio","Voz y audio"),("Diagnósticos","Actividad y diagnóstico")})
            quickWrap.Children.Add(ActionButton(item.Item1,(_,_)=>Navigate(item.Item2)));
        quick.Children.Add(quickWrap); Grid.SetColumn(quick,0); lower.Children.Add(quick);
        var health = Panel("Estado real de módulos");
        health.Children.Add(StatusRow("Núcleo y SQLite","Disponible",true));
        health.Children.Add(StatusRow("Avatar","",!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath)));
        health.Children.Add(StatusRow("Twitch",twitch.IsConnected?"Autenticado":"Requiere configuración",twitch.IsConnected));
        health.Children.Add(StatusRow("Micrófono",microphone is null?"No iniciado":"Capturando",microphone is not null));
        health.Children.Add(StatusRow("OBS",obs.IsConnected?"Autenticado":"Requiere configuración",obs.IsConnected));
        Grid.SetColumn(health,1); lower.Children.Add(health); root.Children.Add(lower);
        root.Children.Add(Label("Los módulos locales funcionan sin cuentas externas. Una conexión externa nunca se marca como activa solo por guardar sus datos.",12,"#7F8AA5"));
        return root;
    }

    private UIElement BuildAvatarStudio()
    {
        var root = new StackPanel();
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var layerImages = new Dictionary<long, Image>();
        Image? basePreviewImage = null;
        var previewState = store.GetSetting("avatar.state", "Idle");
        if (previewState == "Blink") previewState = "Idle";
        var left = Panel("Previsualización del avatar");
        left.MinHeight = 420;
        var preview = new Border { Background = Brush("#101525"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), MinHeight = 350 };
        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            var previewCanvas = new Grid();
            var savedExpression = store.GetSetting("avatar.expression." + previewState);
            var initialImagePath = !string.IsNullOrWhiteSpace(savedExpression) && File.Exists(savedExpression) ? savedExpression : avatarPath;
            basePreviewImage = new Image { Source = LoadImage(initialImagePath), Stretch = Stretch.Uniform, MaxHeight = 360 };
            previewCanvas.Children.Add(basePreviewImage);
            foreach (var layer in store.GetAvatarLayers().Where(l => l.Visible && File.Exists(l.Path)).OrderBy(l => l.SortOrder))
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
        foreach (var state in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
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
            var expressionPath = store.GetSetting("avatar.expression." + selectedState);
            basePreviewImage.Source = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath)
                ? LoadImage(expressionPath)
                : LoadImage(avatarPath);
        }

        var expressionPanel = Panel("Sprites por expresión");
        expressionPanel.Margin = new Thickness(0, 12, 0, 0);
        expressionPanel.Children.Add(Label("Asigná imágenes del mismo tamaño para cambiar la cara cuando el micrófono activa Talking o al elegir una expresión. Si no asignás una imagen, se usa la base.", 12, "#98A4BE"));
        foreach (var expressionName in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
        {
            var savedPath = store.GetSetting("avatar.expression." + expressionName);
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(Label(expressionName + ": " + (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath) ? Path.GetFileName(savedPath) : "usa imagen base"), 12, "#F8FAFC"));
            row.Children.Add(ActionButton("Importar / cambiar", (_, _) => ImportAvatarExpression(expressionName)));
            row.Children.Add(ActionButton("Quitar", (_, _) =>
            {
                store.SetSetting("avatar.expression." + expressionName, "");
                store.Log("Avatar", "Sprite de expresión quitado: " + expressionName);
                Navigate("Avatar Studio");
            }));
            expressionPanel.Children.Add(row);
        }
        right.Children.Add(expressionPanel);

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

    private void ImportAvatar()
    {
        var dialog=new OpenFileDialog{Title="Importar arte del avatar",Filter="Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        var info=new FileInfo(dialog.FileName);
        if(info.Length>25*1024*1024){MessageBox.Show(this,"El archivo supera el límite de 25 MB.","VTuberBot",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp"))return;
        try
        {
            _=LoadImage(dialog.FileName);
            var destination=Path.Combine(avatarDirectory,$"avatar-{DateTime.UtcNow:yyyyMMdd-HHmmss}{extension}");
            File.Copy(dialog.FileName,destination,false);avatarPath=destination;store.SetSetting("avatar.path",destination);store.Log("Avatar",$"Asset importado: {Path.GetFileName(destination)}");
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
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
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
            store.SetSetting("avatar.expression." + expressionName, destination);
            store.Log("Avatar", "Sprite de expresión importado: " + expressionName);
            Navigate("Avatar Studio");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo importar el sprite de expresión: " + ex.Message);
        }
    }

    private IReadOnlyDictionary<string, string> GetAvatarExpressionPaths()
    {
        var states = new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" };
        return states
            .Select(state => new { State = state, Path = store.GetSetting("avatar.expression." + state) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Path) && File.Exists(item.Path))
            .ToDictionary(item => item.State, item => item.Path, StringComparer.OrdinalIgnoreCase);
    }

    private void ShowOverlay()
    {
        if(string.IsNullOrWhiteSpace(avatarPath)||!File.Exists(avatarPath)){MessageBox.Show(this,"Primero importá una imagen en Avatar Studio.");return;}
        try { overlay??=new AvatarOverlayWindow(); overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths()); overlay.ReduceMotion=store.GetSetting("ui.reduceMotion","false")=="true"; overlay.SetState(store.GetSetting("avatar.state","Idle")); if(!overlay.IsVisible)overlay.Show();overlay.Activate();store.Log("Avatar","Overlay mostrado."); }
        catch(Exception ex){MessageBox.Show(this,$"No se pudo abrir el overlay: {ex.Message}");}
    }

    private UIElement BuildTwitchPage()
    {
        var root=new StackPanel();var panel=Panel("Conexión de Twitch");root.Children.Add(panel);
        var channel=TextField("Nombre del canal",store.GetSetting("twitch.channel"));
        var clientId=TextField("Twitch Application Client ID",store.GetSetting("twitch.client_id"));
        var token=new PasswordBox{Password=LoadTwitchToken(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        panel.Children.Add(Label("Canal",12,"#98A4BE"));panel.Children.Add(channel);
        panel.Children.Add(Label("Client ID de tu aplicación Twitch",12,"#98A4BE"));panel.Children.Add(clientId);
        panel.Children.Add(Label("Token OAuth de chat (no se registra en logs)",12,"#98A4BE"));panel.Children.Add(token);
        panel.Children.Add(Label("Usá OAuth de Twitch con permisos chat:read, chat:edit, moderator:read:followers, moderator:manage:banned_users, channel:read:subscriptions y channel:read:redemptions. Registrá la URL de redirección local indicada abajo en la consola de desarrolladores. El token se cifra con DPAPI para el usuario actual y nunca se escribe en GitHub ni en el registro de actividad. Redirección: http://localhost:17563/callback/",12,"#98A4BE"));
        var speakAlerts = new CheckBox
        {
            Content = "Leer alertas de Twitch con la voz local",
            IsChecked = store.GetSetting("twitch.speak_alerts", "false") == "true",
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        speakAlerts.Checked += (_, _) => store.SetSetting("twitch.speak_alerts", "true");
        speakAlerts.Unchecked += (_, _) => store.SetSetting("twitch.speak_alerts", "false");
        panel.Children.Add(speakAlerts);
        panel.Children.Add(Label("Si la opción está activa, las alertas EventSub se agregan a la cola TTS local con prioridad alta; el audio actual no se interrumpe.", 12, "#98A4BE"));
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Autorizar con Twitch",async (_,_)=>{try{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());FooterStatus.Text="Esperando autorización de Twitch en el navegador…";var accessToken=await TwitchOAuth.AuthorizeAsync(clientId.Text.Trim());token.Password=accessToken;SaveTwitchToken(accessToken);FooterStatus.Text="OAuth completado; token cifrado localmente. Presioná Conectar para iniciar chat.";store.Log("Twitch","Flujo OAuth completado; el token no se registra.");}catch(Exception ex){FooterStatus.Text=$"OAuth no completado: {ex.Message}";MessageBox.Show(this,$"No se pudo completar OAuth. Confirmá que el Client ID sea válido y que la URL de redirección registrada sea {TwitchOAuth.RedirectUri}\n\nDetalle: {ex.Message}");}}));
        actions.Children.Add(ActionButton("Guardar configuración segura",(_,_)=>{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());SaveTwitchToken(token.Password);FooterStatus.Text="Canal y token guardados localmente; aún no conectado.";store.Log("Twitch","Configuración local guardada sin registrar el token.");},true));
        actions.Children.Add(ActionButton("Conectar",async (_,_)=>{try{store.SetSetting("twitch.channel",channel.Text.Trim());SaveTwitchToken(token.Password);await twitch.ConnectAsync(channel.Text,token.Password); }catch(Exception ex){FooterStatus.Text=$"Twitch: {ex.Message}";store.Log("Twitch",$"Falló conexión: {ex.GetType().Name}; revisá configuración y permisos.");MessageBox.Show(this,$"No se pudo conectar a Twitch. Revisá canal, token OAuth, permisos y conexión a Internet.\n\nDetalle: {ex.Message}");}}));
        actions.Children.Add(ActionButton("Conectar EventSub (alertas)",async (_,_)=>{try{SaveTwitchToken(token.Password);await eventSub.ConnectAsync(clientId.Text.Trim(),token.Password,channel.Text.Trim());}catch(Exception ex){FooterStatus.Text="EventSub no conectado: "+ex.Message;MessageBox.Show(this,"No se pudieron registrar los eventos oficiales. Si el token ya existía, volvé a autorizar para aceptar los scopes adicionales.\n\n"+ex.Message);}}));
        actions.Children.Add(ActionButton("Desconectar EventSub",async (_,_)=>await eventSub.DisconnectAsync()));
        actions.Children.Add(ActionButton("Desconectar",async (_,_)=>await twitch.DisconnectAsync()));
        panel.Children.Add(actions);
        var chatPanel=Panel("Chat en directo");chatPanel.Margin=new Thickness(0,16,0,0);
        var list=new ListBox{ItemsSource=chatLines,Height=260};chatPanel.Children.Add(list);
        var send=TextField("Escribí un mensaje para el chat…");var sendRow=new DockPanel();DockPanel.SetDock(sendRow, Dock.Left);sendRow.Children.Add(send);
        sendRow.Children.Add(ActionButton("Enviar",async (_,_)=>{try{await twitch.SendMessageAsync(channel.Text,send.Text);store.Log("Twitch","Mensaje enviado al chat.");send.Clear();}catch(Exception ex){FooterStatus.Text=$"No se pudo enviar: {ex.Message}";}}));chatPanel.Children.Add(sendRow);
        root.Children.Add(chatPanel);
        root.Children.Add(Label("La lectura de chat, los comandos y las alertas EventSub solo funcionan después de autenticación real. EventSub incluye follows, suscripciones, regalos, redenciones de puntos y raids hacia el canal, reintenta tras perder la sesión y puede leer alertas en voz local si activaste esa opción. La conexión IRC del chat todavía requiere reconexión manual si se corta.",12,"#98A4BE"));
        return root;
    }

    private void HandleTwitchMessage(string raw)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            var sep=raw.IndexOf(" :",StringComparison.Ordinal);
            var text=sep>=0?raw[(sep+2)..]:raw;
            var prefixEnd=raw.IndexOf('!');
            var username=raw.StartsWith(':')&&prefixEnd>1?raw[1..prefixEnd]:"viewer";
            chatLines.Insert(0,username+": "+text);while(chatLines.Count>200)chatLines.RemoveAt(chatLines.Count-1);
            store.Log("Twitch","Mensaje recibido de "+username+".");
            foreach(var rule in store.GetModerationRules().Where(r=>r.Enabled))
            {
                try{if(Regex.IsMatch(text,rule.Pattern,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))store.Log("Moderación","Coincidencia de regla "+rule.Name+" en mensaje de "+username+" (modo observación).");}
                catch(RegexMatchTimeoutException){store.Log("Moderación","La regla "+rule.Name+" agotó el tiempo de análisis.");}
            }
            if(Regex.IsMatch(username,"^[a-zA-Z0-9_]{1,25}$"))
            {
                var now=DateTimeOffset.UtcNow;
                if(!lastPointAward.TryGetValue(username,out var last)||now-last>=TimeSpan.FromMinutes(5))
                {
                    lastPointAward[username]=now;
                    try{store.AddPoints(username,1,"Actividad de chat");}catch(Exception ex){store.Log("Comunidad","No se pudieron asignar puntos de actividad: "+ex.GetType().Name);}
                }
            }
            if(text.StartsWith('!'))
            {
                var command=text.Split(' ',StringSplitOptions.RemoveEmptyEntries)[0];
                if(string.Equals(command,"!entrar",StringComparison.OrdinalIgnoreCase))
                {
                    var active=store.GetGiveaways().FirstOrDefault(g=>g.Status=="OPEN");
                    var reply=active is not null&&store.EnterGiveaway(active.Id,username)?username+" ya estás anotado en el sorteo.":"No hay sorteo abierto o ya estabas anotado.";
                    try{if(twitch.IsConnected)await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),reply);}catch(Exception ex){FooterStatus.Text="No se pudo responder al chat: "+ex.Message;}
                    return;
                }
                var result=store.RunLocalCommand(command,username);
                if(result.Success){try{await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),result.Message);}catch(Exception ex){FooterStatus.Text="Comando ejecutado localmente, respuesta no enviada: "+ex.Message;}}
            }
        }));
    }
    private UIElement BuildCommandsPage()
    {
        var root=new StackPanel();var panel=Panel("Registro de comandos");root.Children.Add(panel);
        var list=new ListBox{Height=260};long? editingCommandId=null;void Reload(){list.ItemsSource=null;list.ItemsSource=store.GetCommands().Select(c=>$"{c.Id}  !{c.Name}  ·  {(c.Enabled?"Activo":"Pausado")}  ·  cooldown {c.CooldownSeconds}s\n     {c.Response}").ToList();}
        Reload();panel.Children.Add(list);
        var name=TextField("Nombre (sin !, ej. hola)");var response=TextField("Respuesta. Variables: {user}, {points}, {roll}");var cooldown=TextField("Cooldown en segundos");cooldown.Text="5";var enabled=new CheckBox{Content="Comando habilitado",IsChecked=true,Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,10)};
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split((char)32)[0]);var selected=store.GetCommands().FirstOrDefault(c=>c.Id==selectedId);if(selected is null)return;editingCommandId=selected.Id;name.Text=selected.Name;response.Text=selected.Response;cooldown.Text=selected.CooldownSeconds.ToString();enabled.IsChecked=selected.Enabled;}catch{}};
        panel.Children.Add(Label("Crear o editar",13,"#F8FAFC",true));panel.Children.Add(name);panel.Children.Add(response);panel.Children.Add(cooldown);panel.Children.Add(enabled);
        var row=new WrapPanel();
        row.Children.Add(ActionButton("Guardar comando",(_,_)=>{try{store.SaveCommand(editingCommandId,name.Text,response.Text,int.Parse(cooldown.Text),enabled.IsChecked==true);Reload();list.SelectedItem=null;editingCommandId=null;name.Clear();response.Clear();FooterStatus.Text="Comando guardado en SQLite.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        row.Children.Add(ActionButton("Probar localmente",(_,_)=>{var result=store.RunLocalCommand(name.Text);MessageBox.Show(this,result.Message,"Prueba local");}));
        row.Children.Add(ActionButton("Eliminar seleccionado",(_,_)=>{if(list.SelectedIndex<0)return;var id=long.Parse(list.SelectedItem!.ToString()!.Split(' ')[0]);if(MessageBox.Show(this,"¿Eliminar el comando seleccionado?","Confirmar",MessageBoxButton.YesNo)==MessageBoxResult.Yes){store.DeleteCommand(id);Reload();}}));
        panel.Children.Add(row);root.Children.Add(Label("Las pruebas locales recorren la misma respuesta persistida que usa el chat. El cooldown se guarda y expone para configuración; enforcement global de cooldown se consolidará en el router de eventos.",12,"#98A4BE"));return root;
    }

    private UIElement BuildCommunityPage()
    {
        var root=new StackPanel();var panel=Panel("Comunidad y puntos");root.Children.Add(panel);
        var list=new ListBox{Height=310};void Reload(){list.ItemsSource=store.GetLeaderboard().Select((u,i)=>$"{i+1,2}.  {u.Username,-24}  {u.Points,7} pts   · visto {u.LastSeen.ToLocalTime():dd/MM HH:mm}").ToList();}Reload();panel.Children.Add(list);
        var user=TextField("Usuario (letras/números/_/-)");var amount=TextField("Cantidad de puntos (+/-)");amount.Text="10";var reason=TextField("Motivo de la operación");reason.Text="Ajuste manual";
        panel.Children.Add(user);panel.Children.Add(amount);panel.Children.Add(reason);
        var row=new WrapPanel();row.Children.Add(ActionButton("Agregar / quitar puntos",(_,_)=>{try{var result=store.AddPoints(user.Text,long.Parse(amount.Text),reason.Text);Reload();FooterStatus.Text=$"{result.Username}: {result.Points} puntos.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        row.Children.Add(ActionButton("Crear perfil",(_,_)=>{try{var u=store.TouchUser(user.Text);Reload();FooterStatus.Text=$"Perfil {u.Username} disponible.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        row.Children.Add(ActionButton("Exportar ranking CSV",(_,_)=>ExportLeaderboard()));panel.Children.Add(row);
        return root;
    }

    private void ExportLeaderboard()
    {
        var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="vtuberbot-ranking.csv"};if(dialog.ShowDialog(this)!=true)return;
        var lines=new List<string>{"usuario,puntos,ultima_actividad"};
        lines.AddRange(store.GetLeaderboard(200).Select(u => Csv(u.Username) + "," + u.Points + "," + Csv(u.LastSeen.ToString("O"))));
        File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));store.Log("Comunidad","Ranking exportado a CSV.");
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private UIElement BuildGiveawaysPage()
    {
        var root=new StackPanel();var panel=Panel("Sorteos");root.Children.Add(panel);
        var list=new ListBox{Height=250};void Reload(){list.ItemsSource=store.GetGiveaways().Select(g=>$"{g.Id} · {g.Name} · {g.Status} · entradas: {g.Entries} · ganador: {g.Winner??"—"}").ToList();}Reload();panel.Children.Add(list);
        var name=TextField("Nombre del sorteo");var entrant=TextField("Usuario para registrar como participante");var id=TextField("ID del sorteo");panel.Children.Add(name);panel.Children.Add(ActionButton("Crear sorteo",(_,_)=>{try{id.Text=store.CreateGiveaway(name.Text).ToString();Reload();FooterStatus.Text=$"Sorteo #{id.Text} abierto."; }catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));panel.Children.Add(Label("ID de sorteo",12,"#98A4BE"));panel.Children.Add(id);panel.Children.Add(entrant);
        var row=new WrapPanel();row.Children.Add(ActionButton("Registrar participante",(_,_)=>{try{if(!store.EnterGiveaway(long.Parse(id.Text),entrant.Text))FooterStatus.Text="No se registró: el sorteo puede estar cerrado o el usuario ya participaba.";else FooterStatus.Text="Participante registrado.";Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        row.Children.Add(ActionButton("Elegir ganador y cerrar",(_,_)=>{try{var g=store.DrawGiveaway(long.Parse(id.Text));Reload();MessageBox.Show(this,$"Ganador: {g.Winner}\nParticipantes: {g.Entries}","Sorteo cerrado");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));panel.Children.Add(row);
        root.Children.Add(Label("La selección usa el conjunto de entradas únicas almacenadas; el ganador y el cierre quedan persistidos.",12,"#98A4BE"));return root;
    }

    private UIElement BuildMinigamesPage()
    {
        var root=new StackPanel();var panel=Panel("Minijuegos locales");root.Children.Add(panel);
        panel.Children.Add(Label("Duelo de moneda",22,"#F8FAFC",true));panel.Children.Add(Label("Elegí un nombre de usuario y jugá una ronda. Las victorias otorgan 10 puntos; cada sesión queda registrada.",13,"#98A4BE"));
        var user=TextField("Usuario");user.Text="Streamer";var game=new ComboBox{ItemsSource=new[]{"Duelo de moneda","Ruleta de suerte","Dado"},SelectedIndex=0};panel.Children.Add(user);panel.Children.Add(game);
        panel.Children.Add(ActionButton("Jugar ronda",(_,_)=>{try{var result=store.PlayGame(game.SelectedItem?.ToString()??"Duelo de moneda",user.Text,10);MessageBox.Show(this,result,"Resultado del minijuego");}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        var history=Panel("Historial reciente");history.Margin=new Thickness(0,16,0,0);history.Children.Add(new ListBox{ItemsSource=store.GetActivity(40).Where(a=>a.Module=="Minijuegos").Select(a=>$"{a.At.ToLocalTime():HH:mm:ss} · {a.Message}").ToList(),Height=220});root.Children.Add(history);return root;
    }

    private UIElement BuildModerationPage()
    {
        var root=new StackPanel();var panel=Panel("Reglas de moderación");root.Children.Add(panel);
        var list=new ListBox{Height=180};void Reload(){list.ItemsSource=store.GetModerationRules().Select(r=>$"{r.Name} | /{r.Pattern}/ | {(r.Enabled?"activa":"pausada")} | {r.Action}").ToList();}Reload();panel.Children.Add(list);
        var name=TextField("Nombre de la regla");var pattern=TextField("Patrón regex (ej. https?://)");var action=new ComboBox{ItemsSource=new[]{"flag","warn","delete"},SelectedIndex=0};var enabled=new CheckBox{Content="Habilitada",IsChecked=true,Foreground=Brush("#F8FAFC")};panel.Children.Add(name);panel.Children.Add(pattern);panel.Children.Add(action);panel.Children.Add(enabled);
        panel.Children.Add(ActionButton("Guardar regla",(_,_)=>{try{store.SaveModerationRule(name.Text,pattern.Text,enabled.IsChecked==true,action.SelectedItem?.ToString()??"flag");Reload();}catch(Exception ex){MessageBox.Show(this,$"Regla inválida: {ex.Message}");}},true));
        var test=TextField("Texto para analizar");panel.Children.Add(Label("Probar un mensaje (solo observación, no borra ni sanciona)",13,"#F8FAFC",true));panel.Children.Add(test);
        panel.Children.Add(ActionButton("Analizar mensaje",(_,_)=>{var matches=new List<string>();foreach(var r in store.GetModerationRules().Where(x=>x.Enabled)){try{if(Regex.IsMatch(test.Text,r.Pattern,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))matches.Add($"{r.Name} ({r.Action})");}catch(RegexMatchTimeoutException){matches.Add($"{r.Name} — tiempo agotado");}}var message=matches.Count==0?"No se detectaron coincidencias.": "Coincidencias: "+string.Join(", ",matches);store.Log("Moderación",message);MessageBox.Show(this,message,"Diagnóstico de moderación");}));
        root.Children.Add(Label("Las reglas se evalúan en modo observación. No se ejecutan acciones punitivas remotas automáticamente.",12,"#98A4BE"));
        var remotePanel = Panel("Acciones de Twitch — manuales y con confirmación");
        remotePanel.Margin = new Thickness(0, 12, 0, 0);
        remotePanel.Children.Add(Label("Estas acciones solo se ejecutan al pulsar un botón y confirmar. Las reglas automáticas siguen en modo observación.", 12, "#98A4BE"));
        var moderationChannel = TextField("Canal Twitch", store.GetSetting("twitch.channel"));
        var moderationTarget = TextField("Usuario de Twitch (login, sin @)");
        var moderationReason = TextField("Motivo (opcional, máximo 500 caracteres)");
        var durationValues = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["30 segundos"] = 30, ["1 minuto"] = 60, ["5 minutos"] = 300,
            ["10 minutos"] = 600, ["1 hora"] = 3600, ["1 día"] = 86400
        };
        var durationBox = new ComboBox { ItemsSource = durationValues.Keys.ToList(), SelectedItem = "10 minutos", MinWidth = 180, Margin = new Thickness(0, 0, 0, 8) };
        remotePanel.Children.Add(Label("Canal", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationChannel);
        remotePanel.Children.Add(Label("Usuario objetivo", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationTarget);
        remotePanel.Children.Add(Label("Motivo", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationReason);
        remotePanel.Children.Add(Label("Duración del timeout", 12, "#98A4BE"));
        remotePanel.Children.Add(durationBox);

        async Task ExecuteManualModerationAction(string actionName, int? durationSeconds, bool unban)
        {
            var channelName = moderationChannel.Text.Trim().TrimStart('#');
            var targetLogin = moderationTarget.Text.Trim().TrimStart('@');
            var reason = moderationReason.Text.Trim();
            var clientId = store.GetSetting("twitch.client_id").Trim();
            var accessToken = LoadTwitchToken();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
            {
                MessageBox.Show(this, "Primero configurá Twitch y autorizá la cuenta. El token se lee del almacenamiento cifrado local.", "Twitch no configurado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var details = unban
                ? $"Vas a retirar un ban/timeout a {targetLogin} en {channelName}."
                : durationSeconds.HasValue
                    ? $"Vas a aplicar un timeout de {durationSeconds.Value} segundos a {targetLogin} en {channelName}."
                    : $"Vas a BANEAR PERMANENTEMENTE a {targetLogin} en {channelName}.";
            if (!string.IsNullOrWhiteSpace(reason)) details += "\nMotivo: " + reason;
            details += "\n\nEsta acción afecta al chat real de Twitch. ¿Confirmás?";
            if (MessageBox.Show(this, details, "Confirmar acción de moderación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using var moderator = new TwitchModerationClient();
                var result = unban
                    ? await moderator.UnbanAsync(clientId, accessToken, channelName, targetLogin)
                    : await moderator.BanOrTimeoutAsync(clientId, accessToken, channelName, targetLogin, durationSeconds, reason);
                store.Log("Moderación", "Acción manual '" + actionName + "' ejecutada para " + targetLogin + " en " + channelName + ".");
                FooterStatus.Text = result;
                MessageBox.Show(this, result, "Moderación de Twitch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var safeMessage = "No se pudo ejecutar la acción. Verificá conexión Twitch, cuenta moderadora y el scope moderator:manage:banned_users. Si acabás de añadir permisos, volvé a autorizar la cuenta.\n\n" + ex.Message;
                FooterStatus.Text = "Moderación no ejecutada: " + ex.GetType().Name;
                store.Log("Moderación", "Falló una acción manual (" + ex.GetType().Name + ").");
                MessageBox.Show(this, safeMessage, "Acción de moderación fallida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        var remoteActions = new WrapPanel();
        remoteActions.Children.Add(ActionButton("Aplicar timeout", async (_, _) =>
        {
            var label = durationBox.SelectedItem as string;
            if (label is null || !durationValues.TryGetValue(label, out var duration))
            {
                MessageBox.Show(this, "Elegí una duración de timeout válida.");
                return;
            }
            await ExecuteManualModerationAction("timeout", duration, false);
        }, true));
        remoteActions.Children.Add(ActionButton("Ban permanente", async (_, _) => await ExecuteManualModerationAction("ban permanente", null, false)));
        remoteActions.Children.Add(ActionButton("Quitar ban / timeout", async (_, _) => await ExecuteManualModerationAction("desbanear", null, true)));
        remotePanel.Children.Add(remoteActions);
        remotePanel.Children.Add(Label("Si Twitch rechaza la solicitud, la aplicación registra el tipo de error pero nunca presenta la sanción como exitosa. No se ejecutan sanciones por coincidencias regex.", 12, "#98A4BE"));
        root.Children.Add(remotePanel);
        var auto=Panel("Mensajes automáticos programados");auto.Margin=new Thickness(0,12,0,0);
        var autoList=new ListBox{Height=130};long? editingAutomationId=null;
        void ReloadAuto(){autoList.ItemsSource=store.GetAutomationRules().Select(a=>string.Format("{0} · {1} · cada {2} min · {3}",a.Id,a.Name,a.IntervalMinutes,a.Enabled?"activo":"pausado")).ToList();}
        ReloadAuto();auto.Children.Add(autoList);
        var autoName=TextField("Nombre del timer");var autoMessage=TextField("Mensaje programado");var autoInterval=TextField("Intervalo en minutos (mín. 5)");autoInterval.Text="30";
        var autoEnabled=new CheckBox{Content="Activar al guardar",IsChecked=false,Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,8)};
        autoList.SelectionChanged+=(_,_)=>{if(autoList.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split('·')[0].Trim());var selected=store.GetAutomationRules().FirstOrDefault(a=>a.Id==selectedId);if(selected is null)return;editingAutomationId=selected.Id;autoName.Text=selected.Name;autoMessage.Text=selected.Message;autoInterval.Text=selected.IntervalMinutes.ToString();autoEnabled.IsChecked=selected.Enabled;}catch{}};
        auto.Children.Add(autoName);auto.Children.Add(autoMessage);auto.Children.Add(autoInterval);auto.Children.Add(autoEnabled);
        auto.Children.Add(ActionButton("Guardar mensaje automático",(_,_)=>{try{store.SaveAutomationRule(editingAutomationId,autoName.Text,autoMessage.Text,int.Parse(autoInterval.Text),autoEnabled.IsChecked==true);ReloadAuto();autoList.SelectedItem=null;editingAutomationId=null;autoName.Clear();autoMessage.Clear();autoInterval.Text="30";autoEnabled.IsChecked=false;}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        auto.Children.Add(ActionButton("Pausar / reactivar seleccionado",(_,_)=>{if(autoList.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split('·')[0].Trim());var selected=store.GetAutomationRules().FirstOrDefault(a=>a.Id==selectedId);if(selected is null)return;store.SaveAutomationRule(selected.Id,selected.Name,selected.Message,selected.IntervalMinutes,!selected.Enabled);ReloadAuto();}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        auto.Children.Add(Label("Los mensajes programados solo se envían cuando Twitch está conectado; sin conexión, el intento se registra como omitido.",11,"#98A4BE"));
        root.Children.Add(auto);return root;
    }

    private async Task ProcessAutomationsAsync()
    {
        foreach(var rule in store.GetAutomationRules().Where(r=>r.Enabled))
        {
            var now=DateTimeOffset.UtcNow;
            if(automationLastRun.TryGetValue(rule.Id,out var last)&&now-last<TimeSpan.FromMinutes(rule.IntervalMinutes))continue;
            automationLastRun[rule.Id]=now;
            if(!twitch.IsConnected){store.Log("Automatización",$"'{rule.Name}' omitido: Twitch desconectado.");continue;}
            try{await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),rule.Message);store.Log("Automatización",$"Mensaje '{rule.Name}' enviado.");}
            catch(Exception ex){store.Log("Automatización",$"'{rule.Name}' falló: {ex.GetType().Name}.");}
        }
    }

    private UIElement BuildAudioPage()
    {
        var root = new StackPanel();
        var panel = Panel("Micrófono, detección de voz y TTS local");
        root.Children.Add(panel);

        var devices = new ComboBox
        {
            ItemsSource = Enumerable.Range(0, WaveIn.DeviceCount).Select(i => $"{i}: {WaveIn.GetCapabilities(i).ProductName}").ToList(),
            SelectedIndex = WaveIn.DeviceCount > 0 ? 0 : -1
        };
        panel.Children.Add(Label("Dispositivo de entrada", 12, "#98A4BE"));
        panel.Children.Add(devices);
        var meter = new ProgressBar { Minimum = 0, Maximum = 100, Height = 16, Margin = new Thickness(0, 8, 0, 12) };
        var meterLabel = Label("Nivel de entrada: N/D", 12, "#98A4BE");
        panel.Children.Add(meter);
        panel.Children.Add(meterLabel);

        var thresholdSlider = new Slider { Minimum = 2, Maximum = 30, Value = voiceActivity.ThresholdPercent, TickFrequency = 1 };
        var thresholdLabel = Label($"Umbral de actividad: {voiceActivity.ThresholdPercent:0}%", 12, "#98A4BE");
        thresholdSlider.ValueChanged += (_, _) =>
        {
            voiceActivity.SetThreshold(thresholdSlider.Value);
            thresholdLabel.Text = $"Umbral de actividad: {voiceActivity.ThresholdPercent:0}%";
        };
        panel.Children.Add(thresholdLabel);
        panel.Children.Add(thresholdSlider);

        var holdSlider = new Slider { Minimum = 100, Maximum = 1200, Value = voiceActivity.ReleaseHold.TotalMilliseconds, TickFrequency = 50 };
        var holdLabel = Label($"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms", 12, "#98A4BE");
        holdSlider.ValueChanged += (_, _) =>
        {
            voiceActivity.SetReleaseHold(TimeSpan.FromMilliseconds(holdSlider.Value));
            holdLabel.Text = $"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms";
        };
        panel.Children.Add(holdLabel);
        panel.Children.Add(holdSlider);

        panel.Children.Add(ActionButton("Iniciar micrófono y detección", (_, _) =>
        {
            try
            {
                StopMicrophone();
                if (WaveIn.DeviceCount == 0)
                {
                    FooterStatus.Text = "No hay micrófonos disponibles.";
                    return;
                }

                voiceActivity.Reset();
                microphone = new WaveInEvent
                {
                    DeviceNumber = Math.Max(0, devices.SelectedIndex),
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                microphone.DataAvailable += (_, e) =>
                {
                    double sum = 0;
                    for (var i = 0; i < e.BytesRecorded - 1; i += 2)
                    {
                        short sample = BitConverter.ToInt16(e.Buffer, i);
                        sum += (double)sample * sample;
                    }
                    var rms = e.BytesRecorded > 0 ? Math.Sqrt(sum / (e.BytesRecorded / 2)) / 32768 : 0;
                    var rawLevel = Math.Clamp(rms * 450, 0, 100);
                    VoiceActivitySnapshot snapshot;
                    try { snapshot = voiceActivity.Process(rawLevel, DateTimeOffset.UtcNow); }
                    catch (ArgumentOutOfRangeException) { return; }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        meter.Value = snapshot.SmoothedLevelPercent;
                        meterLabel.Text = $"Nivel de entrada: {snapshot.SmoothedLevelPercent:0}% · " +
                            (snapshot.IsActive ? "actividad acústica detectada" : "silencio / debajo del umbral");
                        if (!snapshot.StateChanged) return;

                        var state = snapshot.IsActive ? "Talking" : "Idle";
                        store.SetSetting("avatar.state", state);
                        overlay?.SetState(state);
                        FooterStatus.Text = snapshot.IsActive
                            ? "Actividad acústica detectada; el overlay activa su estado Talking."
                            : "Actividad acústica finalizada; el overlay vuelve a Idle.";
                        store.Log("Audio", snapshot.IsActive
                            ? "Señal de entrada por encima del umbral; cue Talking activado."
                            : "Señal de entrada por debajo del umbral durante el tiempo de retención; cue Idle activado.");
                    }));
                };
                microphone.RecordingStopped += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                    meterLabel.Text = "Captura detenida."));
                microphone.StartRecording();
                store.Log("Audio", "Micrófono real y detección acústica iniciados.");
                FooterStatus.Text = "Micrófono activo. Ajustá umbral y retención para tu ambiente.";
            }
            catch (Exception ex)
            {
                FooterStatus.Text = $"Micrófono: {ex.Message}";
                MessageBox.Show(this, ex.Message);
            }
        }, true));

        panel.Children.Add(ActionButton("Detener micrófono", (_, _) =>
        {
            StopMicrophone();
            voiceActivity.Reset();
            meter.Value = 0;
            meterLabel.Text = "Captura detenida.";
            FooterStatus.Text = "Micrófono detenido; cue de avatar restablecido.";
        }));

        var speech = TextField("Texto para sintetizar en voz local");
        speech.Text = "Hola, chat. ¡Gracias por acompañarme!";
        var priority = new ComboBox
        {
            ItemsSource = Enum.GetValues<SpeechPriority>(),
            SelectedItem = SpeechPriority.Normal,
            Width = 180
        };
        panel.Children.Add(Label("Síntesis de voz de Windows (SAPI)", 14, "#F8FAFC", true));
        panel.Children.Add(speech);
        panel.Children.Add(Label("Prioridad de reproducción", 12, "#98A4BE"));
        panel.Children.Add(priority);
        panel.Children.Add(ActionButton("＋ Encolar voz", (_, _) =>
        {
            var selectedPriority = priority.SelectedItem is SpeechPriority selected ? selected : SpeechPriority.Normal;
            if (speechQueue.Enqueue(speech.Text, selectedPriority))
                store.Log("Audio", "Frase agregada a la cola TTS local con prioridad " + selectedPriority + ".");
            else
                FooterStatus.Text = "No se pudo encolar: revisá el texto o el límite de la cola.";
        }, true));
        panel.Children.Add(ActionButton("Vaciar frases pendientes", (_, _) =>
        {
            speechQueue.ClearPending();
            store.Log("Audio", "Cola de voz pendiente vaciada.");
        }));
        root.Children.Add(Label("La cola procesa una frase por vez en un hilo STA separado para que la ventana siga respondiendo. Prioridad Alta se reproduce antes que las frases normales/bajas que aún esperan; no interrumpe la frase que ya está sonando. La detección de micrófono usa amplitud suavizada/retención, no reconocimiento semántico ni lip-sync por fonemas.", 12, "#98A4BE"));
        return root;
    }

    private void StopMicrophone()
    {
        var capture = microphone;
        microphone = null;
        try { capture?.StopRecording(); } catch (InvalidOperationException) { }
        try { capture?.Dispose(); } catch (InvalidOperationException) { }
        if (voiceActivity.IsActive)
        {
            voiceActivity.Reset();
            store.SetSetting("avatar.state", "Idle");
            overlay?.SetState("Idle");
        }
    }

    private UIElement BuildObsPage()
    {
        var root=new StackPanel();var panel=Panel("OBS Studio · WebSocket v5");root.Children.Add(panel);
        panel.Children.Add(Label("Conexión autenticada y control de escenas",19,"#F8FAFC",true));
        panel.Children.Add(Label("Activá WebSocket Server en OBS → Herramientas. La app no registra una conexión hasta completar Hello/Identify. La contraseña se cifra con DPAPI CurrentUser.",13,"#98A4BE"));
        var host=TextField("Host",store.GetSetting("obs.host","127.0.0.1"));
        var port=TextField("Puerto",store.GetSetting("obs.port","4455"));
        var password=new PasswordBox{Password=LoadObsPassword(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        panel.Children.Add(Label("Host",12,"#98A4BE"));panel.Children.Add(host);
        panel.Children.Add(Label("Puerto",12,"#98A4BE"));panel.Children.Add(port);
        panel.Children.Add(Label("Contraseña de WebSocket (si OBS la exige)",12,"#98A4BE"));panel.Children.Add(password);
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Conectar y autenticar",async (_,_)=>{try{store.SetSetting("obs.host",host.Text.Trim());store.SetSetting("obs.port",port.Text.Trim());SaveObsPassword(password.Password);await obs.ConnectAsync(host.Text.Trim(),int.Parse(port.Text),password.Password);var version=await obs.GetVersionAsync();FooterStatus.Text="OBS autenticado · versión "+version;store.Log("OBS","Conexión WebSocket v5 autenticada; versión "+version); }catch(Exception ex){FooterStatus.Text="OBS no conectado: "+ex.Message;store.Log("OBS","Conexión WebSocket fallida: "+ex.GetType().Name);MessageBox.Show(this,"No se pudo autenticar con OBS. Confirmá que WebSocket esté habilitado, el puerto y la contraseña.\n\n"+ex.Message);}} ,true));
        actions.Children.Add(ActionButton("Desconectar",async (_,_)=>await obs.DisconnectAsync()));
        panel.Children.Add(actions);
        var scenes=new ComboBox{MinWidth=260};
        var sceneRow=new WrapPanel();
        sceneRow.Children.Add(ActionButton("Actualizar escenas",async (_,_)=>{try{scenes.ItemsSource=await obs.GetScenesAsync();if(scenes.Items.Count>0)scenes.SelectedIndex=0;FooterStatus.Text="Escenas recibidas de OBS.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        sceneRow.Children.Add(scenes);
        sceneRow.Children.Add(ActionButton("Cambiar escena",async (_,_)=>{try{if(scenes.SelectedItem is not string selected)throw new InvalidOperationException("Primero cargá y elegí una escena.");await obs.SetCurrentSceneAsync(selected);store.Log("OBS","Escena cambiada a "+selected);FooterStatus.Text="Escena activa: "+selected;}catch(Exception ex){MessageBox.Show(this,ex.Message);}} ,true));
        panel.Children.Add(Label("Control de escenas",14,"#F8FAFC",true));panel.Children.Add(sceneRow);
        var overlayPanel=Panel("Avatar overlay para OBS");overlayPanel.Children.Add(Label("El overlay se abre como ventana sin bordes y fondo transparente. En OBS agregá una fuente Captura de ventana y seleccioná “VTuberBot Avatar Overlay”. Doble clic en el overlay alterna Topmost; arrastrá para ubicarlo.",13,"#98A4BE"));
        overlayPanel.Children.Add(ActionButton("Mostrar overlay",(_,_)=>ShowOverlay(),true));root.Children.Add(overlayPanel);
        return root;
    }

    private string LoadObsPassword()
    {
        var path=Path.Combine(secretDirectory,"obs-password.bin");if(!File.Exists(path))return "";
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}
        catch{FooterStatus.Text="No se pudo descifrar la contraseña guardada de OBS.";return "";}
    }
    private void SaveObsPassword(string password)
    {
        var path=Path.Combine(secretDirectory,"obs-password.bin");
        if(string.IsNullOrEmpty(password)){if(File.Exists(path))File.Delete(path);return;}
        File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.CurrentUser));
    }

    private UIElement BuildTelegramPage()
    {
        var root=new DockPanel();var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);
        toolbar.Children.Add(ActionButton("Abrir Telegram Web",async (_,_)=>{if(telegramView is null){FooterStatus.Text="Inicializando WebView2…";return;}try{await telegramView.EnsureCoreWebView2Async();telegramView.Source=new Uri("https://web.telegram.org/");FooterStatus.Text="Telegram Web cargando en WebView2.";}catch(Exception ex){FooterStatus.Text=$"WebView2 no disponible: {ex.Message}";OpenTelegramExternal();}},true));
        toolbar.Children.Add(ActionButton("Abrir en navegador",(_,_)=>OpenTelegramExternal()));
        root.Children.Add(toolbar);telegramView=new WebView2();root.Children.Add(telegramView);
        Loaded+=async (_,_)=>{try{await telegramView.EnsureCoreWebView2Async();telegramView.Source=new Uri("https://web.telegram.org/");}catch{FooterStatus.Text="WebView2 no está disponible. Usá el botón para abrir Telegram en el navegador.";}};return root;
    }
    private void OpenTelegramExternal(){try{Process.Start(new ProcessStartInfo("https://web.telegram.org/"){UseShellExecute=true});store.Log("Telegram","Telegram Web abierto en navegador externo.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}

    private UIElement BuildActivityPage()
    {
        var root=new StackPanel();var panel=Panel("Actividad reciente");root.Children.Add(panel);
        var list=new ListBox{Height=470,ItemsSource=store.GetActivity(250).Select(a=>$"{a.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}  [{a.Module}]  {a.Message}").ToList()};panel.Children.Add(list);
        var row=new WrapPanel();row.Children.Add(ActionButton("Actualizar registro",(_,_)=>Navigate("Actividad y diagnóstico")));
        row.Children.Add(ActionButton("Exportar diagnóstico",(_,_)=>ExportDiagnostics(),true));panel.Children.Add(row);
        panel.Children.Add(Label("El diagnóstico exportado incluye eventos de la app y configuración no secreta; excluye tokens OAuth, cookies de WebView2 y datos binarios de usuario.",12,"#98A4BE"));return root;
    }

    private void ExportDiagnostics()
    {
        var dialog=new SaveFileDialog{Filter="Texto|*.txt",FileName="vtuberbot-diagnostic.txt"};if(dialog.ShowDialog(this)!=true)return;
        var sb=new StringBuilder();sb.AppendLine("VTuberBot — diagnóstico local");sb.AppendLine($"Fecha UTC: {DateTimeOffset.UtcNow:O}");sb.AppendLine($"Versión core: {VtuberApplication.Version}");sb.AppendLine($"Estado core: {application.State}");sb.AppendLine($"SQLite: {application.Database.Path}");sb.AppendLine($"Avatar configurado: {!string.IsNullOrWhiteSpace(avatarPath)&&File.Exists(avatarPath)}");sb.AppendLine($"Twitch conectado: {twitch.IsConnected}");sb.AppendLine($"Dispositivos de entrada: {WaveIn.DeviceCount}");sb.AppendLine("No se exportan tokens, cookies ni contraseñas.");sb.AppendLine();foreach(var a in store.GetActivity(250))sb.AppendLine($"{a.At:O}\t{a.Module}\t{a.Message}");
        File.WriteAllText(dialog.FileName,sb.ToString(),new UTF8Encoding(true));store.Log("Diagnóstico","Informe de diagnóstico exportado.");
    }

    private UIElement BuildSettingsPage()
    {
        var root=new StackPanel();var panel=Panel("Preferencias locales");root.Children.Add(panel);
        var profile=new ComboBox{ItemsSource=new[]{"Ahorro","Equilibrado","Calidad"},SelectedItem=store.GetSetting("performance.profile","Equilibrado")};panel.Children.Add(Label("Perfil de rendimiento",12,"#98A4BE"));panel.Children.Add(profile);
        var reduceMotion=new CheckBox{Content="Reducir movimiento del overlay",IsChecked=store.GetSetting("ui.reduceMotion","false")=="true",Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,8,0,12)};panel.Children.Add(reduceMotion);
        panel.Children.Add(ActionButton("Guardar preferencias",(_,_)=>{store.SetSetting("performance.profile",profile.SelectedItem?.ToString()??"Equilibrado");application.SetPerformanceProfile(Enum.Parse<PerformanceProfile>(profile.SelectedItem?.ToString()??"Equilibrado"));store.SetSetting("ui.reduceMotion",reduceMotion.IsChecked==true?"true":"false");if(overlay is not null)overlay.ReduceMotion=reduceMotion.IsChecked==true;FooterStatus.Text="Preferencias guardadas en SQLite y aplicadas al monitor de rendimiento/overlay.";store.Log("Ajustes","Preferencias locales actualizadas.");},true));
        var privacy=Panel("Privacidad y almacenamiento");privacy.Margin=new Thickness(0,16,0,0);privacy.Children.Add(Label($"Datos locales: {dataDirectory}",12,"#98A4BE"));privacy.Children.Add(Label("Los assets se copian al directorio local. El token de Twitch se cifra con DPAPI CurrentUser. No se incluyen datos personales en el repositorio ni en los paquetes de CI.",12,"#98A4BE"));privacy.Children.Add(ActionButton("Abrir carpeta de datos",(_,_)=>{try{Process.Start(new ProcessStartInfo(dataDirectory){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));root.Children.Add(privacy);
        return root;
    }

    private string LoadTwitchToken()
    {
        var path=Path.Combine(secretDirectory,"twitch-token.bin");if(!File.Exists(path))return "";
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}
        catch{FooterStatus.Text="No se pudo descifrar el token guardado. Volvé a ingresarlo.";return "";}
    }
    private void SaveTwitchToken(string token)
    {
        var path=Path.Combine(secretDirectory,"twitch-token.bin");
        if(string.IsNullOrWhiteSpace(token)){if(File.Exists(path))File.Delete(path);return;}
        var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()),null,DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path,bytes);
    }

    private static Transform BuildLayerTransform(AvatarLayer layer)
    {
        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(layer.Scale, layer.Scale));
        transform.Children.Add(new RotateTransform(layer.RotationDegrees));
        transform.Children.Add(new TranslateTransform(layer.OffsetX, layer.OffsetY));
        return transform;
    }

    private static BitmapImage LoadImage(string path)
    {
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=900;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();return image;
    }
    private static SolidColorBrush Brush(string color)=>(SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    private static TextBlock Label(string text,double size,string color,bool bold=false)=>new(){Text=text,FontSize=size,Foreground=Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
    private static TextBox TextField(string placeholder,string initial="")=>new(){Text=initial,ToolTip=placeholder,MinWidth=220};
    private static Button ActionButton(string text,RoutedEventHandler click,bool primary=false)
    {
        var b=new Button{Content=text,Padding=new Thickness(14,9,14,9),Margin=new Thickness(0,0,8,8),Background=Brush(primary?"#176B85":"#252E46"),BorderBrush=Brush(primary?"#2886A3":"#34415E"),Foreground=Brush("#F8FAFC"),Cursor=System.Windows.Input.Cursors.Hand};b.Click+=click;return b;
    }
    private static StackPanel Panel(string title)
    {
        var p=new StackPanel();p.Children.Add(Label(title,17,"#F8FAFC",true));p.Background=Brush("#151A2A");p.Margin=new Thickness(0,0,0,12);return p;
    }
    private static Border MetricCard(string title,string value,string detail)
    {
        var content=new StackPanel();content.Children.Add(Label(title,10,"#98A4BE",true));content.Children.Add(Label(value,21,"#F8FAFC",true));content.Children.Add(Label(detail,11,"#69D8F2"));
        return new Border{Background=Brush("#151A2A"),BorderBrush=Brush("#293650"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Margin=new Thickness(0,0,10,0),Child=content};
    }
    private static StackPanel StatusRow(string name,string detail,bool ok)
    {
        var p=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,5)};
        p.Children.Add(Label(ok?"●":"○",12,ok?"#75E0BF":"#E7B56A",true));
        p.Children.Add(Label(name,13,"#F8FAFC",true));
        p.Children.Add(Label(string.IsNullOrWhiteSpace(detail)?(ok?"Configurado":"No disponible"):detail,12,"#98A4BE"));
        return p;
    }
}
