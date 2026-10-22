using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

public sealed record KickChatMessageEvent(
    string MessageId,
    long BroadcasterUserId,
    string ChannelSlug,
    long SenderUserId,
    string Username,
    string Content,
    bool IsModerator,
    bool IsBroadcaster);

public sealed record KickWebhookEvent(
    string EventType,
    string EventMessageId,
    string Version,
    string Summary,
    KickChatMessageEvent? ChatMessage,
    string SubscriptionId);

public static class KickWebhookSignatureVerifier
{
    public static bool Verify(
        string publicKeyPem,
        string messageId,
        string timestamp,
        string rawBody,
        string signatureBase64,
        DateTimeOffset now,
        TimeSpan maximumAge)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem) ||
            string.IsNullOrWhiteSpace(messageId) || messageId.Length > 128 ||
            string.IsNullOrWhiteSpace(timestamp) || timestamp.Length > 80 ||
            string.IsNullOrWhiteSpace(signatureBase64) || signatureBase64.Length > 2048 ||
            maximumAge <= TimeSpan.Zero || rawBody is null)
            return false;

        if (!DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var sentAt) ||
            (now - sentAt).Duration() > maximumAge)
            return false;

        try
        {
            var signature = Convert.FromBase64String(signatureBase64);
            var signedContent = Encoding.UTF8.GetBytes(messageId + "." + timestamp + "." + rawBody);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return rsa.VerifyData(
                signedContent,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception exception) when (
            exception is CryptographicException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}

public static class KickWebhookPayloadParser
{
    public static KickWebhookEvent Parse(
        string eventType,
        string version,
        string eventMessageId,
        string rawBody,
        string subscriptionId = "")
    {
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 120)
            throw new InvalidDataException("El tipo de evento Kick no es válido.");
        if (string.IsNullOrWhiteSpace(version) || version.Length > 12)
            throw new InvalidDataException("La versión del evento Kick no es válida.");
        if (string.IsNullOrWhiteSpace(eventMessageId) || eventMessageId.Length > 128)
            throw new InvalidDataException("El ID del webhook Kick no es válido.");

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("El payload del webhook Kick debe ser un objeto JSON.");

        if (!root.TryGetProperty("broadcaster", out var broadcaster) ||
            broadcaster.ValueKind != JsonValueKind.Object)
        {
            return new KickWebhookEvent(eventType, eventMessageId, version, "Evento verificado de Kick.", null, subscriptionId);
        }

        var broadcasterId = GetInt64(broadcaster, "user_id");
        var channelSlug = GetString(broadcaster, "channel_slug") ?? "";
        var channelName = GetString(broadcaster, "username") ?? channelSlug;
        if (channelSlug.Length > 200 || channelName.Length > 200 ||
            channelSlug.Any(char.IsControl) || channelName.Any(char.IsControl))
            throw new InvalidDataException("Los datos del canal Kick superan los límites admitidos.");

        if (eventType == "chat.message.sent" && version == "1")
        {
            if (!root.TryGetProperty("sender", out var sender) || sender.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("El mensaje de chat Kick no incluye sender.");
            var senderId = GetInt64(sender, "user_id");
            var username = GetString(sender, "username") ?? "";
            var messageId = GetString(root, "message_id") ?? "";
            var content = GetString(root, "content") ?? "";
            if (senderId <= 0 || broadcasterId <= 0 || string.IsNullOrWhiteSpace(username) ||
                username.Length > 100 || username.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(messageId) || messageId.Length > 128 ||
                content.Length > 4096)
                throw new InvalidDataException("El mensaje de chat Kick no contiene los campos mínimos válidos.");

            var isBroadcaster = senderId == broadcasterId;
            var isModerator = HasBadge(sender, "moderator") || HasBadge(sender, "broadcaster") || isBroadcaster;
            var chat = new KickChatMessageEvent(
                messageId,
                broadcasterId,
                channelSlug,
                senderId,
                username,
                content,
                isModerator,
                isBroadcaster);
            return new KickWebhookEvent(eventType, eventMessageId, version,
                "Chat de " + username + " en " + (string.IsNullOrWhiteSpace(channelName) ? "el canal Kick" : channelName) + ".",
                chat, subscriptionId);
        }

        var summary = eventType switch
        {
            "channel.followed" => "Nuevo seguidor en " + channelName + ".",
            "channel.subscription.new" => "Nueva suscripción en " + channelName + ".",
            "channel.subscription.renewal" => "Renovación de suscripción en " + channelName + ".",
            "channel.subscription.gifts" => "Regalos de suscripción en " + channelName + ".",
            "livestream.status.updated" => "Estado del directo actualizado en " + channelName + ".",
            "moderation.banned" => "Kick notificó una sanción en " + channelName + ".",
            _ => "Evento verificado de Kick: " + eventType + "."
        };
        return new KickWebhookEvent(eventType, eventMessageId, version, summary, null, subscriptionId);
    }

    private static bool HasBadge(JsonElement sender, string badgeType)
    {
        if (!sender.TryGetProperty("identity", out var identity) ||
            identity.ValueKind != JsonValueKind.Object ||
            !identity.TryGetProperty("badges", out var badges) ||
            badges.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var badge in badges.EnumerateArray())
        {
            if (string.Equals(GetString(badge, "type"), badgeType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static long GetInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var parsed) ? parsed : 0;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// A loopback-only HTTP receiver. Kick must reach it through a user-configured public HTTPS webhook/tunnel.
/// The listener never binds to a LAN or public network interface.
/// </summary>
public sealed class KickWebhookReceiver : IAsyncDisposable
{
    private const int MaximumHeaderBytes = 16 * 1024;
    private const int MaximumBodyBytes = 1024 * 1024;
    private const int MaximumRememberedMessageIds = 4096;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly int port;
    private readonly TimeSpan maximumMessageAge;
    private readonly KickWebhookDeliveryDeduplicator deliveryDeduplicator = new(MaximumRememberedMessageIds);
    private readonly SemaphoreSlim connectionSlots = new(16, 16);
    private readonly SemaphoreSlim publicKeyGate = new(1, 1);
    private readonly Func<CancellationToken, Task<string>>? publicKeyProvider;
    private readonly object activeTasksGate = new();
    private readonly HashSet<Task> activeTasks = [];
    private string? cachedPublicKey;
    private DateTimeOffset cachedPublicKeyAt = DateTimeOffset.MinValue;
    private TcpListener? listener;
    private CancellationTokenSource? stopSource;
    private Task? listenTask;

    public KickWebhookReceiver(
        int port = 17565,
        TimeSpan? maximumMessageAge = null,
        Func<CancellationToken, Task<string>>? publicKeyProvider = null)
    {
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        this.port = port;
        this.maximumMessageAge = maximumMessageAge ?? TimeSpan.FromMinutes(10);
        if (this.maximumMessageAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumMessageAge));
        this.publicKeyProvider = publicKeyProvider;
    }

    public bool IsRunning => listener is not null;

    public string LocalEndpoint => "http://127.0.0.1:" + port + "/kick-webhook";

    public event Action<KickWebhookEvent>? EventReceived;
    public event Action<string>? StatusChanged;

    public void Start()
    {
        if (IsRunning) return;

        var localListener = new TcpListener(System.Net.IPAddress.Loopback, port);
        try
        {
            localListener.Start(32);
        }
        catch (SocketException exception)
        {
            localListener.Stop();
            throw new InvalidOperationException(
                "No se pudo iniciar el receptor local de Kick en el puerto " + port +
                ". Cerrá otra instancia o elegí otro puerto.",
                exception);
        }

        listener = localListener;
        stopSource = new CancellationTokenSource();
        listenTask = Task.Run(() => AcceptLoopAsync(localListener, stopSource.Token));
        StatusChanged?.Invoke("Receptor local de Kick iniciado en 127.0.0.1:" + port + ". Aún falta verificar entregas reales.");
    }

    public async Task StopAsync()
    {
        var source = stopSource;
        var currentListener = listener;
        var currentTask = listenTask;
        listener = null;
        stopSource = null;
        listenTask = null;

        if (source is null) return;
        try { source.Cancel(); }
        catch (ObjectDisposedException) { }
        try { currentListener?.Stop(); }
        catch (SocketException) { }

        if (currentTask is not null)
        {
            try { await currentTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        Task[] requests;
        lock (activeTasksGate) requests = activeTasks.ToArray();
        try { await Task.WhenAll(requests).ConfigureAwait(false); }
        catch (Exception exception)
        {
            StatusChanged?.Invoke("El cierre del receptor Kick terminó con un error de solicitud (" + exception.GetType().Name + ").");
        }

        source.Dispose();
        StatusChanged?.Invoke("Receptor local de Kick detenido.");
    }

    private async Task AcceptLoopAsync(TcpListener localListener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await localListener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!await connectionSlots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                using (client)
                {
                    try { await WriteResponseAsync(client.GetStream(), 503, "Busy", CancellationToken.None).ConfigureAwait(false); }
                    catch (IOException) { }
                }
                continue;
            }

            Task requestTask;
            lock (activeTasksGate)
            {
                requestTask = Task.Run(async () =>
                {
                    using (client)
                    {
                        try { await HandleClientAsync(client, cancellationToken).ConfigureAwait(false); }
                        catch (OperationCanceledException) { }
                        catch (IOException) { }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { }
                        catch (Exception exception)
                        {
                            StatusChanged?.Invoke("Se rechazó una solicitud de webhook Kick por un error interno (" + exception.GetType().Name + ").");
                            try { await WriteResponseAsync(client.GetStream(), 503, "Retry", CancellationToken.None).ConfigureAwait(false); }
                            catch (Exception responseException) when (responseException is IOException or SocketException or ObjectDisposedException) { }
                        }
                        finally { connectionSlots.Release(); }
                    }
                }, CancellationToken.None);
                activeTasks.Add(requestTask);
            }
            _ = requestTask.ContinueWith(
                completed =>
                {
                    lock (activeTasksGate) activeTasks.Remove(completed);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var stream = client.GetStream();
        HttpRequest request;
        try
        {
            request = await ReadRequestAsync(stream, timeout.Token).ConfigureAwait(false);
        }
        catch (WebhookRequestException exception)
        {
            await WriteResponseAsync(stream, exception.StatusCode, "Invalid Request", timeout.Token).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(request.Path, "/kick-webhook", StringComparison.Ordinal))
        {
            await WriteResponseAsync(stream, 404, "Not Found", timeout.Token).ConfigureAwait(false);
            return;
        }
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 405, "Method Not Allowed", timeout.Token).ConfigureAwait(false);
            return;
        }

        var messageId = GetHeader(request.Headers, "Kick-Event-Message-Id");
        var subscriptionId = GetHeader(request.Headers, "Kick-Event-Subscription-Id");
        var timestamp = GetHeader(request.Headers, "Kick-Event-Message-Timestamp");
        var signature = GetHeader(request.Headers, "Kick-Event-Signature");
        var eventType = GetHeader(request.Headers, "Kick-Event-Type");
        var version = GetHeader(request.Headers, "Kick-Event-Version");
        if (string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(version))
        {
            await WriteResponseAsync(stream, 401, "Missing Signature Headers", timeout.Token).ConfigureAwait(false);
            return;
        }

        var rawBody = Encoding.UTF8.GetString(request.Body);
        var publicKey = await GetPublicKeyPemAsync(false, timeout.Token).ConfigureAwait(false);
        var isValid = KickWebhookSignatureVerifier.Verify(
            publicKey, messageId, timestamp, rawBody, signature, DateTimeOffset.UtcNow, maximumMessageAge);
        if (!isValid)
        {
            // Kick may rotate its signing key; refresh once before rejecting an otherwise valid event.
            publicKey = await GetPublicKeyPemAsync(true, timeout.Token).ConfigureAwait(false);
            isValid = KickWebhookSignatureVerifier.Verify(
                publicKey, messageId, timestamp, rawBody, signature, DateTimeOffset.UtcNow, maximumMessageAge);
        }
        if (!isValid)
        {
            StatusChanged?.Invoke("Kick envió un webhook con firma o timestamp inválido; se rechazó.");
            await WriteResponseAsync(stream, 401, "Invalid Signature", timeout.Token).ConfigureAwait(false);
            return;
        }

        KickWebhookEvent webhookEvent;
        try
        {
            webhookEvent = KickWebhookPayloadParser.Parse(eventType, version, messageId, rawBody, subscriptionId!);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            StatusChanged?.Invoke("Se recibió un webhook Kick firmado con payload no válido (" + exception.GetType().Name + ").");
            await WriteResponseAsync(stream, 400, "Invalid Event Payload", timeout.Token).ConfigureAwait(false);
            return;
        }

        var consumer = EventReceived;
        if (consumer is null)
        {
            StatusChanged?.Invoke("El receptor Kick no tiene un consumidor de eventos registrado; se solicita reintento.");
            await WriteResponseAsync(stream, 503, "No Event Consumer", timeout.Token).ConfigureAwait(false);
            return;
        }

        bool admitted;
        try
        {
            admitted = deliveryDeduplicator.TryDispatchOnce(messageId!, () => consumer(webhookEvent));
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke("No se pudo admitir un evento Kick verificado (" + exception.GetType().Name + "); se solicita reintento.");
            await WriteResponseAsync(stream, 503, "Event Processing Failed", timeout.Token).ConfigureAwait(false);
            return;
        }

        if (!admitted)
        {
            await WriteResponseAsync(stream, 204, "Duplicate Event", timeout.Token).ConfigureAwait(false);
            return;
        }

        await WriteResponseAsync(stream, 200, "OK", timeout.Token).ConfigureAwait(false);
    }

    private async Task<string> GetPublicKeyPemAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && cachedPublicKey is not null &&
            DateTimeOffset.UtcNow - cachedPublicKeyAt < TimeSpan.FromMinutes(30))
            return cachedPublicKey;

        await publicKeyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && cachedPublicKey is not null &&
                DateTimeOffset.UtcNow - cachedPublicKeyAt < TimeSpan.FromMinutes(30))
                return cachedPublicKey;

            string candidateKey;
            if (publicKeyProvider is not null)
            {
                // Injection seam for deterministic loopback integration tests. Production uses
                // Kick's documented public-key endpoint below; the provider must still return PEM.
                candidateKey = (await publicKeyProvider(cancellationToken).ConfigureAwait(false)).Trim();
            }
            else
            {
                using var response = await Http.GetAsync("https://api.kick.com/public/v1/public-key", cancellationToken).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Kick no permitió obtener la clave pública de verificación (HTTP " + (int)response.StatusCode + ").");

                // Kick documents this endpoint as returning the PEM body directly. Accept that canonical
                // response and JSON wrappers defensively, but never cache a non-PEM value.
                candidateKey = json.Trim();
                if (!candidateKey.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(json);
                        var root = document.RootElement;
                        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                            data.TryGetProperty("public_key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String)
                            candidateKey = keyElement.GetString() ?? "";
                        else if (root.TryGetProperty("public_key", out var key) && key.ValueKind == JsonValueKind.String)
                            candidateKey = key.GetString() ?? "";
                    }
                    catch (JsonException)
                    {
                        candidateKey = "";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(candidateKey) ||
                !candidateKey.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal))
                throw new InvalidOperationException("Kick devolvió una clave pública con formato inesperado.");
            cachedPublicKey = candidateKey;

            cachedPublicKeyAt = DateTimeOffset.UtcNow;
            return cachedPublicKey;
        }
        finally { publicKeyGate.Release(); }
    }

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : null;

    private static async Task<HttpRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var headerBuffer = new MemoryStream();
        var oneByte = new byte[1];
        while (headerBuffer.Length < MaximumHeaderBytes)
        {
            var read = await stream.ReadAsync(oneByte.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new WebhookRequestException(400, "Connection closed before headers.");
            headerBuffer.WriteByte(oneByte[0]);
            var count = headerBuffer.Length;
            if (count >= 4)
            {
                var bytes = headerBuffer.GetBuffer();
                var end = (int)count;
                if (bytes[end - 4] == 13 && bytes[end - 3] == 10 && bytes[end - 2] == 13 && bytes[end - 1] == 10)
                    break;
            }
        }
        if (headerBuffer.Length >= MaximumHeaderBytes)
            throw new WebhookRequestException(431, "Request headers too large.");

        var headerText = Encoding.ASCII.GetString(headerBuffer.ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length != 3 || !requestParts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            throw new WebhookRequestException(400, "Invalid request line.");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0) break;
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new WebhookRequestException(400, "Invalid request header.");
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key.Length > 100 || value.Length > 4096)
                throw new WebhookRequestException(431, "Request header too large.");
            headers[key] = value;
        }

        if (headers.ContainsKey("Transfer-Encoding"))
            throw new WebhookRequestException(501, "Chunked webhook requests are not supported by the loopback receiver.");
        if (!headers.TryGetValue("Content-Length", out var contentLengthText) ||
            !int.TryParse(contentLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var contentLength) ||
            contentLength < 0)
            throw new WebhookRequestException(411, "Content-Length is required.");
        if (contentLength > MaximumBodyBytes)
            throw new WebhookRequestException(413, "Request body too large.");

        var body = new byte[contentLength];
        var offset = 0;
        while (offset < body.Length)
        {
            var read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new WebhookRequestException(400, "Truncated request body.");
            offset += read;
        }

        var path = requestParts[1].Split('?', 2)[0];
        return new HttpRequest(requestParts[0], path, headers, body);
    }

    private static async Task WriteResponseAsync(Stream stream, int statusCode, string reason, CancellationToken cancellationToken)
    {
        var statusText = statusCode switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            405 => "Method Not Allowed",
            411 => "Length Required",
            413 => "Payload Too Large",
            431 => "Request Header Fields Too Large",
            501 => "Not Implemented",
            503 => "Service Unavailable",
            _ => reason
        };
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n" +
            "Content-Length: 0\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        connectionSlots.Dispose();
        publicKeyGate.Dispose();
    }

    private sealed record HttpRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Body);

    private sealed class WebhookRequestException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}
