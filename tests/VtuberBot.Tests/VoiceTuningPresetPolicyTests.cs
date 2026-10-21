using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class VoiceTuningPresetPolicyTests
{
    [Fact]
    public void PresetsContainNaturalHighLowAndEnergeticModes()
    {
        Assert.Contains(VoiceTuningPresetPolicy.Presets, preset => preset.Id == "natural");
        Assert.Contains(VoiceTuningPresetPolicy.Presets, preset => preset.Id == "anime-high");
        Assert.Contains(VoiceTuningPresetPolicy.Presets, preset => preset.Id == "anime-low");
        Assert.Contains(VoiceTuningPresetPolicy.Presets, preset => preset.Id == "anime-energy");
    }

    [Fact]
    public void HighAndLowPresetsMovePitchInOppositeDirections()
    {
        var natural = VoiceTuningPresetPolicy.FromPreset(VoiceTuningPresetPolicy.GetPreset("natural"));
        var high = VoiceTuningPresetPolicy.FromPreset(VoiceTuningPresetPolicy.GetPreset("anime-high"));
        var low = VoiceTuningPresetPolicy.FromPreset(VoiceTuningPresetPolicy.GetPreset("anime-low"));

        Assert.Equal(0d, natural.PitchScale);
        Assert.True(high.PitchScale > natural.PitchScale);
        Assert.True(low.PitchScale < natural.PitchScale);
        Assert.True(high.IntonationScale > natural.IntonationScale);
    }

    [Fact]
    public void NormalizeClampsUnsafeValuesAndRepairsNonFiniteNumbers()
    {
        var profile = VoiceTuningPresetPolicy.Normalize(new VoiceTuningProfile(double.PositiveInfinity, -10, 50, -5));

        Assert.Equal(0d, profile.PitchScale);
        Assert.Equal(0.5d, profile.IntonationScale);
        Assert.Equal(10, profile.Rate);
        Assert.Equal(0, profile.Volume);
    }

    [Theory]
    [InlineData("voicevox:0", "audio.voicevox.profile.0")]
    [InlineData("VOICEVOX:12", "audio.voicevox.profile.12")]
    public void SettingKeyIsBoundToSpeakerId(string voiceId, string expected) =>
        Assert.Equal(expected, VoiceTuningPresetPolicy.GetSettingKey(voiceId));

    [Theory]
    [InlineData("sapi:voice")]
    [InlineData("voicevox:-1")]
    [InlineData("")]
    public void SettingKeyRejectsNonVoicevoxIds(string voiceId) =>
        Assert.Throws<ArgumentException>(() => VoiceTuningPresetPolicy.GetSettingKey(voiceId));

    [Fact]
    public void SerializedProfilesRoundTripWithValidation()
    {
        var original = new VoiceTuningProfile(0.21, 1.3, 2, 87);
        var restored = VoiceTuningPresetPolicy.TryDeserialize(VoiceTuningPresetPolicy.Serialize(original));

        Assert.Equal(original, restored);
        Assert.Null(VoiceTuningPresetPolicy.TryDeserialize("{invalid"));
    }
}
