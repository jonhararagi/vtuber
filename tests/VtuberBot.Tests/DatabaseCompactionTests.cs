using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class DatabaseCompactionTests
{
    [Fact]
    public void CompactionCreatesVerifiedBackupAndPreservesDatabaseSettings()
    {
        using var root = Directory.CreateTempSubdirectory("vtuber-vacuum-");
        var databasePath = Path.Combine(root.FullName, "product.db");
        var backupPath = Path.Combine(root.FullName, "backups", "before-vacuum.vtuberbot.db");
        var store = new ProductStore(databasePath);
        store.SetSetting("storage.compaction.test", "keep-me");
        store.SaveCommand(null, "compaction-test", "persistent command", 0, true);

        var result = store.CompactDatabase(backupPath);

        Assert.True(File.Exists(databasePath));
        Assert.True(File.Exists(result.BackupPath));
        Assert.True(result.SizeBeforeBytes > 0);
        Assert.True(result.SizeAfterBytes > 0);
        Assert.Equal(Path.GetFullPath(backupPath), result.BackupPath);
        Assert.Equal("keep-me", store.GetSetting("storage.compaction.test"));
        Assert.Contains(store.GetCommands(), command => command.Name == "compaction-test");

        var backupStore = new ProductStore(result.BackupPath);
        Assert.Equal("keep-me", backupStore.GetSetting("storage.compaction.test"));
        Assert.Contains(backupStore.GetCommands(), command => command.Name == "compaction-test");
    }

    [Fact]
    public void CompactionRejectsEmptyBackupPathBeforeChangingDatabase()
    {
        using var root = Directory.CreateTempSubdirectory("vtuber-vacuum-invalid-");
        var databasePath = Path.Combine(root.FullName, "product.db");
        var store = new ProductStore(databasePath);
        store.SetSetting("compaction.test", "untouched");

        Assert.Throws<ArgumentException>(() => store.CompactDatabase(" "));
        Assert.Equal("untouched", store.GetSetting("compaction.test"));
    }
}
