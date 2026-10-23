using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtuberBot.App;

public sealed record ObsSimplePreparationResult(
    bool IsReady,
    string Summary,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Beginner-first, modeless OBS window. It deliberately exposes a small number of safe
/// actions and opens help/setup in separate windows so this checklist can remain visible.
/// </summary>
public sealed class ObsSimpleModeWindow : Window
{
    private readonly Func<Action<string>, Task<ObsSimplePreparationResult>> prepareAsync;
    private readonly Func<bool> hasPreparationPermission;
    private readonly Action savePreparationPermission;
    private readonly Action openSetupGuide;
    private readonly Action openAdvancedSettings;
    private readonly Action showAvatarOverlay;
    private readonly StackPanel stepsPanel = new();
    private readonly TextBlock statusText = new();
    private readonly ProgressBar progress = new();
    private readonly Button prepareButton;
    private int preparationRunning;

    public ObsSimpleModeWindow(
        Func<Action<string>, Task<ObsSimplePreparationResult>> prepareAsync,
        Func<bool> hasPreparationPermission,
        Action savePreparationPermission,
        Action openSetupGuide,
        Action openAdvancedSettings,
        Action showAvatarOverlay,
        Window owner)
    {
        this.prepareAsync = prepareAsync;
        this.hasPreparationPermission = hasPreparationPermission;
        this.savePreparationPermission = savePreparationPermission;
        this.openSetupGuide = openSetupGuide;
        this.openAdvancedSettings = openAdvancedSettings;
        this.showAvatarOverlay = showAvatarOverlay;

        Owner = owner;
        Title = "VTuberBot · OBS fácil";
        Width = 800;
        Height = 790;
        MinWidth = 640;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0E17");
        Foreground = Brush("#F8FAFC");

        var body = new StackPanel { Margin = new Thickness(22) };
        body.Children.Add(new TextBlock
        {
            Text = "OBS FÁCIL",
            FontSize = 27,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#69D8F2")
        });
        body.Children.Add(new TextBlock
        {
            Text = "Un botón prepara OBS para empezar. No necesitas entender sus controles avanzados.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#C7D2E5"),
            Margin = new Thickness(0, 5, 0, 14)
        });

        var mainAction = new Border
        {
            Background = Brush("#111B30"),
            BorderBrush = Brush("#33506F"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var mainStack = new StackPanel();
        mainStack.Children.Add(new TextBlock
        {
            Text = "Preparación automática y de bajo consumo",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F8FAFC")
        });
        mainStack.Children.Add(new TextBlock
        {
            Text = "Inicia OBS en modo ligero cuando está cerrado, intenta conectar con tu permiso guardado, aplica 1280×720 a 30 FPS y comprueba que OBS responda. Nunca inicia un directo ni una grabación.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#AEBBD2"),
            Margin = new Thickness(0, 6, 0, 12)
        });
        prepareButton = CreateButton("▶  INICIAR Y PREPARAR OBS", true);
        prepareButton.FontSize = 16;
        prepareButton.MinHeight = 50;
        prepareButton.Click += async (_, _) => await RunPreparationAsync();
        var primaryActions = new WrapPanel();
        primaryActions.Children.Add(prepareButton);
        var prepareHelp = CreateButton("?", false);
        prepareHelp.ToolTip = "¿Qué va a hacer este botón?";
        prepareHelp.Click += (_, _) => ShowHelp(
            "Iniciar y preparar OBS",
            "El botón abre una instancia local de OBS si hace falta, usa el modo ligero en un inicio nuevo, intenta autenticarse con los datos guardados y verifica el perfil 720p30. La primera vez te pide permiso. Si falta WebSocket o queda poca memoria, informa el problema. Nunca inicia ni detiene una transmisión o grabación.");
        primaryActions.Children.Add(prepareHelp);
        mainStack.Children.Add(primaryActions);
        mainAction.Child = mainStack;
        body.Children.Add(mainAction);

        AddFeature(
            body,
            "Protección de recursos",
            "Revisa la memoria libre del sistema y evita abrir otra instancia si queda menos de 1 GiB. El perfil ligero usa 720p/30 FPS y omite plugins externos al iniciar una instancia nueva.",
            "Esto es una protección preventiva, no un límite absoluto de CPU, GPU o RAM. OBS, juegos, navegadores, controladores y otros programas comparten los recursos del PC. Si queda menos de 1 GiB de RAM disponible, VTuberBot no abre una nueva instancia de OBS. Entre 1 y 1,5 GiB muestra una advertencia. Nunca mata procesos para liberar memoria. Si OBS ya estaba abierto, no lo reinicia a la fuerza: eso podría perder estado o ajustes sin guardar.");
        AddFeature(
            body,
            "Primera conexión",
            "La primera vez necesitas habilitar WebSocket en OBS y guardar su contraseña cifrada en VTuberBot.",
            "En OBS abre Herramientas → Ajustes del servidor WebSocket. Activa el servidor y conserva el puerto 4455 salvo que tengas otro. Guarda una contraseña fuerte. En la guía de VTuberBot introduce la misma contraseña en el campo protegido. No pegues claves de transmisión en ese campo: son cosas distintas. Después de esta preparación inicial, VTuberBot puede reutilizar la autorización local guardada.");
        AddFeature(
            body,
            "Avatar en OBS",
            "Puedes abrir el avatar de VTuberBot y luego incorporarlo a la escena de OBS.",
            "El botón Mostrar avatar abre la ventana transparente. Para que salga en la grabación o directo, OBS debe tener una fuente Captura de ventana que seleccione el overlay de VTuberBot dentro de la escena correcta. Esta pantalla no crea ni reemplaza fuentes automáticamente porque primero debes elegir y revisar en qué escena aparecerá.");
        AddFeature(
            body,
            "Audio y micrófono",
            "El estado de conexión no demuestra que el micrófono se escuche. Comprueba el medidor de audio y haz una grabación local antes de transmitir.",
            "En el Mezclador de audio de OBS habla cerca del micrófono y revisa que su medidor se mueva. Si no se mueve, abre Ajustes → Audio y selecciona el dispositivo correcto. Reproduce una grabación de prueba para confirmar que la voz realmente se escucha. VTuberBot no puede demostrar que los altavoces o el micrófono físico funcionan solo consultando WebSocket.");
        AddFeature(
            body,
            "Transmisión segura",
            "La preparación no inicia emisiones, no activa el micrófono y no publica nada en tu canal.",
            "El botón principal jamás ejecuta StartStream ni StartRecord. Configurar un destino o guardar una clave no significa que debas salir en directo. Revisa la escena, el audio y la privacidad antes de iniciar una transmisión desde una acción separada y explícita.");
        
        var quickActions = new WrapPanel { Margin = new Thickness(0, 6, 0, 12) };
        var guideButton = CreateButton("Abrir guía paso a paso", false);
        guideButton.Click += (_, _) => openSetupGuide();
        quickActions.Children.Add(guideButton);
        var guideHelp = CreateButton("?", false);
        guideHelp.ToolTip = "Ayuda de la guía";
        guideHelp.Click += (_, _) => ShowHelp(
            "Guía paso a paso",
            "La guía explica la primera instalación, el servidor WebSocket, la escena, el avatar, el audio y la prueba local. Se abre en otra ventana y esta pantalla permanece abierta.");
        quickActions.Children.Add(guideHelp);

        var advancedButton = CreateButton("Opciones avanzadas", false);
        advancedButton.Click += (_, _) => openAdvancedSettings();
        quickActions.Children.Add(advancedButton);
        var advancedHelp = CreateButton("?", false);
        advancedHelp.ToolTip = "¿Necesitas estas opciones?";
        advancedHelp.Click += (_, _) => ShowHelp(
            "Opciones avanzadas",
            "Esta ventana contiene controles técnicos de OBS: escenas, fuentes, vídeo, salida, claves protegidas, diagnóstico y grabación de prueba. Para el uso cotidiano no necesitas abrirla; empieza por Iniciar y preparar OBS. Cambia solo el ajuste que entiendas y revisa la ayuda o guía cuando tengas dudas.");
        quickActions.Children.Add(advancedHelp);

        var avatarButton = CreateButton("Mostrar avatar", false);
        avatarButton.Click += (_, _) =>
        {
            try
            {
                showAvatarOverlay();
                AppendStep("Se solicitó mostrar la ventana del avatar. Añádela a la escena de OBS como Captura de ventana si todavía no aparece.");
            }
            catch (Exception exception)
            {
                AppendStep("No se pudo mostrar el avatar: " + exception.Message);
            }
        };
        quickActions.Children.Add(avatarButton);
        var avatarHelp = CreateButton("?", false);
        avatarHelp.ToolTip = "Ayuda del avatar";
        avatarHelp.Click += (_, _) => ShowHelp(
            "Mostrar avatar",
            "Abre el overlay transparente de VTuberBot. Para que aparezca en OBS, agrega una fuente «Captura de ventana» y selecciona «VTuberBot Avatar Overlay» dentro de la escena adecuada. Comprueba el encuadre antes de emitir.");
        quickActions.Children.Add(avatarHelp);
        body.Children.Add(quickActions);

        var resultPanel = new Border
        {
            Background = Brush("#101525"),
            BorderBrush = Brush("#293650"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var resultStack = new StackPanel();
        resultStack.Children.Add(new TextBlock
        {
            Text = "Estado de la preparación",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 0, 0, 7)
        });
        statusText.Text = "Todavía no se ha ejecutado la preparación.";
        statusText.TextWrapping = TextWrapping.Wrap;
        statusText.Foreground = Brush("#C7D2E5");
        statusText.FontSize = 12;
        resultStack.Children.Add(statusText);
        progress.IsIndeterminate = true;
        progress.Height = 5;
        progress.Margin = new Thickness(0, 8, 0, 8);
        progress.Foreground = Brush("#69D8F2");
        progress.Visibility = Visibility.Collapsed;
        resultStack.Children.Add(progress);
        var stepScroll = new ScrollViewer
        {
            MaxHeight = 145,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stepsPanel
        };
        resultStack.Children.Add(stepScroll);
        resultPanel.Child = resultStack;
        body.Children.Add(resultPanel);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body
        };
    }

    private void AddFeature(StackPanel parent, string title, string description, string helpText)
    {
        var border = new Border
        {
            Background = Brush("#101525"),
            BorderBrush = Brush("#293650"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F8FAFC")
        });
        copy.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#AEBBD2"),
            Margin = new Thickness(0, 3, 6, 0)
        });
        Grid.SetColumn(copy, 0);
        grid.Children.Add(copy);

        var helpButton = CreateButton("?", false);
        helpButton.MinWidth = 34;
        helpButton.Padding = new Thickness(6, 4, 6, 4);
        helpButton.VerticalAlignment = VerticalAlignment.Center;
        helpButton.ToolTip = "¿Para qué sirve esta opción?";
        helpButton.Click += (_, _) => ShowHelp(title, helpText);
        Grid.SetColumn(helpButton, 1);
        grid.Children.Add(helpButton);

        border.Child = grid;
        parent.Children.Add(border);
    }

    private async Task RunPreparationAsync()
    {
        if (Interlocked.Exchange(ref preparationRunning, 1) != 0)
            return;

        try
        {
            if (!hasPreparationPermission())
            {
                var approval = MessageBox.Show(
                    this,
                    "¿Autorizas a VTuberBot a abrir OBS en modo ligero y aplicar el perfil de vídeo 1280×720 a 30 FPS? Puede cambiar los ajustes globales de vídeo y, en un inicio nuevo, omitir plugins de terceros. No desinstala plugins, pero una escena podría depender de ellos. El botón nunca iniciará una transmisión ni una grabación.",
                    "Autorizar preparación de OBS",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (approval != MessageBoxResult.Yes)
                    return;

                savePreparationPermission();
            }

            prepareButton.IsEnabled = false;
            prepareButton.Content = "PREPARANDO OBS…";
            progress.Visibility = Visibility.Visible;
            statusText.Text = "Preparando el entorno y verificando respuestas reales de OBS…";
            stepsPanel.Children.Clear();

            var result = await prepareAsync(AppendStep);
            statusText.Text = (result.IsReady ? "✓ " : "⚠ ") + result.Summary;
            statusText.Foreground = Brush(result.IsReady ? "#8FE3B1" : "#F2C078");
            foreach (var warning in result.Warnings.Take(10))
                AppendStep("Aviso: " + warning);

            AppendStep(result.IsReady
                ? "Preparación terminada. Revisa la previsualización y el audio antes de transmitir."
                : "La preparación quedó incompleta. Lee los pasos anteriores y abre la guía si necesitas ayuda.");
        }
        catch (Exception exception)
        {
            statusText.Text = "La preparación no se completó: " + exception.Message;
            statusText.Foreground = Brush("#F2A6A6");
            AppendStep("Error: " + exception.GetType().Name);
        }
        finally
        {
            progress.Visibility = Visibility.Collapsed;
            prepareButton.IsEnabled = true;
            prepareButton.Content = "▶  INICIAR Y PREPARAR OBS";
            Interlocked.Exchange(ref preparationRunning, 0);
        }
    }

    private void AppendStep(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => AppendStep(text)));
            return;
        }

        var item = new TextBlock
        {
            Text = "• " + text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#C7D2E5"),
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 3)
        };
        stepsPanel.Children.Add(item);
        if (stepsPanel.Children.Count > 30)
            stepsPanel.Children.RemoveAt(0);
    }

    private void ShowHelp(string title, string content)
    {
        var helpWindow = new Window
        {
            Owner = this,
            Title = "Ayuda · " + title,
            Width = 510,
            Height = 340,
            MinWidth = 420,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#0B0E17"),
            Foreground = Brush("#F8FAFC"),
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 21,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brush("#69D8F2"),
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = content,
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brush("#D5DEED")
                        }
                    }
                }
            }
        };
        helpWindow.Show();
        helpWindow.Activate();
    }

    private Button CreateButton(string text, bool primary)
    {
        return new Button
        {
            Content = text,
            MinHeight = 36,
            Padding = new Thickness(13, 8, 13, 8),
            Margin = new Thickness(0, 0, 8, 6),
            Background = Brush(primary ? "#16445A" : "#182238"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush(primary ? "#3C8EAC" : "#34415E"),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));
}