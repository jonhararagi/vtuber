namespace VtuberBot.Core;

public sealed record MiniGameResolution(
    string Game,
    string Choice,
    string Outcome,
    bool Won,
    int PayoutMultiplier);

/// <summary>
/// Rules and choice validation for local minigames. Roll() uses cryptographic randomness;
/// Resolve() is deterministic so every rule can be tested without flaky random outcomes.
/// </summary>
public static class MiniGameEngine
{
    private static readonly IReadOnlyList<string> CoinChoices =
        Array.AsReadOnly(new[] { "Cara", "Cruz" });
    private static readonly IReadOnlyList<string> RouletteChoices =
        Array.AsReadOnly(new[] { "Rojo", "Negro", "Verde" });
    private static readonly IReadOnlyList<string> DiceChoices =
        Array.AsReadOnly(new[] { "Bajo (1-3)", "Alto (4-6)" });

    private static readonly HashSet<int> RouletteRedNumbers =
    [
        1, 3, 5, 7, 9, 12, 14, 16, 18,
        19, 21, 23, 25, 27, 30, 32, 34, 36
    ];

    public static IReadOnlyList<string> GetChoices(string game) =>
        NormalizeGame(game) switch
        {
            "Duelo de moneda" => CoinChoices,
            "Ruleta de suerte" => RouletteChoices,
            "Dado" => DiceChoices,
            _ => throw new ArgumentException("Juego no soportado.", nameof(game))
        };

    public static MiniGameResolution Roll(string game, string choice)
    {
        var normalizedGame = NormalizeGame(game);
        var normalizedChoice = NormalizeChoice(normalizedGame, choice);
        var roll = normalizedGame switch
        {
            "Duelo de moneda" => System.Security.Cryptography.RandomNumberGenerator.GetInt32(2),
            "Ruleta de suerte" => System.Security.Cryptography.RandomNumberGenerator.GetInt32(37),
            "Dado" => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7),
            _ => throw new ArgumentException("Juego no soportado.", nameof(game))
        };
        return Resolve(normalizedGame, normalizedChoice, roll);
    }

    public static MiniGameResolution Resolve(string game, string choice, int roll)
    {
        var normalizedGame = NormalizeGame(game);
        var normalizedChoice = NormalizeChoice(normalizedGame, choice);

        return normalizedGame switch
        {
            "Duelo de moneda" when roll is 0 or 1 =>
                ResolveCoin(normalizedChoice, roll == 0 ? "Cara" : "Cruz"),
            "Ruleta de suerte" when roll is >= 0 and <= 36 =>
                ResolveRoulette(normalizedChoice, roll),
            "Dado" when roll is >= 1 and <= 6 =>
                ResolveDice(normalizedChoice, roll),
            "Duelo de moneda" => throw new ArgumentOutOfRangeException(nameof(roll), "La moneda admite 0 o 1."),
            "Ruleta de suerte" => throw new ArgumentOutOfRangeException(nameof(roll), "La ruleta europea admite números de 0 a 36."),
            "Dado" => throw new ArgumentOutOfRangeException(nameof(roll), "El dado admite números de 1 a 6."),
            _ => throw new ArgumentException("Juego no soportado.", nameof(game))
        };
    }

    private static MiniGameResolution ResolveCoin(string choice, string outcome)
    {
        var won = string.Equals(choice, outcome, StringComparison.OrdinalIgnoreCase);
        return new("Duelo de moneda", choice, outcome, won, won ? 2 : 0);
    }

    private static MiniGameResolution ResolveRoulette(string choice, int number)
    {
        var color = number == 0 ? "Verde" : RouletteRedNumbers.Contains(number) ? "Rojo" : "Negro";
        var won = string.Equals(choice, color, StringComparison.OrdinalIgnoreCase);
        return new("Ruleta de suerte", choice, $"{number} · {color}", won,
            won ? (color == "Verde" ? 36 : 2) : 0);
    }

    private static MiniGameResolution ResolveDice(string choice, int roll)
    {
        var band = roll <= 3 ? "Bajo (1-3)" : "Alto (4-6)";
        var won = string.Equals(choice, band, StringComparison.OrdinalIgnoreCase);
        return new("Dado", choice, $"{roll} · {band}", won, won ? 2 : 0);
    }

    private static string NormalizeChoice(string game, string choice)
    {
        if (string.IsNullOrWhiteSpace(choice))
            throw new ArgumentException("Elegí una opción antes de jugar.", nameof(choice));

        return GetChoices(game).FirstOrDefault(
            option => string.Equals(option, choice.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("La opción no corresponde al minijuego seleccionado.", nameof(choice));
    }

    private static string NormalizeGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            throw new ArgumentException("Elegí un minijuego.", nameof(game));

        var requested = game.Trim();
        if (string.Equals(requested, "Duelo de moneda", StringComparison.OrdinalIgnoreCase))
            return "Duelo de moneda";
        if (string.Equals(requested, "Ruleta de suerte", StringComparison.OrdinalIgnoreCase))
            return "Ruleta de suerte";
        if (string.Equals(requested, "Dado", StringComparison.OrdinalIgnoreCase))
            return "Dado";

        throw new ArgumentException("Juego no soportado: " + requested, nameof(game));
    }
}
