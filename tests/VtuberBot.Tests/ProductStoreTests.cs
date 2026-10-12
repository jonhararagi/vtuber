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
        store.SaveCommand(null, "welcome", "Hola {user}", 0, true, "global", "bienvenida, saludo", "subscriber");

        var command = Assert.Single(store.GetCommands(), item => item.Name == "welcome");
        Assert.Contains("bienvenida", command.Aliases);
        Assert.Contains("saludo", command.Aliases);
        Assert.Equal("subscriber", command.PermissionScope);
        Assert.False(store.RunLocalCommand("!bienvenida", "viewer").Success);
        Assert.True(store.RunLocalCommand("!bienvenida", "alice", userRoles: new[] { "subscriber" }).Success);
        Assert.True(store.RunLocalCommand("saludo", "bob", userRoles: new[] { "subscriber" }).Success);

        var reopened = new ProductStore(temp.DatabasePath);
        Assert.True(reopened.RunLocalCommand("!bienvenida", "carol", userRoles: new[] { "moderator" }).Success);
        Assert.Throws<ArgumentException>(() => reopened.SaveCommand(null, "otro", "Hola", 0, true, "global", "bienvenida"));
        Assert.Throws<ArgumentException>(() => reopened.SaveCommand(null, "bienvenida", "Hola", 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => reopened.SaveCommand(null, "bad-permission", "Hola", 0, true, "global", "", "owner"));

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
    public void PreviewChecksConfiguredPermissionUsingSimulatedRolesWithoutCreatingUser()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "staff-only", "Hola {user}", 3600, true, "global", "", "moderator");

        var denied = store.PreviewCommandResponse(
            "staff-only", "Hola {user}", "preview_staff", "", "moderator", Array.Empty<string>());
        Assert.False(denied.Success);
        Assert.Contains("permiso", denied.Message, StringComparison.OrdinalIgnoreCase);

        var allowed = store.PreviewCommandResponse(
            "staff-only", "Hola {user}", "preview_staff", "", "moderator", new[] { "moderator" });
        Assert.True(allowed.Success);
        Assert.Equal("Hola preview_staff", allowed.Message);
        Assert.DoesNotContain(store.GetLeaderboard(), user => user.Username == "preview_staff");

        // Preview never uses the persisted command record to start its cooldown.
        Assert.True(store.RunLocalCommand("staff-only", "preview_staff", "", new[] { "moderator" }).Success);
    }

    [Fact]
    public void PreviewRejectsUnknownPermissionScope()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var result = store.PreviewCommandResponse("demo", "Hello", "preview_user", "", "owner", new[] { "broadcaster" });
        Assert.False(result.Success);
        Assert.Contains("permiso", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public void BackupAndRestorePreserveDatabaseAndCreateEmergencyCopy()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "before", "Antes {user}", 0, true);
        store.SetSetting("backup.test.marker", "from-backup");
        var backupPath = Path.Combine(temp.DirectoryPath, "manual-backup.vtuberbot.db");

        Assert.Equal(backupPath, store.BackupTo(backupPath));
        Assert.True(File.Exists(backupPath));

        store.SaveCommand(null, "after", "Después {user}", 0, true);
        store.SetSetting("backup.test.marker", "before-restore");
        var emergencyPath = store.RestoreFromBackup(backupPath);

        Assert.True(File.Exists(emergencyPath));
        Assert.Contains(store.GetCommands(), command => command.Name == "before");
        Assert.DoesNotContain(store.GetCommands(), command => command.Name == "after");
        Assert.Equal("from-backup", store.GetSetting("backup.test.marker"));

        var emergencyStore = new ProductStore(emergencyPath);
        Assert.Contains(emergencyStore.GetCommands(), command => command.Name == "after");
        Assert.Equal("before-restore", emergencyStore.GetSetting("backup.test.marker"));
    }

    [Fact]
    public void InvalidBackupIsRejectedWithoutChangingTheActiveDatabase()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SaveCommand(null, "keep", "No perder", 0, true);
        var invalidBackup = Path.Combine(temp.DirectoryPath, "not-a-database.db");
        File.WriteAllText(invalidBackup, "esto no es SQLite");

        Assert.Throws<InvalidDataException>(() => store.RestoreFromBackup(invalidBackup));
        Assert.Contains(store.GetCommands(), command => command.Name == "keep");
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
    public void OnlyOneGiveawayCanBeOpenAtATime()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var firstId = store.CreateGiveaway("First");
        var exception = Assert.Throws<InvalidOperationException>(() => store.CreateGiveaway("Second"));

        Assert.Contains($"#{firstId}", exception.Message);
        Assert.True(store.EnterGiveaway(firstId, "alice"));
        Assert.True(store.EnterGiveaway(firstId, "bob"));
        var result = store.DrawGiveaway(firstId);
        Assert.Equal("CLOSED", result.Status);

        var nextId = store.CreateGiveaway("Second");
        Assert.NotEqual(firstId, nextId);
        Assert.Equal("OPEN", Assert.Single(store.GetGiveaways(), giveaway => giveaway.Id == nextId).Status);
    }

    [Fact]
    public void GiveawayCanBeCancelledWithoutDrawingAndKeepsEntriesForAudit()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var id = store.CreateGiveaway("Cancelled raffle");
        Assert.True(store.EnterGiveaway(id, "alice"));

        store.CancelGiveaway(id);

        var cancelled = Assert.Single(store.GetGiveaways(), giveaway => giveaway.Id == id);
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal(1, cancelled.Entries);
        Assert.False(store.EnterGiveaway(id, "bob"));
        Assert.Throws<InvalidOperationException>(() => store.DrawGiveaway(id));
        Assert.Throws<InvalidOperationException>(() => store.CancelGiveaway(id));

        var next = store.CreateGiveaway("Replacement");
        Assert.Equal("OPEN", Assert.Single(store.GetGiveaways(), giveaway => giveaway.Id == next).Status);
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
    public void LegacyAvatarAssetLayersAndExpressionsMigrateIntoDefaultProfile()
    {
        using var temp = new TempDb();
        Directory.CreateDirectory(Path.GetDirectoryName(temp.DatabasePath)!);
        var legacyBase = Path.Combine(temp.DirectoryPath, "legacy-base.png");
        var legacyLayer = Path.Combine(temp.DirectoryPath, "legacy-layer.png");
        var legacyTalking = Path.Combine(temp.DirectoryPath, "legacy-talking.png");
        foreach (var path in new[] { legacyBase, legacyLayer, legacyTalking })
            File.WriteAllBytes(path, [1, 2, 3]);

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = temp.DatabasePath,
            Pooling = false
        }.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE product_settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                INSERT INTO product_settings(key,value) VALUES('avatar.path',$base);
                INSERT INTO product_settings(key,value) VALUES('avatar.expression.Talking',$talking);
                CREATE TABLE avatar_layers(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    path TEXT NOT NULL,
                    sort_order INTEGER NOT NULL,
                    visible INTEGER NOT NULL DEFAULT 1);
                INSERT INTO avatar_layers(name,path,sort_order,visible)
                VALUES('Pelo heredado',$layer,0,1);
                """;
            command.Parameters.AddWithValue("$base", legacyBase);
            command.Parameters.AddWithValue("$talking", legacyTalking);
            command.Parameters.AddWithValue("$layer", legacyLayer);
            command.ExecuteNonQuery();
        }

        var store = new ProductStore(temp.DatabasePath);
        var active = store.GetActiveAvatarProfile();
        Assert.Equal("Avatar principal", active.Name);
        Assert.Equal(Path.GetFullPath(legacyBase), active.BasePath);
        Assert.Collection(store.GetAvatarLayers(), layer =>
        {
            Assert.Equal("Pelo heredado", layer.Name);
            Assert.Equal(Path.GetFullPath(legacyLayer), layer.Path);
        });
        Assert.Equal(Path.GetFullPath(legacyTalking), store.GetAvatarExpression("Talking"));
    }

    [Fact]
    public void AvatarProfilesIsolateBaseLayersAndExpressionsAcrossSwitches()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var baseOne = Path.Combine(temp.DirectoryPath, "base-one.png");
        var layerOne = Path.Combine(temp.DirectoryPath, "layer-one.png");
        var baseTwo = Path.Combine(temp.DirectoryPath, "base-two.png");
        var layerTwo = Path.Combine(temp.DirectoryPath, "layer-two.png");
        var expressionOne = Path.Combine(temp.DirectoryPath, "talk-one.png");
        var expressionTwo = Path.Combine(temp.DirectoryPath, "talk-two.png");
        foreach (var path in new[] { baseOne, layerOne, baseTwo, layerTwo, expressionOne, expressionTwo })
            File.WriteAllBytes(path, [1, 2, 3]);

        var firstProfile = store.GetActiveAvatarProfile();
        store.SetActiveAvatarProfileBasePath(baseOne);
        var firstLayer = store.AddAvatarLayer("Sombrero", layerOne);
        store.SetAvatarExpression("Talking", expressionOne);

        var secondProfile = store.CreateAvatarProfile("Avatar alternativo", duplicateActiveProfile: false);
        store.SetActiveAvatarProfile(secondProfile.Id);
        Assert.Null(store.GetActiveAvatarProfile().BasePath);
        Assert.Empty(store.GetAvatarLayers());
        Assert.Null(store.GetAvatarExpression("Talking"));

        store.SetActiveAvatarProfileBasePath(baseTwo);
        var secondLayer = store.AddAvatarLayer("Capa alternativa", layerTwo);
        store.SetAvatarExpression("Talking", expressionTwo);

        store.SetActiveAvatarProfile(firstProfile.Id);
        Assert.Equal(Path.GetFullPath(baseOne), store.GetActiveAvatarProfile().BasePath);
        Assert.Collection(store.GetAvatarLayers(), layer => Assert.Equal(firstLayer, layer.Id));
        Assert.Equal(Path.GetFullPath(expressionOne), store.GetAvatarExpression("Talking"));

        store.SetActiveAvatarProfile(secondProfile.Id);
        Assert.Equal(Path.GetFullPath(baseTwo), store.GetActiveAvatarProfile().BasePath);
        Assert.Collection(store.GetAvatarLayers(), layer => Assert.Equal(secondLayer, layer.Id));
        Assert.Equal(Path.GetFullPath(expressionTwo), store.GetAvatarExpression("Talking"));
    }

    [Fact]
    public void DuplicatingAvatarProfileCopiesItsCompositionWithoutSharingLayerRecords()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var baseImage = Path.Combine(temp.DirectoryPath, "base.png");
        var layerImage = Path.Combine(temp.DirectoryPath, "layer.png");
        var talkingImage = Path.Combine(temp.DirectoryPath, "talking.png");
        foreach (var path in new[] { baseImage, layerImage, talkingImage })
            File.WriteAllBytes(path, [1, 2, 3]);

        store.SetActiveAvatarProfileBasePath(baseImage);
        var originalLayer = store.AddAvatarLayer("Pelo", layerImage);
        store.UpdateAvatarLayerTransform(originalLayer, 12, -7, 1.2, 4);
        store.SetAvatarExpression("Talking", talkingImage);

        var duplicate = store.CreateAvatarProfile("Copia de prueba", duplicateActiveProfile: true);
        store.SetActiveAvatarProfile(duplicate.Id);
        Assert.Equal(Path.GetFullPath(baseImage), store.GetActiveAvatarProfile().BasePath);
        var clonedLayer = Assert.Single(store.GetAvatarLayers());
        Assert.NotEqual(originalLayer, clonedLayer.Id);
        Assert.Equal("Pelo", clonedLayer.Name);
        Assert.Equal(12, clonedLayer.OffsetX);
        Assert.Equal(-7, clonedLayer.OffsetY);
        Assert.Equal(1.2, clonedLayer.Scale);
        Assert.Equal(Path.GetFullPath(talkingImage), store.GetAvatarExpression("Talking"));

        store.SetAvatarLayerVisibility(clonedLayer.Id, false);
        store.SetActiveAvatarProfile(1);
        Assert.True(Assert.Single(store.GetAvatarLayers()).Visible);
    }

    [Fact]
    public void AvatarProfileCannotDeleteLastProfileAndDeletingActiveSelectsAnother()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var original = store.GetActiveAvatarProfile();
        Assert.Throws<InvalidOperationException>(() => store.DeleteAvatarProfile(original.Id));

        var secondary = store.CreateAvatarProfile("Para eliminar", duplicateActiveProfile: false);
        store.SetActiveAvatarProfile(secondary.Id);
        var activeAfterDelete = store.DeleteAvatarProfile(secondary.Id);
        Assert.Equal(original.Id, activeAfterDelete.Id);
        Assert.Equal(original.Id, store.GetActiveAvatarProfile().Id);
        Assert.Single(store.GetAvatarProfiles());
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

    [Fact]
    public void FreeMinigamePracticeDoesNotCreateOrAwardPoints()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var result = store.PlayGame("Duelo de moneda", "practice_user", 0, "Cara");

        Assert.Contains("Práctica gratuita", result);
        Assert.Equal(0, store.TouchUser("practice_user").Points);
        Assert.Contains(store.GetActivity(20), activity => activity.Module == "Minijuegos");
    }

    [Fact]
    public void MinigameStakeAndPayoutAreSettledWithoutAllowingNegativeBalance()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.AddPoints("alice", 100, "Saldo de prueba");

        var result = store.PlayGame("Duelo de moneda", "alice", 10, "Cara");

        Assert.Contains("Saldo final:", result);
        Assert.Contains(store.TouchUser("alice").Points, new long[] { 90, 110 });
    }

    [Fact]
    public void InsufficientMinigameBalanceLeavesPointsUnchanged()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.AddPoints("alice", 5, "Saldo de prueba");

        var exception = Assert.Throws<InvalidOperationException>(
            () => store.PlayGame("Dado", "alice", 10, "Bajo (1-3)"));

        Assert.Contains("Saldo insuficiente", exception.Message);
        Assert.Equal(5, store.TouchUser("alice").Points);
    }
}
