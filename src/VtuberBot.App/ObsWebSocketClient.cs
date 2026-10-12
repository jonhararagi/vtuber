using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VtuberBot.Core;

namespace VtuberBot.App;

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
            StatusChanged?.Invoke("Conectando a OBS WebSocket…");
            var establishedSocket = await OpenAndIdentifyAsync(
                normalizedHost, port, normalizedPassword, cancellation.Token).ConfigureAwait(false);
            socket = establishedSocket;
            cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            IsConnected = true;
            StatusChanged?.Invoke("OBS WebSocket autenticado.");
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
            string? authentication = null;
            if (data.TryGetProperty("authentication", out var auth))
            {
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("OBS requiere contraseña para WebSocket.");
                var challenge = auth.GetProperty("challenge").GetString()
                    ?? throw new InvalidOperationException("OBS no envió el challenge de autenticación.");
                var salt = auth.GetProperty("salt").GetString()
                    ?? throw new InvalidOperationException("OBS no envió el salt de autenticación.");
                var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
                authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
            }

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
            await SendJsonAsync(activeSocket,
                new { op = 6, d = new { requestType, requestId = id, requestData = requestData ?? new { } } },
                cancellationToken).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (gate) pending.Remove(id);
        }
    }

    public async Task<string> GetVersionAsync()
    {
        var response = await RequestAsync("GetVersion").ConfigureAwait(false);
        return response.GetProperty("responseData").GetProperty("obsVersion").GetString() ?? "Versión desconocida";
    }

    public async Task<IReadOnlyList<string>> GetScenesAsync()
    {
        var response = await RequestAsync("GetSceneList").ConfigureAwait(false);
        return response.GetProperty("responseData").GetProperty("scenes").EnumerateArray()
            .Select(scene => scene.GetProperty("sceneName").GetString() ?? "")
            .Where(name => name.Length > 0).ToArray();
    }

    public async Task SetCurrentSceneAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Elegí una escena.");
        var response = await RequestAsync("SetCurrentProgramScene", new { sceneName }).ConfigureAwait(false);
        var status = response.GetProperty("requestStatus");
        if (!status.GetProperty("result").GetBoolean())
            throw new InvalidOperationException(status.TryGetProperty("comment", out var comment)
                ? comment.GetString()
                : "OBS no pudo cambiar la escena.");
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
                    StatusChanged?.Invoke($"OBS perdió conexión; nuevo intento en {delay.TotalSeconds:0} s.");
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
                        StatusChanged?.Invoke("OBS reconectado y autenticado.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        failedAttempts++;
                        IsConnected = false;
                        StatusChanged?.Invoke("Falló la reconexión de OBS (" + exception.GetType().Name + "); se reintentará.");
                        continue;
                    }
                }

                try
                {
                    while (!cancellationToken.IsCancellationRequested && activeSocket.State == WebSocketState.Open)
                    {
                        using var message = await ReadJsonAsync(activeSocket, cancellationToken).ConfigureAwait(false);
                        var root = message.RootElement;
                        if (!root.TryGetProperty("op", out var op) || op.GetInt32() != 7)
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
                    StatusChanged?.Invoke("OBS WebSocket interrumpido (" + exception.GetType().Name + "). Se intentará reconectar.");
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
            StatusChanged?.Invoke("OBS WebSocket desconectado.");
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
        StatusChanged?.Invoke("OBS desconectado.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        sendGate.Dispose();
    }
}
