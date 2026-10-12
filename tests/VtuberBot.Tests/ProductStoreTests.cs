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
        store.SaveCommand(null, "saludo", "Hola {user}; tenés {points} puntos; dijiste: {args}", 60, true);
        var result = store.RunLocalCommand("!saludo", "PruebaUser", "buenas chat");
        Assert.True(result.Success);
        Assert.Contains("Hola PruebaUser", result.Message);
        Assert.Contains("0 puntos", result.Message);
        Assert.Contains("dijiste: buenas chat", result.Message);
        Assert.Contains(store.GetCommands(), c => c.Name == "saludo" && c.CooldownSeconds == 60);
        Assert.False(store.RunLocalCommand("!saludo", "PruebaUser").Success);
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
        var automation = Assert.Single(store.GetAutomationRules(), rule => rule.Name == "Timer");
        Assert.Equal("Timer", automation.Name);
        Assert.True(automation.Enabled);
        Assert.Null(automation.LastRunUtc);

        var sentAt = DateTimeOffset.Parse("2026-10-12T12:34:56Z");
        store.MarkAutomationRun(automation.Id, sentAt);
        Assert.Equal(sentAt, Assert.Single(store.GetAutomationRules(), rule => rule.Name == "Timer").LastRunUtc);

        // Editing an automation must preserve its persisted anti-spam timestamp.
        store.SaveAutomationRule(automation.Id, "Timer", "Mensaje editado", 15, true);
        var edited = Assert.Single(store.GetAutomationRules(), rule => rule.Name == "Timer");
        Assert.Equal("Mensaje editado", edited.Message);
        Assert.Equal(15, edited.IntervalMinutes);
        Assert.Equal(sentAt, edited.LastRunUtc);

        store.DeleteAutomationRule(automation.Id);
        Assert.DoesNotContain(store.GetAutomationRules(), rule => rule.Id == automation.Id);
        Assert.Throws<KeyNotFoundException>(() => store.DeleteAutomationRule(automation.Id));

        store.SaveModerationRule("URLs", "https?://", true, "flag");
        Assert.Contains(store.GetModerationRules(), r => r.Name == "URLs" && r.Enabled);
    }
    [Fact]
    public void AvatarLayerTransformsAndStackingOrderPersist()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var firstAsset = Path.Combine(temp.DirectoryPath, "first.png");
        var secondAsset = Path.Combine(temp.DirectoryPath, "second.png");
        File.WriteAllBytes(firstAsset, [1, 2, 3]);
        File.WriteAllBytes(secondAsset, [4, 5, 6]);
        var first = store.AddAvatarLayer("Body", firstAsset);
        var second = store.AddAvatarLayer("Hair", secondAsset);
        store.UpdateAvatarLayerTransform(first, 18.5, -12, 1.25, 16);
        store.MoveAvatarLayer(first, 1);
        var layers = store.GetAvatarLayers();
        Assert.Equal(new[] { second, first }, layers.Select(layer => layer.Id));
        var transformed = layers.First(layer => layer.Id == first);
        Assert.Equal(18.5, transformed.OffsetX);
        Assert.Equal(-12, transformed.OffsetY);
        Assert.Equal(1.25, transformed.Scale);
        Assert.Equal(16, transformed.RotationDegrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.UpdateAvatarLayerTransform(first, double.NaN, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.UpdateAvatarLayerTransform(first, 0, 0, 10, 0));
    }

    [Fact]
    public void AvatarLayersPersistVisibilityAndRemoval()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var asset = Path.Combine(temp.DirectoryPath, "layer.png");
        File.WriteAllBytes(asset, [1, 2, 3]);
        var id = store.AddAvatarLayer("Face", asset);
        Assert.Contains(store.GetAvatarLayers(), layer => layer.Id == id && layer.Visible && layer.Name == "Face");
        store.SetAvatarLayerVisibility(id, false);
        Assert.Contains(store.GetAvatarLayers(), layer => layer.Id == id && !layer.Visible);
        store.DeleteAvatarLayer(id);
        Assert.DoesNotContain(store.GetAvatarLayers(), layer => layer.Id == id);
    }

}
