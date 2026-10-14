using System.Collections.ObjectModel;
using System.ComponentModel;
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

public partial class MainWindow : Window
{
    private readonly VtuberApplication application;
    private readonly ProductStore store;
    private CommunityCommandRouter communityCommandRouter = null!;
    private readonly string dataDirectory;
    private readonly string avatarDirectory;
    private readonly string secretDirectory;
    private readonly TwitchChatClient twitch = new();
    private readonly TwitchEventSubClient eventSub = new();
    private readonly ObsWebSocketClient obs = new();
    private readonly LocalSpeechQueue speechQueue;
    private readonly LocalOpenAiCompatibleProvider localAiProvider = new();
    private readonly RemoteOpenAiCompatibleProvider remoteAiProvider;
    private readonly ConfiguredChatIntelligenceProvider configuredAiProvider;
    private readonly UserSecretStore secretStore;
    private SocialBotService socialBot = null!;
    private ChatReactionService chatReactions = null!;
    private Action<ChatReactionResult>? liveCenterReactionHandler;
    private Action<int>? liveCenterQueueHandler;
    private Action<bool>? liveCenterConnectionHandler;
    private AvatarOverlayWindow? overlay;
    private string? avatarPath;
    private bool microphoneVoiceActive;
    private bool speechPlaybackActive;
    private WaveInEvent? microphone;
    private WaveOutEvent? soundOutput;
    private AudioFileReader? soundReader;
    private readonly VoiceActivityDetector voiceActivity;
    private WebView2? telegramView;
    private ChatGptMiniWindow? chatGptMiniWindow;
    private readonly ObservableCollection<string> chatLines = [];
    private readonly DispatcherTimer automationTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer twitchCredentialTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly Dictionary<long, DateTimeOffset> automationLastRun = [];
    private readonly Dictionary<long, DateTimeOffset> automationOfflineLog = [];
    private readonly Dictionary<long, DateTimeOffset> automationFirstObserved = [];
    private int automationProcessing;
    private const int MaxPendingEventSubEvents = 200;
    private const int MaxPendingCriticalEventSubEvents = 20;
    private const int MaxEventSubEventsPerUiBatch = 50;
    private readonly BoundedQueue<(string Type, string Summary)> pendingEventSubEvents = new(MaxPendingEventSubEvents);
    private readonly BoundedQueue<(string Type, string Summary)> pendingCriticalEventSubEvents = new(MaxPendingCriticalEventSubEvents);
    private int eventSubProcessorScheduled;
    private int eventSubOverflowNotificationPending;
    private readonly Dictionary<string, DateTimeOffset> lastPointAward = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset lastPointAwardStorageWarning = DateTimeOffset.MinValue;
    private readonly Dictionary<string, DateTimeOffset> automaticModerationCooldown = new(StringComparer.OrdinalIgnoreCase);
    private const int CooldownPruneIntervalMessages = 1000;
    private int messagesSinceCooldownPrune;
    private string currentPage = "Inicio";
    private bool shutdownStarted;
    private bool shutdownCompleted;

    // Live values are sourced from the Core health monitor, not simulated.
    private DispatcherTimer? dashboardMetricsTimer;
    private TextBlock? dashboardCpuValue;
    private ProgressBar? dashboardCpuBar;
    private TextBlock? dashboardRamValue;
    private ProgressBar? dashboardRamBar;

    // Keep PRIVMSG processing ordered and bounded when commands await network calls.
    private const int MaxPendingTwitchMessages = 1000;
    private readonly Queue<string> pendingTwitchMessages = new();
    private readonly SemaphoreSlim twitchMessageSlots = new(MaxPendingTwitchMessages, MaxPendingTwitchMessages);
    private bool isProcessingTwitchMessages;
    private int twitchQueueOverflowNotificationPending;

    private PasswordBox? twitchTokenBox;
    private bool twitchChatDesired;
    private bool twitchEventSubDesired;
    private int twitchCredentialMaintenanceInProgress;
    private DateTimeOffset lastTwitchCredentialWarning = DateTimeOffset.MinValue;

    private static readonly string[] Pages =
    [
        "Inicio","Centro de directo","Avatar Studio","Avatar externo","Streamer.bot","Twitch y chat","Bot social","Comandos","Comunidad y puntos","Automatizaciones","Sorteos",
        "Minijuegos","Moderación","Voz y audio","OBS","Integraciones","Telegram Web","Actividad y diagnóstico","Ajustes"
    ];

    private void UpdateAvatarTalkState()
    {
        var nextState = VtuberBot.Core.AvatarTalkStatePolicy.Resolve(microphoneVoiceActive, speechPlaybackActive);
        var currentState = store.GetSetting("avatar.state", "Idle");
        if (string.Equals(currentState, nextState, StringComparison.OrdinalIgnoreCase))
            return;
        store.SetSetting("avatar.state", nextState);
        overlay?.SetState(nextState);
    }

    private void OpenChatGptMiniWindow_Click(object sender, RoutedEventArgs e) => OpenChatGptMiniWindow();

    private void OpenChatGptMiniWindow()
    {
        try
        {
            if (chatGptMiniWindow is null)
            {
                var created = new ChatGptMiniWindow(dataDirectory) { Owner = this };
                chatGptMiniWindow = created;
                created.Closed += (_, _) =>
                {
                    if (ReferenceEquals(chatGptMiniWindow, created))
                        chatGptMiniWindow = null;
                };
            }

            if (!chatGptMiniWindow.IsVisible)
                chatGptMiniWindow.Show();
            if (chatGptMiniWindow.WindowState == System.Windows.WindowState.Minimized)
                chatGptMiniWindow.WindowState = System.Windows.WindowState.Normal;
            chatGptMiniWindow.Activate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo abrir la ventana de ChatGPT: " + exception.Message +
                "\nPodés abrir ChatGPT en el navegador normal desde Bot social / IA.",
                "ChatGPT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HandleEventSubEvent(string type, string summary)
    {
        var targetQueue = EventSubQueuePolicy.IsCritical(type)
            ? pendingCriticalEventSubEvents
            : pendingEventSubEvents;

        if (!targetQueue.TryEnqueue((type, summary)))
        {
            if (Interlocked.Exchange(ref eventSubOverflowNotificationPending, 1) == 0)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        store.Log("Twitch EventSub",
                            "La cola de alertas alcanzó su capacidad; se descartan eventos de la categoría saturada hasta que baje la carga.")));
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Exchange(ref eventSubOverflowNotificationPending, 0);
                }
            }
            return;
        }

        ScheduleEventSubEventProcessing();
    }

    private void ScheduleEventSubEventProcessing()
    {
        if (Interlocked.Exchange(ref eventSubProcessorScheduled, 1) != 0)
            return;

        try
        {
            Dispatcher.BeginInvoke(new Action(ProcessPendingEventSubEvents));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref eventSubProcessorScheduled, 0);
        }
    }

    private void ProcessPendingEventSubEvents()
    {
        try
        {
            var processed = 0;
            while (processed < MaxEventSubEventsPerUiBatch)
            {
                // Revocations and subscription errors must not sit behind a burst of viewer alerts.
                if (!pendingCriticalEventSubEvents.TryDequeue(out var pending) &&
                    !pendingEventSubEvents.TryDequeue(out pending))
                    break;

                try
                {
                    ProcessEventSubEvent(pending.Type, pending.Summary);
                }
                catch (Exception exception)
                {
                    store.Log("Twitch EventSub",
                        "Falló el procesamiento de una notificación (" + exception.GetType().Name + ").");
                    FooterStatus.Text = "No se pudo procesar una alerta de Twitch; revisá Actividad y diagnóstico.";
                }

                processed++;
            }
        }
        finally
        {
            Interlocked.Exchange(ref eventSubProcessorScheduled, 0);
            if (pendingCriticalEventSubEvents.Count + pendingEventSubEvents.Count > 0)
                ScheduleEventSubEventProcessing();
            else
                Interlocked.Exchange(ref eventSubOverflowNotificationPending, 0);
        }
    }

    private void ProcessEventSubEvent(string type, string summary)
    {
        chatLines.Insert(0, "[EventSub] " + type + ": " + summary);
        while (chatLines.Count > 200)
            chatLines.RemoveAt(chatLines.Count - 1);
        store.Log("Twitch EventSub", type + ": " + summary);
        try
        {
            using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(new { type, summary }));
            eventActionCoordinator?.TryPublish(NormalizedBotEvent.Create(
                "VTuberBot.EventSub", "Twitch", type, payloadDocument.RootElement,
                origin: NormalizedEventOrigin.Internal));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
        {
            store.Log("Automatizaciones", "Se descartó un evento EventSub malformado (" + exception.GetType().Name + ").");
        }

        if (type is "subscription_error" or "revocation")
            return;

        if (store.GetSetting("twitch.speak_alerts", "false") == "true" &&
            !EnqueueLocalSpeech("Alerta de Twitch. " + summary, SpeechPriority.High))
        {
            store.Log("Audio", "Alerta EventSub no encolada: la cola de voz está llena o cerrada.");
        }

        if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
            return;

        if (overlay is null || !overlay.IsVisible)
            ShowOverlay();
        overlay?.ShowAlert(type + ": " + summary);

        if (store.GetSetting("twitch.avatar_reactions.enabled", "true") == "true")
        {
            var reactionState = AvatarReactionPolicy.GetTransientState(type);
            if (reactionState is not null && overlay?.ShowReaction(reactionState) == true)
                store.Log("Avatar", "Reacción temporal " + reactionState + " activada por el evento " + type + ".");
        }
    }

    public MainWindow(VtuberApplication application)
    {
        InitializeComponent();
        this.application = application;
        store = new ProductStore(application.Database.Path);
        const string previousDefaultVoiceInstruction = "Habla en español con una voz femenina adulta joven, cálida, expresiva y de estilo anime original. Buena dicción, energía y emoción natural; no imites a un personaje o actriz específica.";
        const string highPitchVoiceInstruction = "Habla en español con una voz femenina adulta joven MUY aguda y chillona, tipo personaje anime enérgico: timbre brillante, ligeramente nasal y juguetón, tono alto, reacciones exageradas y ritmo vivaz. Debe sonar intencionalmente aguda y caricaturesca, pero mantener dicción clara sin gritar ni distorsionar; no imites a un personaje, actriz ni persona real específica.";
        var savedVoice = store.GetSetting("social.tts.remoteVoice", "Leda");
        var savedVoiceInstructions = store.GetSetting("social.tts.remoteInstructions", previousDefaultVoiceInstruction);
        if (string.Equals(savedVoice, "Leda", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(savedVoiceInstructions, previousDefaultVoiceInstruction, StringComparison.Ordinal))
        {
            store.SetSetting("social.tts.remoteVoice", "Zephyr");
            store.SetSetting("social.tts.remoteInstructions", highPitchVoiceInstruction);
        }
        secretStore = new UserSecretStore(Path.Combine(application.Config.BaseDirectory, "secrets"));
        remoteAiProvider = new RemoteOpenAiCompatibleProvider(() => secretStore.Read("remote-ai-api-key"));
        configuredAiProvider = new ConfiguredChatIntelligenceProvider(localAiProvider, remoteAiProvider,
            () => store.GetSetting("social.ai.provider", "remote") == "remote");
        speechQueue = new LocalSpeechQueue(() => new ConfigurableSpeechSession(
            () => store.GetSetting("social.tts.provider", "remote") == "remote",
            LocalSpeechQueue.CreateSystemSpeechSession,
            () => new RemoteSpeechSession(
                secretStore,
                () => store.GetSetting("social.tts.remoteBaseUrl", "https://generativelanguage.googleapis.com/v1beta"),
                () => store.GetSetting("social.tts.remoteModel", "gemini-3.8-flash-tts"),
                () => store.GetSetting("social.tts.remoteVoice", "Zephyr"),
                () => store.GetSetting("social.tts.remoteInstructions",
                    "Habla en español con una voz femenina adulta joven MUY aguda y chillona, tipo personaje anime enérgico: timbre brillante, ligeramente nasal y juguetón, tono alto, reacciones exageradas y ritmo vivaz. Debe sonar intencionalmente aguda y caricaturesca, pero mantener dicción clara sin gritar ni distorsionar; no imites a un personaje, actriz ni persona real específica."),
                () => store.GetSetting("social.ai.remoteBaseUrl", "https://generativelanguage.googleapis.com/v1beta/openai/"))));
        communityCommandRouter = new CommunityCommandRouter(store);
        socialBot = new SocialBotService(store, speechQueue, twitch, configuredAiProvider, SetChatReactionExpression);
        chatReactions = new ChatReactionService(
            store,
            async (message, cancellationToken) =>
            {
                if (!twitch.IsConnected) return false;
                await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), message, cancellationToken).ConfigureAwait(false);
                return true; // IRC write completed; Twitch does not acknowledge that viewers saw it.
            },
            (text, language, requestedVoiceId) =>
            {
                var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var r) ? Math.Clamp(r, -10, 10) : 0;
                var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var v) ? Math.Clamp(v, 0, 100) : 100;
                var voiceId = string.IsNullOrWhiteSpace(requestedVoiceId) ? store.GetSetting("audio.tts.voiceId") : requestedVoiceId;
                if (language == "ja")
                {
                    try
                    {
                        var voices = LocalSpeechQueue.GetInstalledVoices();
                        var selectedVoice = voices.FirstOrDefault(voice => string.Equals(voice.Id, voiceId, StringComparison.OrdinalIgnoreCase));
                        if (selectedVoice is null || !(selectedVoice.Name.Contains("Japanese", StringComparison.OrdinalIgnoreCase) ||
                            selectedVoice.Name.Contains("ja-JP", StringComparison.OrdinalIgnoreCase) ||
                            selectedVoice.Name.Contains("Japan", StringComparison.OrdinalIgnoreCase)))
                        {
                            var japaneseVoice = voices.FirstOrDefault(voice =>
                                voice.Name.Contains("Japanese", StringComparison.OrdinalIgnoreCase) ||
                                voice.Name.Contains("ja-JP", StringComparison.OrdinalIgnoreCase) ||
                                voice.Name.Contains("Japan", StringComparison.OrdinalIgnoreCase));
                            if (japaneseVoice is null)
                            {
                                store.Log("Reacciones chat", "No hay una voz SAPI japonesa identificable; elegí un clip japonés en la regla o instalá una voz japonesa. No se encoló una pronunciación alternativa.");
                                return false;
                            }
                            voiceId = japaneseVoice.Id;
                        }
                    }
                    catch (Exception exception)
                    {
                        store.Log("Reacciones chat", "No se pudo verificar una voz japonesa SAPI (" + exception.GetType().Name + "); elegí un clip japonés.");
                        return false;
                    }
                }
                return speechQueue.Enqueue(text, SpeechPriority.Normal, voiceId, rate, volume, store.GetSetting("audio.tts.outputId"));
            },
            SetChatReactionExpression,
            () => twitch.IsConnected,
            playClip: PlayChatReactionClip);
        chatReactions.SetTwitchConnected(twitch.IsConnected);
        chatReactions.ResultChanged += result => Dispatcher.BeginInvoke(new Action(() =>
        {
            FooterStatus.Text = $"Reacción · {result.Outcome}: {result.Detail}";
        }));
        chatReactions.QueueChanged += pending => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (currentPage == "Centro de directo") FooterStatus.Text = $"Reacciones pendientes: {pending}";
        }));
        socialBot.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() =>
            FooterStatus.Text = $"Bot social · {status.State}: {status.Detail} · cola {status.Pending}"));
        InitializeEventActionCoordination();

        var savedVadThreshold = double.TryParse(
            store.GetSetting("audio.vad.threshold", "8"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var configuredThreshold)
            ? Math.Clamp(configuredThreshold, 2, 30)
            : 8;
        var savedVadHoldMilliseconds = double.TryParse(
            store.GetSetting("audio.vad.releaseHoldMs", "350"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var configuredHold)
            ? Math.Clamp(configuredHold, 100, 1200)
            : 350;
        voiceActivity = new VoiceActivityDetector(
            thresholdPercent: savedVadThreshold,
            releaseHold: TimeSpan.FromMilliseconds(savedVadHoldMilliseconds),
            smoothingFactor: 0.35);

        // Keep database, logs, avatar assets and encrypted secrets under the same
        // configured data root. This also honors VTUBERBOT_HOME for isolated validation.
        dataDirectory = application.Config.BaseDirectory;
        avatarDirectory = Path.Combine(dataDirectory, "avatars");
        secretDirectory = Path.Combine(dataDirectory, "secrets");
        Directory.CreateDirectory(avatarDirectory);
        Directory.CreateDirectory(secretDirectory);
        avatarPath = store.GetActiveAvatarProfile().BasePath;
        twitch.StatusChanged += status => Dispatcher.Invoke(() => { FooterStatus.Text = status; RuntimeStatus.Text = twitch.IsConnected ? "●  TWITCH CONECTADO" : "●  LOCAL ACTIVO"; });
        twitch.MessageReceived += HandleTwitchMessage;
        twitch.ConnectionStateChanged += chatReactions.SetTwitchConnected;
        eventSub.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() => FooterStatus.Text = status));
        speechQueue.StatusChanged += (status, pending) => Dispatcher.BeginInvoke(new Action(() =>
            FooterStatus.Text = $"Voz: {status} · pendientes: {pending}"));
        speechQueue.PlaybackStateChanged += active =>
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    speechPlaybackActive = active;
                    UpdateAvatarTalkState();
                }));
            }
            catch (InvalidOperationException)
            {
                // The speech worker may finish while WPF is shutting down.
            }
        };
        eventSub.EventReceived += HandleEventSubEvent;
        obs.StatusChanged += status => Dispatcher.BeginInvoke(new Action(() => FooterStatus.Text = status));
        streamerBot.StateChanged += HandleStreamerBotStateChanged;
        streamerBot.EventReceived += HandleStreamerBotEventReceived;
        // WPF may shut down its dispatcher as soon as the last window closes.
        // Defer the close until every async network client has completed disposal.

        automationTimer.Tick += async (_, _) => await ProcessAutomationsAsync();
        automationTimer.Start();
        twitchCredentialTimer.Tick += async (_, _) => await MaintainTwitchCredentialsAsync();
        twitchCredentialTimer.Start();
        Navigate("Inicio");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!shutdownCompleted)
        {
            e.Cancel = true;
            if (!shutdownStarted)
            {
                shutdownStarted = true;
                _ = ShutdownBeforeCloseAsync();
            }
            return;
        }

        base.OnClosing(e);
    }

    private async Task ShutdownBeforeCloseAsync()
    {
        dashboardMetricsTimer?.Stop();
        automationTimer.Stop();
        twitchCredentialTimer.Stop();

        try { StopMicrophone(); }
        catch (Exception exception) { LogShutdownFailure("micrófono", exception); }
        try { StopSoundPlayback(); }
        catch (Exception exception) { LogShutdownFailure("reproducción de audio", exception); }
        try { await chatReactions.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("reacciones de chat", exception); }
        try { await socialBot.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("bot social", exception); }
        try { localAiProvider.Dispose(); }
        catch (Exception exception) { LogShutdownFailure("proveedor IA local", exception); }
        try { remoteAiProvider.Dispose(); }
        catch (Exception exception) { LogShutdownFailure("proveedor IA remoto", exception); }
        try { speechQueue.Dispose(); }
        catch (Exception exception) { LogShutdownFailure("cola de voz", exception); }
        try { overlay?.Close(); }
        catch (Exception exception) { LogShutdownFailure("overlay", exception); }

        // Dispose each client independently so one failed network shutdown does not
        // prevent cleanup of the remaining clients.
        try { await twitch.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("Twitch IRC", exception); }
        try { await eventSub.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("Twitch EventSub", exception); }
        try { await obs.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("OBS WebSocket", exception); }
        try { if (eventActionCoordinator is not null) await eventActionCoordinator.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("coordinador de automatizaciones", exception); }
        try { await streamerBot.DisposeAsync(); }
        catch (Exception exception) { LogShutdownFailure("Streamer.bot WebSocket", exception); }

        shutdownCompleted = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    private void LogShutdownFailure(string resource, Exception exception)
    {
        try
        {
            store.Log("Cierre", "No se pudo cerrar limpiamente " + resource + " (" +
                exception.GetType().Name + ").");
        }
        catch
        {
            // A database/logging failure must not prevent the app from closing.
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) Navigate(page);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Navigate(currentPage);

    private static IEnumerable<Button> EnumerateNavigationButtons(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Button button)
                yield return button;
            if (child is DependencyObject nested)
            {
                foreach (var descendant in EnumerateNavigationButtons(nested))
                    yield return descendant;
            }
        }
    }

    private void Navigate(string page)
    {
        DetachLiveCenterHandlers();
        dashboardMetricsTimer?.Stop();
        currentPage = Pages.Contains(page, StringComparer.Ordinal) ? page : "Inicio";
        foreach (var navigationButton in EnumerateNavigationButtons(NavigationPanel))
        {
            var selected = string.Equals(navigationButton.Tag as string, currentPage, StringComparison.Ordinal);
            navigationButton.Background = Brush(selected ? "#173A4A" : "#161D2E");
            navigationButton.BorderBrush = Brush(selected ? "#2B879E" : "#29334D");
            navigationButton.Foreground = Brush(selected ? "#8DE6F8" : "#D8DEEE");
        }
        PageTitle.Text = currentPage;
        PageSubtitle.Text = currentPage switch
        {
            "Inicio" => "Todo lo importante del stream, en un solo lugar.",
            "Centro de directo" => "Salida pública del avatar y controles privados del creador, separados.",
            "Avatar Studio" => "Probá un ejemplo PNGTuber o 2D por capas, o importá tu propio arte.",
            "Avatar externo" => "Configura PNGTuber Plus, verifica los pasos de avatar y administra atajos locales.",
            "Streamer.bot" => "Conexión WebSocket, catálogo de acciones y suscripciones de eventos confirmadas por el servidor.",
            "Twitch y chat" => "Conexión oficial por IRC/TLS; la cuenta solo aparece conectada tras autenticación real.",
            "Bot social" => "Voz aguda de personaje, respuesta con IA y lectura del chat. Empieza en «Empezar aquí».",
            "Comandos" => "Un registro de comandos compartido entre pruebas locales y chat.",
            "Comunidad y puntos" => "Perfiles, puntos e historial persistidos en SQLite local.",
            "Automatizaciones" => "Eventos reales, reglas persistentes, destinos validados y límites de seguridad.",
            "Sorteos" => "Participantes deduplicados y ganador guardado en el historial.",
            "Minijuegos" => "Juegos locales con resultados y recompensas registradas.",
            "Moderación" => "Reglas configurables; el modo actual marca coincidencias sin castigar automáticamente.",
            "Voz y audio" => "Micrófono, VAD, TTS local y biblioteca de efectos de sonido.",
            "OBS" => "Escenas, estado real y destino de transmisión Twitch/YouTube mediante OBS autenticado.",
            "Integraciones" => "Aplicaciones externas opcionales; el proceso no equivale a una conexión verificada.",
            "Telegram Web" => "Telegram Web integrado cuando WebView2 está disponible.",
            "Actividad y diagnóstico" => "Registro local de acciones y exportación sin tokens ni cookies.",
            "Ajustes" => "Preferencias locales, seguridad y perfil de rendimiento.",
            _ => ""
        };
        Workspace.Content = currentPage switch
        {
            "Inicio" => BuildDashboard(),
            "Centro de directo" => BuildLiveCenterWorkspace(),
            "Avatar Studio" => BuildAvatarStudio(),
            "Avatar externo" => BuildPngtuberWorkspace(),
            "Streamer.bot" => BuildStreamerBotWorkspace(),
            "Twitch y chat" => BuildTwitchPage(),
            "Bot social" => BuildSocialBotPage(),
            "Comandos" => BuildCommandsPage(),
            "Comunidad y puntos" => BuildCommunityPage(),
            "Automatizaciones" => BuildEventActionWorkspace(),
            "Sorteos" => BuildGiveawaysPage(),
            "Minijuegos" => BuildMinigamesPage(),
            "Moderación" => BuildModerationPage(),
            "Voz y audio" => BuildAudioPage(),
            "OBS" => BuildObsPage(),
            "Integraciones" => BuildIntegrationsPage(),
            "Telegram Web" => BuildTelegramPage(),
            "Actividad y diagnóstico" => BuildActivityPage(),
            "Ajustes" => BuildSettingsPage(),
            _ => BuildDashboard()
        };

        if (currentPage == "Inicio")
            StartDashboardMetrics();
    }

    private TwitchOAuthCredentials? LoadTwitchCredentials()
    {
        var path = Path.Combine(secretDirectory, "twitch-token.bin");
        if (!File.Exists(path)) return null;
        try
        {
            var plaintext = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
            try
            {
                var credentials = JsonSerializer.Deserialize<TwitchOAuthCredentials>(plaintext);
                if (credentials is not null && !string.IsNullOrWhiteSpace(credentials.AccessToken))
                    return credentials;
            }
            catch (JsonException) { }

            // Backward compatibility: previous builds encrypted only the raw access token.
            if (!string.IsNullOrWhiteSpace(plaintext))
                return new TwitchOAuthCredentials(
                    plaintext.Trim(), null, DateTimeOffset.MinValue,
                    store.GetSetting("twitch.client_id"), []);
            return null;
        }
        catch
        {
            FooterStatus.Text = "No se pudieron descifrar las credenciales guardadas. Volvé a autorizar Twitch.";
            return null;
        }
    }

    private string LoadTwitchToken() => LoadTwitchCredentials()?.AccessToken ?? string.Empty;

    private void SaveTwitchCredentials(TwitchOAuthCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var json = JsonSerializer.Serialize(credentials);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Path.Combine(secretDirectory, "twitch-token.bin"), bytes);
    }

    private void SaveTwitchToken(string token)
    {
        var path = Path.Combine(secretDirectory, "twitch-token.bin");
        if (string.IsNullOrWhiteSpace(token))
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        SaveTwitchCredentials(new TwitchOAuthCredentials(
            token.Trim(), null, DateTimeOffset.MinValue,
            store.GetSetting("twitch.client_id"), []));
    }

    private async Task<TwitchOAuthCredentials> EnsureValidTwitchCredentialsAsync(
        string clientId,
        string? suppliedAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        var credentials = LoadTwitchCredentials();
        if (!string.IsNullOrWhiteSpace(suppliedAccessToken) &&
            (credentials is null || !string.Equals(credentials.AccessToken, suppliedAccessToken.Trim(), StringComparison.Ordinal)))
        {
            credentials = new TwitchOAuthCredentials(
                suppliedAccessToken.Trim(), null, DateTimeOffset.MinValue, clientId.Trim(), []);
        }

        if (credentials is null)
            throw new InvalidOperationException("No hay credenciales OAuth guardadas. Elegí Autorizar con Twitch primero.");

        var verified = await TwitchOAuth.ValidateOrRefreshAsync(
            clientId.Trim(), credentials, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(true);
        SaveTwitchCredentials(verified);
        if (twitchTokenBox is not null)
            twitchTokenBox.Password = verified.AccessToken;
        if (verified.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(10) &&
            string.IsNullOrWhiteSpace(verified.RefreshToken))
        {
            FooterStatus.Text = "Twitch está autenticado, pero no hay refresh token: será necesario volver a autorizar antes de que expire.";
        }
        return verified;
    }

    private async Task MaintainTwitchCredentialsAsync()
    {
        if ((!twitchChatDesired && !twitchEventSubDesired) ||
            Interlocked.Exchange(ref twitchCredentialMaintenanceInProgress, 1) != 0)
            return;

        try
        {
            var stored = LoadTwitchCredentials();
            if (stored is null) return;
            var clientId = store.GetSetting("twitch.client_id").Trim();
            if (string.IsNullOrWhiteSpace(clientId)) return;

            var oldAccessToken = stored.AccessToken;
            var credentials = await EnsureValidTwitchCredentialsAsync(clientId).ConfigureAwait(true);
            if (!string.Equals(oldAccessToken, credentials.AccessToken, StringComparison.Ordinal))
            {
                var reconnectChat = twitchChatDesired;
                var reconnectEventSub = twitchEventSubDesired;
                FooterStatus.Text = "Twitch renovó el token; restableciendo chat y alertas…";
                await twitch.DisconnectAsync().ConfigureAwait(true);
                await eventSub.DisconnectAsync().ConfigureAwait(true);

                if (reconnectChat)
                    await twitch.ConnectAsync(store.GetSetting("twitch.channel"), credentials.AccessToken).ConfigureAwait(true);
                if (reconnectEventSub)
                    await eventSub.ConnectAsync(clientId, credentials.AccessToken, store.GetSetting("twitch.channel")).ConfigureAwait(true);
                FooterStatus.Text = "Token Twitch renovado y conexiones solicitadas restauradas.";
            }
        }
        catch (Exception exception)
        {
            if (DateTimeOffset.UtcNow - lastTwitchCredentialWarning >= TimeSpan.FromMinutes(10))
            {
                lastTwitchCredentialWarning = DateTimeOffset.UtcNow;
                FooterStatus.Text = "No se pudo mantener OAuth de Twitch: " + exception.Message;
                store.Log("Twitch OAuth", "Mantenimiento de credenciales falló (" + exception.GetType().Name + ").");
            }

            var credentials = LoadTwitchCredentials();
            var authFailure = exception is InvalidOperationException &&
                (exception.Message.Contains("expiró", StringComparison.OrdinalIgnoreCase) ||
                 exception.Message.Contains("revocado", StringComparison.OrdinalIgnoreCase) ||
                 exception.Message.Contains("refresh token", StringComparison.OrdinalIgnoreCase) ||
                 exception.Message.Contains("Volvé a autorizar", StringComparison.OrdinalIgnoreCase));
            if (authFailure && credentials is not null && credentials.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                twitchChatDesired = false;
                twitchEventSubDesired = false;
                await twitch.DisconnectAsync().ConfigureAwait(true);
                await eventSub.DisconnectAsync().ConfigureAwait(true);
                FooterStatus.Text = "El token de Twitch expiró y no pudo renovarse. Volvé a autorizar la cuenta.";
            }
        }
        finally
        {
            Volatile.Write(ref twitchCredentialMaintenanceInProgress, 0);
        }
    }

    private static Transform BuildLayerTransform(AvatarLayer layer)
    {
        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(layer.Scale, layer.Scale));
        transform.Children.Add(new RotateTransform(layer.RotationDegrees));
        transform.Children.Add(new TranslateTransform(layer.OffsetX, layer.OffsetY));
        return transform;
    }

    private static BitmapImage LoadImage(string path) => AvatarImageLoader.Load(path, decodePixelWidth: 900);
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
