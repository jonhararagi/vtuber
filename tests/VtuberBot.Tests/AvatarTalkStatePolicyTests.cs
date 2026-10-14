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
}
