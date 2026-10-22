using System.Globalization;
using System.IO;
using VtuberBot.Core;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;

namespace VtuberBot.App;

public partial class MainWindow
{
    private KickWebhookReceiver? kickWebhookReceiver;
    private const int MaxKickWebhookEventsPerUiBatch = 20;
    private readonly Dictionary<string, DateTimeOffset> kickModerationCooldown = new(StringComparer.OrdinalIgnoreCase);
    private readonly CommunityCommandRateLimiter kickCommunityCommandRateLimiter = new();
    private int kickWebhookProcessorScheduled;
    private int kickWebhookOverflowNotificationPending;

    private void RunKickWebhookRetentionSweep()
    {
        try
        {
            var expiredPayloads = store.PruneKickWebhookInboxRetention();
            if (expiredPayloads > 0)
            {
                store.Log("Kick Events",
                    expiredPayloads.ToString(CultureInfo.InvariantCulture) +
                    " payload(s) de eventos fallidos vencieron la retención local de 30 días; se borró el contenido y el ID quedó retenido temporalmente para deduplicación.");
            }
        }
        catch (Exception exception)
        {
            try
            {
                store.Log("Kick Events",
                    "No se pudo aplicar la limpieza programada de la bandeja Kick (" + exception.GetType().Name + "). El contenido no se eliminó en esta pasada.");
            }
            catch { }
        }
    }

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

        // Persist before returning from the receiver callback: its HTTP 200 now means the
        // signed event is durably stored, not merely sitting in process memory.
        var payloadJson = JsonSerializer.Serialize(webhookEvent);
        var admission = store.TryStoreKickWebhookEvent(webhookEvent.EventMessageId, payloadJson);
        if (admission == KickWebhookInboxAdmissionResult.CapacityExceeded)
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
                                "La bandeja persistente llegó a su límite; la nueva entrega se rechazó temporalmente para solicitar reintentos. No se borraron eventos pendientes ni fallidos.");
                            FooterStatus.Text = "Kick: bandeja persistente llena; se requieren reintentos o revisión de eventos fallidos.";
                        }
                        finally
                        {
                            Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
                        }
                    }));
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
                }
            }

            // The event was not admitted. Propagate to the HTTP receiver so Kick can retry.
            throw new InvalidOperationException("La bandeja persistente de eventos Kick está llena; esta entrega no fue aceptada.");
        }

        Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
        try
        {
            // Duplicate IDs are acknowledged, but scheduling also helps drain the original
            // pending row if a previous UI-dispatch attempt failed.
            ScheduleKickWebhookEventProcessing();
        }
        catch (InvalidOperationException)
        {
            // The SQLite commit already succeeded. Do not turn an accepted durable event into
            // a retry-only in-memory event; it will be picked up on the next application start.
            try
            {
                store.Log("Kick Events",
                    "Un evento quedó guardado de forma persistente, pero no se pudo programar la interfaz; seguirá pendiente para el próximo inicio.");
            }
            catch { }
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
            var batch = store.ClaimPendingKickWebhookEvents(MaxKickWebhookEventsPerUiBatch);
            foreach (var pending in batch)
            {
                try
                {
                    var webhookEvent = JsonSerializer.Deserialize<KickWebhookEvent>(pending.PayloadJson);
                    if (webhookEvent is null ||
                        !string.Equals(webhookEvent.EventMessageId, pending.EventMessageId, StringComparison.Ordinal))
                        throw new InvalidDataException("El evento almacenado no coincide con su ID persistente.");

                    await ProcessKickWebhookEventAsync(webhookEvent);
                    store.MarkKickWebhookEventProcessed(pending.EventMessageId);
                }
                catch (Exception exception)
                {
                    try
                    {
                        // Keep failed payloads for investigation; do not replay an action
                        // automatically because Kick may already have applied it before a crash.
                        store.MarkKickWebhookEventFailed(
                            pending.EventMessageId,
                            exception.GetType().Name + ": " + exception.Message);
                    }
                    catch (Exception persistenceException)
                    {
                        store.Log("Kick Events",
                            "No se pudo actualizar el estado persistente del evento " + pending.EventMessageId +
                            " (" + persistenceException.GetType().Name + "). Se conservará como procesamiento interrumpido si la base sigue accesible.");
                    }

                    store.Log("Kick Events",
                        "Falló el procesamiento del evento persistido " + pending.EventMessageId +
                        " (" + exception.GetType().Name + "). Quedó pendiente de revisión; no se reintentará automáticamente.");
                    FooterStatus.Text = "Un evento Kick quedó para revisión; revisá Actividad y diagnóstico.";
                }
            }
        }
        catch (Exception exception)
        {
            try
            {
                store.Log("Kick Events",
                    "Se aisló un error al consultar la bandeja persistente (" + exception.GetType().Name + "). Los eventos guardados no se borraron.");
            }
            catch { }
        }
        finally
        {
            Interlocked.Exchange(ref kickWebhookProcessorScheduled, 0);
            try
            {
                var summary = store.GetKickWebhookInboxSummary();
                if (summary.Pending > 0)
                {
                    try { ScheduleKickWebhookEventProcessing(); }
                    catch (InvalidOperationException)
                    {
                        try
                        {
                            store.Log("Kick Events",
                                "Hay eventos persistentes pendientes que no se pudieron programar; siguen guardados para el próximo inicio.");
                        }
                        catch { }
                    }
                }
                else
                {
                    Interlocked.Exchange(ref kickWebhookOverflowNotificationPending, 0);
                }

                if (summary.Failed > 0)
                    FooterStatus.Text = "Hay eventos Kick guardados para revisión; no se repiten sanciones automáticamente.";
            }
            catch (Exception exception)
            {
                try { store.Log("Kick Events", "No se pudo consultar el estado de la bandeja (" + exception.GetType().Name + ")."); }
                catch { }
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
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var summary = store.GetKickWebhookInboxSummary();
                if (summary.Pending == 0 && Volatile.Read(ref kickWebhookProcessorScheduled) == 0)
                    break;
            }
            catch (Exception exception)
            {
                try { store.Log("Kick Events", "No se pudo confirmar el drenaje al cerrar (" + exception.GetType().Name + "); la bandeja no se borró."); }
                catch { }
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        var finalSummary = store.GetKickWebhookInboxSummary();
        if (finalSummary.Pending > 0)
        {
            store.Log("Kick Events",
                "Al cerrar quedaron " + finalSummary.Pending.ToString(CultureInfo.InvariantCulture) +
                " eventos pendientes en SQLite. Se conservaron para el próximo inicio; no se descartaron.");
        }
        if (finalSummary.Processing > 0 || Volatile.Read(ref kickWebhookProcessorScheduled) != 0)
        {
            store.Log("Kick Events",
                "El cierre llegó al límite de espera mientras se procesaba un evento. Si el proceso termina de forma inesperada, el siguiente inicio lo dejará para revisión y no repetirá automáticamente posibles acciones externas.");
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

    private async Task<bool> SendKickChatReplyAsync(string channelSlug, string text, CancellationToken cancellationToken = default)
    {
        if (store.GetSetting("kick.chat.commandsAndReactions.enabled", "false") != "true")
            throw new InvalidOperationException("Los comandos/reacciones de Kick están desactivados en la configuración local.");

        var credentials = await GetValidKickModerationCredentialsAsync().ConfigureAwait(false);
        var result = await new KickApiClient().SendChatMessageAsync(
            credentials.AccessToken, channelSlug, text, "user", cancellationToken).ConfigureAwait(false);
        if (!result.IsSent)
            throw new InvalidOperationException("Kick no confirmó el envío de la respuesta.");
        return true;
    }

    private Task<bool> SendKickChatReactionAsync(string channelSlug, string text, CancellationToken cancellationToken) =>
        SendKickChatReplyAsync(channelSlug, text, cancellationToken);

    private async Task<bool> ProcessKickCommunityCommandAsync(KickChatMessageEvent message)
    {
        var text = message.Content.Trim();
        if (!text.StartsWith('!') || text.Length > 1000)
            return false;

        // Keep the command and giveaway paths consistent with the community router's identity validation.
        if (message.Username.Length is 0 or > 25 ||
            message.Username.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
        {
            store.Log("Comunidad Kick", "Se omitió un comando de Kick porque el nombre de usuario no cumple el formato admitido.");
            return true;
        }

        var separatorIndex = text.IndexOf(' ');
        var command = separatorIndex < 0 ? text : text[..separatorIndex];
        var arguments = separatorIndex < 0 ? string.Empty : text[(separatorIndex + 1)..].Trim();
        var normalizedCommand = command.TrimStart('!').ToLowerInvariant();
        var isGiveawayCommand = string.Equals(normalizedCommand, "entrar", StringComparison.Ordinal);
        if (!isGiveawayCommand &&
            !CommunityCommandRouter.BuiltInCommands.Contains(normalizedCommand, StringComparer.OrdinalIgnoreCase))
            return false;

        if (!kickCommunityCommandRateLimiter.TryAcquire(normalizedCommand, message.Username, DateTimeOffset.UtcNow))
        {
            // A recognized but rate-limited command is consumed so it cannot accidentally trigger a reaction rule.
            store.Log("Comunidad Kick", "Se omitió una respuesta de comando por el límite de frecuencia (" +
                normalizedCommand + ", usuario " + message.Username + ").");
            return true;
        }

        string reply;
        if (isGiveawayCommand)
        {
            var activeGiveaways = store.GetGiveaways()
                .Where(giveaway => string.Equals(giveaway.Status, "OPEN", StringComparison.Ordinal))
                .ToList();

            if (activeGiveaways.Count == 0)
                reply = "No hay ningún sorteo abierto en este momento.";
            else if (activeGiveaways.Count > 1)
            {
                reply = "Hay varios sorteos abiertos; pedile al streamer que revise el panel.";
                store.Log("Sorteos", "Se rechazó !entrar desde Kick porque hay varios sorteos abiertos.");
            }
            else
            {
                var active = activeGiveaways[0];
                reply = store.EnterGiveaway(active.Id, message.Username)
                    ? message.Username + ", quedaste anotado en el sorteo " + active.Name + "."
                    : message.Username + ", ya estabas anotado en el sorteo " + active.Name + ".";
            }
        }
        else
        {
            var result = communityCommandRouter.TryExecute(command, message.Username, arguments);
            if (result is null)
                return false;
            reply = result.Message;
        }

        try
        {
            await SendKickChatReplyAsync(message.ChannelSlug, reply).ConfigureAwait(true);
            store.Log("Comunidad Kick", "Se procesó " + command + " para " + message.Username + "; Kick confirmó el envío.");
        }
        catch (Exception exception)
        {
            store.Log("Comunidad Kick", "El comando " + command + " se procesó localmente, pero no se confirmó la respuesta de Kick (" +
                exception.GetType().Name + ").");
            FooterStatus.Text = "Comando de Kick procesado; la respuesta no se confirmó. Revisá permisos y conexión.";
        }

        return true;
    }

    private async Task ProcessKickChatMessageAsync(KickChatMessageEvent message)
    {
        chatLines.Insert(0, "[Kick] " + message.Username + ": " + message.Content);
        while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
        store.Log("Kick", "Mensaje recibido de " + message.Username + " en " + message.ChannelSlug + ".");
        FooterStatus.Text = "Chat de Kick recibido y firma verificada: " + message.Username;

        var automaticModerationEnabled = store.GetSetting("kick.auto_moderation.enabled", "false") == "true";
        var suppressDownstreamSideEffects = false;
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

            // Once a real moderation action is attempted, do not read the flagged message aloud
            // or trigger a second public reaction, even if the external API later times out.
            suppressDownstreamSideEffects = true;
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

        var configuredKickBot = store.GetSetting("kick.bot.username", "").Trim().TrimStart('@');
        var isKickBotEcho = message.IsBroadcaster ||
            (configuredKickBot.Length > 0 && message.Username.Equals(configuredKickBot, StringComparison.OrdinalIgnoreCase));

        if (!suppressDownstreamSideEffects && !isKickBotEcho)
        {
            try
            {
                // TTS follows existing explicit chat-reading filters and never invokes AI or
                // republishes Kick messages to Twitch.
                socialBot.OnExternalChatMessageForSpeech(message.Username, message.Content, "Kick");
            }
            catch (Exception exception)
            {
                store.Log("Bot social", "Se aisló un error de lectura TTS de Kick (" + exception.GetType().Name + ").");
            }

            if (store.GetSetting("kick.chat.commandsAndReactions.enabled", "false") == "true")
            {
                var handledCommand = false;
                try { handledCommand = await ProcessKickCommunityCommandAsync(message); }
                catch (Exception exception)
                {
                    store.Log("Comunidad Kick", "Se aisló un error en un comando de Kick (" + exception.GetType().Name + ").");
                }

                // Avoid sending two replies if a safe built-in command already consumed the message.
                if (!handledCommand)
                {
                    try { chatReactions.OnKickChatMessage(message.Username, message.Content, message.MessageId, message.ChannelSlug); }
                    catch (Exception exception)
                    {
                        store.Log("Reacciones chat", "Se aisló un error al procesar una reacción de Kick (" + exception.GetType().Name + ").");
                    }
                }
            }
        }
        else if (isKickBotEcho)
        {
            store.Log("Kick", "Se omitieron TTS, comandos y reacciones para evitar ecos automáticos del broadcaster/bot.");
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
