using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class DiagnosticTextRedactorTests
{
    [Theory]
    [InlineData("Authorization: Bearer abcdefghijklmnop", "Bearer [REDACTED]")]
    [InlineData("oauth:abcdefghijklmnopqrstuvwxyz", "oauth:[REDACTED]")]
    [InlineData("access_token=secret-value", "access_token=[REDACTED]")]
    [InlineData("refresh_token: \"secret value\"", "refresh_token:[REDACTED]")]
    [InlineData("client_secret=super-secret", "client_secret=[REDACTED]")]
    [InlineData("password=hunter2", "password=[REDACTED]")]
    [InlineData("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", "[REDACTED_TELEGRAM_TOKEN]")]
    public void RedactsCommonCredentialFormats(string input, string expectedFragment)
    {
        var redacted = DiagnosticTextRedactor.Redact(input);
        Assert.Contains(expectedFragment, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnop", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesOrdinaryDiagnosticText()
    {
        Assert.Equal("OBS autenticado · versión 30.2", DiagnosticTextRedactor.Redact("OBS autenticado · versión 30.2"));
    }

    [Fact]
    public void HandlesNullAndEmptyText()
    {
        Assert.Equal(string.Empty, DiagnosticTextRedactor.Redact(null));
        Assert.Equal(string.Empty, DiagnosticTextRedactor.Redact(string.Empty));
    }
}
