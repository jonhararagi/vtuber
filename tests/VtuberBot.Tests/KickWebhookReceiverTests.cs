using System.Security.Cryptography;
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

}
