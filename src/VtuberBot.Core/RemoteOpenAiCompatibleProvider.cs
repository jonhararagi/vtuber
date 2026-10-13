using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VtuberBot.Core;

/// <summary>Explicit opt-in HTTPS adapter for cloud OpenAI-compatible chat APIs.</summary>
public sealed class RemoteOpenAiCompatibleProvider : IChatIntelligenceProvider, IDisposable
{
    private readonly HttpClient http;
    private readonly Func<string> getApiKey;
    private readonly bool ownsClient;

    public RemoteOpenAiCompatibleProvider(Func<string> getApiKey, HttpClient? httpClient = null)
    {
        this.getApiKey = getApiKey ?? throw new ArgumentNullException(nameof(getApiKey));
        http = httpClient ?? new HttpClient();
        ownsClient = httpClient is null;
    }

    public async Task<ChatIntelligenceResult> CheckAsync(ChatIntelligenceOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(getApiKey()))
            return new(false, "", "ApiKeyMissing", "Guardá la clave API en ChatSpeak Studio; no se guarda en SQLite.");
        try
        {
            var baseUri = Validate(options.BaseUrl);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120)));
            using var request = AuthorizedRequest(HttpMethod.Get, new Uri(baseUri, "models"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new(false, "", "Rejected", $"El proveedor remoto respondió HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new(false, "", "InvalidResponse", "El endpoint remoto /models no devolvió una lista compatible.");
            var model = options.Model.Trim();
            if (model.Length == 0)
                return new(false, "", "ModelMissing", "Configurá el ID exacto del modelo remoto.");
            if (!data.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("id", out var id) && string.Equals(id.GetString(), model, StringComparison.Ordinal)))
                return new(false, "", "ModelMissing", "El modelo configurado no aparece en /models del proveedor.");
            return new(true, "", "Available");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, "", "Timeout", "La comprobación remota agotó el tiempo."); }
        catch (OperationCanceledException) { throw; }
        catch (JsonException) { return new(false, "", "InvalidResponse", "El proveedor remoto devolvió JSON inválido."); }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException or InvalidOperationException)
        { return new(false, "", "Unavailable", ex.Message); }
    }

    public async Task<ChatIntelligenceResult> GenerateAsync(ChatIntelligenceOptions options,
        IReadOnlyList<ChatConversationMessage> messages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(getApiKey()))
            return new(false, "", "ApiKeyMissing", "Guardá la clave API remota antes de generar respuestas.");
        try
        {
            var baseUri = Validate(options.BaseUrl);
            if (messages is null || messages.Count is < 1 or > 16)
                return new(false, "", "InvalidInput", "El contexto debe contener entre 1 y 16 mensajes.");
            if (messages.Any(m => m.Role is not ("system" or "user" or "assistant") || string.IsNullOrWhiteSpace(m.Content)))
                return new(false, "", "InvalidInput", "El contexto contiene un rol o mensaje inválido.");
            if (messages.Sum(m => (m.Content ?? "").Length) > Math.Clamp(options.MaxInputCharacters, 100, 20000))
                return new(false, "", "InputTooLong", "El contexto supera el límite configurado.");
            if (string.IsNullOrWhiteSpace(options.Model) || options.Model.Length > 200)
                return new(false, "", "ModelMissing", "Configurá el ID del modelo remoto.");
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
            using var request = AuthorizedRequest(HttpMethod.Post, new Uri(baseUri, "chat/completions"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new(false, "", "Rejected", $"El proveedor remoto respondió HTTP {(int)response.StatusCode}. Revisá modelo, clave y cuota.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return new(false, "", "InvalidResponse", "La respuesta remota no contiene choices.");
            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                return new(false, "", "InvalidResponse", "La respuesta remota no contiene texto.");
            var text = content.GetString()?.Trim() ?? "";
            if (text.Length == 0) return new(false, "", "EmptyResponse", "El modelo remoto devolvió una respuesta vacía.");
            return new(true, text[..Math.Min(text.Length, 4000)], "Generated");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, "", "Timeout", "La generación remota agotó el tiempo configurado."); }
        catch (OperationCanceledException) { throw; }
        catch (JsonException) { return new(false, "", "InvalidResponse", "El proveedor remoto devolvió JSON inválido."); }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException or InvalidOperationException)
        { return new(false, "", "Unavailable", ex.Message); }
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, Uri uri)
    {
        var key = getApiKey()?.Trim() ?? "";
        if (key.Length == 0) throw new InvalidOperationException("Falta la clave API remota.");
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public static Uri Validate(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new UriFormatException("El proveedor remoto debe usar una URL HTTPS válida, sin credenciales ni query.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
            uri = new Uri(uri.GetLeftPart(UriPartial.Authority) + path + "/v1");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public void Dispose() { if (ownsClient) http.Dispose(); }
}

/// <summary>Routes calls at runtime; local inference remains strictly loopback-only.</summary>
public sealed class ConfiguredChatIntelligenceProvider(
    IChatIntelligenceProvider local, IChatIntelligenceProvider remote, Func<bool> useRemote) : IChatIntelligenceProvider
{
    private IChatIntelligenceProvider Current => useRemote() ? remote : local;
    public Task<ChatIntelligenceResult> CheckAsync(ChatIntelligenceOptions options, CancellationToken cancellationToken = default) =>
        Current.CheckAsync(options, cancellationToken);
    public Task<ChatIntelligenceResult> GenerateAsync(ChatIntelligenceOptions options, IReadOnlyList<ChatConversationMessage> messages,
        CancellationToken cancellationToken = default) => Current.GenerateAsync(options, messages, cancellationToken);
}
