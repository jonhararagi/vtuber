using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsWebSocketClientIntegrationTests
{
    [Fact]
    public async Task RealObsClientAuthenticatesAndVerifiesVersionScenesAudioAndVideoProfile()
    {
        var port = ReserveFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
        listener.Start();

        const string password = "integration-test-password";
        const string salt = "salt-from-local-fake";
        const string challenge = "challenge-from-local-fake";
        var expectedProof = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
            Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt))) + challenge)));
        var observedRequests = new List<string>();
        var baseWidth = 1920;
        var baseHeight = 1080;
        var outputWidth = 1920;
        var outputHeight = 1080;
        var fpsNumerator = 60;
        var fpsDenominator = 1;

        var server = RunServerAsync(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendJsonAsync(socket, new
            {
                op = 0,
                d = new
                {
                    obsWebSocketVersion = "5.4.0",
                    rpcVersion = 1,
                    authentication = new { salt, challenge }
                }
            });

            using (var identify = await ReceiveJsonAsync(socket))
            {
                Assert.Equal(1, identify.RootElement.GetProperty("op").GetInt32());
                Assert.Equal(expectedProof, identify.RootElement.GetProperty("d").GetProperty("authentication").GetString());
                Assert.Equal(1, identify.RootElement.GetProperty("d").GetProperty("rpcVersion").GetInt32());
            }
            await SendJsonAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });

            while (socket.State == WebSocketState.Open)
            {
                JsonDocument request;
                try { request = await ReceiveJsonAsync(socket); }
                catch (WebSocketException) { break; }
                using (request)
                {
                    var root = request.RootElement;
                    if (root.GetProperty("op").GetInt32() != 6) continue;
                    var data = root.GetProperty("d");
                    var requestType = data.GetProperty("requestType").GetString() ?? string.Empty;
                    var requestId = data.GetProperty("requestId").GetString() ?? string.Empty;
                    observedRequests.Add(requestType);

                    object responseData;
                    switch (requestType)
                    {
                        case "GetVersion":
                            responseData = new { obsVersion = "30.2.1", obsWebSocketVersion = "5.4.0" };
                            break;
                        case "SetVideoSettings":
                            var settings = data.GetProperty("requestData");
                            baseWidth = settings.GetProperty("baseWidth").GetInt32();
                            baseHeight = settings.GetProperty("baseHeight").GetInt32();
                            outputWidth = settings.GetProperty("outputWidth").GetInt32();
                            outputHeight = settings.GetProperty("outputHeight").GetInt32();
                            fpsNumerator = settings.GetProperty("fpsNumerator").GetInt32();
                            fpsDenominator = settings.GetProperty("fpsDenominator").GetInt32();
                            responseData = new { };
                            break;
                        case "GetVideoSettings":
                            responseData = new { baseWidth, baseHeight, outputWidth, outputHeight, fpsNumerator, fpsDenominator };
                            break;
                        case "GetSceneList":
                            responseData = new
                            {
                                currentProgramSceneName = "Desktop",
                                scenes = new[] { new { sceneName = "Desktop" }, new { sceneName = "Game" } }
                            };
                            break;
                        case "GetInputList":
                            responseData = new
                            {
                                inputs = new[]
                                {
                                    new { inputName = "Mic/Aux", inputKind = "wasapi_input_capture" }
                                }
                            };
                            break;
                        case "GetInputMute":
                            Assert.Equal("Mic/Aux", data.GetProperty("requestData").GetProperty("inputName").GetString());
                            responseData = new { inputMuted = false };
                            break;
                        case "GetRecordStatus":
                        case "GetStreamStatus":
                            responseData = new { outputActive = false };
                            break;
                        case "GetStats":
                            responseData = new
                            {
                                activeFps = 30.0,
                                averageFrameRenderTime = 2.4,
                                renderSkippedFrames = 0,
                                renderTotalFrames = 100,
                                outputSkippedFrames = 0,
                                outputTotalFrames = 100
                            };
                            break;
                        default:
                            throw new InvalidOperationException("La prueba recibió un tipo de solicitud OBS inesperado: " + requestType);
                    }

                    await SendJsonAsync(socket, new
                    {
                        op = 7,
                        d = new
                        {
                            requestType,
                            requestId,
                            requestStatus = new { result = true, code = 100, comment = "" },
                            responseData
                        }
                    });
                }
            }
        });

        await using var client = new ObsWebSocketClient();
        await client.ConnectAsync("127.0.0.1", port, password, TestContext.Current.CancellationToken);
        Assert.True(client.IsConnected);
        Assert.Equal("30.2.1", await client.GetVersionAsync(TestContext.Current.CancellationToken));

        var profileMessage = await client.ApplyLightVideoProfileAsync(TestContext.Current.CancellationToken);
        Assert.Contains("1280×720", profileMessage, StringComparison.Ordinal);
        Assert.Contains("30 FPS", profileMessage, StringComparison.Ordinal);

        var scenes = await client.GetScenesAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Desktop", scenes);
        Assert.Contains("Game", scenes);

        var audioInputs = await client.GetAudioInputsAsync(TestContext.Current.CancellationToken);
        var microphone = Assert.Single(audioInputs);
        Assert.Equal("Mic/Aux", microphone.Name);
        Assert.False(microphone.Muted);

        var diagnostics = await client.RunReadOnlyDiagnosticsAsync(TestContext.Current.CancellationToken);
        Assert.Equal("30.2.1", diagnostics.ObsVersion);
        Assert.Contains("Desktop", diagnostics.Scenes);
        Assert.Single(diagnostics.AudioInputs);
        Assert.True(diagnostics.Video.Is720p30);
        Assert.False(diagnostics.IsRecording);
        Assert.False(diagnostics.IsStreaming);
        Assert.Equal(30d, diagnostics.Performance.ActiveFps);
        Assert.Contains("No se inició", diagnostics.ToDisplayText(), StringComparison.Ordinal);

        Assert.False(await client.IsRecordingAsync(TestContext.Current.CancellationToken));
        Assert.False(await client.IsStreamingAsync(TestContext.Current.CancellationToken));
        Assert.Contains("GetVersion", observedRequests);
        Assert.Contains("SetVideoSettings", observedRequests);
        Assert.Contains("GetVideoSettings", observedRequests);
        Assert.Contains("GetSceneList", observedRequests);
        Assert.Contains("GetInputList", observedRequests);
        Assert.Contains("GetInputMute", observedRequests);
        Assert.Contains("GetRecordStatus", observedRequests);
        Assert.Contains("GetStreamStatus", observedRequests);

        await client.DisconnectAsync();
        await server.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ObsClientDoesNotReportConnectionWhenIdentifyIsRejected()
    {
        var port = ReserveFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
        listener.Start();

        var server = RunServerAsync(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await SendJsonAsync(socket, new { op = 99, d = new { } });
        });

        await using var client = new ObsWebSocketClient();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ConnectAsync("127.0.0.1", port, "wrong", TestContext.Current.CancellationToken));
        Assert.False(client.IsConnected);
        await server.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
    }

    private static int ReserveFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static Task RunServerAsync(Func<Task> action) => Task.Run(action);

    private static async Task SendJsonAsync(WebSocket socket, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(WebSocket socket)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, TestContext.Current.CancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("OBS fake server received a close frame.");
            stream.Write(buffer, 0, result.Count);
            if (stream.Length > 1_048_576)
                throw new InvalidDataException("Test WebSocket message exceeded 1 MiB.");
            if (result.EndOfMessage) break;
        }
        return JsonDocument.Parse(stream.ToArray());
    }
}
