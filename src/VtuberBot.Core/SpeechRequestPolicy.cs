namespace VtuberBot.Core;

/// <summary>Bounds and normalizes text before local speech reaches the SAPI worker queue.</summary>
public static class SpeechRequestPolicy
{
    public const int MaximumTextLength = 2000;

    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Escribí un texto antes de encolarlo.", nameof(text));

        var normalized = string.Join(" ", text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > MaximumTextLength)
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"El texto de voz no puede superar {MaximumTextLength} caracteres.");

        return normalized;
    }
}
