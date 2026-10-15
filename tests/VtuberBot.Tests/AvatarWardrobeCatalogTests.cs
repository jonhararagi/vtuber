using System.Collections;
using System.Reflection;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarWardrobeCatalogTests
{
    [Fact]
    public void BrownSkinAndBrownInnerHairRemainAvailableAsIndependentOptions()
    {
        var factory = Assembly.Load("VtuberBot.App").GetType(
            "VtuberBot.App.ModularWardrobeFactory", throwOnError: true)!;

        var skinIds = ReadOptionIds(factory, "SkinToneOptions");
        var hairIds = ReadOptionIds(factory, "HairOptions");
        var eyeIds = ReadOptionIds(factory, "EyeStyleOptions");

        Assert.Contains("morena", skinIds);
        Assert.Contains("brown", skinIds);
        Assert.Contains("brown-inner-light", hairIds);
        Assert.Contains("brown", eyeIds);
    }

    private static string[] ReadOptionIds(Type factory, string propertyName)
    {
        var property = factory.GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(property);

        var options = Assert.IsAssignableFrom<IEnumerable>(property!.GetValue(null));
        return options.Cast<object>()
            .Select(option => option.GetType().GetProperty("Id")!.GetValue(option)!.ToString()!)
            .ToArray();
    }
}
