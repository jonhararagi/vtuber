using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class SocialBotPromptCatalogTests
{
    [Fact]
    public void OffersUsefulTwitchAndCharacterProfiles()
    {
        Assert.Contains(SocialBotPromptCatalog.TwitchStreamer, SocialBotPromptCatalog.Profiles);
        Assert.Contains(SocialBotPromptCatalog.AnimeTsundere, SocialBotPromptCatalog.Profiles);
        Assert.Contains(SocialBotPromptCatalog.Custom, SocialBotPromptCatalog.Profiles);
        Assert.Contains("Twitch", SocialBotPromptCatalog.GetInstruction(SocialBotPromptCatalog.TwitchStreamer));
        Assert.Contains("tsundere", SocialBotPromptCatalog.GetInstruction(SocialBotPromptCatalog.AnimeTsundere));
    }

    [Fact]
    public void CreatorInstructionsAreCombinedAndBounded()
    {
        var combined = SocialBotPromptCatalog.BuildCreatorInstructions(
            SocialBotPromptCatalog.TwitchStreamer, "No repitas la misma frase.");
        Assert.Contains("jerga de Twitch", combined);
        Assert.Contains("No repitas la misma frase.", combined);
        Assert.True(SocialBotPromptCatalog.BuildCreatorInstructions(
            SocialBotPromptCatalog.Custom, new string('x', 4000)).Length <= 3000);
    }

    [Fact]
    public void UnknownAndCustomProfilesDoNotInjectAnUnexpectedPreset()
    {
        Assert.Equal("", SocialBotPromptCatalog.GetInstruction(SocialBotPromptCatalog.Custom));
        Assert.Equal("", SocialBotPromptCatalog.GetInstruction("perfil inventado"));
    }
}
