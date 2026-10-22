using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class StorageMaintenanceServiceTests
{
    private sealed class TempRoot : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("vtuber-storage-").FullName;
        public string DatabasePath => Path.Combine(Root, "product.db");

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void ScanReportsUsageAndOnlySuggestsConservativeUnreferencedOrStaleFiles()
    {
        using var temp = new TempRoot();
        _ = new ProductStore(temp.DatabasePath);

        var avatars = Directory.CreateDirectory(Path.Combine(temp.Root, "avatars")).FullName;
        var sounds = Directory.CreateDirectory(Path.Combine(temp.Root, "sounds")).FullName;
        var catalogDirectory = Directory.CreateDirectory(Path.Combine(avatars, "art-library", "catalog")).FullName;
        var catalogAssets = Directory.CreateDirectory(Path.Combine(catalogDirectory, "assets")).FullName;
        var cache = Directory.CreateDirectory(Path.Combine(temp.Root, "cache")).FullName;
        var logs = Directory.CreateDirectory(Path.Combine(temp.Root, "logs")).FullName;
        var temporary = Directory.CreateDirectory(Path.Combine(temp.Root, "tmp")).FullName;
        var secrets = Directory.CreateDirectory(Path.Combine(temp.Root, "secrets")).FullName;
        var backups = Directory.CreateDirectory(Path.Combine(temp.Root, "backups")).FullName;

        var referencedProfileImage = Write(Path.Combine(avatars, "used-profile.png"), "profile image");
        var orphanImage = Write(Path.Combine(avatars, "orphan.png"), "orphan image");
        var catalogImage = Write(Path.Combine(catalogAssets, "catalog-owned.png"), "catalog asset");
        File.WriteAllText(Path.Combine(catalogDirectory, "catalog.json"),
            """{"entries":[{"RelativePath":"assets/catalog-owned.png"}]}""");

        var registeredSound = Write(Path.Combine(sounds, "registered.wav"), "registered sound");
        var orphanSound = Write(Path.Combine(sounds, "unlisted.mp3"), "unlisted sound");
        var staleCache = Write(Path.Combine(cache, "old.cache"), "stale cache");
        var staleLog = Write(Path.Combine(logs, "old.log"), "stale log");
        var staleTemp = Write(Path.Combine(temporary, "abandoned.tmp"), "temporary");
        var protectedSecret = Write(Path.Combine(secrets, "oauth-token.dpapi"), "protected token");
        var protectedBackup = Write(Path.Combine(backups, "user-backup.vtuberbot.db"), "user backup bytes");

        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        MakeOld(staleCache, now.AddDays(-8).UtcDateTime);
        MakeOld(staleLog, now.AddDays(-31).UtcDateTime);
        MakeOld(staleTemp, now.AddHours(-25).UtcDateTime);
        var inventory = StorageMaintenanceService.Scan(
            temp.Root,
            temp.DatabasePath,
            [referencedProfileImage],
            [registeredSound],
            now);

        var candidates = inventory.CleanupCandidates.Select(item => item.FullPath).ToHashSet(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        Assert.True(inventory.TotalBytes > 0);
        Assert.True(inventory.FileCount >= 8);
        Assert.Contains(inventory.Categories, item => item.Name == "Base SQLite activa");
        Assert.Contains(inventory.Categories, item => item.Name == "Imágenes y recursos de avatar");
        Assert.Contains(inventory.Categories, item => item.Name == "Sonidos");

        Assert.Contains(orphanImage, candidates);
        Assert.Contains(orphanSound, candidates);
        Assert.Contains(staleCache, candidates);
        Assert.Contains(staleLog, candidates);
        Assert.Contains(staleTemp, candidates);

        Assert.DoesNotContain(referencedProfileImage, candidates);
        Assert.DoesNotContain(catalogImage, candidates);
        Assert.DoesNotContain(registeredSound, candidates);
        Assert.DoesNotContain(protectedSecret, candidates);
        Assert.DoesNotContain(protectedBackup, candidates);
        Assert.DoesNotContain(Path.GetFullPath(temp.DatabasePath), candidates);
    }

    [Fact]
    public void DeleteSelectedRequiresAValidPreviewAndNeverDeletesDatabaseOrReferencedAssets()
    {
        using var temp = new TempRoot();
        _ = new ProductStore(temp.DatabasePath);
        var avatars = Directory.CreateDirectory(Path.Combine(temp.Root, "avatars")).FullName;
        var sounds = Directory.CreateDirectory(Path.Combine(temp.Root, "sounds")).FullName;

        var referencedImage = Write(Path.Combine(avatars, "used.png"), "keep");
        var orphan = Write(Path.Combine(avatars, "orphan.png"), "delete me");
        var registeredSound = Write(Path.Combine(sounds, "registered.wav"), "keep sound");
        var orphanSound = Write(Path.Combine(sounds, "orphan.wav"), "unreferenced");
        var inventory = StorageMaintenanceService.Scan(temp.Root, temp.DatabasePath, [referencedImage], [registeredSound]);
        var selected = Assert.Single(inventory.CleanupCandidates, item => item.FullPath == orphan);
        var registeredCandidates = inventory.CleanupCandidates;
        Assert.DoesNotContain(registeredCandidates, item => item.FullPath == referencedImage);
        Assert.DoesNotContain(registeredCandidates, item => item.FullPath == registeredSound);

        var forgedDatabaseCandidate = new StorageCleanupCandidate(
            Path.GetFullPath(temp.DatabasePath),
            Path.GetFileName(temp.DatabasePath),
            "Otros",
            new FileInfo(temp.DatabasePath).Length,
            File.GetLastWriteTimeUtc(temp.DatabasePath),
            "forged selection");
        var result = StorageMaintenanceService.DeleteSelected(
            inventory, [selected, forgedDatabaseCandidate], temp.DatabasePath);

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(selected.SizeBytes, result.BytesFreed);
        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(temp.DatabasePath));
        Assert.True(File.Exists(referencedImage));
        Assert.True(File.Exists(registeredSound));
        Assert.True(File.Exists(orphanSound));
        Assert.Contains(forgedDatabaseCandidate.RelativePath, result.SkippedRelativePaths);
    }

    [Fact]
    public void DamagedAvatarCatalogPreventsOrphanImageSuggestions()
    {
        using var temp = new TempRoot();
        _ = new ProductStore(temp.DatabasePath);
        var catalog = Directory.CreateDirectory(Path.Combine(temp.Root, "avatars", "art-library", "catalog")).FullName;
        var image = Write(Path.Combine(catalog, "assets", "possibly-used.png"), "keep if catalog is damaged");
        File.WriteAllText(Path.Combine(catalog, "catalog.json"), """{"entries":[{"RelativePath":"assets/possibly-used.png""" );

        var inventory = StorageMaintenanceService.Scan(temp.Root, temp.DatabasePath);

        Assert.DoesNotContain(inventory.CleanupCandidates, item => item.FullPath == image);
    }

    [Fact]
    public void DeleteSelectedSkipsAFileChangedAfterPreview()
    {
        using var temp = new TempRoot();
        _ = new ProductStore(temp.DatabasePath);
        var cache = Directory.CreateDirectory(Path.Combine(temp.Root, "cache")).FullName;
        var stale = Write(Path.Combine(cache, "stale.bin"), "first bytes");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-9));
        var inventory = StorageMaintenanceService.Scan(temp.Root, temp.DatabasePath);
        var candidate = Assert.Single(inventory.CleanupCandidates, item => item.FullPath == stale);
        File.AppendAllText(stale, "changed after preview");

        var result = StorageMaintenanceService.DeleteSelected(inventory, [candidate], temp.DatabasePath);

        Assert.Equal(0, result.DeletedCount);
        Assert.Contains(candidate.RelativePath, result.SkippedRelativePaths);
        Assert.True(File.Exists(stale));
    }

    [Fact]
    public void ProductStoreReferenceIndexIncludesInactiveProfilesAndMouthSprites()
    {
        using var temp = new TempRoot();
        var store = new ProductStore(temp.DatabasePath);
        var assets = Directory.CreateDirectory(Path.Combine(temp.Root, "avatars")).FullName;

        var defaultBase = Write(Path.Combine(assets, "default-base.png"), "default base");
        var defaultExpression = Write(Path.Combine(assets, "default-idle.png"), "default expression");
        var defaultLayer = Write(Path.Combine(assets, "default-layer.png"), "default layer");
        var closedMouth = Write(Path.Combine(assets, "mouth-closed.png"), "closed mouth");
        var openMouth = Write(Path.Combine(assets, "mouth-open.png"), "open mouth");
        store.SetActiveAvatarProfileBasePath(defaultBase);
        store.SetAvatarExpression("Idle", defaultExpression);
        var defaultLayerId = store.AddAvatarLayer("default layer", defaultLayer);
        store.SetAvatarLayerMouthSprites(defaultLayerId, closedMouth, openMouth);

        var secondProfile = store.CreateAvatarProfile("Alternate", duplicateActiveProfile: false);
        store.SetActiveAvatarProfile(secondProfile.Id);
        var alternateBase = Write(Path.Combine(assets, "alternate-base.png"), "alternate base");
        var alternateExpression = Write(Path.Combine(assets, "alternate-talk.png"), "alternate expression");
        var alternateLayer = Write(Path.Combine(assets, "alternate-layer.png"), "alternate layer");
        store.SetActiveAvatarProfileBasePath(alternateBase);
        store.SetAvatarExpression("Talking", alternateExpression);
        _ = store.AddAvatarLayer("alternate layer", alternateLayer);

        var referenced = store.GetReferencedAvatarAssetPaths().ToHashSet(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        Assert.Contains(defaultBase, referenced);
        Assert.Contains(defaultExpression, referenced);
        Assert.Contains(defaultLayer, referenced);
        Assert.Contains(closedMouth, referenced);
        Assert.Contains(openMouth, referenced);
        Assert.Contains(alternateBase, referenced);
        Assert.Contains(alternateExpression, referenced);
        Assert.Contains(alternateLayer, referenced);
    }

    private static string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return Path.GetFullPath(path);
    }

    private static void MakeOld(string path, DateTime whenUtc) => File.SetLastWriteTimeUtc(path, whenUtc);
}
