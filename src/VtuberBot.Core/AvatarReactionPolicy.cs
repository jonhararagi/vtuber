namespace VtuberBot.Core;

/// <summary>Maps supported Twitch events to short avatar reactions using the user's configured art.</summary>
public static class AvatarReactionPolicy
{
    public static string? GetTransientState(string eventType) =>
        eventType switch
        {
            "channel.follow" => "Happy",
            "channel.subscribe" => "Happy",
            "channel.subscription.gift" => "Happy",
            "channel.channel_points_custom_reward_redemption.add" => "Happy",
            "channel.raid" => "Surprised",
            _ => null
        };
}
