using System.Net;
using System.Text.Json;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class TwitchModerationClientTests
{
    [Fact]
    public async Task SendChatMessageReportsSuccessOnlyWhenHelixConfirmsDelivery()
    {
        var handler = new StubHandler(
            Json(HttpStatusCode.OK, """{"data":[{"id":"100"}]}"""),
            Json(HttpStatusCode.OK, """{"data":[{"id":"200"}]}"""),
            Json(HttpStatusCode.OK, """{"data":[{"message_id":"msg-300","is_sent":true}]}"""));
        using var http = new HttpClient(handler);
        using var client = new TwitchModerationClient(http);

        var result = await client.SendChatMessageAsync("client-id", "access-token", "#Streamer",
            "viewer, respetá las reglas.", TestContext.Current.CancellationToken);

        Assert.Contains("Twitch confirmó el envío", result, StringComparison.Ordinal);
        Assert.Contains("msg-300", result, StringComparison.Ordinal);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("GET https://api.twitch.tv/helix/users?login=streamer", handler.Requests);
        Assert.Contains("GET https://api.twitch.tv/helix/users", handler.Requests);
        Assert.Contains("POST https://api.twitch.tv/helix/chat/messages", handler.Requests);

        using var body = JsonDocument.Parse(handler.Bodies[2]);
        Assert.Equal("100", body.RootElement.GetProperty("broadcaster_id").GetString());
        Assert.Equal("200", body.RootElement.GetProperty("sender_id").GetString());
        Assert.Equal("viewer, respetá las reglas.", body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SendChatMessageDoesNotClaimSuccessWhenTwitchDropsMessage()
    {
        var handler = new StubHandler(
            Json(HttpStatusCode.OK, """{"data":[{"id":"100"}]}"""),
            Json(HttpStatusCode.OK, """{"data":[{"id":"200"}]}"""),
            Json(HttpStatusCode.OK, """{"data":[{"message_id":"msg-300","is_sent":false,"drop_reason":{"code":"automod_held","message":"Message held for review"}}]}"""));
        using var http = new HttpClient(handler);
        using var client = new TwitchModerationClient(http);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendChatMessageAsync("client-id", "access-token", "streamer", "unwanted link", TestContext.Current.CancellationToken));

        Assert.Contains("Twitch no confirmó el envío", error.Message, StringComparison.Ordinal);
        Assert.Contains("automod_held", error.Message, StringComparison.Ordinal);
        Assert.Contains("Message held for review", error.Message, StringComparison.Ordinal);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> remaining = new(responses);
        public List<string> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Method.Method + " " + request.RequestUri);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (remaining.Count == 0)
                throw new InvalidOperationException("Unexpected HTTP request in test.");
            return remaining.Dequeue();
        }
    }
}
