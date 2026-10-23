using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
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

public partial class MainWindow
{
    private TextBlock? obsStreamOutputStatusLabel;
    private TextBlock? obsRecordOutputStatusLabel;
    private Action<string, JsonElement>? obsEventHandler;
    private Action<string>? obsStatusHandler;
    private ObsSetupWizardWindow? obsSetupWizardWindow;
    private ObsSimpleModeWindow? obsSimpleModeWindow;
    private Action? openAdvancedObsWindowAction;

    private void OpenObsSimpleModeWindow()
    {
        if (obsSimpleModeWindow is null)
        {
            var window = new ObsSimpleModeWindow(
                PrepareObsSimpleModeAsync,
                () => string.Equals(
                    store.GetSetting("obs.simpleMode.authorized", "false"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                () => store.SetSetting("obs.simpleMode.authorized", "true"),
                OpenObsSetupWizard,
                () => openAdvancedObsWindowAction?.Invoke(),
                ShowOverlay,
                this);
            obsSimpleModeWindow = window;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(obsSimpleModeWindow, window))
                    obsSimpleModeWindow = null;
            };
        }

        if (!obsSimpleModeWindow.IsVisible)
            obsSimpleModeWindow.Show();
        if (obsSimpleModeWindow.WindowState == WindowState.Minimized)
            obsSimpleModeWindow.WindowState = WindowState.Normal;
        obsSimpleModeWindow.Activate();
    }

    private async Task<ObsSimplePreparationResult> PrepareObsSimpleModeAsync(Action<string> reportStep)
    {
        var warnings = new List<string>();
        void Step(string message)
        {
            reportStep(message);
            store.Log("OBS fácil", message);
        }

        ObsSimplePreparationResult Incomplete(string summary, params string[] additionalWarnings) =>
            new(false, summary, warnings.Concat(additionalWarnings).Take(10).ToArray());

        try
        {
            var localHost = store.GetSetting("obs.host", "127.0.0.1");
            if (!ObsSystemResourceGuard.IsLoopbackHost(localHost))
            {
                Step("Preparación detenida: el host de OBS configurado no es local.");
                return Incomplete("El modo sencillo solo administra una instancia local de OBS.",
                    "En la página avanzada de OBS, usa 127.0.0.1 o localhost si OBS está instalado en esta PC.");
            }

            var alreadyRunning = IsObsRunning();
            var resourceRisk = ObsSystemResourceRisk.Normal;
            if (ObsSystemResourceGuard.TryReadSystemMemory(out var memory))
            {
                resourceRisk = ObsSystemResourceGuard.ClassifyAvailableMemory(memory.AvailablePhysicalBytes);
                Step($"Memoria libre detectada: {memory.AvailablePhysicalMiB:N0} MiB de {memory.TotalPhysicalMiB:N0} MiB.");
                if (resourceRisk == ObsSystemResourceRisk.Critical)
                {
                    if (!alreadyRunning)
                    {
                        Step("No se abrió OBS porque queda menos de 1 GiB de RAM libre.");
                        return Incomplete("Hay muy poca memoria disponible para iniciar otra aplicación pesada.",
                            "Cierra aplicaciones que no necesites y vuelve a pulsar el botón. VTuberBot no finalizará procesos a la fuerza.");
                    }

                    warnings.Add("Queda menos de 1 GiB de RAM libre. OBS ya estaba abierto; se evitará reiniciarlo y se aconseja no iniciar un directo.");
                }
                else if (resourceRisk == ObsSystemResourceRisk.Low)
                {
                    warnings.Add("La RAM disponible está entre 1 y 1,5 GiB. El modo ligero reduce carga, pero no garantiza que todo el sistema tenga recursos suficientes.");
                }
            }
            else
            {
                warnings.Add("Windows no permitió medir la RAM disponible. No se puede verificar el margen de memoria antes de iniciar OBS.");
                Step("Aviso: la medición de memoria del sistema no está disponible.");
            }

            if (!alreadyRunning)
            {
                Step("Comprobando la carga global del procesador antes de iniciar OBS…");
                var cpuUsageBeforeStart = await ObsSystemResourceGuard.TryReadSystemCpuUsagePercentAsync();
                if (cpuUsageBeforeStart is double cpuPercent)
                {
                    var cpuRisk = ObsSystemResourceGuard.ClassifySystemCpuUsage(cpuPercent);
                    Step($"Uso global de CPU medido durante 500 ms: {cpuPercent:N1}%.");
                    if (cpuRisk == ObsSystemCpuRisk.Critical)
                    {
                        Step("Inicio cancelado: el procesador ya estaba al 95% o más.");
                        return Incomplete("El procesador está muy ocupado; no se abrió una nueva instancia de OBS.",
                            "Cierra tareas pesadas que no necesites y vuelve a intentarlo. Esta muestra es preventiva, no un límite permanente de CPU.");
                    }

                    if (cpuRisk == ObsSystemCpuRisk.High)
                        warnings.Add("El uso total de CPU ya es de al menos el 80%. OBS puede aumentar la carga; revisa el rendimiento antes de transmitir.");
                }
                else
                {
                    warnings.Add("Windows no permitió medir la carga total de CPU. No se pudo verificar el margen del procesador antes de iniciar OBS.");
                    Step("Aviso: la medición de CPU global no está disponible.");
                }
            }

            store.SetSetting("obs.lightMode", "true");

            if (!alreadyRunning)
            {
                var executable = ResolveObsExecutable();
                if (executable is null)
                {
                    var picker = new OpenFileDialog
                    {
                        Title = "Elige el ejecutable de OBS Studio",
                        Filter = "OBS Studio (obs64.exe)|obs64.exe|OBS de 32 bits (obs32.exe)|obs32.exe|Ejecutables (*.exe)|*.exe",
                        CheckFileExists = true,
                        Multiselect = false
                    };
                    Window owner = obsSimpleModeWindow is { IsVisible: true } visibleWindow ? visibleWindow : this;
                    if (picker.ShowDialog(owner) != true)
                        return Incomplete("No se inició OBS porque no se seleccionó el programa.",
                            "Vuelve a pulsar el botón cuando tengas localizada la instalación de OBS.");
                    executable = Path.GetFullPath(picker.FileName);
                }

                var executableName = Path.GetFileName(executable);
                if (!string.Equals(executableName, "obs64.exe", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(executableName, "obs32.exe", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(executableName, "obs.exe", StringComparison.OrdinalIgnoreCase))
                    return Incomplete("El archivo seleccionado no parece ser el ejecutable de OBS.",
                        "Selecciona obs64.exe en la carpeta de instalación de OBS Studio.");

                store.SetSetting("obs.executablePath", Path.GetFullPath(executable));
                Step("Iniciando OBS en una ventana visible y omitiendo plugins de terceros para este inicio…");
                StartObs(executable, forceWindowVisible: true);

                for (var attempt = 0; attempt < 24 && !IsObsRunning(); attempt++)
                    await Task.Delay(500);

                if (!IsObsRunning())
                    return Incomplete("Windows no detectó el proceso de OBS después de solicitar su inicio.",
                        "Comprueba la ruta de obs64.exe y si Windows mostró algún aviso.");
                Step("Proceso de OBS detectado.");
            }
            else
            {
                Step("OBS ya estaba abierto; no se reinició ni se cerró.");
                warnings.Add("El modo sin plugins de terceros solo puede aplicarse al abrir una instancia nueva. Para no arriesgar una escena o ajustes sin guardar, OBS no se reinicia automáticamente.");
            }

            if (!obs.IsConnected)
            {
                if (string.IsNullOrEmpty(LoadObsPassword()))
                {
                    Step("Falta la contraseña WebSocket local guardada en VTuberBot.");
                    return Incomplete("OBS está abierto, pero falta completar la conexión inicial.",
                        "Pulsa «Abrir guía paso a paso». Activa WebSocket en OBS y guarda la contraseña en el campo protegido de la página OBS.");
                }

                Step("Intentando autenticar la conexión local con OBS…");
                await AutoConnectObsFromUiAsync();
                for (var attempt = 0; attempt < 20 && !obs.IsConnected; attempt++)
                    await Task.Delay(500);
            }

            if (!obs.IsConnected)
                return Incomplete("OBS se abrió, pero VTuberBot no pudo verificar la conexión WebSocket.",
                    "Abre la guía de primera configuración y comprueba el puerto y la contraseña. No se declara listo hasta recibir una respuesta real.");

            Step("Conexión WebSocket autenticada.");

            using var checkTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var isStreaming = await obs.IsStreamingAsync(checkTimeout.Token);
            var isRecording = await obs.IsRecordingAsync(checkTimeout.Token);
            if (!ObsSystemResourceGuard.CanApplyLightProfile(obs.IsConnected, isStreaming, isRecording))
            {
                Step("No se cambió el perfil porque OBS está transmitiendo o grabando.");
                return Incomplete("OBS está conectado, pero una salida está activa; no se modificó el vídeo.",
                    "Detén la salida desde OBS cuando estés preparado y vuelve a ejecutar la preparación. VTuberBot no inició ni detuvo ninguna salida.");
            }

            Step("Aplicando el perfil ligero 1280×720 a 30 FPS…");
            using var applyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var profileResult = await obs.ApplyLightVideoProfileAsync(applyTimeout.Token);
            Step(profileResult);

            using var verifyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var scenes = await obs.GetScenesAsync(verifyTimeout.Token);
            var verifiedVideo = await obs.GetVideoSettingsAsync(verifyTimeout.Token);
            if (!verifiedVideo.Is720p30)
                return Incomplete("OBS respondió, pero el perfil de vídeo no coincide con el esperado.",
                    "No inicies una transmisión todavía. Revisa el perfil de vídeo en OBS e inténtalo otra vez.");
            Step("Perfil de vídeo comprobado por lectura de vuelta.");
            if (scenes.Count == 0)
                warnings.Add("OBS no informó ninguna escena. Crea o importa una escena antes de usar una emisión.");
            else
                Step($"Escenas reales detectadas: {scenes.Count}.");

            var insufficientMemoryAfterStart = false;
            if (ObsSystemResourceGuard.TryReadSystemMemory(out var finalMemory))
            {
                var finalRisk = ObsSystemResourceGuard.ClassifyAvailableMemory(finalMemory.AvailablePhysicalBytes);
                if (finalRisk == ObsSystemResourceRisk.Critical)
                {
                    insufficientMemoryAfterStart = true;
                    warnings.Add($"Tras abrir OBS quedan solo {finalMemory.AvailablePhysicalMiB:N0} MiB de RAM libres. No empieces el directo: cierra aplicaciones que no necesites y comprueba de nuevo.");
                }
                else if (finalRisk == ObsSystemResourceRisk.Low && resourceRisk == ObsSystemResourceRisk.Normal)
                {
                    warnings.Add($"Quedan {finalMemory.AvailablePhysicalMiB:N0} MiB de RAM libres. Revisa la carga antes de iniciar un directo.");
                }
            }

            var criticalCpuAfterStart = false;
            var cpuUsageAfterStart = await ObsSystemResourceGuard.TryReadSystemCpuUsagePercentAsync();
            if (cpuUsageAfterStart is double finalCpuPercent)
            {
                var finalCpuRisk = ObsSystemResourceGuard.ClassifySystemCpuUsage(finalCpuPercent);
                Step($"Uso global de CPU tras preparar OBS: {finalCpuPercent:N1}%.");
                if (finalCpuRisk == ObsSystemCpuRisk.Critical)
                {
                    criticalCpuAfterStart = true;
                    warnings.Add("La CPU global sigue en un nivel crítico (95% o más). No inicies un directo; cierra tareas pesadas o reduce más la carga.");
                }
                else if (finalCpuRisk == ObsSystemCpuRisk.High)
                {
                    warnings.Add("El uso global de CPU alcanza el 80% o más. Comprueba el juego, los filtros y el audio mediante una grabación local antes de transmitir.");
                }
            }
            else
            {
                warnings.Add("Windows no permitió volver a medir el uso global de CPU. La preparación no puede garantizar que el procesador tenga margen.");
                Step("Aviso: la medición de CPU global posterior no está disponible.");
            }

            var criticalResourcesAfterStart = insufficientMemoryAfterStart || criticalCpuAfterStart;
            Step(criticalResourcesAfterStart
                ? "Preparación incompleta por recursos críticos. No se inició ninguna transmisión ni grabación."
                : "No se inició ninguna transmisión ni grabación. Revisa visualmente la escena y el audio.");
            FooterStatus.Text = criticalResourcesAfterStart
                ? "OBS conectado, pero la CPU o la RAM libre están en estado crítico; no está listo para un directo."
                : "OBS preparado en modo sencillo; transmisión no iniciada.";
            return new(
                !criticalResourcesAfterStart,
                criticalResourcesAfterStart
                    ? "OBS respondió y el perfil 720p30 fue verificado, pero la CPU o la RAM libre siguen en estado crítico."
                    : "OBS está conectado y el perfil 720p30 fue verificado.",
                warnings.ToArray());
        }
        catch (Exception exception)
        {
            store.Log("OBS fácil", "La preparación no se completó (" + exception.GetType().Name + ").");
            return new(false, "La preparación se detuvo: " + exception.Message,
                warnings.Append("Si necesitas ayuda, abre la guía paso a paso.").Take(10).ToArray());
        }
    }

    private void OpenObsSetupWizard()
    {
        if (obsSetupWizardWindow is null)
        {
            async Task<bool> CheckConnectionAsync()
            {
                if (!obs.IsConnected)
                    await AutoConnectObsFromUiAsync();
                return obs.IsConnected && !string.IsNullOrWhiteSpace(await obs.GetVersionAsync());
            }

            var wizard = new ObsSetupWizardWindow(
                key => store.GetSetting(key, string.Empty),
                (key, value) => store.SetSetting(key, value),
                () => OpenObsFromUiAsync(),
                CheckConnectionAsync,
                () =>
                {
                    Navigate("OBS");
                    openAdvancedObsWindowAction?.Invoke();
                },
                async () =>
                {
                    if (!obs.IsConnected)
                        throw new InvalidOperationException("Primero conecta VTuberBot con OBS desde las opciones avanzadas de OBS.");
                    return await obs.ApplyLightVideoProfileAsync();
                },
                () => ShowOverlay(),
                this);
            obsSetupWizardWindow = wizard;
            wizard.Closed += (_, _) =>
            {
                if (ReferenceEquals(obsSetupWizardWindow, wizard))
                    obsSetupWizardWindow = null;
            };
        }

        if (!obsSetupWizardWindow.IsVisible)
            obsSetupWizardWindow.Show();
        if (obsSetupWizardWindow.WindowState == WindowState.Minimized)
            obsSetupWizardWindow.WindowState = WindowState.Normal;
        obsSetupWizardWindow.Activate();
    }

    private UIElement BuildObsPage()
    {
        var root=new StackPanel();
        var simpleModePanel = Panel("OBS fácil · un botón para iniciar y preparar");
        simpleModePanel.Children.Add(Label("Un botón inicia OBS en modo ligero, comprueba la RAM y la carga global de CPU, conecta el programa si está autorizado y verifica el perfil de vídeo. La primera vez pedirá permiso y quizá necesites preparar WebSocket. No iniciará una transmisión.", 12, "#98A4BE"));
        var simpleModeActions = new WrapPanel();
        simpleModeActions.Children.Add(ActionButton("▶ INICIAR Y PREPARAR OBS", async (_, _) =>
        {
            OpenObsSimpleModeWindow();
            if (obsSimpleModeWindow is not null)
                await obsSimpleModeWindow.StartPreparationFromMainPageAsync();
        }, true));
        simpleModeActions.Children.Add(ActionButton("?", (_, _) =>
        {
            OpenObsSimpleModeWindow();
            obsSimpleModeWindow?.ShowPrimaryActionHelp();
        }, false));
        simpleModeActions.Children.Add(ActionButton("Abrir modo sencillo y ayuda", (_, _) => OpenObsSimpleModeWindow(), false));
        simpleModePanel.Children.Add(simpleModeActions);
        root.Children.Add(simpleModePanel);

        var advancedRoot = new StackPanel();
        Window? advancedObsWindow = null;
        ScrollViewer? advancedObsScroll = null;

        void OpenAdvancedObsWindow()
        {
            if (advancedObsWindow is null)
            {
                advancedObsScroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = advancedRoot
                };
                var window = new Window
                {
                    Owner = this,
                    Title = "VTuberBot · Controles avanzados de OBS",
                    Width = 1180,
                    Height = 820,
                    MinWidth = 880,
                    MinHeight = 620,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = Brush("#0B0E17"),
                    Foreground = Brush("#F8FAFC"),
                    Content = advancedObsScroll
                };
                advancedObsWindow = window;
                window.Closed += (_, _) =>
                {
                    if (!ReferenceEquals(advancedObsWindow, window))
                        return;
                    if (advancedObsScroll is not null)
                        advancedObsScroll.Content = null;
                    window.Content = null;
                    advancedObsScroll = null;
                    advancedObsWindow = null;
                };
            }

            if (!advancedObsWindow.IsVisible)
                advancedObsWindow.Show();
            if (advancedObsWindow.WindowState == WindowState.Minimized)
                advancedObsWindow.WindowState = WindowState.Normal;
            advancedObsWindow.Activate();
        }

        openAdvancedObsWindowAction = OpenAdvancedObsWindow;

        var guidePanel = Panel("Asistente visual · Configurar OBS paso a paso");
        guidePanel.Children.Add(Label("Una guía que podés volver a abrir: diagramas clicables, enlaces oficiales, pasos para Twitch/YouTube y progreso guardado. Los diagramas son ilustraciones explicativas; la conexión y los cambios solo se confirman cuando la aplicación los comprueba realmente.", 12, "#98A4BE"));
        guidePanel.Children.Add(ActionButton("Abrir asistente visual de OBS", (_, _) => OpenObsSetupWizard(), true));
        advancedRoot.Children.Add(guidePanel);
        var panel=Panel("OBS Studio · WebSocket v5");advancedRoot.Children.Add(panel);
        var launchPanel = Panel("Inicio de OBS y rendimiento");
        launchPanel.Children.Add(Label("VTuberBot puede abrir OBS y conectarse con la configuración guardada. El primer uso requiere guardar una contraseña de WebSocket en OBS y en VTuberBot.", 12, "#98A4BE"));
        var autoStartObs = new CheckBox
        {
            Content = "Abrir OBS automáticamente al iniciar VTuberBot",
            IsChecked = string.Equals(store.GetSetting("obs.autoStart", "false"), "true", StringComparison.OrdinalIgnoreCase),
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 6, 0, 6)
        };
        autoStartObs.Checked += (_, _) => store.SetSetting("obs.autoStart", "true");
        autoStartObs.Unchecked += (_, _) => store.SetSetting("obs.autoStart", "false");
        launchPanel.Children.Add(autoStartObs);
        var minimizeToTrayObs = new CheckBox
        {
            Content = "Iniciar OBS minimizado a la bandeja del sistema",
            IsChecked = string.Equals(store.GetSetting("obs.startMinimized", "true"), "true", StringComparison.OrdinalIgnoreCase),
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 0, 0, 6)
        };
        minimizeToTrayObs.Checked += (_, _) => store.SetSetting("obs.startMinimized", "true");
        minimizeToTrayObs.Unchecked += (_, _) => store.SetSetting("obs.startMinimized", "false");
        launchPanel.Children.Add(minimizeToTrayObs);
        launchPanel.Children.Add(Label("Usa el parámetro oficial --minimize-to-tray al iniciar una instancia nueva. OBS debe tener habilitado el icono de bandeja. No minimiza una instancia que ya estaba abierta y no confirma por sí solo la conexión.", 11, "#98A4BE"));
        var lightModeObs = new CheckBox
        {
            Content = "Modo ligero: iniciar OBS sin plugins de terceros",
            IsChecked = string.Equals(store.GetSetting("obs.lightMode", "true"), "true", StringComparison.OrdinalIgnoreCase),
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        lightModeObs.Checked += (_, _) => store.SetSetting("obs.lightMode", "true");
        lightModeObs.Unchecked += (_, _) => store.SetSetting("obs.lightMode", "false");
        launchPanel.Children.Add(lightModeObs);
        launchPanel.Children.Add(Label("El modo ligero no desinstala plugins: usa la opción oficial de OBS para omitir plugins de terceros en ese inicio. Si una escena depende de uno, desactivá esta opción. No garantiza por sí solo más FPS.", 11, "#E7B56A"));
        var launchActions = new WrapPanel();
        launchActions.Children.Add(ActionButton("Abrir OBS Studio", async (_, _) => await OpenObsFromUiAsync(), true));
        launchActions.Children.Add(ActionButton("Aplicar perfil 720p / 30 FPS", async (_, _) =>
        {
            if (MessageBox.Show(this,
                    "Se cambiarán el lienzo y la salida de OBS a 1280×720 y 30 FPS. Esto puede afectar todas tus escenas. ¿Continuar?",
                    "Perfil ligero de OBS", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                if (!obs.IsConnected)
                    await AutoConnectObsFromUiAsync();
                if (!obs.IsConnected)
                    throw new InvalidOperationException("Primero habilitá WebSocket en OBS y guardá la contraseña desde esta página.");
                var result = await obs.ApplyLightVideoProfileAsync();
                FooterStatus.Text = result;
                store.Log("OBS", result);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo aplicar el perfil de OBS: " + exception.Message;
                store.Log("OBS", "Falló el perfil 720p30 (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.Message, "Perfil de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));

        launchActions.Children.Add(ActionButton("Elegir ubicación de OBS", (_, _) =>
        {
            var picker = new OpenFileDialog
            {
                Title = "Seleccioná obs64.exe",
                Filter = "OBS Studio (obs64.exe)|obs64.exe|Ejecutables (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };
            if (picker.ShowDialog(this) == true)
            {
                store.SetSetting("obs.executablePath", Path.GetFullPath(picker.FileName));
                FooterStatus.Text = "Ubicación de OBS guardada.";
            }
        }));
        launchPanel.Children.Add(launchActions);
        advancedRoot.Children.Add(launchPanel);

        var processMetricsPanel = Panel("Consumo real de recursos · VTuberBot y OBS por separado");
        processMetricsPanel.Children.Add(Label("Mide RAM (working set) y CPU de cada proceso en una ventana breve. Los datos solo aparecen después de una medición local; no son estimaciones ni resultados de CI.", 12, "#98A4BE"));
        var processMetricsStatus = Label("Todavía no se midió el consumo. Pulsa «Medir procesos» cuando OBS esté abierto.", 12, "#F8FAFC");
        processMetricsPanel.Children.Add(processMetricsStatus);

        async Task MeasureProcessMetricsAsync()
        {
            processMetricsStatus.Text = "Midiendo CPU y memoria de los procesos locales…";
            var obsProcessId = ProcessResourceSampler.FindFirstProcessId("obs64", "obs32", "obs");
            var appTask = ProcessResourceSampler.MeasureAsync(Environment.ProcessId);
            var obsTask = obsProcessId.HasValue
                ? ProcessResourceSampler.MeasureAsync(obsProcessId.Value)
                : Task.FromResult<ProcessResourceSnapshot?>(null);

            await Task.WhenAll(appTask, obsTask);
            var appSnapshot = await appTask;
            var obsSnapshot = await obsTask;
            var lines = new List<string>
            {
                appSnapshot?.ToDisplayString() ?? "VTuberBot: no se pudo medir el proceso actual."
            };
            lines.Add(obsSnapshot?.ToDisplayString() ?? "OBS Studio: proceso no encontrado o medición no disponible.");

            if (obs.IsConnected)
            {
                try
                {
                    var stats = await obs.GetPerformanceStatsAsync();
                    lines.Add(stats.DisplayLabel);
                }
                catch (Exception exception)
                {
                    lines.Add("Estadísticas internas de OBS: no disponibles (" + exception.GetType().Name + ").");
                }
            }
            else
            {
                lines.Add("Estadísticas internas de render de OBS: requieren conexión WebSocket autenticada.");
            }

            processMetricsStatus.Text = string.Join(Environment.NewLine, lines);
            FooterStatus.Text = "Medición de CPU/RAM realizada; no se cambió la configuración de OBS.";
            store.Log("OBS", "Medición de procesos completada. VTuberBot y OBS se informan por separado; los valores internos de render requieren WebSocket.");
        }

        processMetricsPanel.Children.Add(ActionButton("Medir procesos", async (_, _) =>
        {
            try { await MeasureProcessMetricsAsync(); }
            catch (Exception exception)
            {
                processMetricsStatus.Text = "La medición no pudo completarse: " + exception.GetType().Name;
                store.Log("OBS", "Falló la medición de recursos (" + exception.GetType().Name + ").");
            }
        }, true));
        advancedRoot.Children.Add(processMetricsPanel);

        var amdDiagnosticPanel = Panel("Diagnóstico local de codificación AMD (AMF)");
        amdDiagnosticPanel.Children.Add(Label("Comprueba señales locales de GPU AMD, archivo del complemento AMF y referencias en logs recientes. Son indicios; el cliente WebSocket no expone el inventario de encoders ni puede confirmar que una grabación haya codificado correctamente.", 12, "#98A4BE"));
        var amdDiagnosticStatus = Label("Disponibilidad del encoder: aún no verificada en este equipo.", 12, "#F8FAFC");
        amdDiagnosticPanel.Children.Add(amdDiagnosticStatus);
        amdDiagnosticPanel.Children.Add(ActionButton("Diagnosticar AMD AMF", (_, _) =>
        {
            var report = ObsAmdEncoderDiagnostics.Inspect(ResolveObsExecutable());
            amdDiagnosticStatus.Text = report.Summary;
            FooterStatus.Text = "Diagnóstico AMD/AMF terminado: los indicios no equivalen a una prueba de codificación.";
            store.Log("OBS", report.ToLogSummary());
            MessageBox.Show(this, report.Details, "Diagnóstico AMD AMF", MessageBoxButton.OK, MessageBoxImage.Information);
        }, true));
        advancedRoot.Children.Add(amdDiagnosticPanel);

        advancedRoot.Loaded += async (_, _) =>
        {
            if (IsObsRunning() && !obs.IsConnected)
                await AutoConnectObsFromUiAsync();
        };

        panel.Children.Add(Label("Conexión autenticada y control de escenas",19,"#F8FAFC",true));
        panel.Children.Add(Label("Activá WebSocket Server en OBS → Herramientas. La app no registra una conexión hasta completar Hello/Identify. La contraseña se cifra con DPAPI CurrentUser.",13,"#98A4BE"));
        panel.Children.Add(Label("Estado real: las escenas, fuentes, audio y salidas se consultan o modifican en OBS autenticado. Un inicio solicitado, una guía y una previsualización local del avatar no se presentan como una emisión activa.", 12, "#98A4BE"));
        var host=TextField("Host",store.GetSetting("obs.host","127.0.0.1"));
        var port=TextField("Puerto",store.GetSetting("obs.port","4455"));
        var password=new PasswordBox{Password=LoadObsPassword(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        obsStreamOutputStatusLabel = Label("Transmisión: estado desconocido", 12, "#98A4BE");
        obsRecordOutputStatusLabel = Label("Grabación: estado desconocido", 12, "#98A4BE");

        if (obsEventHandler is not null)
            obs.EventReceived -= obsEventHandler;
        if (obsStatusHandler is not null)
            obs.StatusChanged -= obsStatusHandler;

        obsEventHandler = (eventType, eventData) =>
        {
            if (eventType is not ("StreamStateChanged" or "RecordStateChanged"))
                return;

            bool? active = eventData.ValueKind == JsonValueKind.Object &&
                eventData.TryGetProperty("outputActive", out var activeValue) &&
                activeValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? activeValue.GetBoolean()
                    : null;

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var label = eventType == "StreamStateChanged"
                        ? obsStreamOutputStatusLabel
                        : obsRecordOutputStatusLabel;
                    if (label is null)
                        return;

                    var outputName = eventType == "StreamStateChanged" ? "Transmisión" : "Grabación";
                    label.Text = active.HasValue
                        ? outputName + ": " + (active.Value ? "ACTIVA" : "Inactiva")
                        : outputName + ": estado no disponible";
                }));
            }
            catch (InvalidOperationException)
            {
                // Dispatcher shutdown is not a WebSocket transport error.
            }
        };
        obs.EventReceived += obsEventHandler;

        async Task RefreshOutputStatusAsync()
        {
            if (!obs.IsConnected)
            {
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: OBS desconectado";
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: OBS desconectado";
                return;
            }

            try
            {
                var active = await obs.IsStreamingAsync();
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: no disponible";
                store.Log("OBS", "No se pudo leer el estado de transmisión (" + exception.GetType().Name + ").");
            }

            try
            {
                var active = await obs.IsRecordingAsync();
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: no disponible";
                store.Log("OBS", "No se pudo leer el estado de grabación (" + exception.GetType().Name + ").");
            }
        }

        // Keep the output indicators aligned with the actual WebSocket session. The
        // client can disconnect or reconnect without the user pressing a page button.
        obsStatusHandler = status =>
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => _ = RefreshOutputStatusAsync()));
            }
            catch (InvalidOperationException)
            {
                // The dispatcher may already be shutting down.
            }
        };
        obs.StatusChanged += obsStatusHandler;
        var pageStatusHandler = obsStatusHandler;
        var pageEventHandler = obsEventHandler;
        root.Unloaded += (_, _) =>
        {
            if (pageStatusHandler is not null)
                obs.StatusChanged -= pageStatusHandler;
            if (pageEventHandler is not null)
                obs.EventReceived -= pageEventHandler;

            if (ReferenceEquals(obsStatusHandler, pageStatusHandler))
                obsStatusHandler = null;
            if (ReferenceEquals(obsEventHandler, pageEventHandler))
                obsEventHandler = null;
        };

        // Refresh on entry as well as on subsequent connection/reconnection events.
        _ = RefreshOutputStatusAsync();

        async Task RunOutputActionAsync(string action, Func<Task> operation)
        {
            try
            {
                await operation();
                store.Log("OBS", action + " ejecutada correctamente.");
                FooterStatus.Text = action + " ejecutada en OBS.";
            }
            catch (Exception exception)
            {
                store.Log("OBS", "Falló " + action + " (" + exception.GetType().Name + ").");
                FooterStatus.Text = action + " falló: " + exception.Message;
                MessageBox.Show(this, action + " no se pudo completar. Revisá el estado real de OBS.\n\n" + exception.Message,
                    "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await RefreshOutputStatusAsync();
        }

        panel.Children.Add(Label("Host",12,"#98A4BE"));panel.Children.Add(host);
        panel.Children.Add(Label("Puerto",12,"#98A4BE"));panel.Children.Add(port);
        panel.Children.Add(Label("Contraseña de WebSocket (obligatoria; habilitala en OBS)",12,"#98A4BE"));panel.Children.Add(password);
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Guardar y conectar (primera vez)",async (_,_)=>{try{store.SetSetting("obs.host",host.Text.Trim());store.SetSetting("obs.port",port.Text.Trim());SaveObsPassword(password.Password);await obs.ConnectAsync(host.Text.Trim(),int.Parse(port.Text),password.Password);var version=await obs.GetVersionAsync();FooterStatus.Text="OBS autenticado · versión "+version;store.Log("OBS","Conexión WebSocket v5 autenticada; versión "+version);await RefreshOutputStatusAsync();}catch(Exception ex){FooterStatus.Text="OBS no conectado: "+ex.Message;store.Log("OBS","Conexión WebSocket fallida: "+ex.GetType().Name);MessageBox.Show(this,"No se pudo autenticar con OBS. Confirmá que WebSocket esté habilitado, el puerto y la contraseña.\n\n"+ex.Message);}} ,true));
        actions.Children.Add(ActionButton("Desconectar",async (_,_)=>{await obs.DisconnectAsync();await RefreshOutputStatusAsync();}));
        actions.Children.Add(ActionButton("Auditar conexión, vídeo, escenas y audio", async (_, _) =>
        {
            FooterStatus.Text = "Consultando el estado real de OBS; no se iniciará ni detendrá ninguna salida.";
            try
            {
                var report = await obs.RunReadOnlyDiagnosticsAsync();
                var reportText = report.ToDisplayText();
                FooterStatus.Text = "Diagnóstico de OBS completado. Revisá avisos y hacé una grabación local para la prueba física.";
                store.Log("OBS", "Diagnóstico de solo lectura terminado: versión " + report.ObsVersion +
                    ", escenas " + report.Scenes.Count + ", entradas de audio " + report.AudioInputs.Count +
                    ", streaming " + report.IsStreaming + ", recording " + report.IsRecording +
                    ", perfil 720p30 " + report.Video.Is720p30 + ". No se iniciaron salidas.");
                var hasWarning = report.AudioInputs.Count == 0 || !report.Video.Is720p30;
                MessageBox.Show(this, reportText, "Auditoría de OBS (solo lectura)", MessageBoxButton.OK,
                    hasWarning ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or TimeoutException or System.Text.Json.JsonException or
                System.Net.WebSockets.WebSocketException or OperationCanceledException or IOException or ArgumentException)
            {
                FooterStatus.Text = "No se completó el diagnóstico de OBS; no se modificó ninguna salida.";
                store.Log("OBS", "Falló el diagnóstico de solo lectura (" + exception.GetType().Name + ").");
                MessageBox.Show(this,
                    "No se pudo completar la auditoría de OBS. Confirmá que WebSocket esté autenticado y que OBS responda. No se inició ni detuvo la grabación o la transmisión.\n\n" +
                    exception.GetBaseException().Message,
                    "Auditoría de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        panel.Children.Add(actions);
        var scenes=new ComboBox{MinWidth=260};
        var sceneRow=new WrapPanel();
        sceneRow.Children.Add(ActionButton("Actualizar escenas",async (_,_)=>{try{scenes.ItemsSource=await obs.GetScenesAsync();if(scenes.Items.Count>0)scenes.SelectedIndex=0;FooterStatus.Text="Escenas recibidas de OBS.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        sceneRow.Children.Add(scenes);
        sceneRow.Children.Add(ActionButton("Cambiar escena",async (_,_)=>{try{if(scenes.SelectedItem is not string selected)throw new InvalidOperationException("Primero cargá y elegí una escena.");await obs.SetCurrentSceneAsync(selected);store.Log("OBS","Escena cambiada a "+selected);FooterStatus.Text="Escena activa: "+selected;}catch(Exception ex){MessageBox.Show(this,ex.Message);}} ,true));
        panel.Children.Add(Label("Control de escenas",14,"#F8FAFC",true));panel.Children.Add(sceneRow);

        var sourceControls = Panel("Fuentes de escena y audio");
        sourceControls.Children.Add(Label("Estos controles actúan sobre OBS conectado. No modifican la previsualización local del avatar.", 12, "#98A4BE"));
        var sceneItemPicker = new ComboBox { MinWidth = 280, DisplayMemberPath = nameof(ObsSceneItem.DisplayLabel), Margin = new Thickness(0, 0, 8, 8) };
        async Task RefreshSceneItemsAsync()
        {
            if (scenes.SelectedItem is not string selectedScene)
                throw new InvalidOperationException("Primero actualizá las escenas y elegí una.");
            var items = await obs.GetSceneItemsAsync(selectedScene);
            sceneItemPicker.ItemsSource = items;
            sceneItemPicker.SelectedIndex = items.Count > 0 ? 0 : -1;
            FooterStatus.Text = $"Fuentes de la escena «{selectedScene}»: {items.Count}.";
        }
        var sceneSourceActions = new WrapPanel();
        sceneSourceActions.Children.Add(ActionButton("Actualizar fuentes de escena", async (_, _) =>
        {
            try { await RefreshSceneItemsAsync(); }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudieron consultar las fuentes de OBS.";
                store.Log("OBS", "Falló la consulta de fuentes de escena (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.GetBaseException().Message, "Fuentes de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        sceneSourceActions.Children.Add(sceneItemPicker);
        sceneSourceActions.Children.Add(ActionButton("Alternar visibilidad", async (_, _) =>
        {
            try
            {
                if (scenes.SelectedItem is not string selectedScene || sceneItemPicker.SelectedItem is not ObsSceneItem selectedItem)
                    throw new InvalidOperationException("Elegí una escena y una fuente.");
                var enabled = !selectedItem.Enabled;
                await obs.SetSceneItemEnabledAsync(selectedScene, selectedItem.Id, enabled);
                var items = await obs.GetSceneItemsAsync(selectedScene);
                sceneItemPicker.ItemsSource = items;
                sceneItemPicker.SelectedItem = items.FirstOrDefault(item => item.Id == selectedItem.Id);
                FooterStatus.Text = $"Fuente «{selectedItem.Name}»: {(enabled ? "visible" : "oculta")} en OBS.";
                store.Log("OBS", $"Visibilidad de fuente cambiada: {selectedItem.Name}.");
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo cambiar la visibilidad de la fuente.";
                store.Log("OBS", "Falló el cambio de visibilidad (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.GetBaseException().Message, "Fuentes de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        sourceControls.Children.Add(Label("Visibilidad de fuente en la escena seleccionada", 13, "#F8FAFC", true));
        sourceControls.Children.Add(sceneSourceActions);

        var audioInputPicker = new ComboBox { MinWidth = 280, DisplayMemberPath = nameof(ObsAudioInput.DisplayLabel), Margin = new Thickness(0, 0, 8, 8) };
        async Task RefreshAudioInputsAsync()
        {
            var inputs = await obs.GetAudioInputsAsync();
            audioInputPicker.ItemsSource = inputs;
            audioInputPicker.SelectedIndex = inputs.Count > 0 ? 0 : -1;
            FooterStatus.Text = inputs.Count == 0
                ? "OBS no informó fuentes con control de silencio compatible."
                : $"Fuentes de audio controlables detectadas: {inputs.Count} (máximo 32).";
        }
        var audioSourceActions = new WrapPanel();
        audioSourceActions.Children.Add(ActionButton("Actualizar fuentes de audio", async (_, _) =>
        {
            try { await RefreshAudioInputsAsync(); }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudieron consultar las fuentes de audio de OBS.";
                store.Log("OBS", "Falló la consulta de audio (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.GetBaseException().Message, "Audio de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        audioSourceActions.Children.Add(audioInputPicker);
        audioSourceActions.Children.Add(ActionButton("Alternar silencio", async (_, _) =>
        {
            try
            {
                if (audioInputPicker.SelectedItem is not ObsAudioInput selectedInput)
                    throw new InvalidOperationException("Primero actualizá y elegí una fuente de audio.");
                var muted = !selectedInput.Muted;
                await obs.SetInputMuteAsync(selectedInput.Name, muted);
                await RefreshAudioInputsAsync();
                FooterStatus.Text = $"Audio «{selectedInput.Name}»: {(muted ? "silenciado" : "activo")} en OBS.";
                store.Log("OBS", $"Silencio de fuente de audio cambiado: {selectedInput.Name}.");
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo cambiar el silencio de la fuente de audio.";
                store.Log("OBS", "Falló el cambio de silencio (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.GetBaseException().Message, "Audio de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        sourceControls.Children.Add(Label("Silencio de fuentes de audio compatibles", 13, "#F8FAFC", true));
        sourceControls.Children.Add(audioSourceActions);
        advancedRoot.Children.Add(sourceControls);

        var outputPanel = Panel("Transmisión y grabación");
        outputPanel.Margin = new Thickness(0, 14, 0, 0);
        outputPanel.Children.Add(obsStreamOutputStatusLabel);
        outputPanel.Children.Add(obsRecordOutputStatusLabel);
        var outputActions = new WrapPanel();
        outputActions.Children.Add(ActionButton("Actualizar estado", async (_, _) => await RefreshOutputStatusAsync()));
        outputActions.Children.Add(ActionButton("Iniciar transmisión", async (_, _) =>
        {
            if (MessageBox.Show(this,
                    "Esto intentará iniciar una transmisión EN VIVO con el destino y la clave configurados en OBS. No es una previsualización. ¿Querés salir en vivo ahora?",
                    "Confirmar inicio de transmisión", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Iniciar transmisión", () => obs.StartStreamingAsync());
        }, true));
        outputActions.Children.Add(ActionButton("Detener transmisión", async (_, _) =>
        {
            if (MessageBox.Show(this, "¿Detener la transmisión en vivo de OBS?", "Confirmar detención",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Detener transmisión", () => obs.StopStreamingAsync());
        }));
        outputActions.Children.Add(ActionButton("Iniciar grabación", async (_, _) =>
        {
            if (MessageBox.Show(this,
                    "OBS comenzará a grabar usando la configuración actual y guardará el archivo en el destino configurado. ¿Iniciar la grabación?",
                    "Confirmar inicio de grabación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Iniciar grabación", () => obs.StartRecordingAsync());
        }));
        outputActions.Children.Add(ActionButton("Detener grabación", async (_, _) =>
        {
            if (MessageBox.Show(this, "¿Detener la grabación de OBS?", "Confirmar detención",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Detener grabación", () => obs.StopRecordingAsync());
        }));
        outputPanel.Children.Add(outputActions);
        outputPanel.Children.Add(Label("Las acciones solo se envían tras la autenticación WebSocket. El estado se lee desde OBS; guardar la configuración no simula una salida activa.", 12, "#98A4BE"));
        advancedRoot.Children.Add(outputPanel);

        var recordingSmokePanel = Panel("Prueba guiada real · grabación local, voz y avatar");
        recordingSmokePanel.Margin = new Thickness(0, 14, 0, 0);
        recordingSmokePanel.Children.Add(Label(
            "Crea una grabación local corta, reproduce una frase del proveedor LOCAL ya configurado (sin APIs remotas), comprueba que OBS haya escrito un archivo y lo abre para que puedas revisarlo. Nunca inicia ni detiene una transmisión en vivo. Requiere OBS local y WebSocket autenticado.",
            12, "#98A4BE"));
        string DescribeRecordingSmokeResult(string result) => result switch
        {
            "confirmado-por-usuario" => "confirmada manualmente por vos: archivo de prueba reproducido",
            "recording-created-review-pending" or "archivo-creado-revisión-pendiente" =>
                "archivo creado; falta comprobar imagen y sonido al reproducirlo",
            "prueba-incompleta" => "último intento incompleto; revisá el mensaje y el registro de OBS",
            _ => "todavía no ejecutada"
        };
        var initialSmokeResult = store.GetSetting("obs.recording.smokeTest.lastResult", "");
        var recordingSmokeStatus = Label(
            "Última prueba: " + DescribeRecordingSmokeResult(initialSmokeResult) +
            (DateTimeOffset.TryParse(store.GetSetting("obs.recording.smokeTest.lastRunAtUtc"), out var priorSmokeRun)
                ? " · " + priorSmokeRun.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : ""),
            12, string.Equals(initialSmokeResult, "confirmado-por-usuario", StringComparison.Ordinal)
                ? "#8FE6D0" : "#E7B56A", true);
        recordingSmokePanel.Children.Add(recordingSmokeStatus);
        var recordingSmokeTestRunning = 0;

        async Task<bool> WaitForRecordingStateAsync(bool expected, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (!obs.IsConnected)
                    return false;
                if (await obs.IsRecordingAsync())
                {
                    if (expected) return true;
                }
                else if (!expected)
                {
                    return true;
                }

                await Task.Delay(250);
            }

            return false;
        }

        async Task<ObsRecordingFileSnapshot?> WaitForCompletedRecordingAsync(
            string directory,
            IReadOnlyList<ObsRecordingFileSnapshot> before,
            DateTime startedAtUtc,
            TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            long lastLength = -1;
            var stableSamples = 0;
            ObsRecordingFileSnapshot? mostRecent = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                var after = ObsRecordingSmokeTestPolicy.CaptureDirectorySnapshot(directory);
                var candidate = ObsRecordingSmokeTestPolicy.FindNewRecording(before, after, startedAtUtc);
                if (candidate is not null)
                {
                    mostRecent = candidate;
                    if (candidate.Length == lastLength)
                        stableSamples++;
                    else
                    {
                        lastLength = candidate.Length;
                        stableSamples = 0;
                    }

                    if (stableSamples >= 2)
                        return candidate;
                }

                await Task.Delay(750);
            }

            return stableSamples >= 2 ? mostRecent : null;
        }

        async Task RunRecordingSmokeTestAsync()
        {
            if (Interlocked.CompareExchange(ref recordingSmokeTestRunning, 1, 0) != 0)
            {
                FooterStatus.Text = "La prueba de grabación ya está en curso.";
                return;
            }

            Action<string, int>? speechStatusHandler = null;
            try
            {
                var configuredHost = host.Text.Trim();
                var savedObsHost = store.GetSetting("obs.host", "127.0.0.1").Trim();
                if (!string.Equals(configuredHost, savedObsHost, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("El host escrito no coincide con el host guardado de la sesión OBS. Guardá y reconectá primero; la prueba no debe atribuir a un OBS local una sesión que podría pertenecer a otro host.");

                var isLoopbackHost = configuredHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    (System.Net.IPAddress.TryParse(configuredHost, out var address) && System.Net.IPAddress.IsLoopback(address));
                if (!isLoopbackHost)
                    throw new InvalidOperationException("La verificación de archivos solo se ejecuta con OBS en localhost/loopback. Con OBS remoto, la carpeta devuelta no pertenece necesariamente a este equipo.");

                if (!obs.IsConnected)
                    await AutoConnectObsFromUiAsync();
                if (!obs.IsConnected)
                    throw new InvalidOperationException("Primero conecta y autentica OBS WebSocket.");

                var voiceId = store.GetSetting("audio.tts.voiceId");
                if (string.IsNullOrWhiteSpace(voiceId))
                    voiceId = store.GetSetting("audio.voicevox.voiceId");
                var voicevoxVoice = VoicevoxSpeechSession.IsVoiceId(voiceId);
                // Explicit VOICEVOX speaker IDs always route to the local engine, even if the
                // general provider preference is remote for other kinds of speech.
                if (!voicevoxVoice && ProviderPreferenceDefaults.IsRemote(store.GetSetting("social.tts.provider", "local")))
                    throw new InvalidOperationException("La prueba guiada no llama a servicios remotos ni usa APIs pagas. Cambiá el proveedor de voz a Local o seleccioná una voz VOICEVOX para hacer esta prueba.");

                var sapiEnabled = store.GetSetting("audio.tts.windowsSapiEnabled", "false") == "true";
                if (!voicevoxVoice && !sapiEnabled)
                    throw new InvalidOperationException("No hay una voz local configurada. En Voz y audio seleccioná VOICEVOX, o habilitá Windows SAPI expresamente, y probá la voz antes de grabar.");

                if (speechQueue.PendingCount > 0 || speechQueue.IsPlaybackActive)
                    throw new InvalidOperationException("La cola de voz ya está ocupada. Dejá que termine antes del ensayo para que el resultado sea atribuible a esta prueba.");

                var isStreaming = await obs.IsStreamingAsync();
                var isRecording = await obs.IsRecordingAsync();
                if (!ObsRecordingSmokeTestPolicy.CanStart(obs.IsConnected, isStreaming, isRecording))
                    throw new InvalidOperationException(isStreaming
                        ? "Ya hay una transmisión activa. La prueba se cancela; VTuberBot no detendrá una transmisión en vivo."
                        : isRecording
                            ? "Ya hay una grabación activa. La prueba no la interrumpirá."
                            : "OBS se desconectó antes de iniciar la prueba.");

                var recordDirectory = await obs.GetRecordDirectoryAsync();
                if (!Directory.Exists(recordDirectory))
                    throw new DirectoryNotFoundException("La carpeta de grabación que declara OBS no existe en este equipo. Revisá Ajustes → Salida → Grabación en OBS.");
                var baseline = ObsRecordingSmokeTestPolicy.CaptureDirectorySnapshot(recordDirectory);
                var startedAtUtc = DateTime.UtcNow;
                var recordingStartAcknowledged = false;
                var recordingStopConfirmed = false;
                var speechOutcome = "La cola de voz no confirmó la reproducción.";
                var speechOutcomeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                speechStatusHandler = (message, _) =>
                {
                    if (message.StartsWith("Error TTS local:", StringComparison.OrdinalIgnoreCase))
                        speechOutcomeSource.TrySetResult("Error local de TTS: " + message);
                    else if (string.Equals(message, "Reproducción finalizada.", StringComparison.Ordinal))
                        speechOutcomeSource.TrySetResult("El motor local terminó la frase.");
                };
                speechQueue.StatusChanged += speechStatusHandler;

                FooterStatus.Text = "Iniciando solo grabación local; no se solicitará transmisión…";
                recordingSmokeStatus.Text = "Preparando grabación local…";
                try
                {
                    await obs.StartRecordingAsync();
                    recordingStartAcknowledged = true;
                    if (!await WaitForRecordingStateAsync(expected: true, TimeSpan.FromSeconds(8)))
                        throw new TimeoutException("OBS aceptó iniciar la solicitud, pero no confirmó el estado Grabando.");

                    var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var storedRate) ? Math.Clamp(storedRate, -10, 10) : 0;
                    var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var storedVolume) ? Math.Clamp(storedVolume, 0, 100) : 100;
                    var outputId = voicevoxVoice
                        ? store.GetSetting("audio.voicevox.outputId")
                        : store.GetSetting("audio.tts.outputId");
                    var voiceQueued = speechQueue.Enqueue(
                        "Hola, chat. Esta es una prueba local de voz y avatar de VTuberBot.",
                        SpeechPriority.High,
                        voiceId,
                        rate,
                        volume,
                        outputId);
                    if (!voiceQueued)
                        throw new InvalidOperationException("La cola local rechazó la frase de prueba; se detendrá la grabación y no se marcará la voz como verificada.");

                    recordingSmokeStatus.Text = "Grabando… la frase se reproduce con el proveedor local; VTuberBot no está transmitiendo.";
                    FooterStatus.Text = "Grabación de prueba en curso. La frase local y el estado Talking deberían aparecer en OBS si la escena está configurada.";

                    var minimumEnd = DateTimeOffset.UtcNow.AddSeconds(8);
                    var maximumEnd = DateTimeOffset.UtcNow.AddSeconds(45);
                    while (DateTimeOffset.UtcNow < maximumEnd)
                    {
                        await Task.Delay(750);
                        if (!obs.IsConnected)
                            throw new IOException("OBS perdió su conexión durante la grabación de prueba.");
                        if (await obs.IsStreamingAsync())
                            throw new InvalidOperationException("Se detectó una transmisión en vivo mientras se probaba. Se detendrá únicamente la grabación de prueba; VTuberBot no tocará la transmisión.");
                        if (DateTimeOffset.UtcNow >= minimumEnd && speechOutcomeSource.Task.IsCompleted)
                        {
                            speechOutcome = await speechOutcomeSource.Task;
                            break;
                        }
                    }

                    if (!speechOutcomeSource.Task.IsCompleted)
                    {
                        speechOutcome = "La frase no finalizó dentro del límite de 45 segundos; se solicitó detener la voz.";
                        speechQueue.StopCurrentSpeech();
                    }
                    else
                    {
                        speechOutcome = await speechOutcomeSource.Task;
                    }

                    await obs.StopRecordingAsync();
                    if (!await WaitForRecordingStateAsync(expected: false, TimeSpan.FromSeconds(8)))
                        throw new TimeoutException("OBS no confirmó que la grabación se detuviera.");
                    recordingStopConfirmed = true;
                }
                finally
                {
                    // We stop only a recording whose StartRecord request was acknowledged by OBS.
                    // If its acknowledgement was lost, output ownership is ambiguous; report that fact instead of risking stopping someone else's recording.
                    if (recordingStartAcknowledged && !recordingStopConfirmed && obs.IsConnected)
                    {
                        try
                        {
                            if (await obs.IsRecordingAsync())
                            {
                                await obs.StopRecordingAsync();
                                recordingStopConfirmed = await WaitForRecordingStateAsync(expected: false, TimeSpan.FromSeconds(8));
                            }
                        }
                        catch (Exception stopException)
                        {
                            store.Log("OBS", "No se pudo confirmar la parada de la grabación de prueba (" + stopException.GetType().Name + ").");
                        }
                    }
                }

                var completedRecording = await WaitForCompletedRecordingAsync(
                    recordDirectory, baseline, startedAtUtc, TimeSpan.FromSeconds(15));
                if (completedRecording is null)
                    throw new IOException("OBS detuvo la grabación, pero no apareció un archivo nuevo y estable en la carpeta que declara OBS. Revisá el formato, los permisos y el log de OBS.");

                lastObsRecordingSmokePath = completedRecording.FullPath;
                var fileName = Path.GetFileName(completedRecording.FullPath);
                var bytesLabel = completedRecording.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes";
                recordingSmokeStatus.Text = "Archivo detectado: " + fileName + " · " + bytesLabel +
                    ". Resultado de la voz: " + speechOutcome;
                store.SetSetting("obs.recording.smokeTest.lastRunAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                store.SetSetting("obs.recording.smokeTest.lastResult", "recording-created-review-pending");
                store.Log("OBS", "La prueba local creó un archivo de grabación nuevo de " +
                    completedRecording.Length.ToString(CultureInfo.InvariantCulture) +
                    " bytes. Voz local: " +
                    (speechOutcome.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ? "error" :
                        speechOutcome.StartsWith("El motor", StringComparison.Ordinal) ? "cola completada" : "sin confirmar") +
                    ". No se inició ni detuvo una transmisión.");

                var openedRecordFile = false;
                try
                {
                    Process.Start(new ProcessStartInfo(completedRecording.FullPath) { UseShellExecute = true });
                    openedRecordFile = true;
                }
                catch (Exception openException)
                {
                    store.Log("OBS", "No se pudo abrir el archivo de prueba en el reproductor predeterminado (" + openException.GetType().Name + ").");
                }

                var openFileInstruction = openedRecordFile
                    ? "Se solicitó abrir el archivo con el reproductor predeterminado."
                    : "No se pudo abrir automáticamente. Abrí el archivo manualmente desde la carpeta de grabación de OBS.";
                var review = MessageBox.Show(this,
                    "OBS creó un archivo nuevo: " + fileName + " (" + bytesLabel + ").\n\n" +
                    "Estado de la voz: " + speechOutcome + "\n\n" +
                    openFileInstruction + " ¿La grabación muestra el avatar correctamente y se escuchan la frase y el resto del audio sin cortes ni recuadros blancos? Esta confirmación es manual y solo debe marcarse si lo acabás de comprobar.",
                    "Revisar grabación real de OBS", MessageBoxButton.YesNo, MessageBoxImage.Question);
                var verifiedByUser = review == MessageBoxResult.Yes;
                var resultText = verifiedByUser ? "confirmado-por-usuario" : "archivo-creado-revisión-pendiente";
                store.SetSetting("obs.recording.smokeTest.lastResult", resultText);
                recordingSmokeStatus.Text = verifiedByUser
                    ? "Confirmada manualmente por vos: el archivo de prueba contiene imagen y sonido."
                    : "El archivo existe, pero la reproducción visual/audible queda pendiente.";
                recordingSmokeStatus.Foreground = Brush(verifiedByUser ? "#8FE6D0" : "#E7B56A");
                FooterStatus.Text = verifiedByUser
                    ? "Prueba física de grabación confirmada por el usuario."
                    : "Se creó el archivo, pero la prueba física necesita revisión.";
                store.Log("OBS", verifiedByUser
                    ? "El usuario confirmó manualmente reproducción de imagen y sonido de la grabación de prueba."
                    : "El usuario no confirmó reproducción de imagen y sonido; no se marcó como validada.");
            }
            catch (Exception exception)
            {
                recordingSmokeStatus.Text = "Prueba incompleta: " + exception.GetBaseException().Message;
                recordingSmokeStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "La prueba local de OBS no se completó. VTuberBot no solicitó iniciar una transmisión; revisá el estado actual en OBS.";
                try
                {
                    store.SetSetting("obs.recording.smokeTest.lastRunAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    store.SetSetting("obs.recording.smokeTest.lastResult", "prueba-incompleta");
                    store.SetSetting("obs.recording.smokeTest.lastErrorType", exception.GetBaseException().GetType().Name);
                }
                catch (Exception persistenceException)
                {
                    store.Log("OBS", "No se pudo guardar el estado de prueba incompleta (" + persistenceException.GetType().Name + ").");
                }
                store.Log("OBS", "Falló o se interrumpió la prueba local de grabación (" + exception.GetBaseException().GetType().Name + "). No se registraron rutas completas.");
                MessageBox.Show(this,
                    exception.GetBaseException().Message + "\n\nLa prueba no inicia transmisiones. Si OBS agotó el tiempo al responder a StartRecord, verificá manualmente el estado de grabación antes de volver a probar.",
                    "Prueba de grabación de OBS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (speechStatusHandler is not null)
                    speechQueue.StatusChanged -= speechStatusHandler;
                Interlocked.Exchange(ref recordingSmokeTestRunning, 0);
                try { await RefreshOutputStatusAsync(); } catch (Exception exception)
                {
                    store.Log("OBS", "No se pudo actualizar el estado tras la prueba (" + exception.GetType().Name + ").");
                }
            }
        }

        recordingSmokePanel.Children.Add(ActionButton("▶ Grabar prueba local + reproducir voz (máx. 45 s)", async (_, _) =>
        {
            if (MessageBox.Show(this,
                    "VTuberBot hará una grabación LOCAL de hasta 45 segundos para comprobar imagen y audio. Se reproducirá una frase usando solo el proveedor de voz local configurado. No se iniciará ninguna transmisión ni se modificarán las escenas o fuentes. Al finalizar se abrirá el archivo para que lo revises. ¿Continuar?",
                    "Confirmar prueba de grabación local", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await RunRecordingSmokeTestAsync();
        }, true));
        recordingSmokePanel.Children.Add(ActionButton("Abrir último archivo de prueba", (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(lastObsRecordingSmokePath) || !File.Exists(lastObsRecordingSmokePath))
            {
                FooterStatus.Text = "No hay un archivo de prueba disponible en esta sesión. Ejecutá primero la grabación local.";
                return;
            }

            try { Process.Start(new ProcessStartInfo(lastObsRecordingSmokePath) { UseShellExecute = true }); }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo abrir el archivo de prueba: " + exception.GetType().Name;
            }
        }));
        recordingSmokePanel.Children.Add(Label(
            "La prueba verifica el estado real de OBS y la creación de un archivo; la voz que termina en la cola tampoco prueba por sí sola que los altavoces hayan sonado. Solo se marca como validada cuando confirmás la reproducción del archivo.",
            11, "#98A4BE"));
        advancedRoot.Children.Add(recordingSmokePanel);

        var destinationsPanel = Panel("Transmitir a Twitch o YouTube");
        destinationsPanel.Margin = new Thickness(0, 14, 0, 0);
        destinationsPanel.Children.Add(Label("Elegí la plataforma, guardá su clave una sola vez y usa «Configurar destino». La clave se cifra con Windows y nunca se muestra en pantalla ni se registra en logs.", 12, "#98A4BE"));

        var destinationChoice = new ComboBox
        {
            ItemsSource = new[] { "Twitch", "YouTube" },
            SelectedItem = store.GetSetting("obs.stream.platform", "Twitch") == "YouTube" ? "YouTube" : "Twitch",
            MinWidth = 240,
            Margin = new Thickness(0, 4, 8, 8)
        };
        var destinationServer = new TextBox
        {
            IsReadOnly = true,
            Text = ObsStreamingDestinationPolicy.GetServer(destinationChoice.SelectedItem?.ToString() ?? "Twitch"),
            ToolTip = "Servidor seguro RTMPS",
            MinWidth = 320,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var destinationKey = new PasswordBox
        {
            MaxLength = 8192,
            MinWidth = 320,
            Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#34415E"),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var destinationKeyStatus = Label("", 12, "#98A4BE");

        string SelectedDestination() =>
            ObsStreamingDestinationPolicy.NormalizePlatform(destinationChoice.SelectedItem?.ToString() ?? "Twitch");

        string DestinationSecretName() => ObsStreamingDestinationPolicy.GetSecretName(SelectedDestination());

        void RefreshDestinationChoice()
        {
            var platform = SelectedDestination();
            var isYouTube = platform == "YouTube";
            destinationServer.Text = ObsStreamingDestinationPolicy.GetServer(platform);
            destinationKey.Clear();
            destinationKeyStatus.Text = secretStore.HasSecret(DestinationSecretName())
                ? "Clave guardada de forma cifrada para " + (isYouTube ? "YouTube." : "Twitch.") + " Dejá la caja vacía para reutilizarla."
                : "Todavía no hay una clave guardada para esta plataforma.";
            store.SetSetting("obs.stream.platform", isYouTube ? "YouTube" : "Twitch");
        }

        destinationChoice.SelectionChanged += (_, _) => RefreshDestinationChoice();
        destinationsPanel.Children.Add(Label("Plataforma", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationChoice);
        destinationsPanel.Children.Add(Label("Servidor RTMPS", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationServer);
        destinationsPanel.Children.Add(Label("Clave de transmisión (no la compartas con nadie)", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationKey);
        destinationsPanel.Children.Add(destinationKeyStatus);

        var destinationButtons = new WrapPanel();
        destinationButtons.Children.Add(ActionButton("Abrir panel de la plataforma", (_, _) =>
        {
            var url = destinationChoice.SelectedItem?.ToString() == "YouTube"
                ? "https://studio.youtube.com/"
                : "https://dashboard.twitch.tv/settings/stream";
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo abrir el panel web: " + exception.Message, "Transmisión", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }, true));
        destinationButtons.Children.Add(ActionButton("Guardar clave cifrada", (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationKey.Password))
                {
                    destinationKeyStatus.Text = secretStore.HasSecret(DestinationSecretName())
                        ? "Ya existe una clave cifrada. Dejá el campo vacío para conservarla."
                        : "Pegá primero la clave de la plataforma.";
                    return;
                }
                secretStore.Save(DestinationSecretName(), destinationKey.Password);
                destinationKey.Clear();
                destinationKeyStatus.Text = "Clave guardada de forma cifrada. No se guardó en SQLite.";
                store.Log("OBS", "Se guardó una clave de transmisión cifrada para " + destinationChoice.SelectedItem + ".");
            }
            catch (Exception exception)
            {
                destinationKeyStatus.Text = "No se pudo guardar la clave: " + exception.GetType().Name;
            }
        }));
        destinationButtons.Children.Add(ActionButton("Configurar destino en OBS", async (_, _) =>
        {
            try
            {
                if (!obs.IsConnected)
                    throw new InvalidOperationException("Primero conectá y autenticá OBS WebSocket en el panel de arriba.");
                var platform = SelectedDestination();
                if (!string.IsNullOrWhiteSpace(destinationKey.Password))
                {
                    secretStore.Save(DestinationSecretName(), destinationKey.Password);
                    destinationKey.Clear();
                }
                var key = secretStore.Read(DestinationSecretName());
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("Todavía no hay una clave guardada. Pegá la clave y pulsa «Guardar clave cifrada».");
                await obs.ConfigureStreamDestinationAsync(platform, key);
                store.SetSetting("obs.stream.platform", platform);
                destinationKeyStatus.Text = "OBS quedó configurado para " + platform + ". La transmisión todavía no se inició.";
                FooterStatus.Text = "Destino de OBS configurado para " + platform + "; falta iniciar la transmisión.";
                store.Log("OBS", "Destino de streaming configurado en OBS para " + platform + "; no se registró la clave.");
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo configurar el destino: " + exception.Message;
                MessageBox.Show(this, "No se pudo configurar la plataforma. La clave no se mostrará en el mensaje.\n\n" + exception.Message,
                    "Configurar transmisión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        destinationsPanel.Children.Add(destinationButtons);
        destinationsPanel.Children.Add(Label("Para iniciar: conectá OBS, configurá una plataforma aquí y pulsa «Iniciar transmisión» en el panel de arriba. Cambiar de destino exige detener la emisión actual.", 12, "#98A4BE"));

        var multistreamPanel = Panel("Emitir en Twitch y YouTube al mismo tiempo");
        multistreamPanel.Margin = new Thickness(0, 14, 0, 0);
        multistreamPanel.Children.Add(Label("OBS normal envía la salida principal a un destino. Para salir en los dos al mismo tiempo necesitás un complemento multi-RTMP de OBS. Es opcional, gratuito y externo; VTuberBot no lo instala ni lo incluye.", 12, "#98A4BE"));
        multistreamPanel.Children.Add(ActionButton("Abrir página del complemento multi-RTMP", (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://obsproject.com/forum/resources/multiple-rtmp-outputs-plugin.964/") { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo abrir la página del complemento: " + exception.Message, "Multistream", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }, true));
        multistreamPanel.Children.Add(Label("Instalá solo una versión compatible con tu versión de OBS; agrega YouTube y Twitch en el complemento y configura allí el destino secundario. Luego inicia cada salida según sus controles. Si compartís encoder, el consumo puede ser menor; si cada destino codifica por separado, aumentan el uso de CPU/GPU y el ancho de subida. La compatibilidad del complemento debe comprobarse en tu instalación.", 12, "#98A4BE"));
        RefreshDestinationChoice();
        advancedRoot.Children.Add(destinationsPanel);
        advancedRoot.Children.Add(multistreamPanel);

        var overlayPanel=Panel("Avatar overlay para OBS");overlayPanel.Children.Add(Label("El overlay se abre como ventana sin bordes y fondo transparente. En OBS agregá una fuente Captura de ventana y seleccioná “VTuberBot Avatar Overlay”. Doble clic en el overlay alterna Topmost; arrastrá para ubicarlo.",13,"#98A4BE"));
        overlayPanel.Children.Add(ActionButton("Mostrar overlay",(_,_)=>ShowOverlay(),true));advancedRoot.Children.Add(overlayPanel);
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
        var root = new DockPanel();
        var toolbar = new WrapPanel();
        DockPanel.SetDock(toolbar, Dock.Top);

        // This page is rebuilt during navigation, so subscribe to the WebView2 control
        // itself, not MainWindow.Loaded (which has already fired by the time the user
        // navigates here). Share one initialization task to avoid racing auto-load and
        // the toolbar action when both run as soon as the control enters the visual tree.
        var view = new WebView2();
        telegramView = view;
        Task? initializationTask = null;

        async Task EnsureTelegramWebAsync(bool openBrowserOnFailure)
        {
            try
            {
                if (view.CoreWebView2 is null)
                {
                    initializationTask ??= view.EnsureCoreWebView2Async();
                    await initializationTask;
                }

                if (view.Source is null)
                    view.Source = new Uri("https://web.telegram.org/");

                FooterStatus.Text = "Telegram Web cargando en WebView2.";
            }
            catch (Exception exception)
            {
                initializationTask = null;
                FooterStatus.Text = "WebView2 no disponible: " + exception.Message;
                store.Log("Telegram", "No se pudo inicializar WebView2 (" + exception.GetType().Name + ").");
                if (openBrowserOnFailure)
                    OpenTelegramExternal();
            }
        }

        toolbar.Children.Add(ActionButton(
            "Abrir Telegram Web",
            async (_, _) => await EnsureTelegramWebAsync(openBrowserOnFailure: true),
            true));
        toolbar.Children.Add(ActionButton("Abrir en navegador", (_, _) => OpenTelegramExternal()));

        root.Children.Add(toolbar);
        root.Children.Add(view);
        view.Loaded += async (_, _) => await EnsureTelegramWebAsync(openBrowserOnFailure: false);
        return root;
    }

    private void OpenTelegramExternal(){try{Process.Start(new ProcessStartInfo("https://web.telegram.org/"){UseShellExecute=true});store.Log("Telegram","Telegram Web abierto en navegador externo.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}
}
