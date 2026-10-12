using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VtuberBot.App;

/// <summary>Explicit user-triggered Twitch Helix moderation. Automatic rules do not call this client.</summary>
public sealed class TwitchModerationClient : IDisposable
{
    private static readonly Regex LoginPattern = new("^[a-zA-Z0-9_]{1,25}$", RegexOptions.CultureInvariant);
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> BanOrTimeoutAsync(
        string clientId, string accessToken, string channelLogin, string targetLogin,
        int? durationSeconds, string reason, CancellationToken cancellationToken = default)
    {
        channelLogin = NormalizeLogin(channelLogin, '#');
        targetLogin = NormalizeLogin(targetLogin, '@');
        ValidateCommon(clientId, accessToken, channelLogin, targetLogin);
        if (durationSeconds is < 1 or > 1_209_600)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "El timeout debe estar entre 1 segundo y 14 días.");
        if (reason.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(reason), "El motivo no puede superar 500 caracteres.");

        var token = NormalizeToken(accessToken);
        var broadcasterId = await ResolveUserIdAsync(clientId, token, channelLogin, cancellationToken).ConfigureAwait(false);
        var moderatorId = await GetAuthenticatedUserIdAsync(clientId, token, cancellationToken).ConfigureAwait(false);
        var targetId = await ResolveUserIdAsync(clientId, token, targetLogin, cancellationToken).ConfigureAwait(false);
        var query = "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) + "&moderator_id=" + Uri.EscapeDataString(moderatorId);
        var data = new Dictionary<string, object> { ["user_id"] = targetId };
        if (durationSeconds.HasValue) data["duration"] = durationSeconds.Value;
        if (!string.IsNullOrWhiteSpace(reason)) data["reason"] = reason.Trim();

        using var request = CreateRequest(HttpMethod.Post, "https://api.twitch.tv/helix/moderation/bans" + query, clientId, token);
        request.Content = new StringContent(JsonSerializer.Serialize(new { data }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken, "moderator:manage:banned_users").ConfigureAwait(false);
        return durationSeconds.HasValue
            ? "Timeout aplicado a " + targetLogin + " por " + durationSeconds.Value + " segundos."
            : "Ban permanente aplicado a " + targetLogin + ".";
    }

    /// <summary>Removes the single message whose IRCv3 id tag was received from Twitch.</summary>
    public async Task<string> DeleteChatMessageAsync(
        string clientId, string accessToken, string channelLogin, string messageId,
        CancellationToken cancellationToken = default)
    {
        channelLogin = NormalizeLogin(channelLogin, '#');
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Configurá Client ID y autorizá la cuenta Twitch.");
        if (!LoginPattern.IsMatch(channelLogin))
            throw new ArgumentException("El canal debe ser un login de Twitch válido.");
        if (string.IsNullOrWhiteSpace(messageId) || messageId.Length > 128 ||
            messageId.Any(char.IsWhiteSpace))
            throw new ArgumentException("El mensaje no contiene un ID válido para borrar.");

        var token = NormalizeToken(accessToken);
        var broadcasterId = await ResolveUserIdAsync(clientId, token, channelLogin, cancellationToken).ConfigureAwait(false);
        var moderatorId = await GetAuthenticatedUserIdAsync(clientId, token, cancellationToken).ConfigureAwait(false);
        var query = "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) +
                    "&moderator_id=" + Uri.EscapeDataString(moderatorId) +
                    "&message_id=" + Uri.EscapeDataString(messageId);
        using var request = CreateRequest(HttpMethod.Delete, "https://api.twitch.tv/helix/moderation/chat" + query, clientId, token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken, "moderator:manage:chat_messages").ConfigureAwait(false);
        return "Mensaje de chat eliminado (ID " + messageId + ").";
    }

    public async Task<string> UnbanAsync(
        string clientId, string accessToken, string channelLogin, string targetLogin,
        CancellationToken cancellationToken = default)
    {
        channelLogin = NormalizeLogin(channelLogin, '#');
        targetLogin = NormalizeLogin(targetLogin, '@');
        ValidateCommon(clientId, accessToken, channelLogin, targetLogin);
        var token = NormalizeToken(accessToken);
        var broadcasterId = await ResolveUserIdAsync(clientId, token, channelLogin, cancellationToken).ConfigureAwait(false);
        var moderatorId = await GetAuthenticatedUserIdAsync(clientId, token, cancellationToken).ConfigureAwait(false);
        var targetId = await ResolveUserIdAsync(clientId, token, targetLogin, cancellationToken).ConfigureAwait(false);
        var query = "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) + "&moderator_id=" + Uri.EscapeDataString(moderatorId) + "&user_id=" + Uri.EscapeDataString(targetId);
        using var request = CreateRequest(HttpMethod.Delete, "https://api.twitch.tv/helix/moderation/bans" + query, clientId, token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken, "moderator:manage:banned_users").ConfigureAwait(false);
        return "Ban/timeout retirado a " + targetLogin + ".";
    }

    private async Task<string> ResolveUserIdAsync(string clientId, string token, string login, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "https://api.twitch.tv/helix/users?login=" + Uri.EscapeDataString(login), clientId, token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw ApiError(response.StatusCode, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) throw new InvalidOperationException("Twitch no encontró la cuenta '" + login + "'.");
        return data[0].GetProperty("id").GetString() ?? throw new InvalidOperationException("Twitch no devolvió el ID de '" + login + "'.");
    }

    private async Task<string> GetAuthenticatedUserIdAsync(string clientId, string token, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "https://api.twitch.tv/helix/users", clientId, token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw ApiError(response.StatusCode, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) throw new InvalidOperationException("Twitch no pudo identificar la cuenta autorizada.");
        return data[0].GetProperty("id").GetString() ?? throw new InvalidOperationException("Twitch no devolvió el ID de la cuenta autorizada.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string clientId, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Client-Id", clientId.Trim());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string NormalizeLogin(string login, char prefix)
    {
        var value = login.Trim();
        if (value.Length > 0 && value[0] == prefix) value = value[1..];
        return value.ToLowerInvariant();
    }

    private static void ValidateCommon(string clientId, string accessToken, string channelLogin, string targetLogin)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Configurá Client ID y autorizá la cuenta Twitch.");
        if (!LoginPattern.IsMatch(channelLogin)) throw new ArgumentException("El canal debe ser un login de Twitch válido.");
        if (!LoginPattern.IsMatch(targetLogin)) throw new ArgumentException("El usuario objetivo debe ser un login de Twitch válido (1–25 letras/números/guion bajo).");
    }

    private static string NormalizeToken(string token)
    {
        var value = token.Trim();
        return value.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase) ? value[6..] : value;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string requiredScope)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw ApiError(response.StatusCode, body, requiredScope);
    }

    private static InvalidOperationException ApiError(
        System.Net.HttpStatusCode status,
        string body,
        string? requiredScope = null)
    {
        var suffix = (int)status switch
        {
            401 => string.IsNullOrWhiteSpace(requiredScope)
                ? " Revisá que el access token sea válido, pertenezca al Client ID configurado y no haya sido revocado."
                : " Revisá el token y el scope " + requiredScope + "; puede ser necesario volver a autorizar.",
            403 => " La cuenta autorizada no tiene permisos de moderación en ese canal.",
            404 => " Twitch no encontró el canal o usuario indicado.",
            429 => " Twitch limitó temporalmente las solicitudes.",
            _ => string.Empty
        };
        var detail = "La API de Twitch devolvió HTTP " + (int)status + "." + suffix;
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(message.GetString()))
                detail += "\n" + message.GetString();
        }
        catch (JsonException) { }
        return new InvalidOperationException(detail);
    }

    public void Dispose() => http.Dispose();
}
