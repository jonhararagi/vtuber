namespace VtuberBot.Core;

/// <summary>
/// Shared local-first community commands used by the desktop test surface and live Twitch chat.
/// Returns null only when the command name is not one of the reserved community commands.
/// </summary>
public sealed class CommunityCommandRouter(ProductStore store)
{
    public static IReadOnlyList<string> BuiltInCommands { get; } =
        Array.AsReadOnly(new[] { "puntos", "top", "jugar" });

    public CommandResult? TryExecute(string name, string username, string arguments = "")
    {
        ArgumentNullException.ThrowIfNull(store);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var command = name.Trim().TrimStart('!').ToLowerInvariant();
        if (!BuiltInCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(username) || username.Length > 25 ||
            username.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            return CommandResult.Failed("El nombre de usuario no es válido.");

        arguments = (arguments ?? string.Empty).Trim();
        if (arguments.Length > 500)
            return CommandResult.Failed("Los argumentos superan el límite de 500 caracteres.");

        try
        {
            return command switch
            {
                "puntos" => GetPoints(username, arguments),
                "top" => GetTop(username, arguments),
                "jugar" => Play(username, arguments),
                _ => null
            };
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Failed(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Failed(exception.Message);
        }
    }

    private CommandResult GetPoints(string username, string arguments)
    {
        if (arguments.Length > 0)
            return CommandResult.Failed("Uso: !puntos");
        var user = store.TouchUser(username);
        return CommandResult.Succeeded("@" + user.Username + ", tenés " +
            user.Points.ToString(System.Globalization.CultureInfo.InvariantCulture) + " puntos.");
    }

    private CommandResult GetTop(string username, string arguments)
    {
        if (arguments.Length > 0)
            return CommandResult.Failed("Uso: !top");
        _ = store.TouchUser(username);
        var leaderboard = store.GetLeaderboard(5);
        if (leaderboard.Count == 0)
            return CommandResult.Succeeded("Todavía no hay puntos registrados.");
        var entries = leaderboard.Select((user, index) =>
            (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ". " +
            user.Username + " (" + user.Points.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        var message = "Top puntos: " + string.Join(" · ", entries);
        return message.Length <= 500
            ? CommandResult.Succeeded(message)
            : CommandResult.Failed("No se pudo generar el ranking dentro del límite de mensaje.");
    }

    private CommandResult Play(string username, string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !int.TryParse(parts[^1], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var stake) ||
            stake is < 0 or > 10000)
        {
            return CommandResult.Failed("Uso: !jugar moneda cara 10 | !jugar ruleta rojo 10 | !jugar dado alto 10 (0 = práctica).");
        }

        var game = parts[0].ToLowerInvariant() switch
        {
            "moneda" => "Duelo de moneda",
            "ruleta" => "Ruleta de suerte",
            "dado" => "Dado",
            _ => string.Empty
        };
        if (game.Length == 0)
            return CommandResult.Failed("Juego desconocido. Elegí moneda, ruleta o dado.");

        var choiceText = string.Join(' ', parts.Skip(1).Take(parts.Length - 2)).Trim();
        var choice = game switch
        {
            "Duelo de moneda" when choiceText.Equals("cara", StringComparison.OrdinalIgnoreCase) => "Cara",
            "Duelo de moneda" when choiceText.Equals("cruz", StringComparison.OrdinalIgnoreCase) => "Cruz",
            "Ruleta de suerte" when choiceText.Equals("rojo", StringComparison.OrdinalIgnoreCase) => "Rojo",
            "Ruleta de suerte" when choiceText.Equals("negro", StringComparison.OrdinalIgnoreCase) => "Negro",
            "Ruleta de suerte" when choiceText.Equals("verde", StringComparison.OrdinalIgnoreCase) => "Verde",
            "Dado" when choiceText.Equals("bajo", StringComparison.OrdinalIgnoreCase) => "Bajo (1-3)",
            "Dado" when choiceText.Equals("alto", StringComparison.OrdinalIgnoreCase) => "Alto (4-6)",
            _ => string.Empty
        };
        if (choice.Length == 0)
            return CommandResult.Failed("Elección inválida. Moneda: cara/cruz; ruleta: rojo/negro/verde; dado: bajo/alto.");

        return CommandResult.Succeeded(store.PlayGame(game, username, stake, choice));
    }
}
