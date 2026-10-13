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

        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var accepted = await context.AcceptWebSocketAsync(null);
            using var socket = accepted.WebSocket;
            var hello = """{"request":"Hello","info":{"instanceId":"test-instance","name":"Test","version":"1.0","os":"windows"},"authentication":{"salt":"salt","challenge":"challenge"}}""";
            var helloBytes = Encoding.UTF8.GetBytes(hello);
            await socket.SendAsync(new ArraySegment<byte>(helloBytes, 0, helloBytes.Length / 2), WebSocketMessageType.Text, false, TestContext.Current.CancellationToken);
            await socket.SendAsync(new ArraySegment<byte>(helloBytes, helloBytes.Length / 2, helloBytes.Length - helloBytes.Length / 2), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);

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
                    case "UnSubscribe":
                        var unselected = root.GetProperty("events").Clone();
                        response = JsonSerializer.Serialize(new { status = "ok", id, events = unselected });
                        break;
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

        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port, "/"), "pw", TestContext.Current.CancellationToken);
        Assert.Equal(StreamerBotConnectionState.Connected, client.Snapshot.State);
        Assert.Equal("test-instance", client.Snapshot.Instance?.InstanceId);
        Assert.Empty(await client.GetActionsAsync(TestContext.Current.CancellationToken));
        var events = await client.GetEventsAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Follow", events["Twitch"]);
        var subscribed = await client.SubscribeAsync(new Dictionary<string, string[]> { ["Twitch"] = ["Follow"] }, TestContext.Current.CancellationToken);
        Assert.Contains("Follow", subscribed["Twitch"]);
        var received = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("Twitch.Follow", received.Key);
        var unsubscribed = await client.UnsubscribeAsync(new Dictionary<string, string[]> { ["Twitch"] = ["Follow"] }, TestContext.Current.CancellationToken);
        Assert.Contains("Follow", unsubscribed["Twitch"]);
        var action = await client.DoActionAsync("a0ff6f91-a51e-4b7d-948b-5e03ff4a82f0", TestContext.Current.CancellationToken);
        Assert.True(action.Success);
        var rejected = await client.DoActionAsync("00000000-0000-0000-0000-000000000001", TestContext.Current.CancellationToken);
        Assert.False(rejected.Success);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await client.DisconnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(StreamerBotConnectionState.ConfiguredDisconnected, client.Snapshot.State);
    }

    [Fact]
    public async Task AuthenticationMayBeDisabledWhenServerDoesNotRequestIt()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"no-auth","name":"Test","version":"1","os":"windows"}}""");
            await Task.Delay(500, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), null, TestContext.Current.CancellationToken);
        Assert.Equal(StreamerBotConnectionState.Connected, client.Snapshot.State);
        await client.DisconnectAsync(TestContext.Current.CancellationToken);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IncorrectAuthenticationIsRejectedAndReported()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"auth","name":"Test","version":"1","os":"windows"},"authentication":{"salt":"salt","challenge":"challenge"}}""");
            using var request = await ReceiveJsonAsync(socket);
            Assert.Equal("Authenticate", request.RootElement.GetProperty("request").GetString());
            var id = request.RootElement.GetProperty("id").GetString();
            await SendTextAsync(socket, JsonSerializer.Serialize(new { request = "Authenticate", status = "error", id, error = "Authentication failed" }));
            await Task.Delay(200, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<StreamerBotAuthenticationException>(() =>
            client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), "wrong-password", TestContext.Current.CancellationToken));
        Assert.Equal(StreamerBotConnectionState.AuthenticationError, client.Snapshot.State);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConcurrentRequestsAreMatchedWhenResponsesArriveOutOfOrder()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"order","name":"Test","version":"1","os":"windows"}}""");
            using var first = await ReceiveJsonAsync(socket);
            using var second = await ReceiveJsonAsync(socket);
            var requests = new[] { first.RootElement.Clone(), second.RootElement.Clone() };
            foreach (var request in requests.Reverse())
            {
                var name = request.GetProperty("request").GetString();
                var id = request.GetProperty("id").GetString();
                var response = name == "GetActions"
                    ? JsonSerializer.Serialize(new { request = name, status = "ok", id, actions = new[] { new { id = "a0ff6f91-a51e-4b7d-948b-5e03ff4a82f0", name = "Raid Action", enabled = true } } })
                    : JsonSerializer.Serialize(new { request = name, status = "ok", id, events = new Dictionary<string, string[]> { ["twitch"] = ["Raid"] } });
                await SendTextAsync(socket, response);
            }
            await Task.Delay(150, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), cancellationToken: TestContext.Current.CancellationToken);
        var actionsTask = client.GetActionsAsync(TestContext.Current.CancellationToken);
        var eventsTask = client.GetEventsAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(actionsTask, eventsTask);
        Assert.Equal("Raid Action", Assert.Single(await actionsTask).Name);
        Assert.Contains("Raid", (await eventsTask)["Twitch"]);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestTimeoutIsExplicitAndDoesNotPretendSuccess()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"timeout","name":"Test","version":"1","os":"windows"}}""");
            using var request = await ReceiveJsonAsync(socket);
            Assert.Equal("GetActions", request.RootElement.GetProperty("request").GetString());
            await Task.Delay(500, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromMilliseconds(150));
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(() => client.GetActionsAsync());
        Assert.Equal(StreamerBotConnectionState.Connected, client.Snapshot.State);
        await client.DisconnectAsync(TestContext.Current.CancellationToken);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestCancellationPropagatesWithoutReportingAcceptance()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var requestSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"cancel","name":"Test","version":"1","os":"windows"}}""");
            using var request = await ReceiveJsonAsync(socket);
            requestSeen.TrySetResult(true);
            await Task.Delay(500, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), cancellationToken: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        var operation = client.GetActionsAsync(cts.Token);
        await requestSeen.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await client.DisconnectAsync(TestContext.Current.CancellationToken);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidJsonMovesConnectionToProtocolError()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"json","name":"Test","version":"1","os":"windows"}}""");
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await SendTextAsync(socket, "{this is not json");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), cancellationToken: TestContext.Current.CancellationToken);
        await WaitForStateAsync(client, StreamerBotConnectionState.ProtocolError);
        Assert.Equal(StreamerBotConnectionState.ProtocolError, client.Snapshot.State);
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnexpectedServerDisconnectIsReported()
    {
        var port = ReservePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = RunServer(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendTextAsync(socket, """{"request":"Hello","info":{"instanceId":"disconnect","name":"Test","version":"1","os":"windows"}}""");
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await socket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "test disconnect", TestContext.Current.CancellationToken);
        });
        await using var client = new StreamerBotWebSocketClient(TimeSpan.FromSeconds(2));
        var stateChanged = new TaskCompletionSource<StreamerBotConnectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.StateChanged += state => { if (state.State == StreamerBotConnectionState.UnexpectedDisconnect) stateChanged.TrySetResult(state.State); };
        await client.ConnectAsync(new StreamerBotEndpoint("127.0.0.1", port), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(StreamerBotConnectionState.UnexpectedDisconnect, await stateChanged.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        await server.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private static Task RunServer(Func<Task> action) => Task.Run(action, TestContext.Current.CancellationToken);

    private static int ReservePort()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        return ((IPEndPoint)reservation.LocalEndpoint).Port;
    }

    private static async Task WaitForStateAsync(IStreamerBotClient client, StreamerBotConnectionState state)
    {
        if (client.Snapshot.State == state) return;
        var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(StreamerBotConnectionSnapshot snapshot) { if (snapshot.State == state) changed.TrySetResult(true); }
        client.StateChanged += Handler;
        try { await changed.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken); }
        finally { client.StateChanged -= Handler; }
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), TestContext.Current.CancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Client closed.");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(stream.ToArray());
    }

    private static Task SendTextAsync(WebSocket socket, string message) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);
}
