using Microsoft.Data.Sqlite;
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
    public void CommandsSupportPersistentAliasesAndRejectConflicts()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "welcome", "Hola {user}", 0, true, "global", "bienvenida, saludo");

        var command = Assert.Single(store.GetCommands(), item => item.Name == "welcome");
        Assert.Contains("bienvenida", command.Aliases);
        Assert.Contains("saludo", command.Aliases);
        Assert.True(store.RunLocalCommand("!bienvenida", "alice").Success);
        Assert.True(store.RunLocalCommand("saludo", "bob").Success);

        var reopened = new ProductStore(temp.DatabasePath);
        Assert.True(reopened.RunLocalCommand("!bienvenida", "carol").Success);
        Assert.Throws<ArgumentException>(() => reopened.SaveCommand(null, "otro", "Hola", 0, true, "global", "bienvenida"));
        Assert.Throws<ArgumentException>(() => reopened.SaveCommand(null, "bienvenida", "Hola", 0, true));

        var persisted = reopened.GetCommands().Single(item => item.Name == "welcome");
        reopened.DeleteCommand(persisted.Id);
        Assert.False(reopened.RunLocalCommand("bienvenida", "dave").Success);
        Assert.DoesNotContain(reopened.GetCommands(), item => item.Aliases.Contains("bienvenida", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LegacyCommandMigrationKeepsCustomCanonicalNameWhenMalformedDefaultExists()
    {
        using var temp = new TempDb();
        Directory.CreateDirectory(Path.GetDirectoryName(temp.DatabasePath)!);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = temp.DatabasePath
        }.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE product_commands(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    response TEXT NOT NULL,
                    cooldown_seconds INTEGER NOT NULL DEFAULT 0,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    created_utc TEXT NOT NULL,
                    last_used_utc TEXT NULL);
                INSERT INTO product_commands(name,response,cooldown_seconds,enabled,created_utc)
                VALUES('hola','Respuesta personalizada del usuario',0,1,'2026-01-01T00:00:00Z');
                INSERT INTO product_commands(name,response,cooldown_seconds,enabled,created_utc)
                VALUES('!hola','Respuesta preinstalada antigua',5,1,'2026-01-01T00:00:00Z');
                """;
            command.ExecuteNonQuery();
        }

        var migrated = new ProductStore(temp.DatabasePath);
        var hola = Assert.Single(migrated.GetCommands(), command => command.Name == "hola");
        Assert.Equal("Respuesta personalizada del usuario", hola.Response);
        Assert.DoesNotContain(migrated.GetCommands(), command => command.Name == "!hola");
        var result = migrated.RunLocalCommand("!hola", "alice");
        Assert.True(result.Success);
        Assert.Equal("Respuesta personalizada del usuario", result.Message);
    }

    [Fact]
    public void BuiltInCommandsUseCanonicalNamesWithoutExclamationPrefix()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var commands = store.GetCommands();
        Assert.Contains(commands, item => item.Name == "hola");
        Assert.Contains(commands, item => item.Name == "puntos");
        Assert.Contains(commands, item => item.Name == "dado");
        Assert.DoesNotContain(commands, item => item.Name.StartsWith('!'));
        Assert.True(store.RunLocalCommand("!hola", "alice").Success);
        Assert.True(store.RunLocalCommand("!puntos", "bob").Success);
    }

    [Fact]
    public void PerUserCooldownAllowsDifferentViewersAndPersistsAcrossRestart()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "cooldown-demo", "Hola {user}", 3600, true, "user");

        var aliceFirst = store.RunLocalCommand("cooldown-demo", "alice");
        Assert.True(aliceFirst.Success);
        var aliceSecond = store.RunLocalCommand("cooldown-demo", "alice");
        Assert.False(aliceSecond.Success);
        Assert.Contains("cooldown", aliceSecond.Message, StringComparison.OrdinalIgnoreCase);

        var bobFirst = store.RunLocalCommand("cooldown-demo", "bob");
        Assert.True(bobFirst.Success);
        Assert.Contains("bob", bobFirst.Message);

        var reopened = new ProductStore(temp.DatabasePath);
        Assert.False(reopened.RunLocalCommand("cooldown-demo", "alice").Success);
        Assert.True(reopened.RunLocalCommand("cooldown-demo", "carol").Success);
        Assert.Contains(reopened.GetCommands(), command => command.Name == "cooldown-demo" && command.CooldownScope == "user");
        Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveCommand(null, "invalid", "x", 5, true, "channel"));
    }

    [Fact]
    public void PreviewingEditedCommandDoesNotConsumeCooldownOrCreateUser()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "eco", "Guardado {user}: {args}", 3600, true);

        var preview = store.PreviewCommandResponse("eco", "¡{user} dice: {message}! ({points} pts)", "preview_user", "hola chat");
        Assert.True(preview.Success);
        Assert.Equal("¡preview_user dice: hola chat! (0 pts)", preview.Message);
        Assert.DoesNotContain(store.GetLeaderboard(), user => user.Username == "preview_user");

        var actual = store.RunLocalCommand("eco", "viewer_1", "primer mensaje");
        Assert.True(actual.Success);
        Assert.Contains("primer mensaje", actual.Message);

        // The preview left the command available; the first real execution then starts
        // the persisted command-wide cooldown as intended.
        var blocked = store.RunLocalCommand("eco", "viewer_2", "segundo mensaje");
        Assert.False(blocked.Success);
        Assert.Contains("cooldown", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(store.GetCommands(), command => command.Name == "eco" && command.Response == "Guardado {user}: {args}");
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
        Assert.Throws<InvalidOperationException>(() => store.DrawGiveaway(id));
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
        store.SaveModerationRule("Spam", "(?i)free\\s+money", true, "timeout");
        Assert.Contains(store.GetModerationRules(), r => r.Name == "URLs" && r.Enabled && r.Action == "flag");
        Assert.Contains(store.GetModerationRules(), r => r.Name == "Spam" && r.Enabled && r.Action == "timeout");
        Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveModerationRule("Invalid", ".*", true, "format-disk"));
    }
    [Fact]
    public void DeletingBuiltInAutomationIsPersistentAcrossRestart()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var builtIn = Assert.Single(store.GetAutomationRules(), rule => rule.Name == "Bienvenida");
        store.DeleteAutomationRule(builtIn.Id);

        var reopened = new ProductStore(temp.DatabasePath);
        Assert.DoesNotContain(reopened.GetAutomationRules(), rule => rule.Name == "Bienvenida");
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
