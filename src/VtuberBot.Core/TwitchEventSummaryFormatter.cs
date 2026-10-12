using System.Text.Json;

namespace VtuberBot.Core;

/// <summary>Turns EventSub payloads into readable activity and alert text without exposing raw JSON.</summary>
public static class TwitchEventSummaryFormatter
{
    private const int MaxFieldLength = 160;
    private const int MaxSummaryLength = 400;

    public static string Summarize(string type, JsonElement evt)
    {
        var safeType = CleanField(type, MaxFieldLength);
        string Value(string key) => GetStringProperty(evt, key);
        var summary = safeType switch
        {
            "channel.follow" => "Nuevo follow: " + Value("user_name"),
            "channel.subscribe" => "Nueva suscripción: " + Value("user_name") + " · " + Value("tier"),
            "channel.subscription.gift" => "Regalo de suscripción: " + Value("user_name") + " · cantidad " + Value("total"),
            "channel.channel_points_custom_reward_redemption.add" => SummarizeRedemption(evt),
            "channel.raid" => "Raid de " + Value("from_broadcaster_user_name") + " · espectadores " + Value("viewers"),
            _ => "Evento de Twitch recibido: " + (string.IsNullOrWhiteSpace(safeType) ? "tipo desconocido" : safeType)
        };
        return CleanField(summary, MaxSummaryLength);
    }

    private static string SummarizeRedemption(JsonElement evt)
    {
        var user = GetStringProperty(evt, "user_name");
        var parts = new List<string>
        {
            "Redención de puntos: " + (string.IsNullOrWhiteSpace(user) ? "usuario desconocido" : user)
        };

        if (evt.ValueKind == JsonValueKind.Object &&
            evt.TryGetProperty("reward", out var reward) &&
            reward.ValueKind == JsonValueKind.Object)
        {
            var rewardTitle = GetStringProperty(reward, "title");
            if (!string.IsNullOrWhiteSpace(rewardTitle))
                parts.Add(rewardTitle);
        }

        var userInput = GetStringProperty(evt, "user_input");
        if (!string.IsNullOrWhiteSpace(userInput))
            parts.Add("mensaje: " + userInput);

        return string.Join(" · ", parts);
    }

    private static string GetStringProperty(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object ||
            !source.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return string.Empty;

        var raw = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array
                ? string.Empty
                : value.ToString();
        return CleanField(raw, MaxFieldLength);
    }

    private static string CleanField(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var safeCharacters = value.Where(character => !char.IsControl(character)).ToArray();
        var sanitized = string.Join(" ", new string(safeCharacters)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..(maxLength - 1)].TrimEnd() + "…";
    }
}
