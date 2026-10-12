using Microsoft.Data.Sqlite;

namespace VtuberBot.Core;

public sealed record ProductCommand(long Id, string Name, string Response, int CooldownSeconds, bool Enabled, string CooldownScope = "global", string Aliases = "", string PermissionScope = "everyone");
public sealed record CommunityUser(string Username, long Points, DateTimeOffset LastSeen);
public sealed record GiveawayRecord(long Id, string Name, string Status, string? Winner, int Entries);
public sealed record ProductActivity(DateTimeOffset At, string Module, string Message);
public sealed record AutomationRule(long Id, string Name, string Message, int IntervalMinutes, bool Enabled, DateTimeOffset? LastRunUtc = null);
public sealed record AvatarProfile(long Id, string Name, string? BasePath, DateTimeOffset CreatedUtc);
public sealed record AvatarLayer(long Id, string Name, string Path, int SortOrder, bool Visible, double OffsetX = 0, double OffsetY = 0, double Scale = 1, double RotationDegrees = 0);

/// <summary>Persistence for product-level features. Uses the same local SQLite file as the core.</summary>
public sealed class ProductStore
{
    private readonly string connectionString;
    private readonly object gate = new();

    private static readonly IReadOnlyDictionary<string, string[]> RequiredBackupColumns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["product_commands"] = ["id","name","response","cooldown_seconds","enabled","created_utc","last_used_utc","cooldown_scope","aliases","permission_scope"],
            ["product_command_user_cooldowns"] = ["command_id","username","last_used_utc"],
            ["community_users"] = ["username","points","first_seen_utc","last_seen_utc"],
            ["point_ledger"] = ["id","username","delta","reason","created_utc"],
            ["giveaways"] = ["id","name","status","winner","created_utc","closed_utc"],
            ["giveaway_entries"] = ["giveaway_id","username","entered_utc"],
            ["game_sessions"] = ["id","game","username","result","reward","created_utc"],
            ["moderation_rules"] = ["id","name","pattern","enabled","action"],
            ["automation_rules"] = ["id","name","message","interval_minutes","enabled","last_run_utc"],
            ["product_activity"] = ["id","at_utc","module","message"],
            ["avatar_profiles"] = ["id","name","base_path","created_utc"],
            ["avatar_profile_expressions"] = ["profile_id","state","path"],
            ["avatar_layers"] = ["id","name","path","sort_order","visible","offset_x","offset_y","scale","rotation_degrees","profile_id"],
            ["product_settings"] = ["key","value"]
        };

    public ProductStore(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var builder = new SqliteConnectionStringBuilder { DataSource = fullPath, ForeignKeys = true, DefaultTimeout = 5 };
        connectionString = builder.ConnectionString;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS product_commands(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL COLLATE NOCASE UNIQUE,
              response TEXT NOT NULL, cooldown_seconds INTEGER NOT NULL DEFAULT 0,
              enabled INTEGER NOT NULL DEFAULT 1, created_utc TEXT NOT NULL, last_used_utc TEXT NULL,
              cooldown_scope TEXT NOT NULL DEFAULT 'global', aliases TEXT NOT NULL DEFAULT '',
              permission_scope TEXT NOT NULL DEFAULT 'everyone');
            CREATE TABLE IF NOT EXISTS product_command_user_cooldowns(
              command_id INTEGER NOT NULL, username TEXT NOT NULL COLLATE NOCASE,
              last_used_utc TEXT NOT NULL, PRIMARY KEY(command_id,username));
            CREATE TABLE IF NOT EXISTS community_users(
              username TEXT PRIMARY KEY COLLATE NOCASE, points INTEGER NOT NULL DEFAULT 0,
              first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS point_ledger(
              id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL COLLATE NOCASE,
              delta INTEGER NOT NULL, reason TEXT NOT NULL, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS giveaways(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, status TEXT NOT NULL,
              winner TEXT NULL, created_utc TEXT NOT NULL, closed_utc TEXT NULL);
            CREATE TABLE IF NOT EXISTS giveaway_entries(
              giveaway_id INTEGER NOT NULL REFERENCES giveaways(id) ON DELETE CASCADE,
              username TEXT NOT NULL COLLATE NOCASE, entered_utc TEXT NOT NULL,
              PRIMARY KEY(giveaway_id, username));
            CREATE TABLE IF NOT EXISTS game_sessions(
              id INTEGER PRIMARY KEY AUTOINCREMENT, game TEXT NOT NULL, username TEXT NOT NULL,
              result TEXT NOT NULL, reward INTEGER NOT NULL, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS moderation_rules(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE,
              pattern TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, action TEXT NOT NULL DEFAULT 'flag');
            CREATE TABLE IF NOT EXISTS automation_rules(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE,
              message TEXT NOT NULL, interval_minutes INTEGER NOT NULL, enabled INTEGER NOT NULL DEFAULT 0,
              last_run_utc TEXT NULL);
            CREATE TABLE IF NOT EXISTS product_activity(
              id INTEGER PRIMARY KEY AUTOINCREMENT, at_utc TEXT NOT NULL, module TEXT NOT NULL, message TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS avatar_profiles(
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              name TEXT NOT NULL COLLATE NOCASE UNIQUE,
              base_path TEXT NULL,
              created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS avatar_profile_expressions(
              profile_id INTEGER NOT NULL REFERENCES avatar_profiles(id) ON DELETE CASCADE,
              state TEXT NOT NULL COLLATE NOCASE,
              path TEXT NOT NULL,
              PRIMARY KEY(profile_id, state));
            CREATE TABLE IF NOT EXISTS avatar_layers(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, path TEXT NOT NULL,
              sort_order INTEGER NOT NULL, visible INTEGER NOT NULL DEFAULT 1,
              offset_x REAL NOT NULL DEFAULT 0, offset_y REAL NOT NULL DEFAULT 0,
              scale REAL NOT NULL DEFAULT 1, rotation_degrees REAL NOT NULL DEFAULT 0,
              profile_id INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS product_settings(
              key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_activity_time ON product_activity(id DESC);
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "product_commands", "last_used_utc", "TEXT NULL");
        EnsureColumn(connection, "product_commands", "cooldown_scope", "TEXT NOT NULL DEFAULT 'global'");
        EnsureColumn(connection, "product_commands", "aliases", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "product_commands", "permission_scope", "TEXT NOT NULL DEFAULT 'everyone'");
        // Repair names from early builds whose built-in commands incorrectly included the display prefix.
        // If a user had already created the canonical name, keep that command and discard only the
        // malformed seeded duplicate; otherwise a UNIQUE-name migration could fail at startup.
        using (var normalizeCommands = connection.CreateCommand())
        {
            normalizeCommands.CommandText = """
                DELETE FROM product_commands
                WHERE name COLLATE NOCASE='!hola'
                  AND EXISTS (SELECT 1 FROM product_commands existing
                              WHERE existing.name COLLATE NOCASE='hola' AND existing.id<>product_commands.id);
                DELETE FROM product_commands
                WHERE name COLLATE NOCASE='!puntos'
                  AND EXISTS (SELECT 1 FROM product_commands existing
                              WHERE existing.name COLLATE NOCASE='puntos' AND existing.id<>product_commands.id);
                DELETE FROM product_commands
                WHERE name COLLATE NOCASE='!dado'
                  AND EXISTS (SELECT 1 FROM product_commands existing
                              WHERE existing.name COLLATE NOCASE='dado' AND existing.id<>product_commands.id);
                UPDATE product_commands SET name='hola' WHERE name COLLATE NOCASE='!hola';
                UPDATE product_commands SET name='puntos' WHERE name COLLATE NOCASE='!puntos';
                UPDATE product_commands SET name='dado' WHERE name COLLATE NOCASE='!dado';
                DELETE FROM product_command_user_cooldowns
                WHERE command_id NOT IN (SELECT id FROM product_commands);
                """;
            normalizeCommands.ExecuteNonQuery();
        }
        EnsureColumn(connection, "automation_rules", "last_run_utc", "TEXT NULL");
        EnsureColumn(connection, "avatar_layers", "offset_x", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "avatar_layers", "offset_y", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "avatar_layers", "scale", "REAL NOT NULL DEFAULT 1");
        EnsureColumn(connection, "avatar_layers", "rotation_degrees", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, "avatar_layers", "profile_id", "INTEGER NOT NULL DEFAULT 1");
        EnsureDefaultAvatarProfile(connection);
        SeedDefaults(connection);
    }

    private static void EnsureDefaultAvatarProfile(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        long profileCount;
        using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM avatar_profiles";
            profileCount = Convert.ToInt64(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        if (profileCount == 0)
        {
            string? legacyBasePath = null;
            using (var legacy = connection.CreateCommand())
            {
                legacy.Transaction = transaction;
                legacy.CommandText = "SELECT value FROM product_settings WHERE key='avatar.path'";
                legacyBasePath = legacy.ExecuteScalar() as string;
            }

            using var insertDefault = connection.CreateCommand();
            insertDefault.Transaction = transaction;
            insertDefault.CommandText = """
                INSERT INTO avatar_profiles(id,name,base_path,created_utc)
                VALUES(1,'Avatar principal',$base,$created);
                """;
            insertDefault.Parameters.AddWithValue("$base", (object?)legacyBasePath ?? DBNull.Value);
            insertDefault.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            insertDefault.ExecuteNonQuery();
        }

        long activeId = 0;
        using (var active = connection.CreateCommand())
        {
            active.Transaction = transaction;
            active.CommandText = "SELECT value FROM product_settings WHERE key='avatar.activeProfileId'";
            var value = active.ExecuteScalar() as string;
            _ = long.TryParse(value, out activeId);
        }

        using (var verifyActive = connection.CreateCommand())
        {
            verifyActive.Transaction = transaction;
            verifyActive.CommandText = "SELECT COUNT(*) FROM avatar_profiles WHERE id=$id";
            verifyActive.Parameters.AddWithValue("$id", activeId);
            if (activeId <= 0 || Convert.ToInt64(verifyActive.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                using var first = connection.CreateCommand();
                first.Transaction = transaction;
                first.CommandText = "SELECT id FROM avatar_profiles ORDER BY id LIMIT 1";
                activeId = Convert.ToInt64(first.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        using (var saveActive = connection.CreateCommand())
        {
            saveActive.Transaction = transaction;
            saveActive.CommandText = """
                INSERT INTO product_settings(key,value) VALUES('avatar.activeProfileId',$id)
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """;
            saveActive.Parameters.AddWithValue("$id", activeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            saveActive.ExecuteNonQuery();
        }

        // Older releases stored expressions as global settings. Preserve them on the default profile.
        foreach (var state in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
        {
            string? legacyPath;
            using (var findLegacy = connection.CreateCommand())
            {
                findLegacy.Transaction = transaction;
                findLegacy.CommandText = "SELECT value FROM product_settings WHERE key=$key";
                findLegacy.Parameters.AddWithValue("$key", "avatar.expression." + state);
                legacyPath = findLegacy.ExecuteScalar() as string;
            }

            if (!string.IsNullOrWhiteSpace(legacyPath))
            {
                using var copyLegacy = connection.CreateCommand();
                copyLegacy.Transaction = transaction;
                copyLegacy.CommandText = """
                    INSERT OR IGNORE INTO avatar_profile_expressions(profile_id,state,path)
                    SELECT 1,$state,$path WHERE EXISTS(SELECT 1 FROM avatar_profiles WHERE id=1);
                    """;
                copyLegacy.Parameters.AddWithValue("$state", state);
                copyLegacy.Parameters.AddWithValue("$path", legacyPath);
                copyLegacy.ExecuteNonQuery();
            }
        }

        using (var syncLegacyPath = connection.CreateCommand())
        {
            syncLegacyPath.Transaction = transaction;
            syncLegacyPath.CommandText = """
                UPDATE product_settings
                SET value=COALESCE((SELECT base_path FROM avatar_profiles WHERE id=$id),'')
                WHERE key='avatar.path';
                INSERT INTO product_settings(key,value)
                SELECT 'avatar.path',COALESCE(base_path,'') FROM avatar_profiles WHERE id=$id
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """;
            syncLegacyPath.Parameters.AddWithValue("$id", activeId);
            syncLegacyPath.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static long GetActiveAvatarProfileId(SqliteConnection connection)
    {
        using (var active = connection.CreateCommand())
        {
            active.CommandText = "SELECT value FROM product_settings WHERE key='avatar.activeProfileId'";
            if (long.TryParse(active.ExecuteScalar() as string, out var id))
            {
                using var exists = connection.CreateCommand();
                exists.CommandText = "SELECT COUNT(*) FROM avatar_profiles WHERE id=$id";
                exists.Parameters.AddWithValue("$id", id);
                if (Convert.ToInt64(exists.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0)
                    return id;
            }
        }

        using var first = connection.CreateCommand();
        first.CommandText = "SELECT id FROM avatar_profiles ORDER BY id LIMIT 1";
        return Convert.ToInt64(first.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static AvatarProfile ReadAvatarProfile(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,base_path,created_utc FROM avatar_profiles WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException($"No existe el perfil de avatar {id}.");
        return new AvatarProfile(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)));
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(" + table + ")";
        using var reader = check.ExecuteReader();
        while (reader.Read()) if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        reader.Close(); using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition; alter.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("O");

    /// <summary>Creates an integrity-checked, transactionally consistent SQLite backup.</summary>
    public string BackupTo(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Elegí una ruta para la copia de seguridad.", nameof(destinationPath));

        lock (gate)
            return BackupLocked(destinationPath);
    }

    /// <summary>
    /// Restores a backup after validating its integrity/schema. Before replacement, an automatic
    /// emergency copy of the current database is written next to it and its path is returned.
    /// </summary>
    public string RestoreFromBackup(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("Elegí una copia de seguridad para restaurar.", nameof(backupPath));

        lock (gate)
        {
            var currentPath = GetDatabasePath();
            var sourcePath = Path.GetFullPath(backupPath);
            if (string.Equals(sourcePath, currentPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La ruta elegida es la base activa, no una copia de seguridad.", nameof(backupPath));

            ValidateBackupFile(sourcePath);
            var directory = Path.GetDirectoryName(currentPath)!;
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            var emergencyPath = Path.Combine(directory,
                Path.GetFileName(currentPath) + ".pre-restore-" + timestamp + "-" + Guid.NewGuid().ToString("N")[..8] + ".vtuberbot.db");

            BackupLocked(emergencyPath);
            try
            {
                CopySnapshotToActiveDatabase(sourcePath);
                ValidateBackupFile(currentPath);
                return emergencyPath;
            }
            catch (Exception restoreError)
            {
                try
                {
                    CopySnapshotToActiveDatabase(emergencyPath);
                    ValidateBackupFile(currentPath);
                }
                catch (Exception rollbackError)
                {
                    throw new InvalidOperationException(
                        "La restauración falló y no se pudo completar la reversión automática. Conservá la copia de seguridad de emergencia: " + emergencyPath,
                        new AggregateException(restoreError, rollbackError));
                }

                throw new InvalidOperationException(
                    "La restauración falló; la base anterior fue recuperada. La copia de seguridad de emergencia está en: " + emergencyPath,
                    restoreError);
            }
        }
    }

    private string BackupLocked(string destinationPath)
    {
        var activePath = GetDatabasePath();
        var fullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(activePath, fullPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La ruta de destino no puede sobrescribir la base activa.", nameof(destinationPath));

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("La ruta de destino no tiene una carpeta.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            var destinationBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                DefaultTimeout = 10,
                Pooling = false
            };

            using (var source = Open())
            using (var destination = new SqliteConnection(destinationBuilder.ConnectionString))
            {
                destination.Open();
                source.BackupDatabase(destination);
            }

            ValidateBackupFile(temporaryPath);
            File.Move(temporaryPath, fullPath, true);
            return fullPath;
        }
        catch
        {
            DeleteSqliteFiles(temporaryPath);
            throw;
        }
        finally
        {
            DeleteSqliteFiles(temporaryPath);
        }
    }

    private void CopySnapshotToActiveDatabase(string sourcePath)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            ForeignKeys = true,
            DefaultTimeout = 10,
            Pooling = false
        };

        using var source = new SqliteConnection(sourceBuilder.ConnectionString);
        source.Open();
        using (var destination = Open())
            source.BackupDatabase(destination);
    }

    private string GetDatabasePath() => Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);

    private static void ValidateBackupFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("No existe el archivo de copia de seguridad.", path);
        if (new FileInfo(path).Length == 0)
            throw new InvalidDataException("El archivo de copia de seguridad está vacío.");

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(path),
                Mode = SqliteOpenMode.ReadOnly,
                ForeignKeys = true,
                DefaultTimeout = 10,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check";
                var result = integrity.ExecuteScalar() as string;
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SQLite no superó integrity_check: " + (result ?? "sin resultado") + ".");
            }

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                using var reader = tables.ExecuteReader();
                while (reader.Read())
                    existingTables.Add(reader.GetString(0));
            }

            foreach (var requiredTable in RequiredBackupColumns.Keys)
            {
                if (!existingTables.Contains(requiredTable))
                    throw new InvalidDataException("La copia no contiene la tabla requerida '" + requiredTable + "' y no corresponde a este esquema de VTuberBot.");

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var columns = connection.CreateCommand();
                columns.CommandText = "PRAGMA table_info(" + requiredTable + ")";
                using (var reader = columns.ExecuteReader())
                    while (reader.Read()) existingColumns.Add(reader.GetString(1));

                foreach (var requiredColumn in RequiredBackupColumns[requiredTable])
                    if (!existingColumns.Contains(requiredColumn))
                        throw new InvalidDataException(
                            "La copia no contiene la columna requerida '" + requiredTable + "." + requiredColumn +
                            "'. Restaurá una copia creada por una versión compatible de VTuberBot.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("No se pudo validar la copia de seguridad como base SQLite íntegra de VTuberBot.", exception);
        }
    }

    private static void DeleteSqliteFiles(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try
            {
                var candidate = path + suffix;
                if (File.Exists(candidate)) File.Delete(candidate);
            }
            catch { /* Best-effort cleanup of transient SQLite snapshot files. */ }
        }
    }

    private void SeedDefaults(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        // Apply defaults only once. Re-seeding on every startup made a user-deleted
        // built-in timer reappear, and could recreate defaults in an existing database.
        using (var marker = connection.CreateCommand())
        {
            marker.Transaction = transaction;
            marker.CommandText = "SELECT value FROM product_settings WHERE key='seed.defaults.v1'";
            if (marker.ExecuteScalar() is not null)
            {
                transaction.Commit();
                return;
            }
        }

        // Existing user data implies that this database has already been used by an older
        // version which did not have a seed marker. Mark it as migrated without recreating
        // defaults that the user may intentionally have removed.
        using (var dataCheck = connection.CreateCommand())
        {
            dataCheck.Transaction = transaction;
            dataCheck.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM product_commands) +
                    (SELECT COUNT(*) FROM automation_rules) +
                    (SELECT COUNT(*) FROM moderation_rules) +
                    (SELECT COUNT(*) FROM community_users) +
                    (SELECT COUNT(*) FROM giveaways) +
                    (SELECT COUNT(*) FROM product_activity)
                """;
            if (Convert.ToInt64(dataCheck.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                using var markExisting = connection.CreateCommand();
                markExisting.Transaction = transaction;
                markExisting.CommandText = "INSERT INTO product_settings(key,value) VALUES('seed.defaults.v1','existing-db')";
                markExisting.ExecuteNonQuery();
                transaction.Commit();
                return;
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO product_commands(name,response,cooldown_seconds,enabled,created_utc)
                VALUES('hola','¡Hola, {user}! Bienvenido al stream.',5,1,$now),
                      ('puntos','{user}, tenés {points} puntos.',3,1,$now),
                      ('dado','{user} sacó {roll} en el dado.',5,1,$now);
                INSERT OR IGNORE INTO moderation_rules(name,pattern,enabled,action)
                VALUES('Enlaces','https?://',1,'flag');
                INSERT OR IGNORE INTO automation_rules(name,message,interval_minutes,enabled)
                VALUES('Bienvenida','¡Gracias por acompañar el stream!',30,0);
                INSERT INTO product_settings(key,value) VALUES('seed.defaults.v1','1')
                ON CONFLICT(key) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$now", Now());
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IReadOnlyList<ProductCommand> GetCommands()
    {
        lock (gate)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,name,response,cooldown_seconds,enabled,cooldown_scope,aliases,permission_scope FROM product_commands ORDER BY name";
            using var r = cmd.ExecuteReader(); var list = new List<ProductCommand>();
            while (r.Read()) list.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4) != 0, r.GetString(5), r.GetString(6).Replace(",", ", "), r.GetString(7)));
            return list;
        }
    }

    public void SaveCommand(
        long? id, string name, string response, int cooldown, bool enabled,
        string cooldownScope = "global", string aliases = "", string permissionScope = "everyone")
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("El nombre y la respuesta son obligatorios.");
        if (cooldown is < 0 or > 86400) throw new ArgumentOutOfRangeException(nameof(cooldown));
        cooldownScope = cooldownScope.Trim().ToLowerInvariant();
        if (cooldownScope is not ("global" or "user"))
            throw new ArgumentOutOfRangeException(nameof(cooldownScope), "Usá 'global' o 'user'.");
        permissionScope = (permissionScope ?? string.Empty).Trim().ToLowerInvariant();
        if (!CommandPermissionPolicy.IsRecognizedScope(permissionScope))
            throw new ArgumentOutOfRangeException(nameof(permissionScope), "Usá everyone, subscriber, vip, moderator o broadcaster.");

        var normalizedName = NormalizeCommandToken(name, "nombre del comando");
        var normalizedAliases = NormalizeCommandAliases(aliases, normalizedName);
        var aliasesText = string.Join(",", normalizedAliases);

        lock (gate)
        {
            var currentCommands = GetCommands();
            if (id.HasValue && currentCommands.All(command => command.Id != id.Value))
                throw new KeyNotFoundException($"No existe el comando {id.Value}.");
            var otherCommands = currentCommands.Where(command => !id.HasValue || command.Id != id.Value).ToList();
            foreach (var other in otherCommands)
            {
                var otherAliases = ParseCommandAliases(other.Aliases);
                if (string.Equals(other.Name, normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    otherAliases.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"El nombre !{normalizedName} ya está usado por otro comando o como alias.");

                foreach (var alias in normalizedAliases)
                {
                    if (string.Equals(other.Name, alias, StringComparison.OrdinalIgnoreCase) ||
                        otherAliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                        throw new ArgumentException($"El alias !{alias} ya está usado por otro comando.");
                }
            }

            using var c = Open();
            using var cmd = c.CreateCommand();
            if (id is null)
            {
                cmd.CommandText = """
                    INSERT INTO product_commands(name,response,cooldown_seconds,enabled,created_utc,cooldown_scope,aliases,permission_scope)
                    VALUES($name,$response,$cooldown,$enabled,$now,$scope,$aliases,$permission)
                    """;
            }
            else
            {
                cmd.CommandText = """
                    UPDATE product_commands
                    SET name=$name,response=$response,cooldown_seconds=$cooldown,enabled=$enabled,
                        cooldown_scope=$scope,aliases=$aliases,permission_scope=$permission
                    WHERE id=$id
                    """;
                cmd.Parameters.AddWithValue("$id", id.Value);
            }
            cmd.Parameters.AddWithValue("$name", normalizedName);
            cmd.Parameters.AddWithValue("$response", response.Trim());
            cmd.Parameters.AddWithValue("$cooldown", cooldown);
            cmd.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$scope", cooldownScope);
            cmd.Parameters.AddWithValue("$aliases", aliasesText);
            cmd.Parameters.AddWithValue("$permission", permissionScope);
            cmd.Parameters.AddWithValue("$now", Now());
            cmd.ExecuteNonQuery();
            Log("Comandos", $"Comando !{normalizedName} guardado (cooldown {cooldownScope}; permiso {permissionScope}; alias {normalizedAliases.Count}).");
        }
    }

    private static string NormalizeCommandToken(string token, string description)
    {
        var normalized = token.Trim().TrimStart('!').ToLowerInvariant();
        if (normalized.Length is < 1 or > 30 ||
            normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-')))
            throw new ArgumentException($"El {description} debe usar de 1 a 30 letras, números, guiones o guiones bajos.");
        return normalized;
    }

    private static List<string> NormalizeCommandAliases(string aliases, string primaryName)
    {
        var normalized = new List<string>();
        foreach (var aliasPart in (aliases ?? string.Empty).Split(','))
        {
            if (string.IsNullOrWhiteSpace(aliasPart))
                continue;
            var alias = NormalizeCommandToken(aliasPart, "alias");
            if (string.Equals(alias, primaryName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!normalized.Contains(alias, StringComparer.OrdinalIgnoreCase))
                normalized.Add(alias);
        }
        if (normalized.Count > 20)
            throw new ArgumentOutOfRangeException(nameof(aliases), "Cada comando puede tener como máximo 20 alias.");
        return normalized;
    }

    private static List<string> ParseCommandAliases(string aliases) =>
        (aliases ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(alias => alias.Trim())
            .Where(alias => alias.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void DeleteCommand(long id)
    {
        lock (gate) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM product_commands WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery(); }
        Log("Comandos", $"Comando ID {id} eliminado.");
    }

    /// <summary>
    /// Renders the current editor text without checking or consuming command cooldowns,
    /// changing user activity, or writing to the activity log.
    /// </summary>
    public CommandResult PreviewCommandResponse(string name, string response, string username = "Streamer", string arguments = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            return CommandResult.Failed("Nombre de comando vacío.");
        if (string.IsNullOrWhiteSpace(response))
            return CommandResult.Failed("La respuesta del comando está vacía.");
        if (string.IsNullOrWhiteSpace(username) || username.Length > 25 ||
            username.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')))
            return CommandResult.Failed("El usuario de prueba debe tener hasta 25 caracteres y usar letras, números, guion o guion bajo.");
        arguments = (arguments ?? string.Empty).Trim();
        if (arguments.Length > 500)
            return CommandResult.Failed("Los argumentos superan el límite de 500 caracteres.");

        lock (gate)
        {
            long points = 0;
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT points FROM community_users WHERE username=$user COLLATE NOCASE";
                cmd.Parameters.AddWithValue("$user", username.Trim());
                var storedPoints = cmd.ExecuteScalar();
                if (storedPoints is not null && storedPoints is not DBNull)
                    points = Convert.ToInt64(storedPoints, System.Globalization.CultureInfo.InvariantCulture);
            }

            var output = RenderCommandResponse(response, username.Trim(), points, arguments);
            if (output.Length > 500)
                return CommandResult.Failed("La respuesta supera el límite de 500 caracteres de Twitch; reducila en la configuración.");
            return CommandResult.Succeeded(output);
        }
    }

    private static string RenderCommandResponse(string response, string username, long points, string arguments)
    {
        return response.Replace("{user}", username, StringComparison.OrdinalIgnoreCase)
            .Replace("{points}", points.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{roll}", Random.Shared.Next(1, 7).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{args}", arguments, StringComparison.OrdinalIgnoreCase)
            .Replace("{message}", arguments, StringComparison.OrdinalIgnoreCase);
    }

    public CommandResult RunLocalCommand(string name, string username = "Streamer", string arguments = "", IReadOnlyCollection<string>? userRoles = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CommandResult.Failed("Nombre de comando vacío.");
        arguments = (arguments ?? string.Empty).Trim();
        if (arguments.Length > 500)
            return CommandResult.Failed("Los argumentos superan el límite de 500 caracteres.");

        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                SELECT id,response,cooldown_seconds,last_used_utc,cooldown_scope,name,permission_scope
                FROM product_commands
                WHERE enabled=1 AND (
                    name=$name COLLATE NOCASE OR
                    instr(',' || lower(aliases) || ',', ',' || lower($name) || ',') > 0)
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("$name", name.Trim().TrimStart('!'));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return CommandResult.Failed("Comando no encontrado o desactivado.");
            var id = reader.GetInt64(0);
            var response = reader.GetString(1);
            var cooldown = reader.GetInt32(2);
            DateTimeOffset? globalLastUsed = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3));
            var cooldownScope = reader.GetString(4);
            var canonicalName = reader.GetString(5);
            var requiredPermission = reader.GetString(6);
            reader.Close();

            if (!CommandPermissionPolicy.IsAllowed(requiredPermission, userRoles))
            {
                Log("Comandos", $"Uso de !{canonicalName} denegado a {username}; requiere {requiredPermission}.");
                return CommandResult.Failed("No tenés permiso para usar este comando.");
            }

            var now = DateTimeOffset.UtcNow;
            DateTimeOffset? lastUsed = globalLastUsed;
            if (cooldownScope == "user")
            {
                using var userCooldown = c.CreateCommand();
                userCooldown.CommandText = "SELECT last_used_utc FROM product_command_user_cooldowns WHERE command_id=$id AND username=$username COLLATE NOCASE";
                userCooldown.Parameters.AddWithValue("$id", id);
                userCooldown.Parameters.AddWithValue("$username", username.Trim());
                var stored = userCooldown.ExecuteScalar();
                lastUsed = stored is string timestamp ? DateTimeOffset.Parse(timestamp) : null;
            }

            if (cooldown > 0 && lastUsed is not null && now - lastUsed.Value < TimeSpan.FromSeconds(cooldown))
            {
                var remaining = Math.Ceiling((TimeSpan.FromSeconds(cooldown) - (now - lastUsed.Value)).TotalSeconds).ToString("0");
                return CommandResult.Failed("Comando en cooldown; esperá " + remaining + " s.");
            }

            var user = GetOrCreateUser(c, username);
            var output = RenderCommandResponse(response, username, user.Points, arguments);
            if (output.Length > 500)
                return CommandResult.Failed("La respuesta supera el límite de 500 caracteres de Twitch; reducila en la configuración del comando.");

            using (var update = c.CreateCommand())
            {
                update.CommandText = "UPDATE product_commands SET last_used_utc=$now WHERE id=$id";
                update.Parameters.AddWithValue("$now", now.ToString("O"));
                update.Parameters.AddWithValue("$id", id);
                update.ExecuteNonQuery();
            }

            if (cooldownScope == "user")
            {
                using var updateUserCooldown = c.CreateCommand();
                updateUserCooldown.CommandText = """
                    INSERT INTO product_command_user_cooldowns(command_id,username,last_used_utc)
                    VALUES($id,$username,$now)
                    ON CONFLICT(command_id,username) DO UPDATE SET last_used_utc=excluded.last_used_utc
                    """;
                updateUserCooldown.Parameters.AddWithValue("$id", id);
                updateUserCooldown.Parameters.AddWithValue("$username", username.Trim());
                updateUserCooldown.Parameters.AddWithValue("$now", now.ToString("O"));
                updateUserCooldown.ExecuteNonQuery();
            }

            Log("Comandos", "Comando !" + canonicalName + " (entrada: !" + name.TrimStart('!') + ") ejecutado por " + username + ".");
            return CommandResult.Succeeded(output);
        }
    }
    public CommunityUser TouchUser(string username)
    {
        ValidateUsername(username);
        lock (gate) { using var c = Open(); return GetOrCreateUser(c, username.Trim()); }
    }

    private CommunityUser GetOrCreateUser(SqliteConnection c, string username)
    {
        var now = Now();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO community_users(username,points,first_seen_utc,last_seen_utc) VALUES($user,0,$now,$now) ON CONFLICT(username) DO UPDATE SET last_seen_utc=excluded.last_seen_utc";
            cmd.Parameters.AddWithValue("$user", username); cmd.Parameters.AddWithValue("$now", now); cmd.ExecuteNonQuery();
        }
        using var read = c.CreateCommand();
        read.CommandText = "SELECT username,points,last_seen_utc FROM community_users WHERE username=$user";
        read.Parameters.AddWithValue("$user", username);
        using var r = read.ExecuteReader(); r.Read();
        return new(r.GetString(0), r.GetInt64(1), DateTimeOffset.Parse(r.GetString(2)));
    }

    public CommunityUser AddPoints(string username, long delta, string reason)
    {
        ValidateUsername(username);
        if (delta == 0 || Math.Abs(delta) > 1_000_000) throw new ArgumentOutOfRangeException(nameof(delta));
        lock (gate)
        {
            using var c = Open(); using var tx = c.BeginTransaction();
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "INSERT INTO community_users(username,points,first_seen_utc,last_seen_utc) VALUES($u,0,$n,$n) ON CONFLICT(username) DO NOTHING";
                cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.Parameters.AddWithValue("$n", Now()); cmd.ExecuteNonQuery();
            }
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "UPDATE community_users SET points=MAX(0,points+$d),last_seen_utc=$n WHERE username=$u";
                cmd.Parameters.AddWithValue("$d", delta); cmd.Parameters.AddWithValue("$n", Now()); cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.ExecuteNonQuery();
            }
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "INSERT INTO point_ledger(username,delta,reason,created_utc) VALUES($u,$d,$r,$n)";
                cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.Parameters.AddWithValue("$d", delta); cmd.Parameters.AddWithValue("$r", reason); cmd.Parameters.AddWithValue("$n", Now()); cmd.ExecuteNonQuery();
            }
            tx.Commit();
            var result = GetOrCreateUser(c, username.Trim()); Log("Comunidad", $"{username}: {delta:+#;-#;0} puntos ({reason})."); return result;
        }
    }

    public IReadOnlyList<CommunityUser> GetLeaderboard(int limit = 50)
    {
        lock (gate) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT username,points,last_seen_utc FROM community_users ORDER BY points DESC,username LIMIT $limit"; cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit,1,200)); using var r=cmd.ExecuteReader(); var list=new List<CommunityUser>(); while(r.Read()) list.Add(new(r.GetString(0),r.GetInt64(1),DateTimeOffset.Parse(r.GetString(2)))); return list; }
    }

    public long CreateGiveaway(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ingresá un nombre para el sorteo.");

        name = name.Trim();
        if (name.Length > 100)
            throw new ArgumentOutOfRangeException(nameof(name), "El nombre del sorteo no puede superar los 100 caracteres.");

        lock (gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            using (var active = connection.CreateCommand())
            {
                active.Transaction = transaction;
                active.CommandText = "SELECT id,name FROM giveaways WHERE status='OPEN' ORDER BY id DESC LIMIT 1";
                using var reader = active.ExecuteReader();
                if (reader.Read())
                {
                    var activeId = reader.GetInt64(0);
                    var activeName = reader.GetString(1);
                    throw new InvalidOperationException(
                        $"El sorteo #{activeId} («{activeName}») sigue abierto. Cerralo o cancelalo antes de crear otro.");
                }
            }

            long id;
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO giveaways(name,status,created_utc) VALUES($name,'OPEN',$now); SELECT last_insert_rowid();";
                insert.Parameters.AddWithValue("$name", name);
                insert.Parameters.AddWithValue("$now", Now());
                id = Convert.ToInt64(insert.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            transaction.Commit();
            Log("Sorteos", $"Sorteo '{name}' creado.");
            return id;
        }
    }

    public void CancelGiveaway(long id)
    {
        lock (gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            string? status;
            using (var query = connection.CreateCommand())
            {
                query.Transaction = transaction;
                query.CommandText = "SELECT status FROM giveaways WHERE id=$id";
                query.Parameters.AddWithValue("$id", id);
                status = query.ExecuteScalar() as string;
            }

            if (status is null)
                throw new KeyNotFoundException($"No existe el sorteo #{id}.");
            if (!string.Equals(status, "OPEN", StringComparison.Ordinal))
                throw new InvalidOperationException($"El sorteo #{id} no está abierto (estado: {status}).");

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE giveaways SET status='CANCELLED',closed_utc=$now WHERE id=$id AND status='OPEN'";
                update.Parameters.AddWithValue("$now", Now());
                update.Parameters.AddWithValue("$id", id);
                if (update.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException($"El sorteo #{id} dejó de estar abierto.");
            }

            transaction.Commit();
            Log("Sorteos", $"Sorteo #{id} cancelado sin elegir ganador.");
        }
    }

    public bool EnterGiveaway(long id, string username)
    {
        ValidateUsername(username);
        lock (gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT OR IGNORE INTO giveaway_entries(giveaway_id,username,entered_utc) SELECT $id,$user,$now WHERE EXISTS(SELECT 1 FROM giveaways WHERE id=$id AND status='OPEN')"; cmd.Parameters.AddWithValue("$id",id); cmd.Parameters.AddWithValue("$user",username.Trim()); cmd.Parameters.AddWithValue("$now",Now()); var changed=cmd.ExecuteNonQuery()>0; if(changed) Log("Sorteos",$"{username} se anotó en sorteo #{id}."); return changed; }
    }

    public GiveawayRecord DrawGiveaway(long id)
    {
        lock (gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            string? status;
            using (var query = connection.CreateCommand())
            {
                query.Transaction = transaction;
                query.CommandText = "SELECT status FROM giveaways WHERE id=$id";
                query.Parameters.AddWithValue("$id", id);
                status = query.ExecuteScalar() as string;
            }

            if (status is null)
                throw new KeyNotFoundException($"No existe el sorteo #{id}.");
            if (!string.Equals(status, "OPEN", StringComparison.Ordinal))
                throw new InvalidOperationException($"El sorteo #{id} no está abierto (estado: {status}).");

            var entrants = new List<string>();
            using (var query = connection.CreateCommand())
            {
                query.Transaction = transaction;
                query.CommandText = "SELECT username FROM giveaway_entries WHERE giveaway_id=$id ORDER BY username";
                query.Parameters.AddWithValue("$id", id);
                using var reader = query.ExecuteReader();
                while (reader.Read())
                    entrants.Add(reader.GetString(0));
            }

            if (entrants.Count == 0)
                throw new InvalidOperationException("No hay participantes registrados.");

            var winner = entrants[System.Security.Cryptography.RandomNumberGenerator.GetInt32(entrants.Count)];
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE giveaways SET status='CLOSED',winner=$winner,closed_utc=$now WHERE id=$id AND status='OPEN'";
                update.Parameters.AddWithValue("$winner", winner);
                update.Parameters.AddWithValue("$now", Now());
                update.Parameters.AddWithValue("$id", id);
                if (update.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException($"El sorteo #{id} dejó de estar abierto.");
            }

            transaction.Commit();
            Log("Sorteos", $"Sorteo #{id}: ganador {winner} de {entrants.Count} participantes.");
            return new(id, string.Empty, "CLOSED", winner, entrants.Count);
        }
    }

    public IReadOnlyList<GiveawayRecord> GetGiveaways()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT g.id,g.name,g.status,g.winner,COUNT(e.username) FROM giveaways g LEFT JOIN giveaway_entries e ON e.giveaway_id=g.id GROUP BY g.id ORDER BY g.id DESC"; using var r=cmd.ExecuteReader(); var list=new List<GiveawayRecord>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetInt32(4))); return list; }
    }

    public string PlayGame(string game, string username, int stake) =>
        PlayGame(game, username, stake, MiniGameEngine.GetChoices(game)[0]);

    /// <summary>
    /// Plays a selected game and settles the stake/payout in one SQLite transaction.
    /// A zero stake is a free practice round.
    /// </summary>
    public string PlayGame(string game, string username, int stake, string choice)
    {
        ValidateUsername(username);
        if (stake is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(stake), "La apuesta debe estar entre 0 y 10.000 puntos; 0 es práctica gratuita.");

        var round = MiniGameEngine.Roll(game, choice);
        var payout = checked(stake * round.PayoutMultiplier);
        var now = Now();
        long finalBalance;

        lock (gate)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();

            using (var ensureUser = c.CreateCommand())
            {
                ensureUser.Transaction = tx;
                ensureUser.CommandText = """
                    INSERT INTO community_users(username,points,first_seen_utc,last_seen_utc)
                    VALUES($u,0,$now,$now)
                    ON CONFLICT(username) DO UPDATE SET last_seen_utc=excluded.last_seen_utc
                    """;
                ensureUser.Parameters.AddWithValue("$u", username.Trim());
                ensureUser.Parameters.AddWithValue("$now", now);
                ensureUser.ExecuteNonQuery();
            }

            long currentBalance;
            using (var queryBalance = c.CreateCommand())
            {
                queryBalance.Transaction = tx;
                queryBalance.CommandText = "SELECT points FROM community_users WHERE username=$u";
                queryBalance.Parameters.AddWithValue("$u", username.Trim());
                currentBalance = Convert.ToInt64(queryBalance.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            if (currentBalance < stake)
                throw new InvalidOperationException($"Saldo insuficiente: {currentBalance} puntos disponibles, pero la apuesta es de {stake}. No se modificó el saldo.");

            if (stake > 0)
            {
                using (var debit = c.CreateCommand())
                {
                    debit.Transaction = tx;
                    debit.CommandText = """
                        UPDATE community_users
                        SET points=points-$stake,last_seen_utc=$now
                        WHERE username=$u AND points >= $stake
                        """;
                    debit.Parameters.AddWithValue("$stake", stake);
                    debit.Parameters.AddWithValue("$now", now);
                    debit.Parameters.AddWithValue("$u", username.Trim());
                    if (debit.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("El saldo cambió durante la apuesta; no se completó la ronda.");
                }

                using var debitLedger = c.CreateCommand();
                debitLedger.Transaction = tx;
                debitLedger.CommandText = "INSERT INTO point_ledger(username,delta,reason,created_utc) VALUES($u,$delta,$reason,$now)";
                debitLedger.Parameters.AddWithValue("$u", username.Trim());
                debitLedger.Parameters.AddWithValue("$delta", -stake);
                debitLedger.Parameters.AddWithValue("$reason", "Apuesta minijuego: " + round.Game);
                debitLedger.Parameters.AddWithValue("$now", now);
                debitLedger.ExecuteNonQuery();
            }

            if (payout > 0)
            {
                using (var credit = c.CreateCommand())
                {
                    credit.Transaction = tx;
                    credit.CommandText = "UPDATE community_users SET points=points+$payout,last_seen_utc=$now WHERE username=$u";
                    credit.Parameters.AddWithValue("$payout", payout);
                    credit.Parameters.AddWithValue("$now", now);
                    credit.Parameters.AddWithValue("$u", username.Trim());
                    if (credit.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("No se pudo acreditar el premio; la transacción se canceló.");
                }

                using var payoutLedger = c.CreateCommand();
                payoutLedger.Transaction = tx;
                payoutLedger.CommandText = "INSERT INTO point_ledger(username,delta,reason,created_utc) VALUES($u,$delta,$reason,$now)";
                payoutLedger.Parameters.AddWithValue("$u", username.Trim());
                payoutLedger.Parameters.AddWithValue("$delta", payout);
                payoutLedger.Parameters.AddWithValue("$reason", "Premio minijuego: " + round.Game);
                payoutLedger.Parameters.AddWithValue("$now", now);
                payoutLedger.ExecuteNonQuery();
            }

            var resultName = round.Won ? "Victoria" : "Derrota";
            using (var session = c.CreateCommand())
            {
                session.Transaction = tx;
                session.CommandText = "INSERT INTO game_sessions(game,username,result,reward,created_utc) VALUES($g,$u,$r,$p,$n)";
                session.Parameters.AddWithValue("$g", round.Game);
                session.Parameters.AddWithValue("$u", username.Trim());
                session.Parameters.AddWithValue("$r", $"{resultName} · elección: {round.Choice} · resultado: {round.Outcome}");
                session.Parameters.AddWithValue("$p", payout);
                session.Parameters.AddWithValue("$n", now);
                session.ExecuteNonQuery();
            }

            using (var queryFinal = c.CreateCommand())
            {
                queryFinal.Transaction = tx;
                queryFinal.CommandText = "SELECT points FROM community_users WHERE username=$u";
                queryFinal.Parameters.AddWithValue("$u", username.Trim());
                finalBalance = Convert.ToInt64(queryFinal.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            tx.Commit();
        }

        var netChange = payout - stake;
        var settlement = stake == 0
            ? "Práctica gratuita: no cambian los puntos."
            : $"Apuesta: {stake}; premio total: {payout}; cambio neto: {netChange:+#;-#;0}.";
        var outcomeSummary = $"{(round.Won ? "Victoria" : "Derrota")}. Resultado: {round.Outcome}; elección: {round.Choice}.";
        Log("Minijuegos",
            $"{username.Trim()}: {round.Game} — {(round.Won ? "Victoria" : "Derrota")}; elección {round.Choice}; resultado {round.Outcome}; apuesta {stake}; pago {payout}; cambio neto {netChange:+#;-#;0}; saldo {finalBalance}.");
        return $"{outcomeSummary} {settlement} Saldo final: {finalBalance} puntos.";
    }

    public void SaveModerationRule(string name, string pattern, bool enabled, string action)
    {
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Nombre y patrón obligatorios.");
        if (action is not ("flag" or "warn" or "delete" or "timeout" or "ban"))
            throw new ArgumentOutOfRangeException(nameof(action), "Acción no reconocida. Opciones: flag, warn, delete, timeout, ban.");
        _=new System.Text.RegularExpressions.Regex(pattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO moderation_rules(name,pattern,enabled,action) VALUES($n,$p,$e,$a) ON CONFLICT(name) DO UPDATE SET pattern=excluded.pattern,enabled=excluded.enabled,action=excluded.action"; cmd.Parameters.AddWithValue("$n",name.Trim()); cmd.Parameters.AddWithValue("$p",pattern); cmd.Parameters.AddWithValue("$e",enabled?1:0); cmd.Parameters.AddWithValue("$a",action); cmd.ExecuteNonQuery(); }
        Log("Moderación",$"Regla '{name}' guardada ({(enabled?"activa":"pausada")}).");
    }

    public IReadOnlyList<(string Name,string Pattern,bool Enabled,string Action)> GetModerationRules()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT name,pattern,enabled,action FROM moderation_rules ORDER BY name"; using var r=cmd.ExecuteReader(); var list=new List<(string,string,bool,string)>(); while(r.Read()) list.Add((r.GetString(0),r.GetString(1),r.GetInt32(2)!=0,r.GetString(3))); return list; }
    }

    public IReadOnlyList<AvatarProfile> GetAvatarProfiles()
    {
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name,base_path,created_utc FROM avatar_profiles ORDER BY created_utc,id";
            using var reader = command.ExecuteReader();
            var profiles = new List<AvatarProfile>();
            while (reader.Read())
                profiles.Add(new AvatarProfile(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3))));
            return profiles;
        }
    }

    public AvatarProfile GetActiveAvatarProfile()
    {
        lock (gate)
        {
            using var connection = Open();
            return ReadAvatarProfile(connection, GetActiveAvatarProfileId(connection));
        }
    }

    public AvatarProfile CreateAvatarProfile(string name, bool duplicateActiveProfile)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is < 1 or > 64)
            throw new ArgumentException("El nombre del perfil debe tener entre 1 y 64 caracteres.", nameof(name));

        lock (gate)
        {
            using var connection = Open();
            var activeId = GetActiveAvatarProfileId(connection);
            using var transaction = connection.BeginTransaction();
            using (var duplicateCheck = connection.CreateCommand())
            {
                duplicateCheck.Transaction = transaction;
                duplicateCheck.CommandText = "SELECT COUNT(*) FROM avatar_profiles WHERE name=$name COLLATE NOCASE";
                duplicateCheck.Parameters.AddWithValue("$name", normalizedName);
                if (Convert.ToInt64(duplicateCheck.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0)
                    throw new ArgumentException("Ya existe un perfil de avatar con ese nombre.", nameof(name));
            }

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO avatar_profiles(name,base_path,created_utc)
                    SELECT $name,CASE WHEN $duplicate=1 THEN base_path ELSE NULL END,$created
                    FROM avatar_profiles WHERE id=$active;
                    SELECT last_insert_rowid();
                    """;
                insert.Parameters.AddWithValue("$name", normalizedName);
                insert.Parameters.AddWithValue("$duplicate", duplicateActiveProfile ? 1 : 0);
                insert.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
                insert.Parameters.AddWithValue("$active", activeId);
                if (insert.ExecuteScalar() is not long newId)
                    throw new InvalidOperationException("No se pudo crear el perfil de avatar.");

                if (duplicateActiveProfile)
                {
                    using var copyLayers = connection.CreateCommand();
                    copyLayers.Transaction = transaction;
                    copyLayers.CommandText = """
                        INSERT INTO avatar_layers(profile_id,name,path,sort_order,visible,offset_x,offset_y,scale,rotation_degrees)
                        SELECT $newId,name,path,sort_order,visible,offset_x,offset_y,scale,rotation_degrees
                        FROM avatar_layers WHERE profile_id=$activeId;
                        """;
                    copyLayers.Parameters.AddWithValue("$newId", newId);
                    copyLayers.Parameters.AddWithValue("$activeId", activeId);
                    copyLayers.ExecuteNonQuery();

                    using var copyExpressions = connection.CreateCommand();
                    copyExpressions.Transaction = transaction;
                    copyExpressions.CommandText = """
                        INSERT INTO avatar_profile_expressions(profile_id,state,path)
                        SELECT $newId,state,path FROM avatar_profile_expressions WHERE profile_id=$activeId;
                        """;
                    copyExpressions.Parameters.AddWithValue("$newId", newId);
                    copyExpressions.Parameters.AddWithValue("$activeId", activeId);
                    copyExpressions.ExecuteNonQuery();
                }

                transaction.Commit();
                return ReadAvatarProfile(connection, newId);
            }
        }
    }

    public void SetActiveAvatarProfile(long profileId)
    {
        lock (gate)
        {
            using var connection = Open();
            var profile = ReadAvatarProfile(connection, profileId);
            using var transaction = connection.BeginTransaction();
            using var settings = connection.CreateCommand();
            settings.Transaction = transaction;
            settings.CommandText = """
                INSERT INTO product_settings(key,value) VALUES('avatar.activeProfileId',$id)
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                INSERT INTO product_settings(key,value) VALUES('avatar.path',$path)
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """;
            settings.Parameters.AddWithValue("$id", profile.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            settings.Parameters.AddWithValue("$path", profile.BasePath ?? string.Empty);
            settings.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public void SetActiveAvatarProfileBasePath(string? path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());
        if (normalizedPath is not null && !File.Exists(normalizedPath))
            throw new FileNotFoundException("No existe el archivo base del avatar.", normalizedPath);

        lock (gate)
        {
            using var connection = Open();
            var profileId = GetActiveAvatarProfileId(connection);
            using var transaction = connection.BeginTransaction();
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE avatar_profiles SET base_path=$path WHERE id=$id";
            update.Parameters.AddWithValue("$path", (object?)normalizedPath ?? DBNull.Value);
            update.Parameters.AddWithValue("$id", profileId);
            if (update.ExecuteNonQuery() == 0) throw new KeyNotFoundException($"No existe el perfil de avatar {profileId}.");

            using var legacy = connection.CreateCommand();
            legacy.Transaction = transaction;
            legacy.CommandText = """
                INSERT INTO product_settings(key,value) VALUES('avatar.path',$path)
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """;
            legacy.Parameters.AddWithValue("$path", normalizedPath ?? string.Empty);
            legacy.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public AvatarProfile DeleteAvatarProfile(long profileId)
    {
        lock (gate)
        {
            using var connection = Open();
            var activeId = GetActiveAvatarProfileId(connection);
            using var transaction = connection.BeginTransaction();
            long count;
            using (var total = connection.CreateCommand())
            {
                total.Transaction = transaction;
                total.CommandText = "SELECT COUNT(*) FROM avatar_profiles";
                count = Convert.ToInt64(total.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
            if (count <= 1) throw new InvalidOperationException("No podés eliminar el último perfil de avatar.");

            if (!AvatarProfileExists(connection, profileId, transaction))
                throw new KeyNotFoundException($"No existe el perfil de avatar {profileId}.");

            var nextActiveId = activeId;
            if (activeId == profileId)
            {
                using var next = connection.CreateCommand();
                next.Transaction = transaction;
                next.CommandText = "SELECT id FROM avatar_profiles WHERE id<>$id ORDER BY created_utc,id LIMIT 1";
                next.Parameters.AddWithValue("$id", profileId);
                nextActiveId = Convert.ToInt64(next.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            using (var removeLayers = connection.CreateCommand())
            {
                removeLayers.Transaction = transaction;
                removeLayers.CommandText = "DELETE FROM avatar_layers WHERE profile_id=$id";
                removeLayers.Parameters.AddWithValue("$id", profileId);
                removeLayers.ExecuteNonQuery();
            }
            using (var remove = connection.CreateCommand())
            {
                remove.Transaction = transaction;
                remove.CommandText = "DELETE FROM avatar_profiles WHERE id=$id";
                remove.Parameters.AddWithValue("$id", profileId);
                remove.ExecuteNonQuery();
            }

            if (activeId == profileId)
            {
                using var setActive = connection.CreateCommand();
                setActive.Transaction = transaction;
                setActive.CommandText = """
                    INSERT INTO product_settings(key,value)
                    SELECT 'avatar.activeProfileId',CAST(id AS TEXT) FROM avatar_profiles WHERE id=$id
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO product_settings(key,value)
                    SELECT 'avatar.path',COALESCE(base_path,'') FROM avatar_profiles WHERE id=$id
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    """;
                setActive.Parameters.AddWithValue("$id", nextActiveId);
                setActive.ExecuteNonQuery();
            }

            transaction.Commit();
            return ReadAvatarProfile(connection, nextActiveId);
        }
    }

    private static bool AvatarProfileExists(SqliteConnection connection, long profileId, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM avatar_profiles WHERE id=$id";
        command.Parameters.AddWithValue("$id", profileId);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static readonly HashSet<string> AvatarExpressionStates = new(StringComparer.OrdinalIgnoreCase)
        { "Idle", "Talking", "Blink", "Happy", "Surprised" };

    public IReadOnlyDictionary<string, string> GetAvatarExpressions()
    {
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT state,path FROM avatar_profile_expressions WHERE profile_id=$id ORDER BY state";
            command.Parameters.AddWithValue("$id", GetActiveAvatarProfileId(connection));
            using var reader = command.ExecuteReader();
            var expressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) expressions[reader.GetString(0)] = reader.GetString(1);
            return expressions;
        }
    }

    public string? GetAvatarExpression(string state)
    {
        ValidateAvatarExpressionState(state);
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT path FROM avatar_profile_expressions WHERE profile_id=$id AND state=$state COLLATE NOCASE";
            command.Parameters.AddWithValue("$id", GetActiveAvatarProfileId(connection));
            command.Parameters.AddWithValue("$state", state.Trim());
            return command.ExecuteScalar() as string;
        }
    }

    public void SetAvatarExpression(string state, string? path)
    {
        ValidateAvatarExpressionState(state);
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());
        if (normalizedPath is not null && !File.Exists(normalizedPath))
            throw new FileNotFoundException("No existe el archivo del sprite de expresión.", normalizedPath);

        lock (gate)
        {
            using var connection = Open();
            var profileId = GetActiveAvatarProfileId(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (normalizedPath is null)
            {
                command.CommandText = "DELETE FROM avatar_profile_expressions WHERE profile_id=$id AND state=$state COLLATE NOCASE";
            }
            else
            {
                command.CommandText = """
                    INSERT INTO avatar_profile_expressions(profile_id,state,path) VALUES($id,$state,$path)
                    ON CONFLICT(profile_id,state) DO UPDATE SET path=excluded.path;
                    """;
            }
            command.Parameters.AddWithValue("$id", profileId);
            command.Parameters.AddWithValue("$state", state.Trim());
            if (normalizedPath is not null) command.Parameters.AddWithValue("$path", normalizedPath);
            command.ExecuteNonQuery();

            // Keep legacy settings aligned for the default profile so a subsequent older build does not resurrect a deleted expression.
            if (profileId == 1)
            {
                using var legacy = connection.CreateCommand();
                legacy.Transaction = transaction;
                if (normalizedPath is null)
                {
                    legacy.CommandText = "DELETE FROM product_settings WHERE key=$key";
                }
                else
                {
                    legacy.CommandText = """
                        INSERT INTO product_settings(key,value) VALUES($key,$path)
                        ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                        """;
                    legacy.Parameters.AddWithValue("$path", normalizedPath);
                }
                legacy.Parameters.AddWithValue("$key", "avatar.expression." + state.Trim());
                legacy.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    private static void ValidateAvatarExpressionState(string state)
    {
        if (string.IsNullOrWhiteSpace(state) || !AvatarExpressionStates.Contains(state.Trim()))
            throw new ArgumentException("Estado de expresión no soportado.", nameof(state));
    }

    public IReadOnlyList<AvatarLayer> GetAvatarLayers()
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,name,path,sort_order,visible,offset_x,offset_y,scale,rotation_degrees FROM avatar_layers WHERE profile_id=$profileId ORDER BY sort_order,id";
            cmd.Parameters.AddWithValue("$profileId", GetActiveAvatarProfileId(c));
            using var r = cmd.ExecuteReader();
            var list = new List<AvatarLayer>();
            while (r.Read())
                list.Add(new AvatarLayer(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4) != 0,
                    r.GetDouble(5), r.GetDouble(6), r.GetDouble(7), r.GetDouble(8)));
            return list;
        }
    }

    public long AddAvatarLayer(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || !File.Exists(path))
            throw new ArgumentException("La capa necesita un nombre y un archivo existente.");
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            var profileId = GetActiveAvatarProfileId(c);
            cmd.CommandText = "INSERT INTO avatar_layers(profile_id,name,path,sort_order,visible) VALUES($profileId,$n,$p,(SELECT COALESCE(MAX(sort_order),-1)+1 FROM avatar_layers WHERE profile_id=$profileId),1); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$profileId", profileId);
            cmd.Parameters.AddWithValue("$n", name.Trim());
            cmd.Parameters.AddWithValue("$p", Path.GetFullPath(path));
            return (long)cmd.ExecuteScalar()!;
        }
    }

    public void SetAvatarLayerVisibility(long id, bool visible)
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE avatar_layers SET visible=$v WHERE id=$id AND profile_id=$profileId";
            cmd.Parameters.AddWithValue("$v", visible ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$profileId", GetActiveAvatarProfileId(c));
            if (cmd.ExecuteNonQuery() == 0) throw new KeyNotFoundException($"No existe la capa {id}.");
        }
    }

    public void UpdateAvatarLayerTransform(long id, double offsetX, double offsetY, double scale, double rotationDegrees)
    {
        if (!double.IsFinite(offsetX) || offsetX is < -500 or > 500) throw new ArgumentOutOfRangeException(nameof(offsetX), "X debe estar entre -500 y 500 píxeles.");
        if (!double.IsFinite(offsetY) || offsetY is < -500 or > 500) throw new ArgumentOutOfRangeException(nameof(offsetY), "Y debe estar entre -500 y 500 píxeles.");
        if (!double.IsFinite(scale) || scale is < 0.1 or > 4) throw new ArgumentOutOfRangeException(nameof(scale), "La escala debe estar entre 0,1 y 4.");
        if (!double.IsFinite(rotationDegrees) || rotationDegrees is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(rotationDegrees), "La rotación debe estar entre -180 y 180 grados.");

        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE avatar_layers SET offset_x=$x,offset_y=$y,scale=$s,rotation_degrees=$r WHERE id=$id AND profile_id=$profileId";
            cmd.Parameters.AddWithValue("$profileId", GetActiveAvatarProfileId(c));
            cmd.Parameters.AddWithValue("$x", offsetX);
            cmd.Parameters.AddWithValue("$y", offsetY);
            cmd.Parameters.AddWithValue("$s", scale);
            cmd.Parameters.AddWithValue("$r", rotationDegrees);
            cmd.Parameters.AddWithValue("$id", id);
            if (cmd.ExecuteNonQuery() == 0) throw new KeyNotFoundException($"No existe la capa {id}.");
        }
        Log("Avatar", $"Transformación guardada para la capa {id}.");
    }

    /// <summary>Moves a layer one step in its visual stacking order. Direction -1 moves down; +1 moves up.</summary>
    public void MoveAvatarLayer(long id, int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        lock (gate)
        {
            using var c = Open();
            var profileId = GetActiveAvatarProfileId(c);
            var ordered = new List<(long Id, int SortOrder)>();
            using (var query = c.CreateCommand())
            {
                query.CommandText = "SELECT id,sort_order FROM avatar_layers WHERE profile_id=$profileId ORDER BY sort_order,id";
                query.Parameters.AddWithValue("$profileId", profileId);
                using var reader = query.ExecuteReader();
                while (reader.Read()) ordered.Add((reader.GetInt64(0), reader.GetInt32(1)));
            }

            var index = ordered.FindIndex(layer => layer.Id == id);
            if (index < 0) throw new KeyNotFoundException($"No existe la capa {id}.");
            var neighbourIndex = index + direction;
            if (neighbourIndex < 0 || neighbourIndex >= ordered.Count) return;

            var current = ordered[index];
            var neighbour = ordered[neighbourIndex];
            using var transaction = c.BeginTransaction();
            using var update = c.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE avatar_layers
                SET sort_order = CASE id
                    WHEN $current_id THEN $neighbour_order
                    WHEN $neighbour_id THEN $current_order
                END
                WHERE profile_id=$profile_id AND id IN ($current_id, $neighbour_id);
                """;
            update.Parameters.AddWithValue("$profile_id", profileId);
            update.Parameters.AddWithValue("$current_id", current.Id);
            update.Parameters.AddWithValue("$neighbour_id", neighbour.Id);
            update.Parameters.AddWithValue("$current_order", current.SortOrder);
            update.Parameters.AddWithValue("$neighbour_order", neighbour.SortOrder);
            update.ExecuteNonQuery();
            transaction.Commit();
        }
        Log("Avatar", $"Orden de la capa {id} actualizado.");
    }

    public void DeleteAvatarLayer(long id)
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM avatar_layers WHERE id=$id AND profile_id=$profileId";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$profileId", GetActiveAvatarProfileId(c));
            cmd.ExecuteNonQuery();
        }
        Log("Avatar", $"Capa ID {id} eliminada del perfil.");
    }

    public IReadOnlyList<AutomationRule> GetAutomationRules()
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,name,message,interval_minutes,enabled,last_run_utc FROM automation_rules ORDER BY name";
            using var r = cmd.ExecuteReader();
            var list = new List<AutomationRule>();
            while (r.Read())
            {
                DateTimeOffset? lastRun = r.IsDBNull(5) ? null : DateTimeOffset.Parse(r.GetString(5));
                list.Add(new AutomationRule(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4) != 0, lastRun));
            }
            return list;
        }
    }

    public void MarkAutomationRun(long id, DateTimeOffset atUtc)
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE automation_rules SET last_run_utc=$at WHERE id=$id";
            cmd.Parameters.AddWithValue("$at", atUtc.ToUniversalTime().ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);
            if (cmd.ExecuteNonQuery() == 0)
                throw new KeyNotFoundException($"No existe la automatización {id}.");
        }
    }

    public void SaveAutomationRule(long? id, string name, string message, int intervalMinutes, bool enabled)
    {
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Nombre y mensaje son obligatorios.");
        if(intervalMinutes is < 5 or > 1440) throw new ArgumentOutOfRangeException(nameof(intervalMinutes),"El intervalo debe estar entre 5 y 1440 minutos.");
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); if(id is null) cmd.CommandText="INSERT INTO automation_rules(name,message,interval_minutes,enabled) VALUES($n,$m,$i,$e)"; else { cmd.CommandText="UPDATE automation_rules SET name=$n,message=$m,interval_minutes=$i,enabled=$e WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id.Value); } cmd.Parameters.AddWithValue("$n",name.Trim()); cmd.Parameters.AddWithValue("$m",message.Trim()); cmd.Parameters.AddWithValue("$i",intervalMinutes); cmd.Parameters.AddWithValue("$e",enabled?1:0); cmd.ExecuteNonQuery(); }
        Log("Automatización",$"Mensaje programado '{name}' guardado.");
    }

    public void DeleteAutomationRule(long id)
    {
        lock (gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM automation_rules WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            if (cmd.ExecuteNonQuery() == 0)
                throw new KeyNotFoundException($"No existe la automatización {id}.");
        }
        Log("Automatización", $"Mensaje programado ID {id} eliminado.");
    }

    public IReadOnlyList<ProductActivity> GetActivity(int limit=100)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT at_utc,module,message FROM product_activity ORDER BY id DESC LIMIT $limit"; cmd.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,500)); using var r=cmd.ExecuteReader(); var list=new List<ProductActivity>(); while(r.Read()) list.Add(new(DateTimeOffset.Parse(r.GetString(0)),r.GetString(1),r.GetString(2))); return list; }
    }

    public void Log(string module,string message)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO product_activity(at_utc,module,message) VALUES($at,$module,$message)"; cmd.Parameters.AddWithValue("$at",Now()); cmd.Parameters.AddWithValue("$module",module); cmd.Parameters.AddWithValue("$message",message); cmd.ExecuteNonQuery(); }
    }

    public string GetSetting(string key,string fallback="")
    {
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT value FROM product_settings WHERE key=$key"; cmd.Parameters.AddWithValue("$key",key); return cmd.ExecuteScalar() as string ?? fallback;
    }
    public void SetSetting(string key,string value)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO product_settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value"; cmd.Parameters.AddWithValue("$k",key); cmd.Parameters.AddWithValue("$v",value); cmd.ExecuteNonQuery(); }
    }
    private static void ValidateUsername(string username) { if(string.IsNullOrWhiteSpace(username)||username.Length>25||username.Any(ch=>!(char.IsLetterOrDigit(ch)||ch=='_'||ch=='-'))) throw new ArgumentException("El usuario debe contener solo letras, números, guion o guion bajo (máx. 25 caracteres).",nameof(username)); }
}
