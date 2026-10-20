using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;

namespace VtuberBot.App;

public partial class MainWindow
{
    private KickWebhookReceiver? kickWebhookReceiver;
    private readonly Dictionary<string, DateTimeOffset> kickModerationCooldown = new(StringComparer.OrdinalIgnoreCase);

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
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast || address.IsIPv6UniqueLocal)
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return true;

        var bytes = address.GetAddressBytes();
        var first = bytes[0];
        var second = bytes[1];
        return first != 0 &&
            first != 10 &&
            first != 127 &&
            first < 224 &&
            !(first == 100 && second is >= 64 and <= 127) &&
            !(first == 169 && second == 254) &&
            !(first == 172 && second is >= 16 and <= 31) &&
            !(first == 192 && second == 168);
    }

    private void HandleKickWebhookEvent(KickWebhookEvent webhookEvent)
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (webhookEvent.ChatMessage is { } chatMessage)
                    {
                        await ProcessKickChatMessageAsync(chatMessage);
                    }
                    else
                    {
                        chatLines.Insert(0, "[Kick Events] " + webhookEvent.Summary);
                        while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
                        store.Log("Kick Events", webhookEvent.Summary + " (firma verificada).");
                        FooterStatus.Text = "Evento Kick recibido y firma validada: " + webhookEvent.EventType;
                    }
                }
                catch (Exception exception)
                {
                    store.Log("Kick Events", "Falló el procesamiento de un evento verificado (" + exception.GetType().Name + ").");
                    FooterStatus.Text = "No se pudo procesar un evento Kick; revisá Actividad y diagnóstico.";
                }
            }));
        }
        catch (InvalidOperationException)
        {
            // WPF may start shutting down while a remote event arrives.
        }
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
