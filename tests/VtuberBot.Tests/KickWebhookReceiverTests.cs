using System.Security.Cryptography;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class KickWebhookReceiverTests
{
    [Fact]
    public void SignatureVerifierAcceptsValidKickSignature()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        var messageId = "01KICKMESSAGEID123456789";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var body = "{\"hello\":\"kick\"}";
        var signed = Encoding.UTF8.GetBytes(messageId + "." + timestamp + "." + body);
        var signature = Convert.ToBase64String(rsa.SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.True(KickWebhookSignatureVerifier.Verify(
            publicKey, messageId, timestamp, body, signature, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void SignatureVerifierRejectsTamperedBody()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        var messageId = "01KICKMESSAGEID123456789";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var body = "{\"hello\":\"kick\"}";
        var signed = Encoding.UTF8.GetBytes(messageId + "." + timestamp + "." + body);
        var signature = Convert.ToBase64String(rsa.SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.False(KickWebhookSignatureVerifier.Verify(
            publicKey, messageId, timestamp, body + " ", signature, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void SignatureVerifierRejectsStaleWebhookTimestamp()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        var messageId = "01KICKMESSAGEID123456789";
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
        var body = "{}";
        var signed = Encoding.UTF8.GetBytes(messageId + "." + timestamp + "." + body);
        var signature = Convert.ToBase64String(rsa.SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.False(KickWebhookSignatureVerifier.Verify(
            publicKey, messageId, timestamp, body, signature, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void PayloadParserReadsChatMessageAndProtectsModerators()
    {
        const string json = """
        {
          "message_id": "chat-message-id",
          "broadcaster": { "user_id": 100, "username": "streamer", "channel_slug": "mychannel" },
          "sender": {
            "user_id": 200,
            "username": "helper",
            "identity": { "badges": [ { "type": "moderator", "text": "Moderator" } ] }
          },
          "content": "https://example.test spam"
        }
        """;

        var parsed = KickWebhookPayloadParser.Parse("chat.message.sent", "1", "webhook-id", json);

        Assert.Equal("chat.message.sent", parsed.EventType);
        Assert.NotNull(parsed.ChatMessage);
        Assert.Equal("chat-message-id", parsed.ChatMessage.MessageId);
        Assert.Equal(100, parsed.ChatMessage.BroadcasterUserId);
        Assert.Equal(200, parsed.ChatMessage.SenderUserId);
        Assert.Equal("mychannel", parsed.ChatMessage.ChannelSlug);
        Assert.Equal("helper", parsed.ChatMessage.Username);
        Assert.True(parsed.ChatMessage.IsModerator);
        Assert.False(parsed.ChatMessage.IsBroadcaster);
    }

    [Fact]
    public void PayloadParserProtectsBroadcasterEvenWithoutBadge()
    {
        const string json = """
        {
          "message_id": "chat-message-id",
          "broadcaster": { "user_id": 100, "username": "streamer", "channel_slug": "mychannel" },
          "sender": { "user_id": 100, "username": "streamer", "identity": { "badges": [] } },
          "content": "test"
        }
        """;

        var parsed = KickWebhookPayloadParser.Parse("chat.message.sent", "1", "webhook-id", json);

        Assert.NotNull(parsed.ChatMessage);
        Assert.True(parsed.ChatMessage.IsModerator);
        Assert.True(parsed.ChatMessage.IsBroadcaster);
    }

    [Fact]
    public void PayloadParserRejectsChatMessageWithoutRequiredSenderFields()
    {
        const string json = """
        {
          "message_id": "chat-message-id",
          "broadcaster": { "user_id": 100, "username": "streamer", "channel_slug": "mychannel" },
          "sender": { "user_id": 0, "username": "" },
          "content": "test"
        }
        """;

        Assert.Throws<InvalidDataException>(() =>
            KickWebhookPayloadParser.Parse("chat.message.sent", "1", "webhook-id", json));
    }
    [Fact]
    public void PayloadParserRejectsUnboundedChannelMetadataBeforePersistingIt()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            broadcaster = new
            {
                user_id = 100,
                username = new string('x', 201),
                channel_slug = "mychannel"
            }
        });

        Assert.Throws<InvalidDataException>(() =>
            KickWebhookPayloadParser.Parse("channel.followed", "1", "webhook-id", json));
    }

    [Fact]
    public async Task LoopbackReceiverAcceptsSignedChatAndDeduplicatesDuplicateDelivery()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        var port = ReserveFreePort();
        var received = new TaskCompletionSource<KickWebhookEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveCount = 0;
        var keyFetchCount = 0;
        await using var receiver = new KickWebhookReceiver(
            port,
            TimeSpan.FromMinutes(10),
            _ =>
            {
                Interlocked.Increment(ref keyFetchCount);
                return Task.FromResult(publicKey);
            });
        receiver.EventReceived += evt =>
        {
            Interlocked.Increment(ref receiveCount);
            received.TrySetResult(evt);
        };
        receiver.Start();

        const string messageId = "01KICK-E2E-MESSAGE-0001";
        const string subscriptionId = "01KICK-E2E-SUB-0001";
        const string body = """
        {
          "message_id": "01KICK-CHAT-0001",
          "broadcaster": { "user_id": 100, "username": "streamer", "channel_slug": "testchannel" },
          "sender": { "user_id": 200, "username": "viewer", "identity": { "badges": [] } },
          "content": "hola desde la prueba local"
        }
        """;
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var signature = Sign(rsa, messageId, timestamp, body);

        var firstStatus = await SendSignedWebhookAsync(
            port, messageId, subscriptionId, timestamp, signature, body);
        Assert.Equal(200, firstStatus);

        var accepted = await received.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal("chat.message.sent", accepted.EventType);
        Assert.Equal(messageId, accepted.EventMessageId);
        Assert.Equal(subscriptionId, accepted.SubscriptionId);
        Assert.NotNull(accepted.ChatMessage);
        Assert.Equal("testchannel", accepted.ChatMessage.ChannelSlug);
        Assert.Equal("viewer", accepted.ChatMessage.Username);
        Assert.Equal("hola desde la prueba local", accepted.ChatMessage.Content);

        var duplicateStatus = await SendSignedWebhookAsync(
            port, messageId, subscriptionId, timestamp, signature, body);
        Assert.Equal(204, duplicateStatus);
        Assert.Equal(1, Volatile.Read(ref receiveCount));
        Assert.Equal(1, Volatile.Read(ref keyFetchCount));
    }

    [Fact]
    public async Task LoopbackReceiverRejectsInvalidSignatureBeforeDispatch()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        var port = ReserveFreePort();
        var receiveCount = 0;
        await using var receiver = new KickWebhookReceiver(
            port,
            TimeSpan.FromMinutes(10),
            _ => Task.FromResult(publicKey));
        receiver.EventReceived += _ => Interlocked.Increment(ref receiveCount);
        receiver.Start();

        const string messageId = "01KICK-E2E-MESSAGE-0002";
        const string body = """{"message_id":"chat-2","broadcaster":{"user_id":100,"username":"streamer","channel_slug":"testchannel"},"sender":{"user_id":200,"username":"viewer"},"content":"no debe pasar"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var invalidSignature = Sign(rsa, messageId, timestamp, body + " ");

        var status = await SendSignedWebhookAsync(
            port, messageId, "01KICK-E2E-SUB-0002", timestamp, invalidSignature, body);

        Assert.Equal(401, status);
        Assert.Equal(0, Volatile.Read(ref receiveCount));
    }

    private static string Sign(RSA rsa, string messageId, string timestamp, string body)
    {
        var signedContent = Encoding.UTF8.GetBytes(messageId + "." + timestamp + "." + body);
        return Convert.ToBase64String(rsa.SignData(signedContent, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    private static int ReserveFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<int> SendSignedWebhookAsync(
        int port, string messageId, string subscriptionId, string timestamp, string signature, string body)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
        await using var stream = client.GetStream();
        var request = Encoding.UTF8.GetBytes(
            "POST /kick-webhook HTTP/1.1\r\n" +
            "Host: 127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "\r\n" +
            "Content-Type: application/json\r\n" +
            "Content-Length: " + Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture) + "\r\n" +
            "Kick-Event-Message-Id: " + messageId + "\r\n" +
            "Kick-Event-Subscription-Id: " + subscriptionId + "\r\n" +
            "Kick-Event-Message-Timestamp: " + timestamp + "\r\n" +
            "Kick-Event-Signature: " + signature + "\r\n" +
            "Kick-Event-Type: chat.message.sent\r\n" +
            "Kick-Event-Version: 1\r\n" +
            "Connection: close\r\n\r\n" + body);
        await stream.WriteAsync(request, TestContext.Current.CancellationToken);
        await stream.FlushAsync(TestContext.Current.CancellationToken);

        using var responseBuffer = new MemoryStream();
        var oneByte = new byte[1];
        while (responseBuffer.Length < 4096)
        {
            var read = await stream.ReadAsync(oneByte.AsMemory(), TestContext.Current.CancellationToken);
            if (read == 0) break;
            responseBuffer.WriteByte(oneByte[0]);
            var bytes = responseBuffer.GetBuffer();
            var count = (int)responseBuffer.Length;
            if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 &&
                bytes[count - 2] == 13 && bytes[count - 1] == 10)
                break;
        }

        var headerText = Encoding.ASCII.GetString(responseBuffer.ToArray());
        var statusLine = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (statusLine is null)
            throw new InvalidDataException("El receptor Kick no devolvió una línea HTTP de respuesta.");
        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var status))
            throw new InvalidDataException("El receptor Kick devolvió un estado HTTP inválido.");
        return status;
    }

}
