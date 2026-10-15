using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarAssetLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "vtuberbot-assets-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string source;

    public AvatarAssetLibraryTests()
    {
        Directory.CreateDirectory(root);
        source = Path.Combine(root, "miel-cafe.png");
        File.WriteAllBytes(source, [137, 80, 78, 71, 13, 10, 26, 10, 0, 1, 2, 3]);
    }

    [Fact]
    public void ImportPersistsAssetWithLicensePendingAndLocalCopy()
    {
        var library = new AvatarAssetLibrary(Path.Combine(root, "library"));

        var asset = library.Import(source, "Personaje base", "morena, castaño", "https://pixai.art/");

        Assert.Equal("Pendiente de revisar", asset.LicenseStatus);
        Assert.False(asset.IsFavorite);
        Assert.True(File.Exists(library.GetAssetPath(asset)));
        Assert.NotEqual(Path.GetFullPath(source), library.GetAssetPath(asset));
        Assert.Single(library.List("morena", "Personaje base"));
    }

    [Fact]
    public void MetadataAndFavoritesSurviveReopeningCatalog()
    {
        var catalogPath = Path.Combine(root, "library");
        var first = new AvatarAssetLibrary(catalogPath);
        var imported = first.Import(source, "Otro", "chibi", null);
        first.UpdateMetadata(imported.Id, "Ropa", "hoodie, violeta", "https://example.com/license", "Uso personal permitido");
        first.ToggleFavorite(imported.Id);

        var reopened = new AvatarAssetLibrary(catalogPath);
        var result = Assert.Single(reopened.List("hoodie", "Ropa", favoritesOnly: true));

        Assert.Equal("Uso personal permitido", result.LicenseStatus);
        Assert.Equal("https://example.com/license", result.SourceUrl);
        Assert.True(result.IsFavorite);
    }

    [Fact]
    public void ImportRejectsUnsupportedExtensionsAndInvalidSourceUrls()
    {
        var library = new AvatarAssetLibrary(Path.Combine(root, "library"));
        var textFile = Path.Combine(root, "not-image.txt");
        File.WriteAllText(textFile, "not an image");

        Assert.Throws<InvalidDataException>(() => library.Import(textFile, "Otro"));
        Assert.Throws<InvalidDataException>(() => library.Import(source, "Otro", sourceUrl: "file:///private/path"));
    }

    [Fact]
    public void CatalogCannotResolveAssetPathsOutsideItsDirectory()
    {
        var library = new AvatarAssetLibrary(Path.Combine(root, "library"));
        var malicious = new AvatarAssetEntry("id", "evil.png", "../outside.png", "Otro", "", "", "No verificado", false, DateTime.UtcNow);

        Assert.Throws<InvalidDataException>(() => library.GetAssetPath(malicious));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
