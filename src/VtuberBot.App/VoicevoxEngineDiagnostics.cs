using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VtuberBot.App;

public sealed record VoicevoxEngineProbeResult(
    string Version,
    int AvailableVoiceCount,
    string SelectedVoiceName,
    int SynthesizedAudioBytes);

/// <summary>
/// Verifies the local VOICEVOX API independently of the Windows audio device.
/// A successful probe confirms API + voice style + WAV synthesis; it does not claim that
/// the WAV has been heard through speakers. That separate check is the in-app "Probar voz".
/// </summary>
public static class VoicevoxEngineDiagnostics
{
    private const int MaximumInputCharacters = 2000;
    private const int MaximumApiJsonBytes = 2 * 1024 * 1024;
    private const int MaximumAudioBytes = 12 * 1024 * 1024;

    public static async Task<VoicevoxEngineProbeResult> ProbeAsync(
        string configuredBaseUrl,
        string voiceId,
        string text,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (!VoicevoxSpeechSession.IsVoiceId(voiceId) ||
            !int.TryParse(voiceId[VoicevoxSpeechSession.VoiceIdPrefix.Length..], out var speakerId) ||
            speakerId < 0)
            throw new InvalidOperationException("Seleccioná una voz VOICEVOX válida antes de comprobar la síntesis.");

        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputCharacters)
            throw new InvalidOperationException("El diagnóstico requiere una frase de entre 1 y 2000 caracteres.");

        var versionEndpoint = BuildEndpoint(configuredBaseUrl, "version");
        var speakersEndpoint = BuildEndpoint(configuredBaseUrl, "speakers");
        var queryEndpoint = BuildEndpoint(configuredBaseUrl,
            "audio_query?speaker=" + speakerId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "&text=" + Uri.EscapeDataString(text));
        var synthesisEndpoint = BuildEndpoint(configuredBaseUrl,
            "synthesis?speaker=" + speakerId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var versionResponse = await SendWithStreamingResponseAsync(httpClient, HttpMethod.Get, versionEndpoint, null, timeout.Token).ConfigureAwait(false);
            await EnsureSuccessAsync(versionResponse, "VOICEVOX no respondió correctamente a /version.").ConfigureAwait(false);
            var versionBody = await ReadLimitedBytesAsync(versionResponse, 512, timeout.Token).ConfigureAwait(false);
            var version = Encoding.UTF8.GetString(versionBody).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(version) || version.Length > 100 || version.Any(char.IsControl))
                throw new InvalidOperationException("VOICEVOX devolvió una versión vacía o inválida.");

            using var speakersResponse = await SendWithStreamingResponseAsync(httpClient, HttpMethod.Get, speakersEndpoint, null, timeout.Token).ConfigureAwait(false);
            await EnsureSuccessAsync(speakersResponse, "VOICEVOX no pudo enumerar /speakers.").ConfigureAwait(false);
            var speakerBytes = await ReadLimitedBytesAsync(speakersResponse, MaximumApiJsonBytes, timeout.Token).ConfigureAwait(false);
            using var speakersDocument = JsonDocument.Parse(speakerBytes);
            if (speakersDocument.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("VOICEVOX devolvió un catálogo de voces con formato inesperado.");

            var voices = new List<LocalSpeechVoice>();
            foreach (var speaker in speakersDocument.RootElement.EnumerateArray())
            {
                var speakerName = ReadString(speaker, "name") ?? "VOICEVOX";
                if (!speaker.TryGetProperty("styles", out var styles) || styles.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var style in styles.EnumerateArray())
                {
                    if (!style.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id) || id < 0)
                        continue;
                    var styleName = ReadString(style, "name") ?? "Normal";
                    voices.Add(new LocalSpeechVoice(
                        VoicevoxSpeechSession.VoiceIdPrefix + id,
                        speakerName + " — " + styleName + " (VOICEVOX)"));
                }
            }

            var selectedVoice = voices.FirstOrDefault(voice =>
                string.Equals(voice.Id, voiceId, StringComparison.OrdinalIgnoreCase));
            if (selectedVoice is null)
                throw new InvalidOperationException(
                    "La voz seleccionada (" + voiceId + ") no aparece en /speakers. Actualizá la lista y elegí un estilo instalado.");

            using var queryResponse = await SendWithStreamingResponseAsync(httpClient, HttpMethod.Post, queryEndpoint, null, timeout.Token).ConfigureAwait(false);
            await EnsureSuccessAsync(queryResponse, "VOICEVOX rechazó /audio_query para la voz seleccionada.").ConfigureAwait(false);
            var queryBytes = await ReadLimitedBytesAsync(queryResponse, MaximumApiJsonBytes, timeout.Token).ConfigureAwait(false);
            var query = JsonNode.Parse(queryBytes) ??
                throw new InvalidOperationException("VOICEVOX devolvió una consulta de audio vacía.");

            using var queryContent = new StringContent(query.ToJsonString(), Encoding.UTF8, "application/json");
            using var synthesisResponse = await SendWithStreamingResponseAsync(
                httpClient, HttpMethod.Post, synthesisEndpoint, queryContent, timeout.Token).ConfigureAwait(false);
            await EnsureSuccessAsync(synthesisResponse, "VOICEVOX rechazó /synthesis para la voz seleccionada.").ConfigureAwait(false);
            var audio = await ReadLimitedBytesAsync(synthesisResponse, MaximumAudioBytes, timeout.Token).ConfigureAwait(false);
            if (!IsWaveFile(audio))
                throw new InvalidOperationException("VOICEVOX no devolvió un archivo WAV RIFF válido. No se probó la salida de audio.");

            return new VoicevoxEngineProbeResult(version, voices.Count, selectedVoice.Name, audio.Length);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException("El diagnóstico VOICEVOX superó 15 segundos. Revisá la carga del Engine, el modelo de voz y el uso de CPU.");
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    public static Uri BuildEndpoint(string configuredBaseUrl, string relativePath)
    {
        if (!Uri.TryCreate((configuredBaseUrl ?? string.Empty).Trim(), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            !(baseUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              (IPAddress.TryParse(baseUri.Host, out var address) && IPAddress.IsLoopback(address))))
            throw new InvalidOperationException("VOICEVOX debe apuntar a localhost o una IP loopback; no se permiten destinos remotos.");

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith('/') || relativePath.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("La ruta de diagnóstico VOICEVOX no es válida.", nameof(relativePath));

        return new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/" + relativePath);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response, string message)
    {
        if (response.IsSuccessStatusCode)
            return Task.CompletedTask;

        throw new InvalidOperationException(message + " HTTP " +
            ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private static async Task<HttpResponseMessage> SendWithStreamingResponseAsync(
        HttpClient client,
        HttpMethod method,
        Uri endpoint,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint) { Content = content };
        return await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadLimitedBytesAsync(
        HttpResponseMessage response, int maximumBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > maximumBytes)
            throw new InvalidOperationException("VOICEVOX devolvió una respuesta superior al límite de " + maximumBytes + " bytes.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(Math.Min(maximumBytes, 16 * 1024));
        var chunk = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            if (buffer.Length + read > maximumBytes)
                throw new InvalidOperationException("VOICEVOX devolvió una respuesta superior al límite de " + maximumBytes + " bytes.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsWaveFile(byte[] bytes)
    {
        if (bytes.Length is < 44 or > MaximumAudioBytes ||
            !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            return false;

        var declaredRiffLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
        if (declaredRiffLength < 36 || declaredRiffLength > bytes.Length - 8)
            return false;

        var offset = 12;
        var foundFormat = false;
        var foundNonEmptyData = false;
        while (offset + 8 <= bytes.Length)
        {
            var chunkName = bytes.AsSpan(offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            var chunkDataOffset = offset + 8;
            if (chunkSize > int.MaxValue || chunkDataOffset + (long)chunkSize > bytes.Length)
                return false;

            if (chunkName.SequenceEqual("fmt "u8))
                foundFormat = chunkSize >= 16;
            else if (chunkName.SequenceEqual("data"u8))
                foundNonEmptyData = chunkSize > 0;

            var paddedChunkSize = (long)chunkSize + (chunkSize & 1u);
            var next = chunkDataOffset + paddedChunkSize;
            if (next > int.MaxValue || next <= offset)
                return false;
            offset = (int)next;
        }

        return foundFormat && foundNonEmptyData;
    }
}
