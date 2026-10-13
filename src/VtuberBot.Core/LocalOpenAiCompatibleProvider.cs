using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VtuberBot.Core;

public sealed record ChatConversationMessage(string Role, string Content);
public sealed record ChatIntelligenceOptions(
    string BaseUrl = "http://127.0.0.1:8080/v1",
    string Model = "",
    int TimeoutSeconds = 30,
    int MaxInputCharacters = 4000,
    int MaxResponseTokens = 180,
    double Temperature = 0.7);
public sealed record ChatIntelligenceResult(bool Success, string Text, string Status, string? Error = null);

public interface IChatIntelligenceProvider
{
    Task<ChatIntelligenceResult> CheckAsync(ChatIntelligenceOptions options, CancellationToken cancellationToken = default);
    Task<ChatIntelligenceResult> GenerateAsync(
        ChatIntelligenceOptions options,
        IReadOnlyList<ChatConversationMessage> messages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Local-only OpenAI-compatible chat-completions client. It refuses non-loopback hosts so chat
/// messages are never sent to a remote inference provider by this implementation.
/// </summary>
public sealed class LocalOpenAiCompatibleProvider : IChatIntelligenceProvider, IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsClient;

    public LocalOpenAiCompatibleProvider(HttpClient? httpClient = null)
    {
        http = httpClient ?? new HttpClient();
        ownsClient = httpClient is null;
    }

    public async Task<ChatIntelligenceResult> CheckAsync(
        ChatIntelligenceOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUri = Validate(options);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120)));
            using var response = await http.GetAsync(new Uri(baseUri, "models"), timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new(false, "", "Unavailable", $"El servidor local respondió HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new(false, "", "InvalidResponse", "El endpoint /models no devolvió la lista esperada.");
            var configuredModel = options.Model.Trim();
            if (configuredModel.Length > 0 && !data.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("id", out var id) &&
                string.Equals(id.GetString(), configuredModel, StringComparison.Ordinal)))
                return new(false, "", "ModelMissing", "El modelo configurado no aparece en /v1/models.");
            return new(true, "", "Available");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "", "Timeout", "El servidor local agotó el tiempo de comprobación.");
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException)
        {
            return new(false, "", "InvalidResponse", "El servidor devolvió JSON inválido.");
        }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException or InvalidOperationException)
        {
            return new(false, "", "Unavailable", ex.Message);
        }
    }

    public async Task<ChatIntelligenceResult> GenerateAsync(
        ChatIntelligenceOptions options,
        IReadOnlyList<ChatConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUri = Validate(options);
            if (messages is null || messages.Count is < 1 or > 16)
                return new(false, "", "InvalidInput", "El contexto debe contener entre 1 y 16 mensajes.");
            var total = messages.Sum(m => (m.Content ?? "").Length);
            if (total > Math.Clamp(options.MaxInputCharacters, 100, 20000))
                return new(false, "", "InputTooLong", "El contexto supera el límite configurado.");
            if (messages.Any(m => m.Role is not ("system" or "user" or "assistant") || string.IsNullOrWhiteSpace(m.Content)))
                return new(false, "", "InvalidInput", "El contexto contiene un rol o mensaje inválido.");
            if (string.IsNullOrWhiteSpace(options.Model) || options.Model.Length > 200)
                return new(false, "", "ModelMissing", "Configurá el identificador del modelo local.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120)));
            var payload = new
            {
                model = options.Model.Trim(),
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                temperature = Math.Clamp(options.Temperature, 0, 2),
                max_tokens = Math.Clamp(options.MaxResponseTokens, 16, 1024),
                stream = false
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "chat/completions"))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new(false, "", "Rejected", $"El servidor local respondió HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return new(false, "", "InvalidResponse", "La respuesta no contiene choices.");
            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                return new(false, "", "InvalidResponse", "La respuesta no contiene texto.");
            var text = content.GetString()?.Trim() ?? "";
            if (text.Length == 0)
                return new(false, "", "EmptyResponse", "El modelo devolvió una respuesta vacía.");
            return new(true, text[..Math.Min(text.Length, 2000)], "Generated");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "", "Timeout", "La generación local agotó el tiempo configurado.");
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException)
        {
            return new(false, "", "InvalidResponse", "El servidor devolvió JSON inválido.");
        }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException or InvalidOperationException)
        {
            return new(false, "", "Unavailable", ex.Message);
        }
    }

    private static Uri Validate(ChatIntelligenceOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new UriFormatException("La URL del proveedor local no es válida.");
        var loopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                       IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip);
        if (!loopback)
            throw new InvalidOperationException("Por privacidad, el proveedor de IA solo puede apuntar a localhost o una IP loopback.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            uri = new Uri(uri.GetLeftPart(UriPartial.Authority) + path + "/v1");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public void Dispose()
    {
        if (ownsClient) http.Dispose();
    }
}
