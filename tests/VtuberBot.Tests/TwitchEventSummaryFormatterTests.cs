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
    public void KnownEventsKeepTheirHumanReadableSummaries()
    {
        using var document = JsonDocument.Parse("""{"user_name":"Mika","tier":"2000"}""");

        Assert.Equal("Nueva suscripción: Mika · 2000",
            TwitchEventSummaryFormatter.Summarize("channel.subscribe", document.RootElement));
    }
}
