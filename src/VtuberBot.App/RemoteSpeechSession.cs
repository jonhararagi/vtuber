using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace VtuberBot.App;

/// <summary>
/// Optional cloud TTS adapter: native Gemini Interactions audio plus OpenAI-compatible MP3 TTS.
/// The default preset is a feminine, original anime-inspired voice, not an impersonation of an actor.
/// </summary>
public sealed class RemoteSpeechSession : ILocalSpeechSession
{
    private const int MaximumInputCharacters = 4000;
    private const int MaximumAudioBytes = 12 * 1024 * 1024;
    private readonly UserSecretStore secrets;
    private readonly Func<string> baseUrl;
    private readonly Func<string> model;
    private readonly Func<string> voice;
    private readonly Func<string> instructions;
    private readonly Func<string> sharedKeyBaseUrl;
    private readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private bool disposed;

    public RemoteSpeechSession(UserSecretStore secrets, Func<string> baseUrl, Func<string> model,
        Func<string> voice, Func<string> instructions, Func<string>? sharedKeyBaseUrl = null)
    {
        this.secrets = secrets;
        this.baseUrl = baseUrl;
        this.model = model;
        this.voice = voice;
        this.instructions = instructions;
        this.sharedKeyBaseUrl = sharedKeyBaseUrl ?? (() => "");
    }

    public SpeechPlaybackResult Speak(string text, string requestedVoiceId, string requestedOutputId, int rate, int volume,
        Func<bool> cancellationRequested)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputCharacters)
            throw new InvalidOperationException("El texto para voz remota debe tener entre 1 y 4000 caracteres.");
        var key = secrets.Read("remote-tts-api-key");
        if (string.IsNullOrWhiteSpace(key) && CanShareAiKey(baseUrl(), sharedKeyBaseUrl()))
            key = secrets.Read("remote-ai-api-key");
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("La voz remota está seleccionada, pero falta una clave API del proveedor TTS (puede reutilizar la clave de IA si corresponde).");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var synthesis = SynthesizeAsync(text, key, rate, cancel.Token);
        while (!synthesis.IsCompleted)
        {
            if (cancellationRequested())
            {
                cancel.Cancel();
                try { synthesis.GetAwaiter().GetResult(); } catch { }
                return new(false, true);
            }
            Thread.Sleep(35);
        }
        var audio = synthesis.GetAwaiter().GetResult();
        if (cancellationRequested()) return new(false, true);
        if (audio.Length is <= 0 or > MaximumAudioBytes)
            throw new InvalidOperationException("El proveedor TTS devolvió audio vacío o superior a 12 MB.");

        using var stream = new MemoryStream(audio, writable: false);
        using WaveStream reader = audio.Length >= 4 && audio[0] == (byte)'R' && audio[1] == (byte)'I' &&
            audio[2] == (byte)'F' && audio[3] == (byte)'F'
            ? new WaveFileReader(stream)
            : new Mp3FileReader(stream);
        using var output = new WaveOutEvent();
        output.Volume = Math.Clamp(volume, 0, 100) / 100f;
        Exception? playbackError = null;
        output.PlaybackStopped += (_, eventArgs) => playbackError = eventArgs.Exception;
        output.Init(reader);
        output.Play();
        while (output.PlaybackState == PlaybackState.Playing)
        {
            if (cancellationRequested())
            {
                output.Stop();
                return new(false, true);
            }
            Thread.Sleep(35);
        }
        if (playbackError is not null)
            throw new InvalidOperationException("No se pudo reproducir el audio remoto.", playbackError);
        return new(false, false);
    }

    private async Task<byte[]> SynthesizeAsync(string text, string apiKey, int rate, CancellationToken cancellationToken)
    {
        if (IsGeminiHost(baseUrl()))
            return await SynthesizeGeminiAsync(text, apiKey, rate, cancellationToken).ConfigureAwait(false);
        return await SynthesizeOpenAiCompatibleAsync(text, apiKey, rate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> SynthesizeOpenAiCompatibleAsync(string text, string apiKey, int rate, CancellationToken cancellationToken)
    {
        var endpoint = BuildEndpoint(baseUrl());
        var selectedModel = model().Trim();
        var selectedVoice = voice().Trim();
        if (selectedModel.Length == 0 || selectedModel.Length > 200 || selectedVoice.Length == 0 || selectedVoice.Length > 200)
            throw new InvalidOperationException("Configurá el modelo y la voz remotos en ChatSpeak Studio.");
        var voiceInstructions = instructions().Trim();
        if (voiceInstructions.Length > 1000) voiceInstructions = voiceInstructions[..1000];
        var speed = Math.Clamp(1.0 + Math.Clamp(rate, -10, 10) * 0.025, 0.75, 1.25);
        var payload = new
        {
            model = selectedModel, input = text, voice = selectedVoice, instructions = voiceInstructions,
            response_format = "mp3", speed
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "audio/speech"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"El proveedor de voz respondió HTTP {(int)response.StatusCode}. Revisá la clave, modelo, endpoint y cuota.");
        if (response.Content.Headers.ContentLength is > MaximumAudioBytes)
            throw new InvalidOperationException("El audio generado supera el límite de 12 MB.");
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var count = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (output.Length + count > MaximumAudioBytes)
                throw new InvalidOperationException("El audio generado supera el límite de 12 MB.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        return output.ToArray();
    }

    private async Task<byte[]> SynthesizeGeminiAsync(string text, string apiKey, int rate, CancellationToken cancellationToken)
    {
        var endpoint = BuildGeminiEndpoint(baseUrl());
        var selectedModel = model().Trim();
        var selectedVoice = voice().Trim();
        if (selectedModel.Length == 0 || selectedModel.Length > 200 || selectedVoice.Length == 0 || selectedVoice.Length > 200)
            throw new InvalidOperationException("Configurá el modelo y la voz Gemini en ChatSpeak Studio.");
        var style = instructions().Trim();
        if (style.Length > 1000) style = style[..1000];
        var speed = Math.Clamp(1.0 + Math.Clamp(rate, -10, 10) * 0.025, 0.75, 1.25);
        style += speed < 0.95 ? " Usa un ritmo pausado." : speed > 1.05 ? " Usa un ritmo ágil y vivaz." : " Usa un ritmo de conversación natural.";
        var payload = new
        {
            model = selectedModel,
            input = new[]
            {
                new
                {
                    type = "user_input",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text,
                            annotations = new[]
                            {
                                new { type = "speech_metadata", style }
                            }
                        }
                    }
                }
            },
            response_format = new { type = "audio" },
            generation_config = new
            {
                speech_config = new[] { new { voice = selectedVoice } }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini TTS respondió HTTP {(int)response.StatusCode}. Revisá la clave, el modelo, el nivel gratuito y la cuota.");
        if (response.Content.Headers.ContentLength is > MaximumAudioBytes)
            throw new InvalidOperationException("El audio generado supera el límite de 12 MB.");
        const int maximumResponseJsonBytes = 20 * 1024 * 1024;
        if (response.Content.Headers.ContentLength is > maximumResponseJsonBytes)
            throw new InvalidOperationException("La respuesta TTS de Gemini supera el límite de 20 MB.");
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var jsonBuffer = new MemoryStream();
        var jsonChunk = new byte[81920];
        while (true)
        {
            var read = await body.ReadAsync(jsonChunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (jsonBuffer.Length + read > maximumResponseJsonBytes)
                throw new InvalidOperationException("La respuesta TTS de Gemini supera el límite de 20 MB.");
            await jsonBuffer.WriteAsync(jsonChunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        var json = Encoding.UTF8.GetString(jsonBuffer.ToArray());
        var audio = ExtractGeminiAudio(json);
        if (audio.Length > MaximumAudioBytes)
            throw new InvalidOperationException("El audio generado supera el límite de 12 MB.");
        return audio;
    }

    public static byte[] ExtractGeminiAudio(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in steps.EnumerateArray().Reverse())
            {
                if (!step.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in content.EnumerateArray().Reverse())
                {
                    if (item.TryGetProperty("type", out var type) &&
                        string.Equals(type.GetString(), "audio", StringComparison.OrdinalIgnoreCase) &&
                        item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
                        return Convert.FromBase64String(data.GetString() ?? "");
                }
            }
        }
        if (root.TryGetProperty("output_audio", out var outputAudio) &&
            outputAudio.ValueKind == JsonValueKind.Object &&
            outputAudio.TryGetProperty("data", out var outputData) && outputData.ValueKind == JsonValueKind.String)
            return Convert.FromBase64String(outputData.GetString() ?? "");
        throw new InvalidDataException("La respuesta de Gemini no contiene un bloque de audio reconocido.");
    }

    public static Uri BuildGeminiEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new UriFormatException("Gemini TTS requiere la URL HTTPS oficial de Google AI sin credenciales ni query.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length == 0) path = "/v1beta";
        if (!path.Equals("/v1beta", StringComparison.OrdinalIgnoreCase))
            throw new UriFormatException("Gemini TTS debe usar la ruta base /v1beta.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/v1beta/interactions");
    }

    private static bool IsGeminiHost(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase);

    private static bool CanShareAiKey(string ttsBaseUrl, string aiBaseUrl)
    {
        if (!Uri.TryCreate(ttsBaseUrl, UriKind.Absolute, out var tts) ||
            !Uri.TryCreate(aiBaseUrl, UriKind.Absolute, out var ai) ||
            tts.Scheme != Uri.UriSchemeHttps || ai.Scheme != Uri.UriSchemeHttps)
            return false;
        if (string.Equals(tts.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            return string.Equals(tts.Host, ai.Host, StringComparison.OrdinalIgnoreCase);
        try
        {
            return string.Equals(BuildEndpoint(ttsBaseUrl).AbsoluteUri, BuildEndpoint(aiBaseUrl).AbsoluteUri,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException) { return false; }
    }

    public static Uri BuildEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new UriFormatException("El TTS remoto exige una URL HTTPS válida, sin credenciales ni query.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            uri = new Uri(uri.GetLeftPart(UriPartial.Authority) + path + "/v1");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        http.Dispose();
    }
}

/// <summary>Switches between existing Windows SAPI and remote speech without altering the queue semantics.</summary>
public sealed class ConfigurableSpeechSession(Func<bool> useRemote, Func<ILocalSpeechSession> localFactory,
    Func<ILocalSpeechSession> remoteFactory) : ILocalSpeechSession
{
    private ILocalSpeechSession? local;
    private ILocalSpeechSession? remote;
    private bool disposed;

    public SpeechPlaybackResult Speak(string text, string requestedVoiceId, string requestedOutputId, int rate, int volume,
        Func<bool> cancellationRequested)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (useRemote())
        {
            remote ??= remoteFactory();
            return remote.Speak(text, requestedVoiceId, requestedOutputId, rate, volume, cancellationRequested);
        }
        local ??= localFactory();
        return local.Speak(text, requestedVoiceId, requestedOutputId, rate, volume, cancellationRequested);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        local?.Dispose();
        remote?.Dispose();
    }
}
