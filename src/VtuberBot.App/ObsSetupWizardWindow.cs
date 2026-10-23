using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VtuberBot.App;

/// <summary>
/// Reusable, progress-saving OBS setup guide with original, clickable diagrams.
/// The diagrams are explanatory illustrations, not screenshots of the user's machine.
/// </summary>
public sealed class ObsSetupWizardWindow : Window
{
    private sealed record GuideStep(
        string Title,
        string Subtitle,
        string[] Instructions,
        (double X, double Y, string Caption, string Detail)[] Hotspots);

    private static readonly GuideStep[] Steps =
    [
        new("1. Preparar OBS", "Instalá OBS Studio desde su sitio oficial. Esta guía se puede volver a abrir cuando la necesites.",
        [
            "Abrí la página oficial de descarga y elegí Windows. No descargues instaladores de anuncios ni sitios de terceros.",
            "Ejecutá el instalador con las opciones recomendadas. OBS es gratuito; no necesita una tarjeta de crédito.",
            "Cuando termine, dejá OBS instalado. Después volverás a esta guía para configurar las escenas."
        ],
        [
            (90, 105, "Descarga oficial", "La página oficial ofrece el instalador de OBS Studio para Windows."),
            (320, 120, "Instalador", "Usá el instalador de Windows descargado desde obsproject.com."),
            (560, 100, "Siguiente", "Una vez instalado, avanzá al paso 2 para abrir OBS.")
        ]),
        new("2. Abrir OBS y preparar una escena", "Primero comprobá que OBS abre por sí solo antes de conectar VTuberBot.",
        [
            "Pulsa «Abrir OBS Studio» en esta guía. Si ya está abierto, podés dejarlo abierto.",
            "En OBS, buscá el panel «Escenas» abajo a la izquierda. Crea una escena con el botón + y poné un nombre, por ejemplo «Directo».",
            "No inicies una transmisión todavía. Primero vamos a revisar la imagen y el audio con una grabación local."
        ],
        [
            (90, 80, "Escenas", "Las escenas son diseños diferentes. Creá una llamada Directo o elegí una que ya tengas."),
            (310, 100, "Vista previa", "Esta zona muestra lo que verá tu audiencia. Revisá que nada importante quede fuera del cuadro."),
            (590, 100, "Controles", "Por ahora no pulses Iniciar transmisión. Probaremos primero una grabación.")
        ]),
        new("3. Agregar el avatar y el micrófono", "La vista previa debe mostrar lo que querés transmitir antes de tocar opciones avanzadas.",
        [
            "En «Fuentes», pulsa + para agregar la fuente adecuada: «Captura de ventana» para una ventana, «Captura de juego» para juegos compatibles o «Dispositivo de captura de video» si usás una cámara.",
            "Para mostrar el avatar de VTuberBot, volvé a la aplicación y usa «Mostrar overlay» en la página OBS. En OBS agregá una fuente «Captura de ventana» y elegí «VTuberBot Avatar Overlay».",
            "En «Mezclador de audio», hablá cerca del micrófono y comprobá que el medidor se mueva. Si no se mueve, elegí el dispositivo correcto en Ajustes → Audio.",
            "Si el avatar tapa el juego, redimensioná o mové su fuente en la vista previa. Las fuentes más arriba en la lista quedan delante de las inferiores."
        ],
        [
            (90, 110, "Fuentes +", "El botón + abre tipos de fuente. Elegí Captura de ventana para el overlay."),
            (320, 105, "Avatar", "La vista previa permite mover y escalar el overlay arrastrando su marco."),
            (570, 105, "Audio", "El medidor del micrófono debe reaccionar cuando hablás; el medidor no debe quedar en rojo constantemente.")
        ]),
        new("4. Conectar OBS con VTuberBot", "Habilitá el servidor WebSocket local de OBS y después guardá los mismos datos en VTuberBot.",
        [
            "En OBS abre Herramientas → WebSocket Server Settings (el nombre puede aparecer traducido como Ajustes del servidor WebSocket). Activá el servidor WebSocket.",
            "Conservá el puerto 4455 salvo que ya uses otro. Crea una contraseña fuerte y guardala en un lugar privado; no la pegues en chats ni capturas públicas.",
            "Volvé a VTuberBot → OBS → «Opciones avanzadas de OBS (otra ventana)». Introducí Host 127.0.0.1, Puerto 4455 y la contraseña en el campo protegido. Pulsa «Guardar y conectar (primera vez)».",
            "Regresá a esta guía y pulsa «Comprobar conexión real». El estado solo será correcto si VTuberBot consigue autenticar y consultar la versión real de OBS."
        ],
        [
            (95, 95, "Herramientas", "En OBS, el menú Herramientas contiene los ajustes del servidor WebSocket."),
            (350, 110, "Servidor activo", "Activá el servidor y verificá el puerto que aparece. Normalmente es 4455."),
            (585, 105, "VTuberBot", "Ingresá el mismo puerto y contraseña en la página OBS de VTuberBot. El programa cifra la contraseña con Windows DPAPI.")
        ]),
        new("5. Elegir Twitch o YouTube", "Abrí el panel oficial de la plataforma y guardá la clave únicamente dentro de VTuberBot.",
        [
            "Elegí Twitch o YouTube en el selector de esta guía y abre el panel oficial. Iniciá sesión directamente en esa página.",
            "Buscá la clave de transmisión en el panel de creador. No envíes una captura donde se vea la clave: cualquier persona que la obtenga podría transmitir en tu canal.",
            "Volvé a VTuberBot → OBS y transmisiones. Elegí la plataforma, pegá la clave en «Clave de transmisión» y pulsa «Guardar clave cifrada». El campo está oculto y la app no registra la clave en sus logs.",
            "Configurá el destino solo cuando OBS esté conectado. Si cambiás la clave después, guardá la nueva; no hace falta compartirla con nadie."
        ],
        [
            (100, 90, "Plataforma", "Abrí el panel oficial de Twitch o YouTube Studio según la plataforma que hayas elegido."),
            (330, 105, "Clave privada", "La clave es un secreto. No la pegues en el chat, esta conversación, capturas o archivos del repositorio."),
            (575, 100, "Guardar", "Pegá la clave solo en la caja protegida de la página OBS de VTuberBot y guardala cifrada.")
        ]),
        new("6. Ajustar el rendimiento del PC", "Para empezar en tu Ryzen 5 5600G y 16 GB de RAM, el perfil conservador es 1280×720 a 30 FPS.",
        [
            "Este perfil cambia el lienzo y la salida globales de OBS a 1280×720 y 30 FPS. Puede afectar todas las escenas, por eso el botón vuelve a pedir confirmación.",
            "La guía no puede conocer tu GPU, velocidad de subida ni el coste de todos tus filtros. 720p30 es un punto de partida, no una medición ni una garantía de FPS.",
            "La primera vez, el botón «Iniciar y preparar OBS» del modo sencillo puede aplicar y verificar el perfil 720p30 con tu permiso, si OBS está conectado y no está transmitiendo ni grabando. También podés usar «Aplicar perfil 720p / 30 FPS» en las opciones avanzadas. Si una escena depende de plugins externos, no uses el modo de inicio que los omite.",
            "Si después de probar todo funciona fluido, podés evaluar 1080p o más FPS. Si hay tirones, bajá primero la resolución de salida, los filtros costosos y la carga del juego."
        ],
        [
            (100, 95, "720p", "1280 × 720 reduce el coste de codificar frente a 1080p y suele ser una base sencilla para empezar."),
            (330, 105, "30 FPS", "30 cuadros por segundo es un objetivo prudente; no garantiza un uso de CPU/GPU específico."),
            (590, 95, "Aplicar", "La acción cambia los ajustes reales de OBS y solicita confirmación antes de enviarlos.")
        ]),
        new("7. Hacer una prueba antes del directo", "Comprobá la imagen, la voz y la grabación local. No emitas en vivo hasta que estés conforme.",
        [
            "En OBS revisá la vista previa completa: avatar, juego/ventana, encuadre, escenas y textos. Si algo no aparece, revisá la lista y el orden de Fuentes.",
            "Elegí una carpeta de grabación en Ajustes → Salida. Asegurate de tener espacio libre suficiente.",
            "Pulsa «Iniciar grabación», hablá durante 20–30 segundos y mové la escena si corresponde. Después detené la grabación y reproducí el archivo.",
            "Escuchá si el micrófono se entiende, si el audio del juego tapa tu voz, si el avatar queda cortado y si hay cuadros congelados. Corregí lo necesario y volvé a grabar.",
            "Cuando la prueba esté bien, ya podés configurar o iniciar el directo manualmente. Esta guía no inicia la transmisión por sí sola."
        ],
        [
            (110, 100, "Vista previa", "La imagen que ves aquí debe coincidir con lo que querés enviar al público."),
            (330, 100, "Grabación", "Hacé una grabación local corta para revisar el resultado sin salir en vivo."),
            (585, 100, "Reproducir", "Abrí el archivo grabado y escuchalo antes de iniciar un directo real.")
        ])
    ];

    private readonly Func<string, string> readSetting;
    private readonly Action<string, string> saveSetting;
    private readonly Func<Task> openObsAsync;
    private readonly Func<Task<bool>> testConnectionAsync;
    private readonly Action navigateToObsSettings;
    private readonly Func<Task<string>> apply720p30Async;
    private readonly Action showAvatarOverlay;
    private readonly StackPanel navigation = new();
    private readonly TextBlock titleText = new();
    private readonly TextBlock subtitleText = new();
    private readonly TextBlock hotspotDetail = new();
    private readonly TextBlock statusText = new();
    private readonly StackPanel instructionPanel = new();
    private readonly Grid diagramHost = new();
    private readonly WrapPanel actionPanel = new();
    private readonly ProgressBar progressBar = new();
    private readonly CheckBox completedCheck = new();
    private readonly TextBlock evidenceText = new();
    private readonly Button backButton = new();
    private readonly Button nextButton = new();
    private ComboBox? platformSelector;
    private bool isRenderingStep;
    private readonly HashSet<int> completedSteps = [];
    private readonly HashSet<int> verifiedSteps = [];
    private int currentStep;

    public ObsSetupWizardWindow(
        Func<string, string> readSetting,
        Action<string, string> saveSetting,
        Func<Task> openObsAsync,
        Func<Task<bool>> testConnectionAsync,
        Action navigateToObsSettings,
        Func<Task<string>> apply720p30Async,
        Action showAvatarOverlay,
        Window owner)
    {
        this.readSetting = readSetting;
        this.saveSetting = saveSetting;
        this.openObsAsync = openObsAsync;
        this.testConnectionAsync = testConnectionAsync;
        this.navigateToObsSettings = navigateToObsSettings;
        this.apply720p30Async = apply720p30Async;
        this.showAvatarOverlay = showAvatarOverlay;

        Owner = owner;
        Title = "VTuberBot · Asistente visual de configuración de OBS";
        Width = 1110;
        Height = 790;
        MinWidth = 900;
        MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0E17");
        Foreground = Brush("#F1F5F9");

        foreach (var value in readSetting("help.obsWizard.completed").Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(value, out var index) && index >= 0 && index < Steps.Length &&
                !ObsSetupStepEvidencePolicy.RequiresApplicationVerification(index))
                completedSteps.Add(index);

        foreach (var value in readSetting("help.obsWizard.verified").Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(value, out var index) && index >= 0 && index < Steps.Length &&
                ObsSetupStepEvidencePolicy.RequiresApplicationVerification(index))
                verifiedSteps.Add(index);

        currentStep = int.TryParse(readSetting("help.obsWizard.step"), out var savedStep)
            ? Math.Clamp(savedStep, 0, Steps.Length - 1)
            : 0;
        BuildLayout();
        RenderCurrentStep();
    }

    private void BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Content = root;

        var header = new Border
        {
            Background = Brush("#101423"),
            BorderBrush = Brush("#25304A"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 12, 22, 12),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "OBS · CONFIGURACIÓN GUIADA", FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Brush("#69D8F2") },
                    new TextBlock { Text = "Paso a paso · ilustraciones clicables · progreso guardado en este equipo", FontSize = 12, Foreground = Brush("#98A4BE"), Margin = new Thickness(0, 3, 0, 0) }
                }
            }
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(258) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var side = new Border
        {
            Background = Brush("#101423"),
            BorderBrush = Brush("#25304A"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(13),
            Child = new DockPanel()
        };
        var sideDock = (DockPanel)side.Child;
        var progressContent = new StackPanel { Margin = new Thickness(5, 4, 5, 14) };
        progressContent.Children.Add(new TextBlock { Text = "TU PROGRESO", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush("#98A4BE"), Margin = new Thickness(0, 0, 0, 7) });
        progressBar.Minimum = 0;
        progressBar.Maximum = Steps.Length;
        progressBar.Height = 7;
        progressBar.Foreground = Brush("#69D8F2");
        progressBar.Background = Brush("#252E46");
        progressContent.Children.Add(progressBar);
        DockPanel.SetDock(progressContent, Dock.Top);
        sideDock.Children.Add(progressContent);
        navigation.Margin = new Thickness(0, 0, 0, 8);
        var navScroll = new ScrollViewer { Content = navigation, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sideDock.Children.Add(navScroll);
        var sideFooter = new TextBlock
        {
            Text = "No compartas claves ni contraseñas. La app verifica la conexión y el perfil solo cuando OBS responde; los demás pasos se confirman manualmente.",
            Foreground = Brush("#E7B56A"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(5, 12, 5, 2)
        };
        DockPanel.SetDock(sideFooter, Dock.Bottom);
        sideDock.Children.Insert(0, sideFooter);
        Grid.SetColumn(side, 0);
        body.Children.Add(side);

        var right = new Grid { Margin = new Thickness(22, 17, 22, 12) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        titleText.FontSize = 23;
        titleText.FontWeight = FontWeights.Bold;
        titleText.Foreground = Brush("#F8FAFC");
        Grid.SetRow(titleText, 0);
        right.Children.Add(titleText);

        subtitleText.FontSize = 13;
        subtitleText.Foreground = Brush("#98A4BE");
        subtitleText.TextWrapping = TextWrapping.Wrap;
        subtitleText.Margin = new Thickness(0, 4, 0, 10);
        Grid.SetRow(subtitleText, 1);
        right.Children.Add(subtitleText);

        var visual = new Border
        {
            Background = Brush("#0D121E"),
            BorderBrush = Brush("#2B3854"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(8),
            Child = diagramHost,
            Margin = new Thickness(0, 0, 0, 9)
        };
        diagramHost.Height = 235;
        Grid.SetRow(visual, 2);
        right.Children.Add(visual);

        var lower = new Grid();
        lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        lower.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        lower.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        lower.Children.Add(hotspotDetail);
        hotspotDetail.Foreground = Brush("#69D8F2");
        hotspotDetail.FontSize = 12;
        hotspotDetail.TextWrapping = TextWrapping.Wrap;
        hotspotDetail.Margin = new Thickness(2, 1, 2, 9);
        Grid.SetRow(hotspotDetail, 0);

        var instructionScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = instructionPanel, Margin = new Thickness(0, 0, 0, 8) };
        instructionPanel.Margin = new Thickness(0, 0, 6, 0);
        Grid.SetRow(instructionScroll, 1);
        lower.Children.Add(instructionScroll);

        completedCheck.Content = "Confirmo que lo completé manualmente";
        completedCheck.Foreground = Brush("#F1F5F9");
        completedCheck.Margin = new Thickness(2, 4, 2, 3);
        completedCheck.Checked += (_, _) => { if (!isRenderingStep) SetStepCompleted(true); };
        completedCheck.Unchecked += (_, _) => { if (!isRenderingStep) SetStepCompleted(false); };
        Grid.SetRow(completedCheck, 2);
        lower.Children.Add(completedCheck);

        evidenceText.Foreground = Brush("#E7B56A");
        evidenceText.FontSize = 11;
        evidenceText.TextWrapping = TextWrapping.Wrap;
        evidenceText.Margin = new Thickness(2, 2, 6, 6);
        Grid.SetRow(evidenceText, 3);
        lower.Children.Add(evidenceText);

        var footer = new DockPanel { LastChildFill = false };
        actionPanel.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(actionPanel, Dock.Top);
        footer.Children.Add(actionPanel);

        var navButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        backButton.Content = "← Anterior";
        StyleButton(backButton, false);
        backButton.Click += (_, _) => ChangeStep(currentStep - 1);
        navButtons.Children.Add(backButton);
        nextButton.Content = "Siguiente →";
        StyleButton(nextButton, true);
        nextButton.Click += (_, _) => ChangeStep(currentStep + 1);
        navButtons.Children.Add(nextButton);
        var closeButton = NewButton("Cerrar (guardar progreso)", false);
        closeButton.Click += (_, _) => Close();
        navButtons.Children.Add(closeButton);
        DockPanel.SetDock(navButtons, Dock.Bottom);
        footer.Children.Add(navButtons);

        statusText.Foreground = Brush("#98A4BE");
        statusText.FontSize = 11;
        statusText.TextWrapping = TextWrapping.Wrap;
        statusText.Margin = new Thickness(0, 5, 0, 0);
        DockPanel.SetDock(statusText, Dock.Bottom);
        footer.Children.Add(statusText);
        Grid.SetRow(footer, 4);
        right.Children.Add(footer);

        Grid.SetRow(lower, 3);
        right.Children.Add(lower);
    }

    private void RenderCurrentStep()
    {
        var step = Steps[currentStep];
        titleText.Text = step.Title;
        subtitleText.Text = step.Subtitle;
        backButton.IsEnabled = currentStep > 0;
        nextButton.IsEnabled = currentStep < Steps.Length - 1;
        nextButton.Content = currentStep == Steps.Length - 1 ? "Último paso" : "Siguiente →";

        diagramHost.Children.Clear();
        diagramHost.Children.Add(new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = BuildDiagram(currentStep)
        });

        hotspotDetail.Text = "Haz clic en los marcadores numerados del diagrama para ver qué representa cada zona.";
        instructionPanel.Children.Clear();
        for (var i = 0; i < step.Instructions.Length; i++)
        {
            var text = new TextBlock
            {
                Text = (i + 1) + ". " + step.Instructions[i],
                FontSize = 13,
                Foreground = Brush("#E2E8F0"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(1, 0, 6, 10)
            };
            instructionPanel.Children.Add(text);
        }
        actionPanel.Children.Clear();
        platformSelector = null;
        AddStepActions(currentStep);
        RefreshEvidenceUi();
        saveSetting("help.obsWizard.step", currentStep.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private Canvas BuildDiagram(int stepIndex)
    {
        var canvas = new Canvas { Width = 700, Height = 230, Background = Brush("#0B0E17") };
        AddPanel(canvas, 5, 5, 690, 24, "OBS STUDIO  ·  Ilustración orientativa, no captura real", "#1F2A40", "#40516F", 11);
        AddPanel(canvas, 8, 34, 126, 184, stepIndex is 0 ? "Descarga\n\nWindows\n\nInstalador\n\nSiguiente" : "Escenas\n\nFuentes\n\nTransiciones\n\nMezclador", "#151A2A", "#34415E", 12);
        AddPanel(canvas, 142, 34, 386, 184, string.Empty, "#101525", "#34415E", 12);
        AddPanel(canvas, 536, 34, 156, 184, stepIndex switch
        {
            3 => "Ajustes WebSocket\n\nServidor activo\n\nPuerto 4455\n\nAutenticación",
            4 => "Panel de creador\n\nClave privada\n\nDestino",
            5 => "Vídeo\n\n1280 × 720\n\n30 FPS",
            6 => "Iniciar grabación\n\nDetener grabación\n\nRevisar archivo",
            _ => "Controles\n\nIniciar directo\n\nIniciar grabación\n\nAjustes"
        }, "#151A2A", "#34415E", 11);

        var centerText = stepIndex switch
        {
            0 => "obsproject.com\n\nDescarga oficial para Windows",
            1 => "Proyecto OBS\n\nCrear o elegir escena",
            2 => "VISTA PREVIA\n\nAvatar + ventana/juego",
            3 => "Servidor WebSocket\n\nActivado · Puerto 4455",
            4 => "Twitch o YouTube\n\nGuardar clave de forma privada",
            5 => "PERFIL DE INICIO\n\n1280 × 720  ·  30 FPS",
            _ => "VISTA PREVIA FINAL\n\nAvatar · imagen · audio"
        };
        AddPanel(canvas, 154, 46, 362, 108, centerText, "#121C30", "#40516F", 16);
        AddPanel(canvas, 154, 162, 362, 43, stepIndex == 2 ? "Fuentes: Captura de ventana   ·   Audio: micrófono" : "El ejemplo visual cambia con el paso seleccionado.", "#1A2438", "#34415E", 10);

        var markers = Steps[stepIndex].Hotspots;
        for (var i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
            AddMarker(canvas, marker.X, marker.Y, i + 1, marker.Caption, marker.Detail);
        }
        return canvas;
    }

    private static void AddPanel(Canvas canvas, double x, double y, double width, double height, string text, string background, string borderColor, double fontSize)
    {
        var panel = new Border
        {
            Width = width,
            Height = height,
            Background = Brush(background),
            BorderBrush = Brush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Child = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = Brush("#DDE7F5"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        Canvas.SetLeft(panel, x);
        Canvas.SetTop(panel, y);
        canvas.Children.Add(panel);
    }

    private void AddMarker(Canvas canvas, double x, double y, int number, string caption, string detail)
    {
        var marker = new Border
        {
            Width = 28,
            Height = 28,
            Background = Brush("#69D8F2"),
            BorderBrush = Brush("#E0FBFF"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(15),
            Cursor = Cursors.Hand,
            ToolTip = caption,
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brush("#0B0E17"),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        marker.MouseLeftButtonUp += (_, e) =>
        {
            hotspotDetail.Text = caption + ": " + detail;
            e.Handled = true;
        };
        Canvas.SetLeft(marker, x);
        Canvas.SetTop(marker, y);
        canvas.Children.Add(marker);
    }

    private void AddStepActions(int stepIndex)
    {
        switch (stepIndex)
        {
            case 0:
                actionPanel.Children.Add(NewButton("Abrir descarga oficial de OBS", true, () => OpenUrl("https://obsproject.com/download")));
                actionPanel.Children.Add(NewButton("Copiar enlace", false, () => CopyText("https://obsproject.com/download")));
                break;
            case 1:
                actionPanel.Children.Add(NewButton("Abrir OBS Studio", true, asyncAction: () => RunAsync(openObsAsync, "OBS abierto o solicitud enviada.")));
                break;
            case 2:
                actionPanel.Children.Add(NewButton("Mostrar overlay de VTuberBot", true, () =>
                {
                    showAvatarOverlay();
                    statusText.Text = "Se solicitó mostrar el overlay. En OBS todavía tenés que añadirlo como Captura de ventana.";
                }));
                actionPanel.Children.Add(NewButton("Ir a la página OBS", false, navigateToObsSettings));
                break;
            case 3:
                actionPanel.Children.Add(NewButton("Ir a configuración OBS", true, () =>
                {
                    navigateToObsSettings();
                    statusText.Text = "En la página OBS, guardá host/puerto/contraseña y pulsa «Guardar y conectar». Después volvés aquí para comprobar.";
                }));
                actionPanel.Children.Add(NewButton("Copiar host y puerto", false, () => CopyText("Host: 127.0.0.1\r\nPuerto WebSocket de OBS: 4455")));
                actionPanel.Children.Add(NewButton("Comprobar conexión real", false, asyncAction: async () =>
                {
                    try
                    {
                        var result = await testConnectionAsync();
                        SetStepVerified(ObsSetupStepEvidencePolicy.ObsConnectionStep, result);
                        statusText.Text = result
                            ? "Conexión real comprobada: VTuberBot autenticó con OBS y leyó su versión. Esta prueba puede caducar si se desconecta."
                            : "Conexión no comprobada. Revisá OBS, el puerto y la contraseña; el paso sigue pendiente.";
                    }
                    catch (Exception exception)
                    {
                        SetStepVerified(ObsSetupStepEvidencePolicy.ObsConnectionStep, false);
                        statusText.Text = "No se pudo comprobar la conexión real: " + exception.Message;
                    }
                }));
                break;
            case 4:
                platformSelector = new ComboBox
                {
                    ItemsSource = new[] { "Twitch", "YouTube" },
                    SelectedItem = readSetting("help.obsWizard.platform") is "YouTube" ? "YouTube" : "Twitch",
                    MinWidth = 120,
                    Margin = new Thickness(0, 0, 8, 7),
                    Padding = new Thickness(7)
                };
                var platformControl = platformSelector;
                platformControl.SelectionChanged += (_, _) => saveSetting("help.obsWizard.platform", platformControl.SelectedItem?.ToString() == "YouTube" ? "YouTube" : "Twitch");
                actionPanel.Children.Add(platformSelector);
                actionPanel.Children.Add(NewButton("Abrir panel oficial", true, () =>
                {
                    var url = platformSelector?.SelectedItem?.ToString() == "YouTube"
                        ? "https://studio.youtube.com/"
                        : "https://dashboard.twitch.tv/settings/stream";
                    OpenUrl(url);
                }));
                actionPanel.Children.Add(NewButton("Ir a OBS en VTuberBot", false, navigateToObsSettings));
                break;
            case 5:
                actionPanel.Children.Add(NewButton("Ir a página OBS", false, navigateToObsSettings));
                actionPanel.Children.Add(NewButton("Aplicar 720p / 30 FPS", true, asyncAction: async () =>
                {
                    if (MessageBox.Show(this,
                        "Se cambiarán el lienzo y la salida global de OBS a 1280×720 y 30 FPS. Puede afectar todas las escenas. ¿Querés continuar?",
                        "Confirmar perfil de OBS", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;

                    try
                    {
                        var result = await apply720p30Async();
                        SetStepVerified(ObsSetupStepEvidencePolicy.VideoProfileStep, true);
                        statusText.Text = "OBS aceptó la aplicación del perfil 720p/30 FPS. " + result;
                    }
                    catch (Exception exception)
                    {
                        SetStepVerified(ObsSetupStepEvidencePolicy.VideoProfileStep, false);
                        statusText.Text = "No se aplicó el perfil a OBS: " + exception.Message;
                    }
                }));
                break;
            case 6:
                actionPanel.Children.Add(NewButton("Ir a página OBS", true, navigateToObsSettings));
                actionPanel.Children.Add(NewButton("Mostrar overlay de VTuberBot", false, showAvatarOverlay));
                break;
        }
    }

    private async Task RunAsync(Func<Task> operation, string successMessage)
    {
        try
        {
            await operation();
            statusText.Text = successMessage;
        }
        catch (Exception exception)
        {
            statusText.Text = "No se completó la acción: " + exception.Message;
        }
    }

    private void SetStepCompleted(bool completed)
    {
        if (ObsSetupStepEvidencePolicy.RequiresApplicationVerification(currentStep))
        {
            statusText.Text = "Este paso requiere una comprobación real de VTuberBot; una confirmación manual no cuenta como verificación.";
            isRenderingStep = true;
            completedCheck.IsChecked = verifiedSteps.Contains(currentStep);
            isRenderingStep = false;
            return;
        }

        if (completed) completedSteps.Add(currentStep);
        else completedSteps.Remove(currentStep);

        saveSetting("help.obsWizard.completed", string.Join(",", completedSteps.Order()));
        RefreshEvidenceUi();
    }

    private void SetStepVerified(int stepIndex, bool verified)
    {
        if (!ObsSetupStepEvidencePolicy.RequiresApplicationVerification(stepIndex))
            throw new ArgumentOutOfRangeException(nameof(stepIndex), "Este paso no tiene una comprobación automática definida.");

        if (verified) verifiedSteps.Add(stepIndex);
        else verifiedSteps.Remove(stepIndex);

        saveSetting("help.obsWizard.verified", string.Join(",", verifiedSteps.Order()));
        RefreshEvidenceUi();
    }

    private void RefreshEvidenceUi()
    {
        var requiresVerification = ObsSetupStepEvidencePolicy.RequiresApplicationVerification(currentStep);
        var state = ObsSetupStepEvidencePolicy.Resolve(
            currentStep,
            completedSteps.Contains(currentStep),
            verifiedSteps.Contains(currentStep));

        isRenderingStep = true;
        completedCheck.IsEnabled = !requiresVerification;
        completedCheck.Content = requiresVerification
            ? "Estado de verificación (lo confirma la aplicación)"
            : "Confirmo que lo completé manualmente (la app no puede comprobarlo)";
        completedCheck.IsChecked = state == ObsSetupStepEvidenceState.VerifiedByApplication ||
                                   state == ObsSetupStepEvidenceState.ConfirmedManually;
        isRenderingStep = false;

        evidenceText.Text = state switch
        {
            ObsSetupStepEvidenceState.VerifiedByApplication when currentStep == ObsSetupStepEvidencePolicy.ObsConnectionStep =>
                "Verificación guardada: VTuberBot autenticó con OBS y leyó la versión en la última comprobación. Volvé a comprobar antes de operar; no garantiza que siga conectado.",
            ObsSetupStepEvidenceState.VerifiedByApplication =>
                "Verificación guardada: OBS aceptó el perfil 720p/30 FPS en la última aplicación. Esto no prueba la calidad de una grabación ni de un directo.",
            ObsSetupStepEvidenceState.ConfirmedManually =>
                "Confirmado por vos, manualmente. VTuberBot guarda esta confirmación, pero no ha comprobado automáticamente el resultado externo.",
            _ when requiresVerification && currentStep == ObsSetupStepEvidencePolicy.ObsConnectionStep =>
                "Pendiente de verificación: guardá host/puerto/contraseña y usá «Comprobar conexión real». Tildar una casilla no valida la conexión.",
            _ when requiresVerification =>
                "Pendiente de verificación: aplicá el perfil desde este asistente y esperá una respuesta satisfactoria de OBS. Se pedirá confirmación antes de cambiarlo.",
            _ => "Pendiente de confirmación manual: marcá la casilla solo después de terminar los pasos de esta pantalla."
        };

        var manualCount = completedSteps.Count;
        var automaticCount = verifiedSteps.Count;
        progressBar.Value = manualCount + automaticCount;
        statusText.Text = $"Progreso guardado: {automaticCount}/2 comprobaciones de la aplicación verificadas y {manualCount}/5 pasos confirmados manualmente. Las confirmaciones manuales no prueban conexiones externas.";
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        navigation.Children.Clear();
        for (var i = 0; i < Steps.Length; i++)
        {
            var index = i;
            var state = ObsSetupStepEvidencePolicy.Resolve(
                i,
                completedSteps.Contains(i),
                verifiedSteps.Contains(i));
            var prefix = state switch
            {
                ObsSetupStepEvidenceState.VerifiedByApplication => "✓",
                ObsSetupStepEvidenceState.ConfirmedManually => "M✓",
                _ => (i + 1).ToString("00")
            };
            var label = Steps[i].Title;
            var space = label.IndexOf(' ');
            var button = NewButton(prefix + "  " + (space >= 0 ? label[(space + 1)..] : label), i == currentStep);
            button.ToolTip = state switch
            {
                ObsSetupStepEvidenceState.VerifiedByApplication => "Comprobado por VTuberBot (resultado de la última prueba).",
                ObsSetupStepEvidenceState.ConfirmedManually => "Confirmado manualmente por el usuario; no comprobado por la aplicación.",
                _ => "Pendiente."
            };
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0, 0, 0, 7);
            button.Click += (_, _) => ChangeStep(index);
            navigation.Children.Add(button);
        }
    }

    private void ChangeStep(int target)
    {
        if (target < 0 || target >= Steps.Length)
            return;
        currentStep = target;
        saveSetting("help.obsWizard.step", currentStep.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RenderCurrentStep();
    }

    private Button NewButton(string label, bool primary, Action? action = null, Func<Task>? asyncAction = null)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 7, 7),
            MinHeight = 34,
            Background = Brush(primary ? "#176B85" : "#252E46"),
            BorderBrush = Brush(primary ? "#2886A3" : "#34415E"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#F8FAFC"),
            Cursor = Cursors.Hand,
            FontSize = 12
        };
        if (action is not null || asyncAction is not null)
            button.Click += async (_, _) =>
            {
                try
                {
                    if (asyncAction is not null)
                        await asyncAction();
                    else
                        action?.Invoke();
                }
                catch (Exception exception)
                {
                    statusText.Text = "No se completó la acción: " + exception.Message;
                }
            };
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.Padding = new Thickness(12, 8, 12, 8);
        button.Margin = new Thickness(0, 0, 7, 7);
        button.MinHeight = 34;
        button.Background = Brush(primary ? "#176B85" : "#252E46");
        button.BorderBrush = Brush(primary ? "#2886A3" : "#34415E");
        button.Foreground = Brush("#F8FAFC");
        button.Cursor = Cursors.Hand;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void CopyText(string value)
    {
        Clipboard.SetText(value);
        statusText.Text = "Copiado al portapapeles.";
    }

    private static SolidColorBrush Brush(string color) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
}
