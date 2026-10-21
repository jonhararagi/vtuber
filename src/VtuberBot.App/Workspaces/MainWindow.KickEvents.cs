using System.Globalization;
using VtuberBot.Core;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;

namespace VtuberBot.App;

public partial class MainWindow
{
    private KickWebhookReceiver? kickWebhookReceiver;
    private const int MaxPendingKickWebhookEvents = 256;
    private const int MaxKickWebhookEventsPerUiBatch = 20;
    private readonly BoundedQueue<KickWebhookEvent> pendingKickWebhookEvents = new(MaxPendingKickWebhookEvents);
    private readonly Dictionary<string, DateTimeOffset> kickModerationCooldown = new(StringComparer.OrdinalIgnoreCase);
    private int kickWebhookProcessorScheduled;
    private int kickWebhookOverflowNotificationPending;

    private KickWebhookReceiver EnsureKickWebhookReceiver()
    {
        if (kickWebhookReceiver is not null) return kickWebhookReceiver;

        var receiver = new KickWebhookReceiver();
        receiver.EventReceived += HandleKickWebhookEvent;
        receiver.StatusChanged += status =>
        {
            try { Dispatcher.BeginInvoke(new Action(() => FooterStatus.Text = status)); }
            catch (InvalidOperationException) { }
        };
        kickWebhookReceiver = receiver;
        return receiver;
    }

    private async Task<string> GetValidKickAppAccessTokenAsync()
    {
        var accessToken = secretStore.Read("kick-app-access-token");
        if (DateTimeOffset.TryParse(
                store.GetSetting("kick.appToken.expiresAtUtc"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var expiresAt) &&
            expiresAt > DateTimeOffset.UtcNow.AddMinutes(5) &&
            !string.IsNullOrWhiteSpace(accessToken))
            return accessToken;

        var clientId = store.GetSetting("kick.client_id").Trim();
        var clientSecret = secretStore.Read("kick-client-secret");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Primero guardá Client ID y Client Secret en Bots y plataformas para generar un App Access Token.");

        var appCredentials = await KickOAuthClient.GetAppAccessTokenAsync(clientId, clientSecret);
        secretStore.Save("kick-app-access-token", appCredentials.AccessToken);
        store.SetSetting("kick.appToken.expiresAtUtc", appCredentials.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        store.Log("Kick Events", "Se renovó un App Access Token para suscripciones; no se registró el token.");
        return appCredentials.AccessToken;
    }

    private static bool IsConfiguredPublicKickWebhookUrl(string input, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate((input ?? "").Trim(), UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Host) ||
            parsed.IsLoopback ||
            !parsed.AbsolutePath.TrimEnd('/').Equals("/kick-webhook", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
            return false;

        var host = parsed.Host.TrimEnd('.').ToLowerInvariant();
        if (host is "localhost" or "localhost.localdomain" ||
            host.EndsWith(".localhost", StringComparison.Ordinal) ||
            host.EndsWith(".local", StringComparison.Ordinal) ||
            host.EndsWith(".internal", StringComparison.Ordinal) ||
            host.EndsWith(".lan", StringComparison.Ordinal) ||
            host.EndsWith(".home", StringComparison.Ordinal) ||
            host.EndsWith(".test", StringComparison.Ordinal) ||
            host.EndsWith(".invalid", StringComparison.Ordinal) ||
            host.EndsWith(".example", StringComparison.Ordinal) ||
            !host.Contains('.'))
            return false;

        if (IPAddress.TryParse(host, out var address) && !IsPublicWebhookIp(address))
            return false;

        uri = parsed;
        return true;
    }

    private static bool IsPublicWebhookIp(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6Multicast)
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) ||
                (bytes[0] & 0xFE) == 0xFC || // unique-local fc00::/7
                (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0xC0)) // deprecated site-local fec0::/10
                return false;
            return true;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var first = bytes[0];
        var second = bytes[1];
        return first != 0 &&
            first != 10 &&
            first != 127 &&
            first < 224 &&
            !(first == 100 && second is >= 64 and <= 127) &&
            !(first == 169 && second == 254) &&
            !(first == 172 && second is >= 16 and <= 31) &&
            !(first == 192 && second == 168) &&
            !(first == 192 && second == 0) &&
            !(first == 198 && second is 18 or 19) &&
            !(first == 198 && second == 51 && bytes[2] == 100) &&
            !(first == 203 && second == 0 && bytes[2] == 113);
    }

    private void HandleKickWebhookEvent(KickWebhookEvent webhookEvent)
    {
        if (Volatile.Read(ref shutdownStarted) || Volatile.Read(ref shutdownCompleted))
            throw new InvalidOperationException("VTuberBot se está cerrando; el webhook debe reintentarse.");

        if (!pendingKickWebhookEvents.TryEnqueue(webhookEvent))
        {
            if (Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 1) == 0)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            store.Log("Kick Events",
                                "La cola de eventos llegó a 256 entradas; se rechazaron temporalmente nuevas entregas para solicitar reintentos.");
                            FooterStatus.Text = "Kick: cola de eventos saturada; se rechazan nuevas entregas hasta recuperar capacidad.";
                        }
                        finally { Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0); }
                    }));
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
                }
            }

            // Throwing keeps the receiver from returning HTTP 200 or committing the message ID.
            throw new InvalidOperationException("La cola acotada de Kick está llena; la entrega no fue admitida y debe reintentarse.");
        }

        try
        {
            ScheduleKickWebhookEventProcessing();
        }
        catch
        {
            // Do not retain an event whose HTTP delivery will be rejected and retried.
            // Otherwise that retry could enqueue the same message a second time.
            pendingKickWebhookEvents.TryRemoveLast(webhookEvent);
            throw;
        }
    }

    private void ScheduleKickWebhookEventProcessing()
    {
        if (Interlocked.Exchange(ref kickWebhookProcessorScheduled, 1) != 0)
            return;

        try
        {
            Dispatcher.BeginInvoke(new Action(() => { _ = ProcessPendingKickWebhookEventsAsync(); }));
        }
        catch (InvalidOperationException exception)
        {
            Interlocked.Exchange(ref kickWebhookProcessorScheduled, 0);
            throw new InvalidOperationException("No se pudo programar el procesamiento de eventos Kick en la interfaz.", exception);
        }
    }

    private async Task ProcessPendingKickWebhookEventsAsync()
    {
        try
        {
            var processed = 0;
            while (processed < MaxKickWebhookEventsPerUiBatch &&
                   pendingKickWebhookEvents.TryDequeue(out var webhookEvent))
            {
                processed++;
                try
                {
                    await ProcessKickWebhookEventAsync(webhookEvent);
                }
                catch (Exception exception)
                {
                    store.Log("Kick Events",
                        "Falló el procesamiento de un evento verificado (" + exception.GetType().Name + ").");
                    FooterStatus.Text = "No se pudo procesar un evento Kick; revisá Actividad y diagnóstico.";
                }
            }
        }
        catch (Exception exception)
        {
            try
            {
                store.Log("Kick Events",
                    "Se aisló un error del consumidor de eventos (" + exception.GetType().Name + ").");
            }
            catch { }
        }
        finally
        {
            Interlocked.Exchange(ref kickWebhookProcessorScheduled, 0);
            if (pendingKickWebhookEvents.Count > 0)
            {
                try { ScheduleKickWebhookEventProcessing(); }
                catch (InvalidOperationException)
                {
                    var dropped = pendingKickWebhookEvents.Count;
                    pendingKickWebhookEvents.Clear();
                    try
                    {
                        store.Log("Kick Events", "No quedó disponible la interfaz para procesar la cola durante el cierre; " +
                            dropped.ToString(CultureInfo.InvariantCulture) + " eventos en espera quedaron sin procesar.");
                    }
                    catch { }
                }
            }
            else
            {
                Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
            }
        }
    }

    private async Task ProcessKickWebhookEventAsync(KickWebhookEvent webhookEvent)
    {
        var rejectionReason = ValidateKickWebhookEvent(webhookEvent);
        if (rejectionReason is not null)
        {
            store.Log("Kick Events", "Evento firmado rechazado por configuración local: " + rejectionReason);
            FooterStatus.Text = "Evento Kick rechazado: no coincide con las suscripciones/canal configurados.";
            return;
        }

        if (webhookEvent.ChatMessage is { } chatMessage)
        {
            await ProcessKickChatMessageAsync(chatMessage);
        }
        else
        {
            chatLines.Insert(0, "[Kick Events] " + webhookEvent.Summary);
            while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
            store.Log("Kick Events", webhookEvent.Summary + " (firma verificada, suscripción local reconocida).");
            FooterStatus.Text = "Evento Kick recibido y firma validada: " + webhookEvent.EventType;
        }
    }

    private async Task DrainPendingKickWebhookEventsForShutdownAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while ((pendingKickWebhookEvents.Count > 0 || Volatile.Read(ref kickWebhookProcessorScheduled) != 0) &&
               DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        var remaining = pendingKickWebhookEvents.Count;
        var processing = Volatile.Read(ref kickWebhookProcessorScheduled) != 0;
        if (remaining > 0)
        {
            pendingKickWebhookEvents.Clear();
            store.Log("Kick Events", "Al cerrar, se agotó el tiempo de drenaje; " +
                remaining.ToString(CultureInfo.InvariantCulture) + " eventos en cola fueron descartados.");
        }
        if (processing)
        {
            store.Log("Kick Events", "El cierre alcanzó el límite de espera mientras un evento Kick estaba en procesamiento.");
        }
    }

    private string? ValidateKickWebhookEvent(KickWebhookEvent webhookEvent)
    {
        var allowedSubscriptionIds = store.GetSetting("kick.webhook.subscription_ids")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(webhookEvent.SubscriptionId) ||
            !allowedSubscriptionIds.Contains(webhookEvent.SubscriptionId, StringComparer.Ordinal))
            return "El ID de suscripción no está registrado por VTuberBot para esta sesión/canal.";

        if (webhookEvent.ChatMessage is { } chatMessage)
        {
            if (!long.TryParse(store.GetSetting("kick.webhook.broadcaster_user_id"),
                    NumberStyles.None, CultureInfo.InvariantCulture, out var configuredBroadcasterId) ||
                configuredBroadcasterId <= 0)
                return "Todavía no hay un broadcaster ID validado por suscripciones de Kick.";
            if (chatMessage.BroadcasterUserId != configuredBroadcasterId)
                return "El mensaje pertenece a otro canal de Kick distinto del configurado.";
        }

        return null;
    }

    private async Task ProcessKickChatMessageAsync(KickChatMessageEvent message)
    {
        chatLines.Insert(0, "[Kick] " + message.Username + ": " + message.Content);
        while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
        store.Log("Kick", "Mensaje recibido de " + message.Username + " en " + message.ChannelSlug + ".");
        FooterStatus.Text = "Chat de Kick recibido y firma verificada: " + message.Username;

        var automaticModerationEnabled = store.GetSetting("kick.auto_moderation.enabled", "false") == "true";
        foreach (var rule in store.GetModerationRules().Where(rule => rule.Enabled))
        {
            bool matches;
            try
            {
                matches = Regex.IsMatch(
                    message.Content,
                    rule.Pattern,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(100));
            }
            catch (RegexMatchTimeoutException)
            {
                store.Log("Moderación Kick", "La regla '" + rule.Name + "' agotó el tiempo de análisis; no se ejecutó ninguna acción.");
                continue;
            }

            if (!matches) continue;
            store.Log("Moderación Kick", "Coincidencia de regla '" + rule.Name + "' para " + message.Username +
                " (acción " + rule.Action + "; automatización global " + (automaticModerationEnabled ? "activa" : "inactiva") + ").");

            if (string.Equals(rule.Action, "flag", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!automaticModerationEnabled)
                continue;
            if (message.IsBroadcaster || message.IsModerator)
            {
                store.Log("Moderación Kick", "Se omitió una sanción automática; broadcaster y moderadores están protegidos.");
                continue;
            }

            var cooldownKey = rule.Name + ":" + message.SenderUserId.ToString(CultureInfo.InvariantCulture);
            if (kickModerationCooldown.TryGetValue(cooldownKey, out var previousAction) &&
                DateTimeOffset.UtcNow - previousAction < TimeSpan.FromMinutes(5))
            {
                store.Log("Moderación Kick", "Acción omitida por cooldown de 5 minutos para " + message.Username + " / " + rule.Name + ".");
                continue;
            }

            try
            {
                var credentials = await GetValidKickModerationCredentialsAsync();
                var client = new KickApiClient();
                string result;
                switch (rule.Action.Trim().ToLowerInvariant())
                {
                    case "warn":
                        var warning = message.Username + ", tu mensaje coincidió con la regla de moderación '" + rule.Name + "'. Revisá las normas del canal.";
                        var sent = await client.SendChatMessageAsync(credentials.AccessToken, message.ChannelSlug, warning, "user");
                        if (!sent.IsSent)
                            throw new InvalidOperationException("Kick no confirmó el envío del aviso.");
                        result = "Aviso enviado.";
                        break;
                    case "delete":
                        result = await client.DeleteChatMessageAsync(credentials.AccessToken, message.MessageId);
                        break;
                    case "timeout":
                        result = await client.BanOrTimeoutUserAsync(credentials.AccessToken, message.ChannelSlug, message.SenderUserId, 10,
                            TruncateKickReason("Regla automática: " + rule.Name));
                        break;
                    case "ban":
                        result = await client.BanOrTimeoutUserAsync(credentials.AccessToken, message.ChannelSlug, message.SenderUserId, null,
                            TruncateKickReason("Regla automática: " + rule.Name));
                        break;
                    default:
                        store.Log("Moderación Kick", "Acción de regla no compatible; no se ejecutó: " + rule.Action);
                        continue;
                }

                kickModerationCooldown[cooldownKey] = DateTimeOffset.UtcNow;
                store.Log("Moderación Kick", "Acción automática confirmada por API: " + rule.Action +
                    " · regla " + rule.Name + " · usuario " + message.Username + " · " + result);
                FooterStatus.Text = "Kick confirmó la acción automática: " + rule.Action + " / " + rule.Name;
                // One non-observation action per incoming message, matching Twitch's safety policy.
                break;
            }
            catch (Exception exception)
            {
                store.Log("Moderación Kick", "Falló la acción automática '" + rule.Action + "' de la regla '" +
                    rule.Name + "' para " + message.Username + " (" + exception.GetType().Name + "): " + exception.Message);
                FooterStatus.Text = "Kick no confirmó la acción automática; revisá permisos y scopes.";
                break;
            }
        }

        if (kickModerationCooldown.Count > 10000)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10);
            foreach (var key in kickModerationCooldown.Where(pair => pair.Value < cutoff).Select(pair => pair.Key).ToArray())
                kickModerationCooldown.Remove(key);
        }
    }

    private static string TruncateKickReason(string reason) =>
        reason.Length <= 100 ? reason : reason[..100];

    private async Task StopKickWebhookReceiverAsync()
    {
        if (kickWebhookReceiver is null) return;
        try { await kickWebhookReceiver.DisposeAsync(); }
        finally { kickWebhookReceiver = null; }
    }
}
