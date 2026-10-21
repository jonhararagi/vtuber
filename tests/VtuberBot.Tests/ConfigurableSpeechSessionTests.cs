using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ConfigurableSpeechSessionTests
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
            return new SpeechPlaybackResult(false, false);
        }

        public void Dispose() { }
    }

    [Fact]
    public void EmptyVoiceUsesSavedVoicevoxStyleInsteadOfWindowsSapi()
    {
        var windows = new FakeSpeechSession();
        var remote = new FakeSpeechSession();
        var neural = new FakeSpeechSession();
        using var session = new ConfigurableSpeechSession(
            () => false, () => windows, () => remote, () => neural,
            allowWindowsSapi: () => false, defaultLocalVoiceId: () => "voicevox:4");

        var result = session.Speak("Hola", "", "", 0, 100, () => false);

        Assert.False(result.Cancelled);
        Assert.Equal(1, neural.Calls);
        Assert.Equal("voicevox:4", neural.VoiceId);
        Assert.Equal(0, windows.Calls);
    }

    [Fact]
    public void WindowsSapiDoesNotRunWhenExplicitlyDisabledAndNoNeuralVoiceExists()
    {
        var windows = new FakeSpeechSession();
        using var session = new ConfigurableSpeechSession(
            () => false, () => windows, () => new FakeSpeechSession(), () => new FakeSpeechSession(),
            allowWindowsSapi: () => false, defaultLocalVoiceId: () => string.Empty);

        var error = Assert.Throws<InvalidOperationException>(() =>
            session.Speak("Hola", "", "", 0, 100, () => false));

        Assert.Contains("SAPI está desactivada", error.Message);
        Assert.Equal(0, windows.Calls);
    }

    [Fact]
    public void WindowsSapiCanBeEnabledExplicitlyAsACompatibilityFallback()
    {
        var windows = new FakeSpeechSession();
        using var session = new ConfigurableSpeechSession(
            () => false, () => windows, () => new FakeSpeechSession(), () => new FakeSpeechSession(),
            allowWindowsSapi: () => true, defaultLocalVoiceId: () => string.Empty);

        session.Speak("Hola", "", "", 0, 100, () => false);

        Assert.Equal(1, windows.Calls);
    }

    [Fact]
    public void RemoteProviderStillWorksWhenWindowsSapiIsDisabled()
    {
        var windows = new FakeSpeechSession();
        var remote = new FakeSpeechSession();
        using var session = new ConfigurableSpeechSession(
            () => true, () => windows, () => remote, () => new FakeSpeechSession(),
            allowWindowsSapi: () => false, defaultLocalVoiceId: () => string.Empty);

        session.Speak("Hola", "", "", 0, 100, () => false);

        Assert.Equal(1, remote.Calls);
        Assert.Equal(0, windows.Calls);
    }
}
