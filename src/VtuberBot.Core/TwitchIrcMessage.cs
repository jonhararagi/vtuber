namespace VtuberBot.Core;

/// <summary>
/// Parses the common Twitch IRC message shape, including IRCv3 tags:
/// @tags :prefix COMMAND middle-parameters :trailing message
/// </summary>
public sealed record TwitchIrcMessage(
    IReadOnlyDictionary<string, string> Tags,
    string? Prefix,
    string Command,
    IReadOnlyList<string> Parameters,
    string? Trailing)
{
    public static bool TryParse(string? line, out TwitchIrcMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var index = 0;
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (line[index] == '@')
        {
            var end = line.IndexOf(' ', index);
            if (end < 0)
                return false;

            var tagsText = line[(index + 1)..end];
            foreach (var part in tagsText.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=');
                var key = separator < 0 ? part : part[..separator];
                if (key.Length == 0)
                    continue;
                var value = separator < 0 ? string.Empty : UnescapeTagValue(part[(separator + 1)..]);
                tags[key] = value;
            }
            index = end;
        }

        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;

        string? prefix = null;
        if (index < line.Length && line[index] == ':')
        {
            var end = line.IndexOf(' ', index);
            if (end < 0)
                return false;
            prefix = line[(index + 1)..end];
            index = end;
            while (index < line.Length && char.IsWhiteSpace(line[index]))
                index++;
        }

        if (index >= line.Length)
            return false;

        var trailingSeparator = line.IndexOf(" :", index, StringComparison.Ordinal);
        string commandAndParameters;
        string? trailing = null;
        if (trailingSeparator >= 0)
        {
            commandAndParameters = line[index..trailingSeparator];
            trailing = line[(trailingSeparator + 2)..];
        }
        else
        {
            commandAndParameters = line[index..];
        }

        var parts = commandAndParameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        message = new TwitchIrcMessage(
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(tags),
            prefix,
            parts[0],
            parts.Skip(1).ToArray(),
            trailing);
        return true;
    }

    private static string UnescapeTagValue(string value)
    {
        if (!value.Contains('\\'))
            return value;

        var builder = new System.Text.StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '\\' || index + 1 >= value.Length)
            {
                builder.Append(character);
                continue;
            }

            index++;
            builder.Append(value[index] switch
            {
                ':' => ';',
                's' => ' ',
                '\\' => '\\',
                'r' => '\r',
                'n' => '\n',
                _ => value[index]
            });
        }

        return builder.ToString();
    }
}
