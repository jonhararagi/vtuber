using System.Net;
using System.Text;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class LocalOpenAiCompatibleProviderTests
{
    [Fact]
    public async Task GenerateUsesOpenAiCompatibleChatCompletionsProtocol()
    {
        var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/v1/chat/completions", request.RequestUri!.AbsolutePath);
            return Task.FromResult(JsonResponse("{\"choices\":[{\"message\":{\"content\":\"Hola, chat!\"}}]}"));
        });
        using var http = new HttpClient(handler);
        using var provider = new LocalOpenAiCompatibleProvider(http);
        var result = await provider.GenerateAsync(new ChatIntelligenceOptions(Model: "test-model"),
            [new("system", "Sé amable."), new("user", "Saludá.")], TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("Hola, chat!", result.Text);
    }

    [Fact]
    public async Task CheckRequiresConfiguredModelToAppearInLocalModelsList()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse("{\"data\":[{\"id\":\"available\"}]}")));
        using var http = new HttpClient(handler);
        using var provider = new LocalOpenAiCompatibleProvider(http);
        var ok = await provider.CheckAsync(new ChatIntelligenceOptions(Model: "available"), TestContext.Current.CancellationToken);
        var missing = await provider.CheckAsync(new ChatIntelligenceOptions(Model: "missing"), TestContext.Current.CancellationToken);
        Assert.True(ok.Success);
        Assert.Equal("ModelMissing", missing.Status);
    }

    [Fact]
    public async Task RejectsRemoteProviderWithoutSendingHttpRequest()
    {
        var handler = new FakeHandler((_, _) => throw new Xunit.Sdk.XunitException("No debe efectuarse una petición remota."));
        using var http = new HttpClient(handler);
        using var provider = new LocalOpenAiCompatibleProvider(http);
        var result = await provider.GenerateAsync(new ChatIntelligenceOptions(BaseUrl: "https://example.com/v1", Model: "m"),
            [new("user", "hola")], TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("Unavailable", result.Status);
    }

    [Fact]
    public async Task ReportsEmptyAndMalformedResponsesInsteadOfInventingText()
    {
        var emptyHandler = new FakeHandler((_, _) => Task.FromResult(JsonResponse("{\"choices\":[{\"message\":{\"content\":\"  \"}}]}")));
        using var emptyHttp = new HttpClient(emptyHandler);
        using var emptyProvider = new LocalOpenAiCompatibleProvider(emptyHttp);
        var empty = await emptyProvider.GenerateAsync(new ChatIntelligenceOptions(Model: "m"), [new("user", "hola")], TestContext.Current.CancellationToken);
        Assert.Equal("EmptyResponse", empty.Status);

        var badHandler = new FakeHandler((_, _) => Task.FromResult(JsonResponse("not json")));
        using var badHttp = new HttpClient(badHandler);
        using var badProvider = new LocalOpenAiCompatibleProvider(badHttp);
        var bad = await badProvider.GenerateAsync(new ChatIntelligenceOptions(Model: "m"), [new("user", "hola")], TestContext.Current.CancellationToken);
        Assert.Equal("InvalidResponse", bad.Status);
    }

    [Fact]
    public async Task EnforcesInputAndMessageRoleValidation()
    {
        using var http = new HttpClient(new FakeHandler((_, _) => throw new Xunit.Sdk.XunitException("Invalid input must be rejected before HTTP.")));
        using var provider = new LocalOpenAiCompatibleProvider(http);
        var badRole = await provider.GenerateAsync(new ChatIntelligenceOptions(Model: "m"), [new("tool", "run this")], TestContext.Current.CancellationToken);
        var tooLong = await provider.GenerateAsync(new ChatIntelligenceOptions(Model: "m", MaxInputCharacters: 100), [new("user", new string('x', 101))], TestContext.Current.CancellationToken);
        Assert.Equal("InvalidInput", badRole.Status);
        Assert.Equal("InputTooLong", tooLong.Status);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
