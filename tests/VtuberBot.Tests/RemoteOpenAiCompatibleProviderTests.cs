using System.Net;
using System.Text;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class RemoteOpenAiCompatibleProviderTests
{
    [Fact]
    public void GeminiOpenAiCompatibleBasePathIsPreserved()
    {
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/",
            RemoteOpenAiCompatibleProvider.Validate("https://generativelanguage.googleapis.com/v1beta/openai/").AbsoluteUri);
    }

    [Fact]
    public void GeminiTtsUsesInteractionsEndpointAndExtractsWavBytes()
    {
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/interactions",
            VtuberBot.App.RemoteSpeechSession.BuildGeminiEndpoint("https://generativelanguage.googleapis.com/v1beta").AbsoluteUri);
        var audio = new byte[] { 82, 73, 70, 70, 1, 2, 3, 4 };
        var encoded = Convert.ToBase64String(audio);
        var json = "{\"steps\":[{\"type\":\"model_output\",\"content\":[{\"type\":\"audio\",\"data\":\"" + encoded + "\"}]}]}";
        Assert.Equal(audio, VtuberBot.App.RemoteSpeechSession.ExtractGeminiAudio(json));
    }

    [Fact]
    public void GeminiTtsRejectsUntrustedHosts()
    {
        Assert.Throws<UriFormatException>(() =>
            VtuberBot.App.RemoteSpeechSession.BuildGeminiEndpoint("https://example.test/v1beta"));
    }

    [Fact]
    public async Task SendsBearerKeyToConfiguredHttpsEndpoint()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal("Bearer secret-test", request.Headers.Authorization?.ToString());
            Assert.EndsWith("/v1/chat/completions", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json("{\"choices\":[{\"message\":{\"content\":\"Hola desde la nube\"}}]}"));
        });
        using var http = new HttpClient(handler);
        using var provider = new RemoteOpenAiCompatibleProvider(() => "secret-test", http);
        var result = await provider.GenerateAsync(
            new ChatIntelligenceOptions("https://api.example.test/v1", "example-model"),
            [new("system", "Sé amable."), new("user", "Saludá.")], TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("Hola desde la nube", result.Text);
    }

    [Fact]
    public async Task MissingKeyDoesNotSendARequest()
    {
        using var http = new HttpClient(new FakeHandler((_, _) => throw new Xunit.Sdk.XunitException("No debe haber una llamada sin clave.")));
        using var provider = new RemoteOpenAiCompatibleProvider(() => "", http);
        var result = await provider.GenerateAsync(
            new ChatIntelligenceOptions("https://api.example.test/v1", "model"),
            [new("user", "hola")], TestContext.Current.CancellationToken);
        Assert.Equal("ApiKeyMissing", result.Status);
    }

    [Fact]
    public async Task RejectsNonHttpsProviderWithoutNetworkAccess()
    {
        using var http = new HttpClient(new FakeHandler((_, _) => throw new Xunit.Sdk.XunitException("No debe enviarse tráfico HTTP.")));
        using var provider = new RemoteOpenAiCompatibleProvider(() => "secret", http);
        var result = await provider.GenerateAsync(
            new ChatIntelligenceOptions("http://api.example.test/v1", "model"),
            [new("user", "hola")], TestContext.Current.CancellationToken);
        Assert.Equal("Unavailable", result.Status);
    }

    [Fact]
    public void SpeechEndpointRequiresHttpsAndNormalizesV1()
    {
        Assert.Equal("https://tts.example.test/v1/", VtuberBot.App.RemoteSpeechSession.BuildEndpoint("https://tts.example.test").AbsoluteUri);
        Assert.Throws<UriFormatException>(() => VtuberBot.App.RemoteSpeechSession.BuildEndpoint("http://tts.example.test/v1"));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
