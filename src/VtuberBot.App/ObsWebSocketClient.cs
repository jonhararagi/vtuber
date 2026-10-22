using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VtuberBot.Core;

namespace VtuberBot.App;

public sealed record ObsSceneItem(string Name, long Id, bool Enabled)
{
    public string DisplayLabel => $"{Name} — {(Enabled ? "visible" : "oculta")}";
}

public sealed record ObsAudioInput(string Name, string Kind, bool Muted)
{
    public string DisplayLabel => $"{Name} — {(Muted ? "silenciado" : "activo")}";
}

public sealed record ObsPerformanceStats(
    double ActiveFps,
    double AverageFrameRenderTimeMs,
    long RenderSkippedFrames,
    long RenderTotalFrames,
    long OutputSkippedFrames,
    long OutputTotalFrames)
{
    public double RenderSkippedPercent => CalculatePercentage(RenderSkippedFrames, RenderTotalFrames);
    public double OutputSkippedPercent => CalculatePercentage(OutputSkippedFrames, OutputTotalFrames);

    public string DisplayLabel =>
        $"OBS render: {ActiveFps:0.0} FPS · {AverageFrameRenderTimeMs:0.00} ms/cuadro · " +
        $"omisiones de render {RenderSkippedPercent:0.##}% · salida {OutputSkippedPercent:0.##}%";

    private static double CalculatePercentage(long skipped, long total) =>
        total <= 0 ? 0 : Math.Clamp(skipped / (double)total * 100d, 0d, 100d);
}

public sealed record ObsVideoSettingsSnapshot(
    int BaseWidth,
    int BaseHeight,
    int OutputWidth,
    int OutputHeight,
    int FpsNumerator,
    int FpsDenominator)
{
    public bool Is720p30 =>
        BaseWidth == 1280 && BaseHeight == 720 &&
        OutputWidth == 1280 && OutputHeight == 720 &&
        FpsDenominator > 0 && FpsNumerator / (double)FpsDenominator == 30d;

    public string DisplayLabel =>
        $"Lienzo {BaseWidth}×{BaseHeight}; salida {OutputWidth}×{OutputHeight}; FPS {FpsNumerator}/{FpsDenominator}";
}

public sealed record ObsReadOnlyDiagnosticsResult(
    string ObsVersion,
    IReadOnlyList<string> Scenes,
    IReadOnlyList<ObsAudioInput> AudioInputs,
    ObsVideoSettingsSnapshot Video,
    bool IsStreaming,
    bool IsRecording,
    ObsPerformanceStats Performance)
{
    public string ToDisplayText()
    {
        var lines = new List<string>
        {
            "RESULTADO: consulta WebSocket autenticada completada. No se inició, detuvo ni modificó ninguna salida.",
            "Versión OBS: " + ObsVersion,
            "Escenas (" + Scenes.Count + "): " +
                (Scenes.Count == 0 ? "ninguna" : string.Join(", ", Scenes.Take(20)) + (Scenes.Count > 20 ? ", …" : "")),
            "Fuentes de audio con estado de silencio consultable (" + AudioInputs.Count + "): " +
                (AudioInputs.Count == 0 ? "ninguna detectada" : string.Join("; ", AudioInputs.Take(16).Select(item => item.DisplayLabel))),
            "Vídeo: " + Video.DisplayLabel + (Video.Is720p30 ? " (720p30 verificado)" : " (distinto de 720p30)"),
            "Transmisión: " + (IsStreaming ? "activa" : "inactiva") + "; grabación: " + (IsRecording ? "activa" : "inactiva"),
            Performance.DisplayLabel,
            "",
            "Esto verifica la API y lo que OBS declara. No comprueba que el micrófono físico tenga voz audible, que el overlay se vea correctamente ni que un archivo grabado se reproduzca bien."
        };

        if (AudioInputs.Count == 0)
            lines.Insert(4, "AVISO: OBS no informó entradas de audio con control de silencio; revisá el Mezclador de audio manualmente.");
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>OBS Studio WebSocket v5 client with automatic reconnect after transient connection loss.</summary>
/// <remarks>Connection is reported as active only after Hello/Identify succeeds. Credentials stay in memory only.</remarks>
public sealed class ObsWebSocketClient : IAsyncDisposable
{
    private ClientWebSocket? socket;
    private CancellationTokenSource? cancellation;
    private Task? receiveTask;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> pending = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public bool IsConnected { get; private set; }
    public bool IsActive => cancellation is not null;
    public event Action<string>? StatusChanged;
    public event Action<string, JsonElement>? EventReceived;

    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default)
    {
        if (cancellation is not null)
            throw new InvalidOperationException("OBS ya está conectado o intentando reconectarse.");
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            throw new ArgumentException("Host o puerto inválido.");

        var normalizedHost = host.Trim();
        var normalizedPassword = password ?? string.Empty;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            SafeEventDispatch.Invoke(StatusChanged, "Conectando a OBS WebSocket…");
            var establishedSocket = await OpenAndIdentifyAsync(
                normalizedHost, port, normalizedPassword, cancellation.Token).ConfigureAwait(false);
            socket = establishedSocket;
            cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            IsConnected = true;
            SafeEventDispatch.Invoke(StatusChanged, "OBS WebSocket autenticado.");
            receiveTask = SuperviseConnectionAsync(
                establishedSocket, normalizedHost, port, normalizedPassword, cancellation.Token);
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<ClientWebSocket> OpenAndIdentifyAsync(
        string host,
        int port,
        string password,
        CancellationToken cancellationToken)
    {
        var candidate = new ClientWebSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await candidate.ConnectAsync(new Uri($"ws://{host}:{port}"), timeout.Token).ConfigureAwait(false);
            using var hello = await ReadJsonAsync(candidate, timeout.Token).ConfigureAwait(false);
            if (hello.RootElement.GetProperty("op").GetInt32() != 0)
                throw new InvalidOperationException("OBS no envió el mensaje Hello esperado.");

            var data = hello.RootElement.GetProperty("d");
            var requiresAuthentication = data.TryGetProperty("authentication", out var auth);
            ObsAuthenticationPolicy.ValidateHandshake(requiresAuthentication, password);

            var challenge = auth.GetProperty("challenge").GetString()
                ?? throw new InvalidOperationException("OBS no envió el challenge de autenticación.");
            var salt = auth.GetProperty("salt").GetString()
                ?? throw new InvalidOperationException("OBS no envió el salt de autenticación.");
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            var authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));

            await SendJsonAsync(candidate,
                new { op = 1, d = new { rpcVersion = 1, authentication, eventSubscriptions = 0x7FF } },
                timeout.Token).ConfigureAwait(false);

            using var identified = await ReadJsonAsync(candidate, timeout.Token).ConfigureAwait(false);
            if (identified.RootElement.GetProperty("op").GetInt32() != 2)
                throw new InvalidOperationException("OBS rechazó Identify. Revisá la contraseña y el puerto.");
            return candidate;
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    public async Task<JsonElement> RequestAsync(
        string requestType,
        object? requestData = null,
        CancellationToken cancellationToken = default)
    {
        var activeSocket = socket;
        if (!IsConnected || activeSocket is null || activeSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("OBS no está autenticado o está reconectando.");

        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate) pending.Add(id, completion);
        try
        {
            // The deadline covers both sending and waiting for the response. Starting it
            // after SendJsonAsync could leave a stalled socket write without any timeout.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await SendJsonAsync(activeSocket,
                new { op = 6, d = new { requestType, requestId = id, requestData = requestData ?? new { } } },
                timeout.Token).ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (gate) pending.Remove(id);
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetVersion", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver su versión.");
        return response.GetProperty("responseData").GetProperty("obsVersion").GetString() ?? "Versión desconocida";
    }

    /// <summary>
    /// Returns the local record directory declared by the authenticated OBS instance.
    /// The value is used only for a user-confirmed local recording verification.
    /// </summary>
    public async Task<string> GetRecordDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetRecordDirectory", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver la carpeta de grabaciones.");
        if (!response.TryGetProperty("responseData", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("recordDirectory", out var directoryElement) ||
            directoryElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("OBS devolvió una carpeta de grabaciones con formato inesperado.");

        var directory = directoryElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new InvalidDataException("OBS devolvió una carpeta de grabaciones vacía o no absoluta.");

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new InvalidDataException("OBS devolvió una carpeta de grabaciones no válida.", exception);
        }
    }

    /// <summary>Reads OBS's own render/output counters; this is separate from Windows process CPU/RAM metrics.</summary>
    public async Task<ObsPerformanceStats> GetPerformanceStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetStats", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver sus estadísticas de render.");

        var data = response.GetProperty("responseData");
        static double ReadDouble(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result))
                return result;
            throw new InvalidOperationException("OBS no devolvió la estadística numérica «" + name + "».");
        }

        static long ReadCount(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result))
                return result;
            throw new InvalidOperationException("OBS no devolvió el contador «" + name + "».");
        }

        return new ObsPerformanceStats(
            ReadDouble(data, "activeFps"),
            ReadDouble(data, "averageFrameRenderTime"),
            ReadCount(data, "renderSkippedFrames"),
            ReadCount(data, "renderTotalFrames"),
            ReadCount(data, "outputSkippedFrames"),
            ReadCount(data, "outputTotalFrames"));
    }

    public async Task<IReadOnlyList<string>> GetScenesAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetSceneList", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver su lista de escenas.");
        return response.GetProperty("responseData").GetProperty("scenes").EnumerateArray()
            .Select(scene => scene.GetProperty("sceneName").GetString() ?? "")
            .Where(name => name.Length > 0).Take(500).ToArray();
    }

    // OBS v5 identifies a scene item by sceneItemId; sourceName alone may not be unique.
    public async Task<IReadOnlyList<ObsSceneItem>> GetSceneItemsAsync(string sceneName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Elegí una escena antes de consultar sus fuentes.", nameof(sceneName));

        var response = await RequestAsync("GetSceneItemList", new { sceneName }, cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver las fuentes de la escena.");
        var items = response.GetProperty("responseData").GetProperty("sceneItems");
        return items.EnumerateArray()
            .Select(item => new ObsSceneItem(
                item.GetProperty("sourceName").GetString() ?? string.Empty,
                item.GetProperty("sceneItemId").GetInt64(),
                item.TryGetProperty("sceneItemEnabled", out var enabled) && enabled.GetBoolean()))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    public async Task SetSceneItemEnabledAsync(string sceneName, long sceneItemId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Elegí una escena.", nameof(sceneName));
        if (sceneItemId < 0)
            throw new ArgumentOutOfRangeException(nameof(sceneItemId));

        var response = await RequestAsync("SetSceneItemEnabled",
            new { sceneName, sceneItemId, sceneItemEnabled = enabled }, cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo cambiar la visibilidad de la fuente.");
    }

    public async Task<IReadOnlyList<ObsAudioInput>> GetAudioInputsAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetInputList", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo consultar las fuentes de entrada.");
        var candidates = response.GetProperty("responseData").GetProperty("inputs").EnumerateArray()
            .Select(input => new
            {
                Name = input.GetProperty("inputName").GetString() ?? string.Empty,
                Kind = input.GetProperty("inputKind").GetString() ?? string.Empty
            })
            .Where(input => !string.IsNullOrWhiteSpace(input.Name) && ObsInputKindPolicy.IsPotentialAudioInput(input.Kind))
            .Take(32)
            .ToArray();

        var result = new List<ObsAudioInput>(candidates.Length);
        foreach (var input in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A failed GetInputMute response means this source has no controllable audio mixer;
            // transport failures still propagate from RequestAsync and are not hidden.
            var muteResponse = await RequestAsync("GetInputMute", new { inputName = input.Name }, cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureRequestSucceeded(muteResponse, "La fuente no expone un estado de silencio.");
                var muted = muteResponse.GetProperty("responseData").GetProperty("inputMuted").GetBoolean();
                result.Add(new ObsAudioInput(input.Name, input.Kind, muted));
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        return result;
    }

    public async Task SetInputMuteAsync(string inputName, bool muted, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputName))
            throw new ArgumentException("Elegí una fuente de audio.", nameof(inputName));

        var response = await RequestAsync("SetInputMute", new { inputName, inputMuted = muted }, cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo cambiar el silencio de la fuente.");
    }

    public async Task SetCurrentSceneAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Elegí una escena.");
        var response = await RequestAsync("SetCurrentProgramScene", new { sceneName }).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo cambiar la escena.");
    }

    public async Task<ObsVideoSettingsSnapshot> GetVideoSettingsAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync("GetVideoSettings", cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, "OBS no pudo devolver los ajustes de vídeo.");
        if (!response.TryGetProperty("responseData", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("OBS devolvió unos ajustes de vídeo con formato inesperado.");

        static int ReadInt(JsonElement value, string name)
        {
            if (value.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var parsed))
                return parsed;
            throw new InvalidDataException("OBS no devolvió el campo de vídeo válido «" + name + "».");
        }

        var snapshot = new ObsVideoSettingsSnapshot(
            ReadInt(data, "baseWidth"),
            ReadInt(data, "baseHeight"),
            ReadInt(data, "outputWidth"),
            ReadInt(data, "outputHeight"),
            ReadInt(data, "fpsNumerator"),
            ReadInt(data, "fpsDenominator"));
        if (snapshot.BaseWidth <= 0 || snapshot.BaseHeight <= 0 ||
            snapshot.OutputWidth <= 0 || snapshot.OutputHeight <= 0 ||
            snapshot.FpsNumerator <= 0 || snapshot.FpsDenominator <= 0)
            throw new InvalidDataException("OBS devolvió dimensiones o FPS fuera de rango.");
        return snapshot;
    }

    /// <summary>
    /// Runs a bounded, read-only preflight against the authenticated OBS instance. It never
    /// starts/stops streaming or recording and does not modify settings.
    /// </summary>
    public async Task<ObsReadOnlyDiagnosticsResult> RunReadOnlyDiagnosticsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Conectá y autenticá OBS antes de ejecutar el diagnóstico.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            var version = await GetVersionAsync(deadline.Token).ConfigureAwait(false);
            var scenes = await GetScenesAsync(deadline.Token).ConfigureAwait(false);
            var audioInputs = await GetAudioInputsAsync(deadline.Token).ConfigureAwait(false);
            var video = await GetVideoSettingsAsync(deadline.Token).ConfigureAwait(false);
            var streaming = await IsStreamingAsync(deadline.Token).ConfigureAwait(false);
            var recording = await IsRecordingAsync(deadline.Token).ConfigureAwait(false);
            var performance = await GetPerformanceStatsAsync(deadline.Token).ConfigureAwait(false);

            return new ObsReadOnlyDiagnosticsResult(version, scenes, audioInputs, video, streaming, recording, performance);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("El diagnóstico OBS superó 40 segundos. Revisá la respuesta de WebSocket y la carga del equipo.");
        }
    }

    /// <summary>Applies a conservative 720p30 video profile and verifies the settings reported back by OBS.</summary>
    public async Task<string> ApplyLightVideoProfileAsync(CancellationToken cancellationToken = default)
    {
        var requested = await RequestAsync("SetVideoSettings", new
        {
            baseWidth = 1280,
            baseHeight = 720,
            outputWidth = 1280,
            outputHeight = 720,
            fpsNumerator = 30,
            fpsDenominator = 1
        }, cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(requested, "OBS no pudo aplicar el perfil de vídeo 720p30.");

        var verified = await GetVideoSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!verified.Is720p30)
            throw new InvalidOperationException(
                "OBS devolvió una configuración diferente de la solicitada: " + verified.DisplayLabel + ".");

        return "Perfil verificado en OBS: lienzo y salida 1280×720 a 30 FPS.";
    }

    public Task<bool> IsStreamingAsync(CancellationToken cancellationToken = default) =>
        GetOutputActiveAsync("GetStreamStatus", "estado de transmisión", cancellationToken);

    public Task<bool> IsRecordingAsync(CancellationToken cancellationToken = default) =>
        GetOutputActiveAsync("GetRecordStatus", "estado de grabación", cancellationToken);

    /// <summary>
    /// Configures OBS's single native streaming destination. Simultaneous multi-platform
    /// output requires a separately installed OBS multi-output plugin.
    /// </summary>
    public async Task ConfigureStreamDestinationAsync(
        string platform,
        string streamKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = ObsStreamingDestinationPolicy.ValidateAndNormalizeStreamKey(streamKey);
        var normalizedPlatform = ObsStreamingDestinationPolicy.NormalizePlatform(platform);
        var server = ObsStreamingDestinationPolicy.GetServer(normalizedPlatform);

        if (await IsStreamingAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Detené la transmisión actual de OBS antes de cambiar el destino.");

        var response = await RequestAsync(
            "SetStreamServiceSettings",
            new
            {
                streamServiceType = "rtmp_custom",
                streamServiceSettings = new { server, key = normalizedKey }
            },
            cancellationToken).ConfigureAwait(false);

        EnsureRequestSucceeded(response, "OBS no pudo configurar el destino de transmisión.");
    }

    public Task StartStreamingAsync(CancellationToken cancellationToken = default) =>
        ExecuteOutputRequestAsync("StartStream", "OBS no pudo iniciar la transmisión.", cancellationToken);

    public Task StopStreamingAsync(CancellationToken cancellationToken = default) =>
        ExecuteOutputRequestAsync("StopStream", "OBS no pudo detener la transmisión.", cancellationToken);

    public Task StartRecordingAsync(CancellationToken cancellationToken = default) =>
        ExecuteOutputRequestAsync("StartRecord", "OBS no pudo iniciar la grabación.", cancellationToken);

    public Task StopRecordingAsync(CancellationToken cancellationToken = default) =>
        ExecuteOutputRequestAsync("StopRecord", "OBS no pudo detener la grabación.", cancellationToken);

    private async Task<bool> GetOutputActiveAsync(
        string requestType,
        string outputDescription,
        CancellationToken cancellationToken)
    {
        var response = await RequestAsync(requestType, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!response.TryGetProperty("responseData", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("outputActive", out var active) ||
            active.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("OBS no devolvió un estado válido para " + outputDescription + ".");
        }

        return active.GetBoolean();
    }

    private async Task ExecuteOutputRequestAsync(
        string requestType,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var response = await RequestAsync(requestType, cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureRequestSucceeded(response, failureMessage);
    }

    private static void EnsureRequestSucceeded(JsonElement response, string failureMessage)
    {
        if (!response.TryGetProperty("requestStatus", out var status) ||
            status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.True ||
            !result.GetBoolean())
        {
            var comment = status.ValueKind == JsonValueKind.Object &&
                status.TryGetProperty("comment", out var commentElement)
                ? commentElement.GetString()
                : null;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(comment)
                ? failureMessage
                : failureMessage + " " + comment);
        }
    }

    private async Task SuperviseConnectionAsync(
        ClientWebSocket initialSocket,
        string host,
        int port,
        string password,
        CancellationToken cancellationToken)
    {
        ClientWebSocket? activeSocket = initialSocket;
        var failedAttempts = 0;
        var connectedAt = DateTimeOffset.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (activeSocket is null)
                {
                    var delay = ReconnectBackoff.GetDelay(Math.Max(1, failedAttempts));
                    SafeEventDispatch.Invoke(StatusChanged, $"OBS perdió conexión; nuevo intento en {delay.TotalSeconds:0} s.");
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        activeSocket = await OpenAndIdentifyAsync(host, port, password, cancellationToken).ConfigureAwait(false);
                        socket = activeSocket;
                        connectedAt = DateTimeOffset.UtcNow;
                        IsConnected = true;
                        SafeEventDispatch.Invoke(StatusChanged, "OBS reconectado y autenticado.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        failedAttempts++;
                        IsConnected = false;
                        SafeEventDispatch.Invoke(StatusChanged, "Falló la reconexión de OBS (" + exception.GetType().Name + "); se reintentará.");
                        continue;
                    }
                }

                try
                {
                    while (!cancellationToken.IsCancellationRequested && activeSocket.State == WebSocketState.Open)
                    {
                        using var message = await ReadJsonAsync(activeSocket, cancellationToken).ConfigureAwait(false);
                        var root = message.RootElement;
                        if (!root.TryGetProperty("op", out var op))
                            continue;

                        var opcode = op.GetInt32();
                        if (opcode == 5)
                        {
                            var envelope = root.GetProperty("d");
                            if (envelope.TryGetProperty("eventType", out var typeElement) &&
                                typeElement.ValueKind == JsonValueKind.String)
                            {
                                var eventType = typeElement.GetString() ?? string.Empty;
                                var eventData = envelope.TryGetProperty("eventData", out var eventPayload)
                                    ? eventPayload.Clone()
                                    : default;
                                VtuberBot.Core.SafeEventDispatch.Invoke(EventReceived, eventType, eventData);
                            }
                            continue;
                        }

                        if (opcode != 7)
                            continue;

                        var data = root.GetProperty("d");
                        var id = data.GetProperty("requestId").GetString() ?? "";
                        TaskCompletionSource<JsonElement>? completion;
                        lock (gate) pending.TryGetValue(id, out completion);
                        completion?.TrySetResult(data.Clone());
                    }

                    if (!cancellationToken.IsCancellationRequested)
                        throw new WebSocketException("OBS cerró la conexión.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    IsConnected = false;
                    FailPendingRequests(exception);
                    if (DateTimeOffset.UtcNow - connectedAt >= TimeSpan.FromSeconds(30))
                        failedAttempts = 0;
                    failedAttempts++;
                    SafeEventDispatch.Invoke(StatusChanged, "OBS WebSocket interrumpido (" + exception.GetType().Name + "). Se intentará reconectar.");
                    if (ReferenceEquals(socket, activeSocket))
                        socket = null;
                    activeSocket.Dispose();
                    activeSocket = null;
                }
            }
        }
        finally
        {
            IsConnected = false;
            FailPendingRequests(new OperationCanceledException("La sesión OBS se desconectó antes de responder."));
            SafeEventDispatch.Invoke(StatusChanged, "OBS WebSocket desconectado.");
        }
    }

    private void FailPendingRequests(Exception exception)
    {
        lock (gate)
        {
            foreach (var completion in pending.Values)
                completion.TrySetException(exception);
            pending.Clear();
        }
    }

    private async Task SendJsonAsync(ClientWebSocket target, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (target.State != WebSocketState.Open)
                throw new WebSocketException("OBS WebSocket no está abierto.");
            await target.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(ClientWebSocket target, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await target.ReceiveAsync(new ArraySegment<byte>(chunk), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("OBS cerró la conexión.");
            buffer.Write(chunk, 0, result.Count);
            if (buffer.Length > 1_048_576)
                throw new InvalidOperationException("Mensaje WebSocket de OBS supera el límite de 1 MiB.");
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(buffer.ToArray());
    }

    public async Task DisconnectAsync()
    {
        cancellation?.Cancel();
        var currentSocket = socket;
        if (currentSocket is { State: WebSocketState.Open })
        {
            try
            {
                await currentSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "VTuberBot disconnect",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
        }

        if (receiveTask is not null)
        {
            try { await receiveTask.ConfigureAwait(false); }
            catch { }
        }

        socket?.Dispose();
        cancellation?.Dispose();
        socket = null;
        cancellation = null;
        receiveTask = null;
        IsConnected = false;
        FailPendingRequests(new OperationCanceledException("OBS se desconectó manualmente."));
        SafeEventDispatch.Invoke(StatusChanged, "OBS desconectado.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        sendGate.Dispose();
    }
}
