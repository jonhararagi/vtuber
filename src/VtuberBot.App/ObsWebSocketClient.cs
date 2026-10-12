using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>OBS Studio WebSocket v5 client. Connection is reported only after Identify succeeds.</summary>
public sealed class ObsWebSocketClient : IAsyncDisposable
{
    private ClientWebSocket? socket;
    private CancellationTokenSource? cancellation;
    private Task? receiveTask;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> pending = new(StringComparer.Ordinal);
    private readonly object gate = new();
    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(string host, int port, string password, CancellationToken cancellationToken = default)
    {
        if (IsConnected) throw new InvalidOperationException("OBS ya está conectado.");
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535) throw new ArgumentException("Host o puerto inválido.");
        socket = new ClientWebSocket();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            StatusChanged?.Invoke("Conectando a OBS WebSocket…");
            await socket.ConnectAsync(new Uri($"ws://{host}:{port}"), cancellation.Token).ConfigureAwait(false);
            using var hello = await ReadJsonAsync(socket, cancellation.Token).ConfigureAwait(false);
            if (hello.RootElement.GetProperty("op").GetInt32() != 0) throw new InvalidOperationException("OBS no envió el mensaje Hello esperado.");
            var data = hello.RootElement.GetProperty("d");
            string? authentication = null;
            if (data.TryGetProperty("authentication", out var auth))
            {
                if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("OBS requiere contraseña para WebSocket.");
                var challenge = auth.GetProperty("challenge").GetString()!;
                var salt = auth.GetProperty("salt").GetString()!;
                var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
                authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
            }
            await SendJsonAsync(new { op = 1, d = new { rpcVersion = 1, authentication, eventSubscriptions = 0x7FF } }, cancellation.Token).ConfigureAwait(false);
            using var identified = await ReadJsonAsync(socket, cancellation.Token).ConfigureAwait(false);
            if (identified.RootElement.GetProperty("op").GetInt32() != 2) throw new InvalidOperationException("OBS rechazó Identify. Revisá la contraseña y el puerto.");
            cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            IsConnected = true;
            StatusChanged?.Invoke("OBS WebSocket autenticado.");
            receiveTask = ReceiveLoopAsync(socket, cancellation.Token);
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<JsonElement> RequestAsync(string requestType, object? requestData = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || socket is null) throw new InvalidOperationException("OBS no está autenticado.");
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate) pending.Add(id, completion);
        try
        {
            await SendJsonAsync(new { op = 6, d = new { requestType, requestId = id, requestData = requestData ?? new { } } }, cancellationToken).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally { lock (gate) pending.Remove(id); }
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
        if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Elegí una escena.");
        var response = await RequestAsync("SetCurrentProgramScene", new { sceneName }).ConfigureAwait(false);
        var status = response.GetProperty("requestStatus");
        if (!status.GetProperty("result").GetBoolean())
            throw new InvalidOperationException(status.TryGetProperty("comment", out var comment) ? comment.GetString() : "OBS no pudo cambiar la escena.");
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                using var message = await ReadJsonAsync(ws, ct).ConfigureAwait(false);
                var root = message.RootElement;
                if (root.GetProperty("op").GetInt32() != 7) continue;
                var data = root.GetProperty("d");
                var id = data.GetProperty("requestId").GetString() ?? "";
                TaskCompletionSource<JsonElement>? completion;
                lock (gate) pending.TryGetValue(id, out completion);
                completion?.TrySetResult(data.Clone());
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) { StatusChanged?.Invoke("OBS WebSocket interrumpido: " + ex.Message); }
        catch (Exception ex) { StatusChanged?.Invoke("Error de protocolo OBS: " + ex.Message); }
        finally { IsConnected = false; StatusChanged?.Invoke("OBS WebSocket desconectado."); }
    }

    private async Task SendJsonAsync(object payload, CancellationToken ct)
    {
        var ws = socket ?? throw new InvalidOperationException("No hay socket OBS.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await sendGate.WaitAsync(ct).ConfigureAwait(false);
        try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false); }
        finally { sendGate.Release(); }
    }

    private static async Task<JsonDocument> ReadJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("OBS cerró la conexión.");
            buffer.Write(chunk, 0, result.Count);
            if (buffer.Length > 1_048_576) throw new InvalidOperationException("Mensaje WebSocket de OBS supera el límite de 1 MiB.");
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(buffer.ToArray());
    }

    public async Task DisconnectAsync()
    {
        cancellation?.Cancel();
        if (socket is { State: WebSocketState.Open } ws)
        {
            try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "VTuberBot disconnect", CancellationToken.None).ConfigureAwait(false); } catch { }
        }
        if (receiveTask is not null) { try { await receiveTask.ConfigureAwait(false); } catch { } }
        socket?.Dispose(); cancellation?.Dispose(); socket=null; cancellation=null; receiveTask=null; IsConnected=false;
        lock (gate) { foreach (var task in pending.Values) task.TrySetCanceled(); pending.Clear(); }
        StatusChanged?.Invoke("OBS desconectado.");
    }

    public async ValueTask DisposeAsync() { await DisconnectAsync().ConfigureAwait(false); sendGate.Dispose(); }
}
