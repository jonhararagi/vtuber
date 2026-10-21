using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using NAudio.Wave;

namespace VtuberBot.App;

/// <summary>
/// Offline Japanese TTS adapter for a locally running VOICEVOX Engine.
/// It talks only to loopback addresses and never sends text to a cloud service.
/// </summary>
public sealed class VoicevoxSpeechSession : ILocalSpeechSession
{
    public const string VoiceIdPrefix = "voicevox:";
    public const string DefaultBaseUrl = "http://127.0.0.1:50021";
    private const int MaximumInputCharacters = 2000;
    private const int MaximumAudioBytes = 12 * 1024 * 1024;
    private readonly Func<string> baseUrl;
    private readonly Func<double> pitchScale;
    private readonly Func<double> intonationScale;
    private readonly Func<int, double>? speakerPitchScale;
    private readonly Func<int, double>? speakerIntonationScale;
    private readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private bool disposed;

    public VoicevoxSpeechSession(
        Func<string>? baseUrl = null,
        Func<double>? pitchScale = null,
        Func<double>? intonationScale = null,
        Func<int, double>? speakerPitchScale = null,
        Func<int, double>? speakerIntonationScale = null)
    {
        this.baseUrl = baseUrl ?? (() => DefaultBaseUrl);
        this.pitchScale = pitchScale ?? (() => 0d);
        this.intonationScale = intonationScale ?? (() => 1d);
        this.speakerPitchScale = speakerPitchScale;
        this.speakerIntonationScale = speakerIntonationScale;
    }

    public static double NormalizePitchScale(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, -0.3, 0.3) : 0d;

    public static double NormalizeIntonationScale(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.5, 1.5) : 1d;

    public static bool IsVoiceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(VoiceIdPrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return int.TryParse(id[VoiceIdPrefix.Length..], out var speakerId) && speakerId >= 0;
    }

    public static IReadOnlyList<LocalSpeechVoice> GetInstalledVoices(string? configuredBaseUrl = null)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var endpoint = BuildEndpoint(configuredBaseUrl ?? DefaultBaseUrl, "speakers");
        using var response = client.GetAsync(endpoint).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(response.Content.ReadAsStream());
        var voices = new List<LocalSpeechVoice>();
        foreach (var speaker in document.RootElement.EnumerateArray())
        {
            var speakerName = speaker.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "VOICEVOX" : "VOICEVOX";
            if (!speaker.TryGetProperty("styles", out var styles) || styles.ValueKind != JsonValueKind.Array) continue;
            foreach (var style in styles.EnumerateArray())
            {
                if (!style.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id)) continue;
                var styleName = style.TryGetProperty("name", out var styleElement) ? styleElement.GetString() ?? "Normal" : "Normal";
                voices.Add(new LocalSpeechVoice(VoiceIdPrefix + id, $"{speakerName} — {styleName} (VOICEVOX)"));
            }
        }
        return voices.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>Lists the playback devices exposed by NAudio/WinMM for VOICEVOX WAV output.</summary>
    public static IReadOnlyList<LocalSpeechOutput> GetInstalledOutputs()
    {
        var outputs = new List<LocalSpeechOutput>();
        var count = WaveOut.DeviceCount;
        for (var index = 0; index < count; index++)
        {
            var capabilities = WaveOut.GetCapabilities(index);
            var name = string.IsNullOrWhiteSpace(capabilities.ProductName)
                ? "Dispositivo de audio " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : capabilities.ProductName;
            outputs.Add(new LocalSpeechOutput("waveout:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), name));
        }
        return outputs;
    }

    /// <summary>
    /// Resolves a saved NAudio output ID without requiring audio hardware in unit tests.
    /// Empty or legacy SAPI IDs mean the system-default output; stale explicit waveout IDs fail clearly.
    /// </summary>
    public static int ResolveOutputDeviceNumber(string? outputId, int availableDeviceCount)
    {
        if (availableDeviceCount < 0)
            throw new ArgumentOutOfRangeException(nameof(availableDeviceCount));
        if (string.IsNullOrWhiteSpace(outputId) ||
            !outputId.StartsWith("waveout:", StringComparison.OrdinalIgnoreCase))
            return -1;

        var suffix = outputId["waveout:".Length..];
        if (!int.TryParse(suffix, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var deviceNumber) ||
            deviceNumber < 0)
            throw new InvalidOperationException("El dispositivo de salida VOICEVOX guardado no es válido. Actualizá la lista de dispositivos.");

        if (deviceNumber >= availableDeviceCount)
            throw new InvalidOperationException("El dispositivo de salida VOICEVOX guardado ya no está disponible. Actualizá la lista y elegí otro.");

        return deviceNumber;
    }

    public SpeechPlaybackResult Speak(string text, string requestedVoiceId, string requestedOutputId,
        int rate, int volume, Func<bool> cancellationRequested)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputCharacters)
            throw new InvalidOperationException("VOICEVOX admite pruebas y reacciones de hasta 2000 caracteres.");
        if (!IsVoiceId(requestedVoiceId) || !int.TryParse(requestedVoiceId[VoiceIdPrefix.Length..], out var speakerId))
            throw new InvalidOperationException("Seleccioná una voz de VOICEVOX válida en la regla de reacción.");

        // Generate the complete waveform before playback so queue order stays predictable.
        // Never hold the user waiting indefinitely on a local Engine that is loading a model.
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var synthesis = SynthesizeAsync(text, speakerId, rate, cancel.Token);
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
        byte[] audio;
        try
        {
            audio = synthesis.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException exception) when (cancel.IsCancellationRequested && !cancellationRequested())
        {
            throw new TimeoutException("VOICEVOX tardó más de 60 segundos en procesar la frase. Probá un texto más corto o reducí los hilos/carga del equipo.", exception);
        }
        if (cancellationRequested()) return new(false, true);
        if (audio.Length is <= 0 or > MaximumAudioBytes)
            throw new InvalidOperationException("VOICEVOX devolvió audio vacío o superior a 12 MB.");

        using var stream = new MemoryStream(audio, writable: false);
        using var reader = new WaveFileReader(stream);
        var deviceNumber = ResolveOutputDeviceNumber(requestedOutputId, WaveOut.DeviceCount);
        using var output = new WaveOutEvent { DeviceNumber = deviceNumber };
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
            throw new InvalidOperationException("No se pudo reproducir el audio de VOICEVOX.", playbackError);
        return new(false, false);
    }

    private async Task<byte[]> SynthesizeAsync(string text, int speakerId, int rate, CancellationToken cancellationToken)
    {
        var queryEndpoint = BuildEndpoint(baseUrl(), $"audio_query?speaker={speakerId}&text={Uri.EscapeDataString(text)}");
        using var queryResponse = await http.PostAsync(queryEndpoint, content: null, cancellationToken).ConfigureAwait(false);
        queryResponse.EnsureSuccessStatusCode();
        var queryJson = await queryResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var query = JsonNode.Parse(queryJson) ?? throw new InvalidOperationException("VOICEVOX devolvió una consulta de audio vacía.");
        query["speedScale"] = Math.Clamp(1.0 + Math.Clamp(rate, -10, 10) * 0.06, 0.5, 2.0);
        query["pitchScale"] = NormalizePitchScale(speakerPitchScale?.Invoke(speakerId) ?? pitchScale());
        query["intonationScale"] = NormalizeIntonationScale(speakerIntonationScale?.Invoke(speakerId) ?? intonationScale());
        using var request = new StringContent(query.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var synthesisEndpoint = BuildEndpoint(baseUrl(), $"synthesis?speaker={speakerId}");
        using var response = await http.PostAsync(synthesisEndpoint, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (audio.Length > MaximumAudioBytes)
            throw new InvalidOperationException("VOICEVOX devolvió un audio superior a 12 MB.");
        return audio;
    }

    private static Uri BuildEndpoint(string configuredBaseUrl, string relativePath)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
            !(baseUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              (IPAddress.TryParse(baseUri.Host, out var address) && IPAddress.IsLoopback(address))))
            throw new InvalidOperationException("VOICEVOX debe apuntar a localhost o una dirección IP de loopback; no se permiten destinos remotos.");
        return new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/" + relativePath.TrimStart('/'));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        http.Dispose();
    }
}
