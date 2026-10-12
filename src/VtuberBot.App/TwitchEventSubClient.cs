using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>Subscribes to Twitch EventSub WebSocket events using the authorized broadcaster account.</summary>
public sealed class TwitchEventSubClient : IAsyncDisposable
{
    private const string InitialWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";
    private ClientWebSocket? socket;
    private CancellationTokenSource? cancellation;
    private Task? receiveTask;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;
    public event Action<string, string>? EventReceived;

    private sealed record SubscriptionDefinition(string Type, string Version, object Condition);

    public async Task ConnectAsync(string clientId, string accessToken, string channel, CancellationToken external = default)
    {
        if (IsConnected || receiveTask is not null)
            throw new InvalidOperationException("EventSub ya está conectado o reconectando.");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("EventSub necesita Client ID, token OAuth y canal.");

        var normalizedClientId = clientId.Trim();
        var normalizedToken = accessToken.Trim();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(external);
        cancellation.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            StatusChanged?.Invoke("Conectando a Twitch EventSub…");
            var broadcasterId = await GetBroadcasterIdAsync(
                normalizedClientId, normalizedToken, channel.Trim().TrimStart('#'), cancellation.Token).ConfigureAwait(false);
            var session = await OpenSessionAsync(InitialWebSocketUrl, cancellation.Token).ConfigureAwait(false);
            socket = session.Socket;

            // Start the receiver before subscription creation, so keepalive and early notifications are processed.
            receiveTask = ReceiveLoopAsync(session.Socket, normalizedClientId, normalizedToken, broadcasterId, cancellation.Token);

            var registered = await RegisterSubscriptionsAsync(
                normalizedClientId, normalizedToken, broadcasterId, session.SessionId, cancellation.Token).ConfigureAwait(false);
            if (registered == 0)
                throw new InvalidOperationException("Twitch rechazó todas las suscripciones EventSub. Revisá scopes y permisos del canal.");

            cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            IsConnected = true;
            StatusChanged?.Invoke(registered + " suscripciones EventSub registradas; esperando eventos oficiales.");
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<(ClientWebSocket Socket, string SessionId)> OpenSessionAsync(string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var candidate = new ClientWebSocket();
        try
        {
            await candidate.ConnectAsync(new Uri(url), timeout.Token).ConfigureAwait(false);
            using var welcome = await ReadJsonAsync(candidate, timeout.Token).ConfigureAwait(false);
            var messageType = welcome.RootElement.GetProperty("metadata").GetProperty("message_type").GetString();
            if (messageType != "session_welcome")
                throw new InvalidOperationException("Twitch EventSub no envió session_welcome.");
            var sessionId = welcome.RootElement.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new InvalidOperationException("EventSub no devolvió session ID.");
            return (candidate, sessionId);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    private async Task<int> RegisterSubscriptionsAsync(
        string clientId, string accessToken, string broadcasterId, string sessionId, CancellationToken ct)
    {
        var subscriptions = GetSubscriptions(broadcasterId);
        var registered = 0;
        var rejected = new List<string>();
        foreach (var subscription in subscriptions)
        {
            ct.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(new
            {
                type = subscription.Type,
                version = subscription.Version,
                condition = subscription.Condition,
                transport = new { method = "websocket", session_id = sessionId }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Headers.Add("Client-Id", clientId);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                registered++;
            else
                rejected.Add(subscription.Type + " (HTTP " + (int)response.StatusCode + ")");
        }

        StatusChanged?.Invoke(registered + " suscripciones EventSub registradas; " + rejected.Count + " rechazadas.");
        foreach (var failure in rejected)
            EventReceived?.Invoke("subscription_error", failure);
        return registered;
    }

    private static SubscriptionDefinition[] GetSubscriptions(string broadcasterId) =>
    [
        new("channel.follow", "2", new { broadcaster_user_id = broadcasterId, moderator_user_id = broadcasterId }),
        new("channel.subscribe", "1", new { broadcaster_user_id = broadcasterId }),
        new("channel.subscription.gift", "1", new { broadcaster_user_id = broadcasterId }),
        new("channel.channel_points_custom_reward_redemption.add", "1", new { broadcaster_user_id = broadcasterId }),
        new("channel.raid", "1", new { to_broadcaster_user_id = broadcasterId })
    ];

    private async Task<string> GetBroadcasterIdAsync(string clientId, string token, string channel, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "https://api.twitch.tv/helix/users?login=" + Uri.EscapeDataString(channel));
        request.Headers.Add("Client-Id", clientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo consultar el usuario Twitch (HTTP " + (int)response.StatusCode + ").");
        using var json = JsonDocument.Parse(body);
        var users = json.RootElement.GetProperty("data");
        if (users.GetArrayLength() == 0)
            throw new InvalidOperationException("Twitch no encontró el canal indicado.");
        return users[0].GetProperty("id").GetString()!;
    }

    private async Task ReceiveLoopAsync(
        ClientWebSocket activeSocket,
        string clientId,
        string accessToken,
        string broadcasterId,
        CancellationToken ct)
    {
        var ws = activeSocket;
        var retryAttempt = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var document = await ReadJsonAsync(ws, ct).ConfigureAwait(false);
                    var root = document.RootElement;
                    var metadata = root.GetProperty("metadata");
                    var messageType = metadata.GetProperty("message_type").GetString() ?? "unknown";

                    if (messageType == "session_keepalive")
                        continue;

                    if (messageType == "session_reconnect")
                    {
                        var reconnectUrl = root.GetProperty("payload").GetProperty("session")
                            .GetProperty("reconnect_url").GetString();
                        if (string.IsNullOrWhiteSpace(reconnectUrl))
                            throw new InvalidOperationException("EventSub envió session_reconnect sin reconnect_url.");

                        StatusChanged?.Invoke("Twitch solicita migrar EventSub a una nueva sesión…");
                        var migrated = await OpenSessionAsync(reconnectUrl, ct).ConfigureAwait(false);
                        var previous = ws;
                        ws = migrated.Socket;
                        socket = ws;
                        IsConnected = true;
                        retryAttempt = 0;
                        previous.Dispose();
                        StatusChanged?.Invoke("EventSub reconectado; Twitch migró las suscripciones existentes.");
                        continue;
                    }

                    if (messageType == "notification")
                    {
                        var subscriptionType = metadata.TryGetProperty("subscription_type", out var typeElement)
                            ? typeElement.GetString() ?? "event" : "event";
                        var eventPayload = root.GetProperty("payload").GetProperty("event");
                        EventReceived?.Invoke(subscriptionType, SummarizeEvent(subscriptionType, eventPayload));
                        StatusChanged?.Invoke("Evento Twitch recibido: " + subscriptionType);
                    }
                    else if (messageType == "revocation")
                    {
                        var subscriptionType = metadata.TryGetProperty("subscription_type", out var typeElement)
                            ? typeElement.GetString() ?? "event" : "event";
                        var subscriptionStatus = root.GetProperty("payload").GetProperty("subscription")
                            .TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? "unknown" : "unknown";
                        EventReceived?.Invoke("revocation", subscriptionType + " · " + subscriptionStatus);
                        StatusChanged?.Invoke("Twitch revocó EventSub: " + subscriptionType + " (" + subscriptionStatus + ").");
                        if (subscriptionStatus.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
                            subscriptionStatus.Contains("user_removed", StringComparison.OrdinalIgnoreCase))
                            StatusChanged?.Invoke("La suscripción requiere revisar autorización/permisos en Twitch; no se simulará su recuperación.");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    IsConnected = false;
                    retryAttempt++;
                    var delaySeconds = Math.Min(30, 1 << Math.Min(retryAttempt, 4));
                    StatusChanged?.Invoke(
                        "EventSub perdió la sesión (" + exception.GetType().Name + "); reintentando en " + delaySeconds + " s.");

                    ClientWebSocket? candidate = null;
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(false);
                        var replacement = await OpenSessionAsync(InitialWebSocketUrl, ct).ConfigureAwait(false);
                        candidate = replacement.Socket;
                        var restored = await RegisterSubscriptionsAsync(
                            clientId, accessToken, broadcasterId, replacement.SessionId, ct).ConfigureAwait(false);
                        if (restored == 0)
                            throw new InvalidOperationException("La reconexión no pudo restaurar ninguna suscripción EventSub.");

                        var previous = ws;
                        ws = candidate;
                        socket = candidate;
                        candidate = null;
                        previous.Dispose();
                        IsConnected = true;
                        retryAttempt = 0;
                        StatusChanged?.Invoke("EventSub reconectado y " + restored + " suscripciones restauradas.");
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        candidate?.Dispose();
                        break;
                    }
                    catch (Exception reconnectException)
                    {
                        candidate?.Dispose();
                        StatusChanged?.Invoke("Falló la reconexión EventSub (" + reconnectException.GetType().Name + "); se volverá a intentar.");
                    }
                }
            }
        }
        finally
        {
            IsConnected = false;
            StatusChanged?.Invoke("EventSub desconectado.");
        }
    }

    private static string SummarizeEvent(string type, JsonElement evt)
    {
        string Value(string key) => evt.TryGetProperty(key, out var value) ? value.ToString() : "";
        return type switch
        {
            "channel.follow" => "Nuevo follow: " + Value("user_name"),
            "channel.subscribe" => "Nueva suscripción: " + Value("user_name") + " · " + Value("tier"),
            "channel.subscription.gift" => "Regalo de suscripción: " + Value("user_name") + " · cantidad " + Value("total"),
            "channel.channel_points_custom_reward_redemption.add" => "Redención de puntos: " + Value("user_name") + " · " + Value("reward").Replace("\"", ""),
            "channel.raid" => "Raid de " + Value("from_broadcaster_user_name") + " · espectadores " + Value("viewers"),
            _ => evt.GetRawText()
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Twitch cerró EventSub.");
            buffer.Write(chunk, 0, result.Count);
            if (buffer.Length > 1_048_576)
                throw new InvalidOperationException("Mensaje EventSub supera 1 MiB.");
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(buffer.ToArray());
    }

    public async Task DisconnectAsync()
    {
        cancellation?.Cancel();
        var current = socket;
        if (current is { State: WebSocketState.Open })
        {
            try { await current.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "VTuberBot disconnect", CancellationToken.None).ConfigureAwait(false); }
            catch { }
        }
        if (receiveTask is not null)
        {
            try { await receiveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        socket?.Dispose();
        cancellation?.Dispose();
        socket = null;
        cancellation = null;
        receiveTask = null;
        IsConnected = false;
        StatusChanged?.Invoke("EventSub desconectado.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        http.Dispose();
    }
}
