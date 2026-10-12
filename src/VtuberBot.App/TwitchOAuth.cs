using System.Net.Http;
using System.Diagnostics;
using System.Net;
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
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var url = "https://id.twitch.tv/oauth2/authorize" +
            "?response_type=code&client_id=" + Uri.EscapeDataString(clientId.Trim()) +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString("chat:read chat:edit") +
            "&state=" + Uri.EscapeDataString(state) +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            var context = await listener.GetContextAsync().WaitAsync(timeout.Token).ConfigureAwait(false);
            var code = context.Request.QueryString["code"];
            var returnedState = context.Request.QueryString["state"];
            var error = context.Request.QueryString["error"];
            var html = "<!doctype html><html><meta charset='utf-8'><title>VTuberBot</title><body style='font:16px sans-serif;background:#101525;color:#eef;padding:3rem'><h2>VTuberBot</h2><p>Podés volver a la aplicación y cerrar esta pestaña.</p></body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            context.Response.Close();
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Twitch rechazó la autorización: " + error);
            if (string.IsNullOrWhiteSpace(code) || !string.Equals(state, returnedState, StringComparison.Ordinal))
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
        finally
        {
            listener.Stop();
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
