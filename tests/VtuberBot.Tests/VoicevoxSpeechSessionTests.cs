using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoicevoxSpeechSessionTests
{
    [Fact]
    public void SynthesisWaitIsBoundedToOneMinute() =>
        Assert.Equal(60, VoicevoxSpeechSession.SynthesisTimeoutSeconds);

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

    [Theory]
    [InlineData(-0.5, -0.3)]
    [InlineData(-0.2, -0.2)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.2, 0.2)]
    [InlineData(0.5, 0.3)]
    public void NormalizePitchScaleClampsToEngineRange(double value, double expected) =>
        Assert.Equal(expected, VoicevoxSpeechSession.NormalizePitchScale(value), 5);

    [Fact]
    public void NormalizePitchScaleHandlesNonFiniteValues() =>
        Assert.Equal(0, VoicevoxSpeechSession.NormalizePitchScale(double.NaN));

    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(0.8, 0.8)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.2, 1.2)]
    [InlineData(2.0, 1.5)]
    public void NormalizeIntonationScaleClampsToEngineRange(double value, double expected) =>
        Assert.Equal(expected, VoicevoxSpeechSession.NormalizeIntonationScale(value), 5);

    [Fact]
    public void NormalizeIntonationScaleHandlesNonFiniteValues() =>
        Assert.Equal(1, VoicevoxSpeechSession.NormalizeIntonationScale(double.PositiveInfinity));

    [Theory]
    [InlineData(null, 0, -1)]
    [InlineData("", 2, -1)]
    [InlineData("sapi:legacy-device", 3, -1)]
    [InlineData("waveout:0", 1, 0)]
    [InlineData("WAVEOUT:2", 3, 2)]
    public void ResolveOutputDeviceNumberUsesNAudioDeviceIds(string? outputId, int availableDevices, int expected) =>
        Assert.Equal(expected, VoicevoxSpeechSession.ResolveOutputDeviceNumber(outputId, availableDevices));

    [Theory]
    [InlineData("waveout:bad", 2)]
    [InlineData("waveout:-1", 2)]
    [InlineData("waveout:2", 2)]
    public void ResolveOutputDeviceNumberRejectsInvalidOrStaleExplicitDeviceIds(string outputId, int availableDevices) =>
        Assert.Throws<InvalidOperationException>(() =>
            VoicevoxSpeechSession.ResolveOutputDeviceNumber(outputId, availableDevices));

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
