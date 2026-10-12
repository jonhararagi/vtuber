using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ProductStoreTests
{
    private sealed class TempDb : IDisposable
    {
        public string DirectoryPath { get; } = System.IO.Directory.CreateTempSubdirectory("vtuber-product-").FullName;
        public string DatabasePath => Path.Combine(DirectoryPath, "data", "product.db");
        public void Dispose() { try { System.IO.Directory.Delete(DirectoryPath, true); } catch { } }
    }

    [Fact]
    public void CommandsPersistAndRunLocally()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "saludo", "Hola {user}; tenés {points} puntos", 2, true);
        var result = store.RunLocalCommand("!saludo", "PruebaUser");
        Assert.True(result.Success);
        Assert.Contains("Hola PruebaUser", result.Message);
        Assert.Contains("0 puntos", result.Message);
        Assert.Contains(store.GetCommands(), c => c.Name == "saludo" && c.CooldownSeconds == 2);
    }

    [Fact]
    public void PointsNeverBecomeNegativeAndLeaderboardPersists()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.AddPoints("viewer_1", 20, "test");
        var user = store.AddPoints("viewer_1", -100, "test debit");
        Assert.Equal(0, user.Points);
        Assert.Contains(store.GetLeaderboard(), u => u.Username == "viewer_1" && u.Points == 0);
    }

    [Fact]
    public void GiveawayDeduplicatesEntriesAndRecordsWinner()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var id = store.CreateGiveaway("Test giveaway");
        Assert.True(store.EnterGiveaway(id, "alice"));
        Assert.False(store.EnterGiveaway(id, "alice"));
        Assert.True(store.EnterGiveaway(id, "bob"));
        var result = store.DrawGiveaway(id);
        Assert.Equal("CLOSED", result.Status);
        Assert.Contains(result.Winner, new string?[] { "alice", "bob" });
        Assert.Equal(2, result.Entries);
    }

    [Fact]
    public void AutomationAndModerationRulesPersist()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveAutomationRule(null, "Timer", "Mensaje de prueba", 10, true);
        Assert.Contains(store.GetAutomationRules(), r => r.Name == "Timer" && r.Enabled);
        store.SaveModerationRule("URLs", "https?://", true, "flag");
        Assert.Contains(store.GetModerationRules(), r => r.Name == "URLs" && r.Enabled);
    }
}
