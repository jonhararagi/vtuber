using System.Text.Json;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class TwitchEventSummaryFormatterTests
{
    [Fact]
    public void RedemptionIncludesRewardTitleAndViewerMessageInsteadOfRawJson()
    {
        using var document = JsonDocument.Parse("""
            {
              "user_name": "Luna",
              "user_input": "¡hola streamer!",
              "reward": { "id": "abc-123", "title": "Elegir el fondo", "cost": 100 }
            }
            """);

        var summary = TwitchEventSummaryFormatter.Summarize(
            "channel.channel_points_custom_reward_redemption.add", document.RootElement);

        Assert.Equal("Redención de puntos: Luna · Elegir el fondo · mensaje: ¡hola streamer!", summary);
        Assert.DoesNotContain("abc-123", summary);
        Assert.DoesNotContain("cost", summary);
    }

    [Fact]
    public void RedemptionWithoutOptionalFieldsStillProducesReadableMessage()
    {
        using var document = JsonDocument.Parse("""{"user_name":"Luna","reward":{}}""");

        Assert.Equal(
            "Redención de puntos: Luna",
            TwitchEventSummaryFormatter.Summarize(
                "channel.channel_points_custom_reward_redemption.add", document.RootElement));
    }

    [Fact]
    public void UnknownEventDoesNotExposeRawJsonPayload()
    {
        using var document = JsonDocument.Parse("""{"secret":"should not be displayed"}""");
        var summary = TwitchEventSummaryFormatter.Summarize("future.event.type", document.RootElement);
        Assert.Equal("Evento de Twitch recibido: future.event.type", summary);
        Assert.DoesNotContain("should not be displayed", summary);
    }

    [Fact]
    public void ViewerInputIsSanitizedAndTotalSummaryIsBounded()
    {
        var longInput = new string('x', 2000);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            user_name = "Viewer\r\n[moderator]",
            user_input = longInput,
            reward = new { title = "Custom\tReward" }
        }));

        var summary = TwitchEventSummaryFormatter.Summarize(
            "channel.channel_points_custom_reward_redemption.add", document.RootElement);

        Assert.True(summary.Length <= 400);
        Assert.DoesNotContain("\r", summary);
        Assert.DoesNotContain("\n", summary);
        Assert.DoesNotContain("\t", summary);
    }

    [Fact]
    public void KnownEventsKeepTheirHumanReadableSummaries()
    {
        using var document = JsonDocument.Parse("""{"user_name":"Mika","tier":"2000"}""");

        Assert.Equal("Nueva suscripción: Mika · 2000",
            TwitchEventSummaryFormatter.Summarize("channel.subscribe", document.RootElement));
    }
}
