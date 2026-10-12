using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VtuberBot.Core;

public enum StreamerBotConnectionState
{
    NotConfigured, ConfiguredDisconnected, Connecting, Connected, AuthenticationError,
    ServerInaccessible, UnexpectedDisconnect, ProtocolError, Disposed
}

public sealed record StreamerBotEndpoint(string Host = "127.0.0.1", int Port = 8080, string Endpoint = "/")
{
    public Uri ToUri()
    {
        if (string.IsNullOrWhiteSpace(Host) || Host.Contains("://", StringComparison.Ordinal) ||
            Host.Contains('/') || Host.Contains('?') ||
            Host.Contains('#'))
            throw new ArgumentException("El host debe ser un nombre o una dirección IP, sin esquema ni ruta.");
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "El puerto debe estar entre 1 y 65535.");
        var path = string.IsNullOrWhiteSpace(Endpoint) ? "/" : Endpoint.Trim();
        if (!path.StartsWith("/", StringComparison.Ordinal) || path.Contains('#') || path.Contains('?'))
            throw new ArgumentException("El endpoint debe ser una ruta absoluta que comience con /.");
        if (path.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("El endpoint no puede contener segmentos '..'.");
        var builder = new UriBuilder(Uri.UriSchemeWs, Host.Trim(), Port, path);
        if (!string.IsNullOrEmpty(builder.Query))
            throw new ArgumentException("El endpoint no puede contener una consulta.");
        return builder.Uri;
    }

    public bool IsLoopback => string.Equals(Host.Trim(), "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Host.Trim(), "127.0.0.1", StringComparison.Ordinal) ||
        string.Equals(Host.Trim(), "::1", StringComparison.Ordinal);
}

public sealed record StreamerBotInstanceInfo(string InstanceId, string Name, string Version, string OperatingSystem);
public sealed record StreamerBotAction(string Id, string Name, bool Enabled, string Group = "");
public sealed record StreamerBotEvent(string Source, string Type, DateTimeOffset? Timestamp, JsonElement Data)
{
    public string Key => Source + "." + Type;
}
public sealed record StreamerBotResponse(string Request, string Id, bool Success, string Message, JsonElement Payload);
public sealed record StreamerBotConnectionSnapshot(StreamerBotConnectionState State, string Message, StreamerBotInstanceInfo? Instance = null);

public interface IStreamerBotClient : IAsyncDisposable
{
    StreamerBotConnectionSnapshot Snapshot { get; }
    event Action<StreamerBotConnectionSnapshot>? StateChanged;
    event Action<StreamerBotEvent>? EventReceived;
    Task ConnectAsync(StreamerBotEndpoint endpoint, string? password = null, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StreamerBotAction>> GetActionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string[]>> GetEventsAsync(CancellationToken cancellationToken = default);
    Task<StreamerBotResponse> DoActionAsync(string actionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string[]>> SubscribeAsync(IReadOnlyDictionary<string, string[]> events, CancellationToken cancellationToken = default);
}

public sealed class StreamerBotProtocolException(string message) : Exception(message);

/// <summary>
/// Direct client for Streamer.bot's documented WebSocket protocol. It never logs message bodies,
/// authentication challenges, passwords or hashes. Connection is considered active only after Hello
/// and any requested authentication have succeeded.
/// </summary>
public sealed class StreamerBotWebSocketClient : IStreamerBotClient
{
    private const int MaxMessageBytes = 1_048_576;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<StreamerBotResponse>> pending = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly object stateGate = new();
    private ClientWebSocket? socket;
    private CancellationTokenSource? lifetime;
    private Task? receiveTask;
    private TaskCompletionSource<JsonElement>? helloReceived;
    private StreamerBotConnectionSnapshot snapshot = new(StreamerBotConnectionState.ConfiguredDisconnected, "Desconectado.");
    private int disposed;

    public StreamerBotConnectionSnapshot Snapshot { get { lock (stateGate) return snapshot; } }
    public event Action<StreamerBotConnectionSnapshot>? StateChanged;
    public event Action<StreamerBotEvent>? EventReceived;

    public async Task ConnectAsync(StreamerBotEndpoint endpoint, string? password = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        var uri = endpoint.ToUri();
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        SetState(StreamerBotConnectionState.Connecting, "Conectando al servidor WebSocket…");
        var ws = new ClientWebSocket();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        socket = ws;
        lifetime = cts;
        helloReceived = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await ws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);
            receiveTask = ReceiveLoopAsync(ws, cts.Token);
            var hello = await helloReceived.Task.WaitAsync(RequestTimeout, cts.Token).ConfigureAwait(false);
            if (!hello.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object)
                throw new StreamerBotProtocolException("El mensaje Hello no contiene el objeto info documentado.");

            var instance = new StreamerBotInstanceInfo(
                ReadString(info, "instanceId"), ReadString(info, "name"),
                ReadString(info, "version"), ReadString(info, "os"));
            if (hello.TryGetProperty("authentication", out var authentication) && authentication.ValueKind == JsonValueKind.Object)
            {
                var salt = ReadString(authentication, "salt");
                var challenge = ReadString(authentication, "challenge");
                if (string.IsNullOrEmpty(password))
                {
                    SetState(StreamerBotConnectionState.AuthenticationError,
                        "El servidor exige autenticación; introducí la contraseña configurada en Streamer.bot.", instance);
                    throw new StreamerBotAuthenticationException("El servidor requiere contraseña.");
                }
                if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(challenge))
                    throw new StreamerBotProtocolException("El desafío de autenticación no contiene salt y challenge válidos.");
                var proof = CreateAuthenticationProof(password, salt, challenge);
                var response = await SendRequestAsync("Authenticate", new JsonObject { ["authentication"] = proof }, cts.Token).ConfigureAwait(false);
                if (!response.Success)
                {
                    SetState(StreamerBotConnectionState.AuthenticationError, "Streamer.bot rechazó la autenticación.", instance);
                    throw new StreamerBotAuthenticationException("Streamer.bot rechazó la autenticación.");
                }
            }
            SetState(StreamerBotConnectionState.Connected, "Handshake y autenticación completados.", instance);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (StreamerBotAuthenticationException)
        {
            await CloseSocketAsync().ConfigureAwait(false);
            throw;
        }
        catch (StreamerBotProtocolException exception)
        {
            SetState(StreamerBotConnectionState.ProtocolError, exception.Message);
            await CloseSocketAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is WebSocketException or TimeoutException or IOException or InvalidOperationException)
        {
            SetState(StreamerBotConnectionState.ServerInaccessible,
                "No se pudo completar la conexión: " + exception.GetType().Name + ".");
            await CloseSocketAsync().ConfigureAwait(false);
            throw new IOException("No se pudo conectar con Streamer.bot.", exception);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var ws = socket;
        var cts = lifetime;
        socket = null;
        lifetime = null;
        cts?.Cancel();
        if (ws is not null)
        {
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "VTuberBot disconnect", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException) { }
            ws.Dispose();
        }
        if (receiveTask is { } task)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        }
        receiveTask = null;
        cts?.Dispose();
        foreach (var item in pending)
            if (pending.TryRemove(item.Key, out var source))
                source.TrySetException(new IOException("La conexión con Streamer.bot se cerró."));
        if (Volatile.Read(ref disposed) == 0)
            SetState(StreamerBotConnectionState.ConfiguredDisconnected, "Desconectado por solicitud del usuario.");
    }

    public async Task<IReadOnlyList<StreamerBotAction>> GetActionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync("GetActions", null, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        if (!response.Payload.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            throw new StreamerBotProtocolException("GetActions no devolvió la lista actions esperada.");
        return actions.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
            .Select(x => new StreamerBotAction(ReadString(x, "id"), ReadString(x, "name"),
                x.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True,
                ReadString(x, "group")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name)).ToArray();
    }

    public async Task<IReadOnlyDictionary<string, string[]>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync("GetEvents", null, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        if (!response.Payload.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Object)
            throw new StreamerBotProtocolException("GetEvents no devolvió el objeto events esperado.");
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in events.EnumerateObject())
            if (category.Value.ValueKind == JsonValueKind.Array)
                result[CanonicalCategory(category.Name)] = category.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return result;
    }

    public async Task<StreamerBotResponse> DoActionAsync(string actionId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(actionId, out var id))
            throw new ArgumentException("Seleccioná una acción con un identificador GUID válido devuelto por GetActions.", nameof(actionId));
        var response = await SendRequestAsync("DoAction", new JsonObject
        {
            ["action"] = new JsonObject { ["id"] = id.ToString() }
        }, cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<IReadOnlyDictionary<string, string[]>> SubscribeAsync(
        IReadOnlyDictionary<string, string[]> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        var selection = new JsonObject();
        foreach (var category in events)
        {
            var names = category.Value.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (names.Length > 0)
                selection[CanonicalCategory(category.Key)] = new JsonArray(names.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray());
        }
        if (selection.Count == 0)
            throw new ArgumentException("Seleccioná al menos un evento admitido por GetEvents.", nameof(events));
        var response = await SendRequestAsync("Subscribe", new JsonObject { ["events"] = selection }, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        if (!response.Payload.TryGetProperty("events", out var confirmed) || confirmed.ValueKind != JsonValueKind.Object)
            throw new StreamerBotProtocolException("La respuesta Subscribe no confirmó las suscripciones mediante events.");
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in confirmed.EnumerateObject())
            if (category.Value.ValueKind == JsonValueKind.Array)
                result[CanonicalCategory(category.Name)] = category.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray();
        return result;
    }

    public static string CreateAuthenticationProof(string password, string salt, string challenge)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(challenge);
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private async Task<StreamerBotResponse> SendRequestAsync(string request, JsonObject? fields, CancellationToken cancellationToken)
    {
        var ws = socket;
        var cts = lifetime;
        if (ws?.State != WebSocketState.Open || cts is null)
            throw new InvalidOperationException("No hay una conexión WebSocket activa con Streamer.bot.");
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<StreamerBotResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion))
            throw new InvalidOperationException("No se pudo reservar un identificador de solicitud único.");
        var message = new JsonObject { ["request"] = request, ["id"] = id };
        if (fields is not null)
            foreach (var field in fields)
                message[field.Key] = field.Value?.DeepClone();
        try
        {
            var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false); }
            finally { sendLock.Release(); }
            return await completion.Task.WaitAsync(RequestTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("Streamer.bot no respondió dentro de 10 segundos; el resultado de la solicitud puede ser desconocido.");
        }
        finally { pending.TryRemove(id, out _); }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        throw new WebSocketException("Streamer.bot cerró la conexión WebSocket.");
                    if (result.MessageType != WebSocketMessageType.Text)
                        throw new StreamerBotProtocolException("Se recibió un mensaje WebSocket que no es texto.");
                    if (message.Length + result.Count > MaxMessageBytes)
                        throw new StreamerBotProtocolException("El mensaje recibido supera el límite de 1 MiB.");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                JsonDocument document;
                try { document = JsonDocument.Parse(message.ToArray()); }
                catch (JsonException) { throw new StreamerBotProtocolException("Streamer.bot envió JSON malformado."); }
                using (document)
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        throw new StreamerBotProtocolException("La raíz del mensaje debe ser un objeto JSON.");
                    if (root.TryGetProperty("request", out var requestName) && requestName.ValueKind == JsonValueKind.String &&
                        string.Equals(requestName.GetString(), "Hello", StringComparison.OrdinalIgnoreCase))
                    {
                        if (helloReceived is not null)
                            helloReceived.TrySetResult(root.Clone());
                        continue;
                    }
                    if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String &&
                        pending.TryGetValue(idElement.GetString()!, out var pendingRequest))
                    {
                        var status = ReadString(root, "status");
                        pendingRequest.TrySetResult(new StreamerBotResponse(
                            ReadString(root, "request"), idElement.GetString()!,
                            string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase),
                            string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)
                                ? "Streamer.bot aceptó la solicitud."
                                : "Streamer.bot devolvió status=error.",
                            root.Clone()));
                        continue;
                    }
                    if (root.TryGetProperty("event", out var eventElement) && eventElement.ValueKind == JsonValueKind.Object)
                    {
                        var source = ReadString(eventElement, "source");
                        var type = ReadString(eventElement, "type");
                        if (source.Length == 0 || type.Length == 0 || !root.TryGetProperty("data", out var data))
                            throw new StreamerBotProtocolException("El evento recibido no contiene event.source, event.type y data.");
                        DateTimeOffset? timestamp = DateTimeOffset.TryParse(ReadString(root, "timeStamp"), out var parsed) ? parsed : null;
                        try { EventReceived?.Invoke(new StreamerBotEvent(source, type, timestamp, data.Clone())); }
                        catch { /* An event consumer cannot kill the protocol receive loop. */ }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (StreamerBotProtocolException exception)
        {
            SetState(StreamerBotConnectionState.ProtocolError, exception.Message);
            helloReceived?.TrySetException(exception);
            FailPending(exception);
            try { ws.Abort(); } catch (ObjectDisposedException) { }
            try { lifetime?.Cancel(); } catch (ObjectDisposedException) { }
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or ObjectDisposedException)
        {
            helloReceived?.TrySetException(exception);
            if (!cancellationToken.IsCancellationRequested)
                SetState(StreamerBotConnectionState.UnexpectedDisconnect, "La conexión se interrumpió (" + exception.GetType().Name + ").");
            FailPending(new IOException("La conexión con Streamer.bot se interrumpió."));
        }
    }

    private static void EnsureSuccess(StreamerBotResponse response)
    {
        if (!response.Success)
            throw new StreamerBotProtocolException("Streamer.bot rechazó la solicitud " + response.Request + ".");
    }

    private void FailPending(Exception exception)
    {
        foreach (var item in pending)
            if (pending.TryRemove(item.Key, out var source))
                source.TrySetException(exception);
    }

    private async Task CloseSocketAsync()
    {
        var ws = socket;
        var cts = lifetime;
        socket = null;
        lifetime = null;
        cts?.Cancel();
        if (ws is not null) { ws.Dispose(); }
        if (receiveTask is { } task)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        }
        receiveTask = null;
        cts?.Dispose();
    }

    private void SetState(StreamerBotConnectionState state, string message, StreamerBotInstanceInfo? instance = null)
    {
        var next = new StreamerBotConnectionSnapshot(state, message, instance ?? Snapshot.Instance);
        lock (stateGate) snapshot = next;
        try { StateChanged?.Invoke(next); } catch { }
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private static string CanonicalCategory(string category)
    {
        var value = category.Trim();
        if (value.Length == 0) return value;
        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        SetState(StreamerBotConnectionState.Disposed, "Cliente WebSocket cerrado.");
        sendLock.Dispose();
    }
}

public sealed class StreamerBotAuthenticationException(string message) : Exception(message);
