using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarTalkStatePolicyTests
{
    [Theory]
    [InlineData(false, false, "Idle")]
    [InlineData(true, false, "Talking")]
    [InlineData(false, true, "Talking")]
    [InlineData(true, true, "Talking")]
    public void MicOrSyntheticSpeechKeepsAvatarTalking(bool mic, bool syntheticSpeech, string expected) =>
        Assert.Equal(expected, AvatarTalkStatePolicy.Resolve(mic, syntheticSpeech));

    [Theory]
    [InlineData("Idle", "Idle")]
    [InlineData("idle", "Idle")]
    [InlineData("Talking", "Talking")]
    [InlineData("talking", "Talking")]
    [InlineData("Happy", "Idle")]
    [InlineData("Surprised", "Idle")]
    [InlineData("Blink", "Idle")]
    [InlineData("2.5D", "Idle")]
    [InlineData(null, "Idle")]
    public void OverlayStateIsRestrictedToIdleAndTalking(string? requested, string expected) =>
        Assert.Equal(expected, AvatarTalkStatePolicy.NormalizeVisualState(requested));
}
