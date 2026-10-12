using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>OAuth authorization-code flow with PKCE for a Twitch public desktop client.</summary>
public static class TwitchOAuth
{
    public const string RedirectUri = "http://localhost:17563/callback/";

    public static async Task<string> AuthorizeAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Ingresá el Client ID de tu aplicación Twitch.", nameof(clientId));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        using var listener = new TcpListener(IPAddress.Loopback, 17563);
        listener.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var url = "https://id.twitch.tv/oauth2/authorize" +
            "?response_type=code&client_id=" + Uri.EscapeDataString(clientId.Trim()) +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString("chat:read chat:edit moderator:read:followers moderator:manage:banned_users channel:read:subscriptions channel:read:redemptions") +
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
            if (requestParts.Length < 2) throw new InvalidOperationException("La solicitud de callback está mal formada.");
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false))) { }
            var callback = new Uri("http://localhost:17563" + requestParts[1]);
            if (!callback.AbsolutePath.Equals("/callback/", StringComparison.Ordinal))
                throw new InvalidOperationException("La ruta de callback OAuth no coincide.");
            var query = ParseQuery(callback.Query);
            var html = "<!doctype html><html><meta charset='utf-8'><title>VTuberBot</title><body style='font:16px sans-serif;background:#101525;color:#eef;padding:3rem'><h2>VTuberBot</h2><p>Podés volver a la aplicación y cerrar esta pestaña.</p></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(html);
            var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + bodyBytes.Length + "\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, timeout.Token).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
            if (query.TryGetValue("error", out var error)) throw new InvalidOperationException("Twitch rechazó la autorización: " + error);
            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code) ||
                !query.TryGetValue("state", out var returnedState) || !string.Equals(state, returnedState, StringComparison.Ordinal))
                throw new InvalidOperationException("La respuesta OAuth no contiene un código válido o el estado no coincide.");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier
            });
            using var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", body, timeout.Token).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Twitch token exchange devolvió HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
                throw new InvalidOperationException("La respuesta de Twitch no incluyó un access token.");
            return token.GetString()!;
        }
        finally { listener.Stop(); }
    }

    private static Dictionary<string,string> ParseQuery(string query)
    {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=',2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+',' '));
            var value = parts.Length>1 ? Uri.UnescapeDataString(parts[1].Replace('+',' ')) : "";
            result[key]=value;
        }
        return result;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
