using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ChatArchiveStoreTests
{
    private sealed class TempDb : IDisposable
    {
        public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("vtuber-chat-archive-").FullName;
        public string DatabasePath => Path.Combine(DirectoryPath, "chat-history.sqlite3");
        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void StoresPublicMessagesAndSearchesCaseInsensitively()
    {
        using var temp = new TempDb();
        var store = new ChatArchiveStore(temp.DatabasePath);
        var timestamp = DateTimeOffset.UtcNow;

        Assert.True(store.TryAdd(new ChatArchiveMessage("Twitch", "MyChannel", timestamp, "Alice", "Hola comunidad", "t-1")));
        Assert.True(store.TryAdd(new ChatArchiveMessage("Kick", "my-kick-channel", timestamp.AddSeconds(1), "Bob", "¡Buenas!", "k-1")));

        var all = store.Search(limit: 200);
        Assert.Equal(2, all.Count);
        Assert.Equal("Bob", all[0].Username);
        Assert.Single(store.Search("ALICE", "Twitch"));
        Assert.Single(store.Search("buenas", "Kick"));
        var stats = store.GetStats();
        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Twitch);
        Assert.Equal(1, stats.Kick);
    }

    [Fact]
    public void DeduplicatesProviderMessageIdsPerPlatformAndChannel()
    {
        using var temp = new TempDb();
        var store = new ChatArchiveStore(temp.DatabasePath);
        var timestamp = DateTimeOffset.UtcNow;

        Assert.True(store.TryAdd(new ChatArchiveMessage("Twitch", "channel", timestamp, "Alice", "same", "message-1")));
        Assert.False(store.TryAdd(new ChatArchiveMessage("Twitch", "channel", timestamp.AddSeconds(1), "Alice", "duplicate", "message-1")));
        Assert.True(store.TryAdd(new ChatArchiveMessage("Kick", "channel", timestamp, "Alice", "another provider", "message-1")));
        Assert.Equal(2, store.GetStats().Total);
    }

    [Fact]
    public void RetentionAndCapacityPruningKeepOnlyRecentRecords()
    {
        using var temp = new TempDb();
        var store = new ChatArchiveStore(temp.DatabasePath);
        var now = DateTimeOffset.UtcNow;

        store.TryAdd(new ChatArchiveMessage("Twitch", "channel", now.AddDays(-10), "Old", "expired", "old-id"));
        store.TryAdd(new ChatArchiveMessage("Twitch", "channel", now, "New", "keep", "new-id"));

        Assert.Equal(1, store.Prune(retentionDays: 3, maximumRows: 10));
        Assert.Single(store.Search());
        Assert.Equal("New", store.Search()[0].Username);

        store.TryAdd(new ChatArchiveMessage("Kick", "channel", now.AddSeconds(1), "Newest", "newest", "last-id"));
        Assert.Equal(1, store.Prune(retentionDays: 30, maximumRows: 1));
        Assert.Single(store.Search());
        Assert.Equal("Newest", store.Search()[0].Username);
    }

    [Fact]
    public void EnforcesSupportedPlatformsAndRetentionLimits()
    {
        using var temp = new TempDb();
        var store = new ChatArchiveStore(temp.DatabasePath);
        var timestamp = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.TryAdd(new ChatArchiveMessage("Discord", "channel", timestamp, "Alice", "not supported")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.TryAdd(new ChatArchiveMessage("Twitch", "channel", timestamp, "Alice", new string('x', 1_001))));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Prune(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Prune(366));
        Assert.Equal(0, store.GetStats().Total);
    }

    [Fact]
    public void CanDeleteAllArchivedMessagesWithoutAffectingOtherDataFiles()
    {
        using var temp = new TempDb();
        var store = new ChatArchiveStore(temp.DatabasePath);
        var markerPath = Path.Combine(temp.DirectoryPath, "other-data.txt");
        File.WriteAllText(markerPath, "keep");

        store.TryAdd(new ChatArchiveMessage("Kick", "channel", DateTimeOffset.UtcNow, "Alice", "hello", "m-1"));
        Assert.Equal(1, store.DeleteAll());
        Assert.Equal(0, store.GetStats().Total);
        Assert.Equal("keep", File.ReadAllText(markerPath));
    }
}
