using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarArtPromptCatalogTests
{
    [Fact]
    public void PromptSettingsAreScopedToAvatarProfileAndRole()
    {
        var firstCharacter = AvatarArtPromptCatalog.GetSettingKey(7, "Personaje base");
        var secondCharacter = AvatarArtPromptCatalog.GetSettingKey(8, "Personaje base");

        Assert.NotEqual(firstCharacter, secondCharacter);
        Assert.Equal("avatar.profile.7.art_prompt.personaje_base", firstCharacter);
        Assert.Equal("avatar.profile.7.art_prompt.objeto_capa",
            AvatarArtPromptCatalog.GetSettingKey(7, "Objeto / capa"));
    }

    [Fact]
    public void AllArtRolesHaveStableKeysAndLegacyKeysRemainRecognizable()
    {
        Assert.Equal(8, AvatarArtPromptCatalog.Roles.Count);
        Assert.Equal(AvatarArtPromptCatalog.Roles.Count,
            AvatarArtPromptCatalog.Roles.Select(role => AvatarArtPromptCatalog.GetSettingKey(1, role))
                .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("avatar.art_prompt.personaje_base",
            AvatarArtPromptCatalog.GetLegacySettingKey("Personaje base"));
        Assert.Equal("avatar.art_prompt.objeto_capa",
            AvatarArtPromptCatalog.GetLegacySettingKey("Objeto / capa"));
    }

    [Fact]
    public void InvalidProfileOrRoleIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AvatarArtPromptCatalog.GetSettingKey(0, "Idle"));
        Assert.Throws<ArgumentException>(() => AvatarArtPromptCatalog.GetSettingKey(1, "unknown role"));
        Assert.Throws<ArgumentException>(() => AvatarArtPromptCatalog.GetLegacySettingKey(""));
    }
}
