using System.Net;
using System.Net.Http;
using System.Text;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoicevoxEngineDiagnosticsTests
{
    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public int RequestCount => Volatile.Read(ref requestCount);
        private int requestCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            return await callback(request, cancellationToken);
        }
    }

    [Fact]
    public async Task ProbeVerifiesLocalVersionVoiceCatalogAndValidWaveSynthesis()
    {
        var paths = new List<string>();
        var wave = CreateWaveFixture();
        var handler = new DelegateHandler((request, _) =>
        {
            paths.Add(request.Method.Method + " " + request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath.EndsWith("/version", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0.15.0") });
            if (request.RequestUri.AbsolutePath.EndsWith("/speakers", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"TestSpeaker","styles":[{"name":"Normal","id":3}]}]""", Encoding.UTF8, "application/json")
                });
            if (request.RequestUri.AbsolutePath.EndsWith("/audio_query", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"speedScale":1.0,"pitchScale":0.0,"intonationScale":1.0}""", Encoding.UTF8, "application/json")
                });
            if (request.RequestUri.AbsolutePath.EndsWith("/synthesis", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(wave) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        using var client = new HttpClient(handler);

        var result = await VoicevoxEngineDiagnostics.ProbeAsync(
            "http://127.0.0.1:50021", "voicevox:3", "こんにちは。音声テストです。",
            client, TestContext.Current.CancellationToken);

        Assert.Equal("0.15.0", result.Version);
        Assert.Equal(1, result.AvailableVoiceCount);
        Assert.Contains("TestSpeaker", result.SelectedVoiceName, StringComparison.Ordinal);
        Assert.Equal(wave.Length, result.SynthesizedAudioBytes);
        Assert.Equal(4, handler.RequestCount);
        Assert.Equal(4, paths.Count);
        Assert.Contains("GET /version", paths);
        Assert.Contains("GET /speakers", paths);
        Assert.Contains("POST /audio_query", paths);
        Assert.Contains("POST /synthesis", paths);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://192.168.1.20:50021")]
    [InlineData("http://127.0.0.1:50021?proxy=1")]
    [InlineData("http://user:pass@127.0.0.1:50021")]
    public async Task ProbeRejectsRemoteOrAmbiguousBaseUrlsBeforeNetworkAccess(string baseUrl)
    {
        var handler = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VoicevoxEngineDiagnostics.ProbeAsync(
                baseUrl, "voicevox:3", "test", client, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ProbeRejectsSelectedVoiceMissingFromInstalledCatalog()
    {
        var handler = new DelegateHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/version", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0.15.0") });
            if (request.RequestUri.AbsolutePath.EndsWith("/speakers", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"TestSpeaker","styles":[{"name":"Normal","id":3}]}]""", Encoding.UTF8, "application/json")
                });
            throw new InvalidOperationException("A diagnostic request was sent before checking the selected voice.");
        });
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VoicevoxEngineDiagnostics.ProbeAsync(
                "http://localhost:50021", "voicevox:4", "test", client, TestContext.Current.CancellationToken));
        Assert.Contains("no aparece en /speakers", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ProbeRejectsSynthesisBytesThatAreNotWaveAudio()
    {
        var handler = new DelegateHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/version", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0.15.0") });
            if (request.RequestUri.AbsolutePath.EndsWith("/speakers", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"TestSpeaker","styles":[{"name":"Normal","id":3}]}]""", Encoding.UTF8, "application/json")
                });
            if (request.RequestUri.AbsolutePath.EndsWith("/audio_query", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"speedScale":1.0,"pitchScale":0.0,"intonationScale":1.0}""", Encoding.UTF8, "application/json")
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("not-a-wave")) });
        });
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VoicevoxEngineDiagnostics.ProbeAsync(
                "http://127.0.0.1:50021", "voicevox:3", "test", client, TestContext.Current.CancellationToken));
        Assert.Contains("WAV RIFF válido", error.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateWaveFixture()
    {
        var bytes = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
        BitConverter.GetBytes(16).CopyTo(bytes, 16);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
        BitConverter.GetBytes(24000).CopyTo(bytes, 24);
        BitConverter.GetBytes(48000).CopyTo(bytes, 28);
        BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        BitConverter.GetBytes(0).CopyTo(bytes, 40);
        return bytes;
    }
}
