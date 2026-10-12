using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class LocalSoundFormatPolicyTests
{
    [Theory]
    [InlineData("alert.wav")]
    [InlineData("alert.WAV")]
    [InlineData("music.mp3")]
    [InlineData("C:/sounds/intro.Mp3")]
    public void AcceptsSupportedAudioFormats(string path) => Assert.True(LocalSoundFormatPolicy.IsSupported(path));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("alert.exe")]
    [InlineData("image.png")]
    [InlineData("alert.wav.exe")]
    public void RejectsUnsupportedAudioFormats(string path) => Assert.False(LocalSoundFormatPolicy.IsSupported(path));
}
