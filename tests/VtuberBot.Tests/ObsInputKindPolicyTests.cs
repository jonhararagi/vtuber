using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsInputKindPolicyTests
{
    [Theory]
    [InlineData("wasapi_input_capture")]
    [InlineData("wasapi_output_capture")]
    [InlineData("pulse_input_capture")]
    [InlineData("coreaudio_input_capture")]
    [InlineData("ffmpeg_source")]
    [InlineData("browser_source")]
    public void AudioCapableKindsAreIncluded(string inputKind) =>
        Assert.True(ObsInputKindPolicy.IsPotentialAudioInput(inputKind));

    [Theory]
    [InlineData("monitor_capture")]
    [InlineData("game_capture")]
    [InlineData("scene")]
    [InlineData("")]
    [InlineData("  ")]
    public void NonAudioKindsAreExcluded(string inputKind) =>
        Assert.False(ObsInputKindPolicy.IsPotentialAudioInput(inputKind));

    [Fact]
    public void KindMatchingIgnoresCaseAndWhitespace() =>
        Assert.True(ObsInputKindPolicy.IsPotentialAudioInput("  WASAPI_INPUT_CAPTURE  "));
}
