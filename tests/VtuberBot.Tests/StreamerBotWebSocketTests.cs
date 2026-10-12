using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class StreamerBotWebSocketTests
{
    [Fact]
    public void EndpointRejectsInvalidHostPortAndPath()
    {
        Assert.Equal("ws://127.0.0.1:8080/", new StreamerBotEndpoint().ToUri().ToString());
        Assert.Throws<ArgumentException>(() => new StreamerBotEndpoint("http://localhost", 8080, "/").ToUri());
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamerBotEndpoint("localhost", 65536, "/").ToUri());
        Assert.Throws<ArgumentException>(() => new StreamerBotEndpoint("localhost", 8080, "relative").ToUri());
        Assert.Throws<ArgumentException>(() => new StreamerBotEndpoint("localhost", 8080, "/../admin").ToUri());
    }

    [Fact]
    public void AuthenticationProofFollowsDocumentedSha256Base64Steps()
    {
        var secret = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("passwordsalt")));
        var expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret + "challenge")));
        Assert.Equal(expected, StreamerBotWebSocketClient.CreateAuthenticationProof("password", "salt", "challenge"));
        Assert.NotEqual("password", expected);
    }

    [Fact]
    public async Task RealClientHandlesFragmentedHelloAuthenticationRequestsAndEvents()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var eventReceived = new TaskCompletionSource<StreamerBotEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = new StreamerBotWebSocketClient();
        client.EventReceived += evt => eventReceived.TrySetResult(evt);

        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var accepted = await context.AcceptWebSocketAsync(null);
            using var socket = accepted.WebSocket;
            var hello = """{"request":"Hello","info":{"instanceId":"test-instance","name":"Test","version":"1.0","os":"windows"},"authentication":{"salt":"salt","challenge":"challenge"}}""";
            var helloBytes = Encoding.UTF8.GetBytes(hello);
            await socket.SendAsync(new ArraySegment<byte>(helloBytes, 0, helloBytes.Length / 2), WebSocketMessageType.Text, false, CancellationToken.None);
            await socket.SendAsync(new ArraySegment<byte>(helloBytes, helloBytes.Length / 2, helloBytes.Length - helloBytes.Length / 2), WebSocketMessageType.Text, true, CancellationToken.None);

            while (socket.State == WebSocketState.Open)
            {
                using var message = await ReceiveJsonAsync(socket);
                var root = message.RootElement;
                var request = root.GetProperty("request").GetString();
                var id = root.GetProperty("id").GetString();
                string response;
                switch (request)
                {
                    case "Authenticate":
                        Assert.Equal(StreamerBotWebSocketClient.CreateAuthenticationProof("pw", "salt", "challenge"), root.GetProperty("authentication").GetString());
                        response = JsonSerializer.Serialize(new { status = "ok", id });
                        break;
                    case "GetActions":
                        response = JsonSerializer.Serialize(new { status = "ok", id, count = 0, actions = Array.Empty<object>() });
                        break;
                    case "GetEvents":
                        response = JsonSerializer.Serialize(new { status = "ok", id, events = new Dictionary<string, string[]> { ["twitch"] = ["Follow", "Raid"] } });
                        break;
                    case "Subscribe":
                        var selected = root.GetProperty("events").Clone();
                        response = JsonSerializer.Serialize(new { status = "ok", id, events = selected });
                        await SendTextAsync(socket, response);
                        await SendTextAsync(socket, """{"timeStamp":"2026-10-12T00:00:00Z","event":{"source":"Twitch","type":"Follow"},"data":{"userName":"viewer"}}""");
                        continue;
                    case "DoAction":
                        var requestedAction = root.GetProperty("action").GetProperty("id").GetString();
                        response = JsonSerializer.Serialize(new
                        {
                            status = requestedAction == "00000000-0000-0000-0000-000000000001" ? "error" : "ok",
                            id
                        });
                        break;
                    default:
                        response = JsonSerializer.Serialize(new { status = "error", id });
                        break;
                }
                await SendTextAsync(socket, response);
                if (request == "DoAction" &&
                    root.GetProperty("action").GetProperty("id").GetString() == "00000000-0000-0000-0000-000000000001") break;
            }
        });

        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port, "/"), "pw");
        Assert.Equal(StreamerBotConnectionState.Connected, client.Snapshot.State);
        Assert.Equal("test-instance", client.Snapshot.Instance?.InstanceId);
        Assert.Empty(await client.GetActionsAsync());
        var events = await client.GetEventsAsync();
        Assert.Contains("Follow", events["Twitch"]);
        var subscribed = await client.SubscribeAsync(new Dictionary<string, string[]> { ["Twitch"] = ["Follow"] });
        Assert.Contains("Follow", subscribed["Twitch"]);
        var received = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Twitch.Follow", received.Key);
        var action = await client.DoActionAsync("a0ff6f91-a51e-4b7d-948b-5e03ff4a82f0");
        Assert.True(action.Success);
        var rejected = await client.DoActionAsync("00000000-0000-0000-0000-000000000001");
        Assert.False(rejected.Success);
        await server.WaitAsync(TimeSpan.FromSeconds(5));
        await client.DisconnectAsync();
        Assert.Equal(StreamerBotConnectionState.ConfiguredDisconnected, client.Snapshot.State);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Client closed.");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(stream.ToArray());
    }

    private static Task SendTextAsync(WebSocket socket, string message) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
}
