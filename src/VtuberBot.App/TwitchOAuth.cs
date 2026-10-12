using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>Credentials retained locally by the desktop app; the containing file is DPAPI protected.</summary>
public sealed record TwitchOAuthCredentials(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string ClientId,
    string[] Scopes);

/// <summary>OAuth authorization-code flow with PKCE plus validation/refresh for long-running Twitch sessions.</summary>
public static class TwitchOAuth
{
    public const string RedirectUri = "http://localhost:17563/callback/";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<string> AuthorizeAsync(string clientId, CancellationToken cancellationToken = default) =>
        (await AuthorizeCredentialsAsync(clientId, cancellationToken).ConfigureAwait(false)).AccessToken;

    public static async Task<TwitchOAuthCredentials> AuthorizeCredentialsAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ValidateClientId(clientId);
        clientId = clientId.Trim();

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        using var listener = new TcpListener(IPAddress.Loopback, 17563);
        listener.Start();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var url = "https://id.twitch.tv/oauth2/authorize" +
            "?response_type=code&client_id=" + Uri.EscapeDataString(clientId) +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString(
                "chat:read chat:edit moderator:read:followers moderator:manage:banned_users " +
                "moderator:manage:chat_messages channel:read:subscriptions channel:read:redemptions") +
            "&state=" + Uri.EscapeDataString(state) +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var requestLine = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine) || !requestLine.StartsWith("GET ", StringComparison.Ordinal))
                throw new InvalidOperationException("El callback OAuth no recibió una solicitud GET válida.");

            var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2)
                throw new InvalidOperationException("La solicitud de callback está mal formada.");
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false))) { }

            var callback = new Uri("http://localhost:17563" + requestParts[1]);
            if (!callback.AbsolutePath.Equals("/callback/", StringComparison.Ordinal))
                throw new InvalidOperationException("La ruta de callback OAuth no coincide.");
            var query = ParseQuery(callback.Query);

            var html = "<!doctype html><html><meta charset='utf-8'><title>VTuberBot</title>" +
                "<body style='font:16px sans-serif;background:#101525;color:#eef;padding:3rem'>" +
                "<h2>VTuberBot</h2><p>Podés volver a la aplicación y cerrar esta pestaña.</p></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(html);
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " +
                bodyBytes.Length + "\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, timeout.Token).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

            if (query.TryGetValue("error", out var error))
                throw new InvalidOperationException("Twitch rechazó la autorización: " + error);
            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code) ||
                !query.TryGetValue("state", out var returnedState) ||
                !string.Equals(state, returnedState, StringComparison.Ordinal))
                throw new InvalidOperationException("La respuesta OAuth no contiene un código válido o el estado no coincide.");

            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier
            });
            using var response = await Http.PostAsync("https://id.twitch.tv/oauth2/token", body, timeout.Token).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw CreateOAuthError(response.StatusCode, json, "No se pudo completar el intercambio del código OAuth.");
            return ParseCredentials(json, clientId, refreshFallback: null);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Validates the saved access token, updates its expiry, and refreshes it before expiration
    /// when a refresh token is available. Refresh tokens returned by Twitch replace the old one.
    /// </summary>
    public static async Task<TwitchOAuthCredentials> ValidateOrRefreshAsync(
        string clientId,
        TwitchOAuthCredentials credentials,
        TimeSpan? refreshBefore = null,
        CancellationToken cancellationToken = default)
    {
        ValidateClientId(clientId);
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
            throw new InvalidOperationException("No hay un access token de Twitch guardado.");
        clientId = clientId.Trim();
        var threshold = refreshBefore ?? TimeSpan.FromMinutes(10);

        int expiresIn;
        try
        {
            expiresIn = await ValidateAccessTokenAsync(clientId, credentials.AccessToken, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOAuthTokenException)
        {
            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
                throw new InvalidOperationException("El token de Twitch expiró o fue revocado y no hay refresh token. Volvé a autorizar la cuenta.");
            var refreshed = await RefreshCredentialsAsync(clientId, credentials.RefreshToken, credentials, cancellationToken).ConfigureAwait(false);
            var refreshedExpiry = await ValidateAccessTokenAsync(clientId, refreshed.AccessToken, cancellationToken).ConfigureAwait(false);
            return refreshed with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(refreshedExpiry), ClientId = clientId };
        }

        var updated = credentials with
        {
            ClientId = clientId,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };
        if (expiresIn > threshold.TotalSeconds || string.IsNullOrWhiteSpace(updated.RefreshToken))
            return updated;

        var rotated = await RefreshCredentialsAsync(clientId, updated.RefreshToken, updated, cancellationToken).ConfigureAwait(false);
        var rotatedExpiry = await ValidateAccessTokenAsync(clientId, rotated.AccessToken, cancellationToken).ConfigureAwait(false);
        return rotated with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(rotatedExpiry), ClientId = clientId };
    }

    private static async Task<TwitchOAuthCredentials> RefreshCredentialsAsync(
        string clientId,
        string refreshToken,
        TwitchOAuthCredentials previous,
        CancellationToken cancellationToken)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var response = await Http.PostAsync("https://id.twitch.tv/oauth2/token", body, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateOAuthError(response.StatusCode, json,
                "Twitch no pudo renovar la sesión. Volvé a autorizar la cuenta si el refresh token fue revocado.");
        var refreshed = ParseCredentials(json, clientId, previous.RefreshToken);
        return refreshed with
        {
            ClientId = clientId,
            Scopes = previous.Scopes is null || previous.Scopes.Length == 0
                ? refreshed.Scopes
                : previous.Scopes
        };
    }

    private static async Task<int> ValidateAccessTokenAsync(
        string clientId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOAuthTokenException("Twitch rechazó el access token.");
        if (!response.IsSuccessStatusCode)
            throw CreateOAuthError(response.StatusCode, json, "No se pudo validar el token de Twitch.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("client_id", out var returnedClientId) &&
            !string.Equals(returnedClientId.GetString(), clientId, StringComparison.Ordinal))
            throw new InvalidOperationException("El token guardado pertenece a otro Client ID de Twitch.");
        if (!root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out var expiresIn) || expiresIn <= 0)
            throw new InvalidOperationException("Twitch no devolvió un tiempo de expiración válido.");
        return expiresIn;
    }

    private static TwitchOAuthCredentials ParseCredentials(string json, string clientId, string? refreshFallback)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var access) || string.IsNullOrWhiteSpace(access.GetString()))
            throw new InvalidOperationException("La respuesta de Twitch no incluyó un access token.");
        if (!root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out var expiresIn) || expiresIn <= 0)
            throw new InvalidOperationException("La respuesta de Twitch no incluyó una expiración válida.");

        var refresh = root.TryGetProperty("refresh_token", out var refreshValue)
            ? refreshValue.GetString()
            : refreshFallback;
        var scopes = root.TryGetProperty("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.Array
            ? scopeElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
            : [];

        return new TwitchOAuthCredentials(
            access.GetString()!,
            string.IsNullOrWhiteSpace(refresh) ? refreshFallback : refresh,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            clientId,
            scopes);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
            result[key] = value;
        }
        return result;
    }

    private static Exception CreateOAuthError(HttpStatusCode status, string body, string context)
    {
        var message = context + " HTTP " + (int)status + ".";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var detail) &&
                detail.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detail.GetString()))
                message += "\n" + detail.GetString();
        }
        catch (JsonException) { }
        return new InvalidOperationException(message);
    }

    private static void ValidateClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Ingresá el Client ID de tu aplicación Twitch.", nameof(clientId));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class InvalidOAuthTokenException(string message) : Exception(message) { }
}
