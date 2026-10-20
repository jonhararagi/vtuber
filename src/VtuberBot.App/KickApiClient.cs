using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VtuberBot.App;

public sealed record KickOAuthCredentials(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Scopes);

public sealed record KickChannelInfo(long BroadcasterUserId, string Slug, string? Name, bool? IsLive);

public sealed record KickChatSendResult(bool IsSent, string? MessageId);

/// <summary>
/// Kick OAuth 2.1 authorization-code flow for the user's own registered application.
/// The client secret is supplied by the user and must never be shipped as a default.
/// </summary>
public static class KickOAuthClient
{
    public const string RedirectUri = "http://localhost:17564/callback/";
    private const int CallbackPort = 17564;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<KickOAuthCredentials> AuthorizeAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        ValidateClient(clientId, clientSecret);
        clientId = clientId.Trim();
        clientSecret = clientSecret.Trim();

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));

        using var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException(
                $"No se pudo abrir el callback local de Kick en el puerto {CallbackPort}. Cerrá otra instancia de autorización e intentá de nuevo.",
                exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var authorizeUrl = "https://id.kick.com/oauth/authorize" +
            "?response_type=code" +
            "&client_id=" + Uri.EscapeDataString(clientId) +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString("user:read channel:read chat:write moderation:ban moderation:chat_message:manage") +
            "&state=" + Uri.EscapeDataString(state) +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256";

        try
        {
            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            listener.Stop();
            throw new InvalidOperationException("No se pudo abrir el navegador para autorizar Kick. Abrí manualmente la URL de autorización desde el panel.", exception);
        }

        using var connection = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine) || !requestLine.StartsWith("GET ", StringComparison.Ordinal))
            throw new InvalidOperationException("El callback OAuth de Kick no recibió una solicitud GET válida.");

        var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length < 2)
            throw new InvalidOperationException("La respuesta OAuth de Kick tiene un formato inesperado.");

        // Consume headers before replying. Only the request line is used and all listening is loopback-only.
        for (var i = 0; i < 64; i++)
        {
            var header = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(header)) break;
        }

        if (!System.Uri.TryCreate("http://localhost" + requestParts[1], System.UriKind.Absolute, out var callbackUri))
        {
            await WriteCallbackResponseAsync(stream, false, timeout.Token).ConfigureAwait(false);
            throw new InvalidOperationException("La URL del callback OAuth de Kick no es válida.");
        }
        if (!string.Equals(callbackUri.AbsolutePath, "/callback/", StringComparison.Ordinal))
        {
            await WriteCallbackResponseAsync(stream, false, timeout.Token).ConfigureAwait(false);
            throw new InvalidOperationException("La ruta del callback no coincide con la URL registrada en Kick Developer.");
        }

        var query = ParseQuery(callbackUri.Query);
        if (query.TryGetValue("error", out var oauthError))
        {
            await WriteCallbackResponseAsync(stream, false, timeout.Token).ConfigureAwait(false);
            throw new InvalidOperationException("Kick no concedió la autorización (" + SanitizeError(oauthError) + ").");
        }

        if (!query.TryGetValue("state", out var returnedState) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(returnedState),
                Encoding.UTF8.GetBytes(state)))
        {
            await WriteCallbackResponseAsync(stream, false, timeout.Token).ConfigureAwait(false);
            throw new InvalidOperationException("El estado OAuth no coincide; se rechazó el callback por seguridad.");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code) || code.Length > 4096)
        {
            await WriteCallbackResponseAsync(stream, false, timeout.Token).ConfigureAwait(false);
            throw new InvalidOperationException("Kick no devolvió un código de autorización válido.");
        }

        try
        {
            var credentials = await ExchangeCodeAsync(clientId, clientSecret, code, verifier, cancellationToken).ConfigureAwait(false);
            await WriteCallbackResponseAsync(stream, true, timeout.Token).ConfigureAwait(false);
            return credentials;
        }
        catch
        {
            // Do not tell the browser the sign-in succeeded before the token exchange did.
            try { await WriteCallbackResponseAsync(stream, false, CancellationToken.None).ConfigureAwait(false); }
            catch { /* Preserve the actual OAuth/token-exchange error for the app UI. */ }
            throw;
        }
    }

    public static async Task<KickOAuthCredentials> RefreshAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ValidateClient(clientId, clientSecret);
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Kick no guardó un refresh token. Volvé a autorizar la cuenta.");

        using var response = await Http.PostAsync(
            "https://id.kick.com/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["client_secret"] = clientSecret.Trim(),
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken.Trim()
            }),
            cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateOAuthError(response.StatusCode, json, "Kick no pudo renovar el token");

        return ParseCredentials(json, refreshToken);
    }

    private static async Task<KickOAuthCredentials> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsync(
            "https://id.kick.com/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            }),
            cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateOAuthError(response.StatusCode, json, "Kick no pudo intercambiar el código OAuth");

        return ParseCredentials(json, null);
    }

    private static KickOAuthCredentials ParseCredentials(string json, string? previousRefreshToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var accessToken = GetString(root, "access_token");
        var refreshToken = GetString(root, "refresh_token");
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Kick devolvió una respuesta OAuth sin access_token.");

        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = previousRefreshToken ?? "";

        if (!root.TryGetProperty("expires_in", out var expiresElement) ||
            !expiresElement.TryGetInt32(out var seconds) || seconds <= 0)
            throw new InvalidOperationException("Kick devolvió un token sin una expiración válida.");
        var expiresIn = seconds;
        var scopes = new List<string>();
        if (root.TryGetProperty("scope", out var scopeElement))
        {
            if (scopeElement.ValueKind == JsonValueKind.String && scopeElement.GetString() is { } scopeText)
                scopes.AddRange(scopeText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            else if (scopeElement.ValueKind == JsonValueKind.Array)
                scopes.AddRange(scopeElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .Where(item => item.Length > 0));
        }

        return new KickOAuthCredentials(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn), scopes);
    }

    private static InvalidOperationException CreateOAuthError(HttpStatusCode status, string body, string context)
    {
        var detail = "HTTP " + (int)status;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                detail += " (" + SanitizeError(error.GetString() ?? "") + ")";
            else if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                detail += ": " + SanitizeError(message.GetString() ?? "");
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(body))
                detail += ": " + SanitizeError(body);
            else
                detail += " (respuesta vacía)";
        }

        var hint = (int)status == 400
            ? " Verificá que redirect URI sea exactamente " + RedirectUri +
              ", que Client ID/Secret coincidan y que los scopes estén habilitados. Kick también tiene reportes públicos de respuestas 400 vacías en el intercambio OAuth."
            : "";
        return new InvalidOperationException(context + " (" + detail + ")." + hint);
    }

    private static async Task WriteCallbackResponseAsync(NetworkStream stream, bool success, CancellationToken cancellationToken)
    {
        var html = success
            ? "<!doctype html><meta charset=\"utf-8\"><title>VTuberBot</title><p>Autorización recibida. Podés volver a VTuberBot.</p>"
            : "<!doctype html><meta charset=\"utf-8\"><title>VTuberBot</title><p>No se completó la autorización. Volvé a VTuberBot para ver el detalle.</p>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: " +
            body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? "" : part[(separator + 1)..];
            values[Uri.UnescapeDataString(key.Replace('+', ' '))] =
                Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        return values;
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string SanitizeError(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length > 500) text = text[..500];
        return text;
    }

    private static void ValidateClient(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Trim().Length > 256)
            throw new ArgumentException("Ingresá el Client ID de la aplicación Kick.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Trim().Length > 8192)
            throw new ArgumentException("Ingresá el Client Secret de la aplicación Kick.", nameof(clientSecret));
    }
}

/// <summary>Small, bounded client for documented Kick public API operations used by the bot panel.</summary>
public sealed class KickApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Regex SlugPattern = new("^[A-Za-z0-9_-]{1,25}$", RegexOptions.CultureInvariant);

    public static string NormalizeChannelSlug(string input)
    {
        var value = (input ?? "").Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!string.Equals(uri.Host, "kick.com", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Host, "www.kick.com", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La dirección debe pertenecer a kick.com.", nameof(input));
            value = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        }
        value = value.TrimStart('@', '#').Trim();
        if (!SlugPattern.IsMatch(value))
            throw new ArgumentException("Ingresá el nombre de canal de Kick (1–25 letras, números, guion o guion bajo), no el nombre visible.", nameof(input));
        return value.ToLowerInvariant();
    }

    public async Task<KickChannelInfo> GetChannelAsync(string accessToken, string? channelSlug = null, CancellationToken cancellationToken = default)
    {
        RequireToken(accessToken);
        var url = "https://api.kick.com/public/v1/channels";
        if (!string.IsNullOrWhiteSpace(channelSlug))
            url += "?slug=" + Uri.EscapeDataString(NormalizeChannelSlug(channelSlug));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateApiError(response.StatusCode, json, "No se pudo consultar el canal de Kick");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            throw new InvalidOperationException("Kick no devolvió información del canal. Revisá el slug y los permisos channel:read.");

        var channel = data[0];
        if (!channel.TryGetProperty("broadcaster_user_id", out var idElement) ||
            !idElement.TryGetInt64(out var id) || id <= 0)
            throw new InvalidOperationException("La respuesta de Kick no contiene un broadcaster_user_id válido.");

        var slug = channel.TryGetProperty("slug", out var slugElement) && slugElement.ValueKind == JsonValueKind.String
            ? slugElement.GetString() ?? ""
            : "";
        var name = channel.TryGetProperty("channel_description", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        bool? live = null;
        if (channel.TryGetProperty("stream", out var stream) && stream.ValueKind == JsonValueKind.Object &&
            stream.TryGetProperty("is_live", out var liveElement) &&
            liveElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            live = liveElement.GetBoolean();

        return new KickChannelInfo(id, slug, name, live);
    }

    public async Task<KickChatSendResult> SendChatMessageAsync(
        string accessToken,
        string channelSlug,
        string content,
        string messageType = "user",
        CancellationToken cancellationToken = default)
    {
        RequireToken(accessToken);
        var slug = NormalizeChannelSlug(channelSlug);
        var message = (content ?? "").Trim();
        if (message.Length == 0)
            throw new ArgumentException("El mensaje de prueba no puede estar vacío.", nameof(content));
        if (new StringInfo(message).LengthInTextElements > 500 || Encoding.UTF8.GetByteCount(message) > 2048)
            throw new ArgumentException("Kick limita el mensaje a 500 caracteres percibidos y 2048 bytes UTF-8.", nameof(content));

        var type = (messageType ?? "").Trim().ToLowerInvariant();
        if (type is not ("bot" or "user"))
            throw new ArgumentException("El tipo de mensaje debe ser bot o user.", nameof(messageType));

        // The official API sends type=bot to the channel associated with the access token.
        // Check it before sending, so a pasted channel name cannot silently target another chat.
        var target = await GetChannelAsync(accessToken, type == "user" ? slug : null, cancellationToken).ConfigureAwait(false);
        if (type == "bot" && !string.Equals(target.Slug, slug, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"El token está vinculado al canal «{target.Slug}», no a «{slug}». Para tipo bot, Kick envía siempre al canal asociado al token.");

        var payload = new Dictionary<string, object>
        {
            ["content"] = message,
            ["type"] = type
        };
        if (type == "user") payload["broadcaster_user_id"] = target.BroadcasterUserId;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.kick.com/public/v1/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateApiError(response.StatusCode, json, "Kick rechazó el mensaje de prueba");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("is_sent", out var sent) ||
            sent.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("Kick respondió sin confirmar que el mensaje se haya enviado.");

        var messageId = data.TryGetProperty("message_id", out var messageIdElement) &&
                        messageIdElement.ValueKind == JsonValueKind.String
            ? messageIdElement.GetString()
            : null;
        return new KickChatSendResult(true, messageId);
    }

    public async Task<string> BanOrTimeoutUserAsync(
        string accessToken,
        string channelSlug,
        long targetUserId,
        int? durationMinutes,
        string reason,
        CancellationToken cancellationToken = default)
    {
        RequireToken(accessToken);
        var slug = NormalizeChannelSlug(channelSlug);
        if (targetUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetUserId), "Ingresá el ID numérico válido del usuario de Kick.");
        if (durationMinutes is < 1 or > 10080)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "El timeout de Kick debe estar entre 1 minuto y 7 días; dejalo vacío para ban permanente.");
        reason = (reason ?? "").Trim();
        if (reason.Length > 100)
            throw new ArgumentOutOfRangeException(nameof(reason), "El motivo de Kick no puede superar 100 caracteres.");

        var channel = await GetChannelAsync(accessToken, slug, cancellationToken).ConfigureAwait(false);
        var payload = new Dictionary<string, object>
        {
            ["broadcaster_user_id"] = channel.BroadcasterUserId,
            ["user_id"] = targetUserId
        };
        if (durationMinutes.HasValue) payload["duration"] = durationMinutes.Value;
        if (reason.Length > 0) payload["reason"] = reason;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.kick.com/public/v1/moderation/bans")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateApiError(response.StatusCode, json, durationMinutes.HasValue ? "Kick rechazó el timeout" : "Kick rechazó el ban", "moderation:ban");

        return durationMinutes.HasValue
            ? "Timeout de Kick aplicado por " + durationMinutes.Value + " minutos al usuario ID " + targetUserId + "."
            : "Ban permanente de Kick aplicado al usuario ID " + targetUserId + ".";
    }

    public async Task<string> UnbanUserAsync(
        string accessToken,
        string channelSlug,
        long targetUserId,
        CancellationToken cancellationToken = default)
    {
        RequireToken(accessToken);
        var slug = NormalizeChannelSlug(channelSlug);
        if (targetUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetUserId), "Ingresá el ID numérico válido del usuario de Kick.");

        var channel = await GetChannelAsync(accessToken, slug, cancellationToken).ConfigureAwait(false);
        var payload = new Dictionary<string, long>
        {
            ["broadcaster_user_id"] = channel.BroadcasterUserId,
            ["user_id"] = targetUserId
        };
        using var request = new HttpRequestMessage(HttpMethod.Delete, "https://api.kick.com/public/v1/moderation/bans")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateApiError(response.StatusCode, json, "Kick rechazó retirar el ban/timeout", "moderation:ban");

        return "Kick confirmó retirar el ban/timeout del usuario ID " + targetUserId + ".";
    }

    public async Task<string> DeleteChatMessageAsync(
        string accessToken,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        RequireToken(accessToken);
        var normalizedId = (messageId ?? "").Trim();
        if (!Guid.TryParse(normalizedId, out var parsedId))
            throw new ArgumentException("Ingresá el ID UUID válido del mensaje de Kick.", nameof(messageId));

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            "https://api.kick.com/public/v1/chat/" + Uri.EscapeDataString(parsedId.ToString()));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateApiError(response.StatusCode, json, "Kick rechazó borrar el mensaje", "moderation:chat_message:manage");

        return "Kick confirmó borrar el mensaje " + parsedId + ".";
    }

    private static void RequireToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192)
            throw new InvalidOperationException("Primero autorizá Kick desde el panel de bots.");
    }

    private static InvalidOperationException CreateApiError(
        HttpStatusCode status,
        string body,
        string context,
        string requiredScope = "chat:write")
    {
        var detail = "HTTP " + (int)status;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                detail += ": " + message.GetString();
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(body))
                detail += ": " + body.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(body.Length, 300)];
        }
        var permissionHint = (int)status == 403
            ? " Verificá que la cuenta autorizada sea moderadora o dueña del canal."
            : "";
        return new InvalidOperationException(
            context + " (" + detail + "). Revisá el scope " + requiredScope +
            ", la autorización y los límites/permisos de la API." + permissionHint);
    }
}
