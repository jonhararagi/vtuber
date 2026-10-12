using System.Text.RegularExpressions;

namespace VtuberBot.Core;

/// <summary>Redacts common credential forms before local diagnostics are shared.</summary>
public static class DiagnosticTextRedactor
{
    private static readonly Regex BearerOrOAuth = new(
        @"\b(Bearer|OAuth)\s+[A-Za-z0-9._~+/=-]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex OAuthPrefix = new(
        @"\boauth:[A-Za-z0-9_]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SecretAssignment = new(
        @"\b(access_token|refresh_token|client_secret|password|passwd|token|api[_-]?key)\s*([=:])\s*(""[^""]*""|'[^']*'|[^\s&,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex TelegramBotToken = new(
        @"\b\d{6,}:[A-Za-z0-9_-]{20,}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        try
        {
            var safe = BearerOrOAuth.Replace(text, "$1 [REDACTED]");
            safe = OAuthPrefix.Replace(safe, "oauth:[REDACTED]");
            safe = SecretAssignment.Replace(safe, "$1$2[REDACTED]");
            return TelegramBotToken.Replace(safe, "[REDACTED_TELEGRAM_TOKEN]");
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed if an unexpectedly large/malicious string exhausts a regex timeout.
            return "[diagnostic text omitted: redaction timeout]";
        }
    }
}
