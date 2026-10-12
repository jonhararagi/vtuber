using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarLayerOrderingTests
{
    [Fact]
    public void BackgroundLayersAreSeparatedAndSortedBeforeCharacter()
    {
        var layers = new[]
        {
            Layer(1, "Pelo", 0),
            Layer(2, "Fondo", 5),
            Layer(3, "Fondo", 1),
            Layer(4, "Ojos", 2),
            Layer(5, " fondo ", 3)
        };

        var backgrounds = AvatarLayerOrdering.BackgroundLayers(layers);
        var foregrounds = AvatarLayerOrdering.ForegroundLayers(layers);

        Assert.Equal(new long[] { 3, 5, 2 }, backgrounds.Select(layer => layer.Id));
        Assert.Equal(new long[] { 1, 4 }, foregrounds.Select(layer => layer.Id));
        Assert.All(backgrounds, layer => Assert.True(AvatarLayerOrdering.IsBackground(layer)));
        Assert.All(foregrounds, layer => Assert.False(AvatarLayerOrdering.IsBackground(layer)));
    }

    [Fact]
    public void BackgroundNameComparisonIsCaseInsensitiveAndRejectsOtherNames()
    {
        Assert.True(AvatarLayerOrdering.IsBackground(Layer(1, "FONDO", 0)));
        Assert.False(AvatarLayerOrdering.IsBackground(Layer(2, "Fondo espacial", 0)));
        Assert.False(AvatarLayerOrdering.IsBackground(Layer(3, "Background", 0)));
    }

    private static AvatarLayer Layer(long id, string name, int sortOrder) =>
        new(id, name, name + ".png", sortOrder, true);
}
