using Xunit;
using VtuberBot.App;

namespace VtuberBot.Tests;

public sealed class AudioPreferenceSelectionPolicyTests
{
    [Theory]
    [InlineData(null, "saved-voice", false, "saved-voice")]
    [InlineData("", "saved-device", false, "saved-device")]
    [InlineData("new-selection", "saved-selection", true, "new-selection")]
    [InlineData("", "saved-selection", true, "")]
    public void ResolvePreservesSavedChoiceUntilCatalogIsLoaded(
        string? selected,
        string? saved,
        bool catalogLoaded,
        string expected) =>
        Assert.Equal(expected, AudioPreferenceSelectionPolicy.Resolve(selected, saved, catalogLoaded));

    [Fact]
    public void ResolveReturnsEmptyWhenLoadedCatalogExplicitlySelectsDefault() =>
        Assert.Equal(string.Empty, AudioPreferenceSelectionPolicy.Resolve(null, "saved-selection", true));
}
