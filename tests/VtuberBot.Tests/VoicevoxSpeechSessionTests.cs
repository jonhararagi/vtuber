using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoicevoxSpeechSessionTests
{
    private sealed class FakeSpeechSession : ILocalSpeechSession
    {
        public int Calls { get; private set; }
        public string VoiceId { get; private set; } = string.Empty;

        public SpeechPlaybackResult Speak(string text, string requestedVoiceId, string requestedOutputId,
            int rate, int volume, Func<bool> cancellationRequested)
        {
            Calls++;
            VoiceId = requestedVoiceId;
            return new(false, false);
        }

        public void Dispose() { }
    }

    [Theory]
    [InlineData("voicevox:0", true)]
    [InlineData("VOICEVOX:12", true)]
    [InlineData("voicevox:-1", false)]
    [InlineData("voicevox:abc", false)]
    [InlineData("sapi:voice", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVoiceIdAcceptsOnlyNonNegativeSpeakerIds(string? id, bool expected) =>
        Assert.Equal(expected, VoicevoxSpeechSession.IsVoiceId(id));

    [Fact]
    public void ConfigurableSessionRoutesVoicevoxRuleToOfflineProviderEvenWhenRemoteIsDefault()
    {
        var local = new FakeSpeechSession();
        var remote = new FakeSpeechSession();
        var voicevox = new FakeSpeechSession();
        using var session = new ConfigurableSpeechSession(
            () => true,
            () => local,
            () => remote,
            () => voicevox);

        var result = session.Speak("お兄ちゃんのばか！", "voicevox:3", "", 0, 100, () => false);

        Assert.False(result.Cancelled);
        Assert.Equal(1, voicevox.Calls);
        Assert.Equal("voicevox:3", voicevox.VoiceId);
        Assert.Equal(0, remote.Calls);
        Assert.Equal(0, local.Calls);
    }
}
