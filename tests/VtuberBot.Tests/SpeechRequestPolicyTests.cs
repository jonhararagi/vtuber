using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class SpeechRequestPolicyTests
{
    [Theory]
    [InlineData(" Hola   chat ", "Hola chat")]
    [InlineData("Hola\r\nchat", "Hola chat")]
    [InlineData("Una\tfrase", "Una frase")]
    public void NormalizesWhitespace(string input, string expected) =>
        Assert.Equal(expected, SpeechRequestPolicy.NormalizeText(input));

    [Fact]
    public void AcceptsTextAtTheMaximumLength() =>
        Assert.Equal(SpeechRequestPolicy.MaximumTextLength,
            SpeechRequestPolicy.NormalizeText(new string('x', SpeechRequestPolicy.MaximumTextLength)).Length);

    [Fact]
    public void RejectsEmptyOrOversizedSpeechText()
    {
        Assert.Throws<ArgumentException>(() => SpeechRequestPolicy.NormalizeText("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpeechRequestPolicy.NormalizeText(new string('x', SpeechRequestPolicy.MaximumTextLength + 1)));
    }
}
