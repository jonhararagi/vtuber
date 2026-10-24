using System.Globalization;
using Microsoft.Data.Sqlite;

namespace VtuberBot.Core;

public sealed record ChatArchiveMessage(
    string Platform,
    string Channel,
    DateTimeOffset AtUtc,
    string Username,
    string Message,
    string? ProviderMessageId = null);

public sealed record ChatArchiveEntry(
    long Id,
    string Platform,
    string Channel,
    DateTimeOffset AtUtc,
    string Username,
    string Message,
    string? ProviderMessageId);

public sealed record ChatArchiveStats(
    long Total,
    long Twitch,
    long Kick,
    DateTimeOffset? FirstAtUtc,
    DateTimeOffset? LastAtUtc);

/// <summary>
/// Local-only, opt-in archive for public Twitch/Kick chat messages. It uses a separate SQLite
/// database so clearing a chat archive can never remove commands, settings, points or giveaways.
/// </summary>
public sealed class ChatArchiveStore
{
    public const int MaximumRows = 100_000;
    public const int MaximumMessageLength = 1_000;
    public const int MaximumUsernameLength = 64;
    public const int MaximumChannelLength = 100;
    public const int MinimumRetentionDays = 1;
    public const int MaximumRetentionDays = 365;
    public const int MaximumSearchResults = 1_000;

    private readonly string connectionString;
    private readonly object gate = new();
    private int writesSincePrune;

    public ChatArchiveStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5
        };
        connectionString = builder.ConnectionString;

        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                CREATE TABLE IF NOT EXISTS chat_archive_messages(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    platform TEXT NOT NULL CHECK(platform IN ('Twitch','Kick')),
                    channel TEXT NOT NULL COLLATE NOCASE,
                    at_utc TEXT NOT NULL,
                    username TEXT NOT NULL COLLATE NOCASE,
                    message TEXT NOT NULL,
                    provider_message_id TEXT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_chat_archive_message_id
                    ON chat_archive_messages(platform, channel, provider_message_id)
                    WHERE provider_message_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS idx_chat_archive_at
                    ON chat_archive_messages(at_utc DESC, id DESC);
                CREATE INDEX IF NOT EXISTS idx_chat_archive_platform_at
                    ON chat_archive_messages(platform, at_utc DESC, id DESC);
                CREATE INDEX IF NOT EXISTS idx_chat_archive_username
                    ON chat_archive_messages(username COLLATE NOCASE);
                """;
            command.ExecuteNonQuery();
        }
    }

    public bool TryAdd(ChatArchiveMessage message, int retentionDays = 30)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateRetentionDays(retentionDays);

        var platform = NormalizePlatform(message.Platform);
        var channel = ValidateRequired(message.Channel, MaximumChannelLength, nameof(message.Channel));
        var username = ValidateRequired(message.Username, MaximumUsernameLength, nameof(message.Username));
        var text = message.Message?.Replace("\0", string.Empty) ?? throw new ArgumentNullException(nameof(message.Message));
        if (text.Length > MaximumMessageLength)
            throw new ArgumentOutOfRangeException(nameof(message.Message), "El mensaje supera el límite de almacenamiento.");
        var messageId = string.IsNullOrWhiteSpace(message.ProviderMessageId)
            ? null
            : message.ProviderMessageId.Trim();
        if (messageId is { Length: > 200 })
            throw new ArgumentOutOfRangeException(nameof(message.ProviderMessageId));
        var atUtc = message.AtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO chat_archive_messages
                    (platform, channel, at_utc, username, message, provider_message_id)
                VALUES ($platform, $channel, $atUtc, $username, $message, $messageId);
                """;
            command.Parameters.AddWithValue("$platform", platform);
            command.Parameters.AddWithValue("$channel", channel);
            command.Parameters.AddWithValue("$atUtc", atUtc);
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$message", text);
            command.Parameters.AddWithValue("$messageId", (object?)messageId ?? DBNull.Value);
            var inserted = command.ExecuteNonQuery() > 0;

            if (inserted && ++writesSincePrune >= 100)
            {
                writesSincePrune = 0;
                PruneCore(connection, retentionDays, MaximumRows);
            }

            return inserted;
        }
    }

    public IReadOnlyList<ChatArchiveEntry> Search(
        string? query = null,
        string? platform = null,
        int limit = 200)
    {
        limit = Math.Clamp(limit, 1, MaximumSearchResults);
        var normalizedPlatform = string.IsNullOrWhiteSpace(platform) ||
            string.Equals(platform, "Todas", StringComparison.OrdinalIgnoreCase)
            ? null
            : NormalizePlatform(platform);
        var term = (query ?? string.Empty).Trim();
        if (term.Length > 100)
            term = term[..100];

        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, platform, channel, at_utc, username, message, provider_message_id
                FROM chat_archive_messages
                WHERE ($platform IS NULL OR platform = $platform)
                  AND ($query = '' OR
                       instr(lower(username), lower($query)) > 0 OR
                       instr(lower(channel), lower($query)) > 0 OR
                       instr(lower(message), lower($query)) > 0)
                ORDER BY at_utc DESC, id DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$platform", (object?)normalizedPlatform ?? DBNull.Value);
            command.Parameters.AddWithValue("$query", term);
            command.Parameters.AddWithValue("$limit", limit);

            using var reader = command.ExecuteReader();
            var results = new List<ChatArchiveEntry>();
            while (reader.Read())
            {
                results.Add(new ChatArchiveEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.None),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
            return results;
        }
    }

    public ChatArchiveStats GetStats()
    {
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*),
                       COALESCE(SUM(CASE WHEN platform='Twitch' THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN platform='Kick' THEN 1 ELSE 0 END), 0),
                       MIN(at_utc), MAX(at_utc)
                FROM chat_archive_messages;
                """;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return new ChatArchiveStats(0, 0, 0, null, null);
            return new ChatArchiveStats(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.None),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.None));
        }
    }

    public int DeleteOlderThanDays(int retentionDays)
    {
        ValidateRetentionDays(retentionDays);
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM chat_archive_messages WHERE at_utc < $cutoff;";
            command.Parameters.AddWithValue("$cutoff",
                DateTimeOffset.UtcNow.AddDays(-retentionDays).ToString("O", CultureInfo.InvariantCulture));
            return command.ExecuteNonQuery();
        }
    }

    public int Prune(int retentionDays = 30, int maximumRows = MaximumRows)
    {
        ValidateRetentionDays(retentionDays);
        if (maximumRows is < 1 or > MaximumRows)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        lock (gate)
        {
            using var connection = Open();
            return PruneCore(connection, retentionDays, maximumRows);
        }
    }

    public int DeleteAll()
    {
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM chat_archive_messages;";
            return command.ExecuteNonQuery();
        }
    }

    private static int PruneCore(SqliteConnection connection, int retentionDays, int maximumRows)
    {
        using var transaction = connection.BeginTransaction();
        using var expired = connection.CreateCommand();
        expired.Transaction = transaction;
        expired.CommandText = "DELETE FROM chat_archive_messages WHERE at_utc < $cutoff;";
        expired.Parameters.AddWithValue("$cutoff",
            DateTimeOffset.UtcNow.AddDays(-retentionDays).ToString("O", CultureInfo.InvariantCulture));
        var deleted = expired.ExecuteNonQuery();

        using var overCapacity = connection.CreateCommand();
        overCapacity.Transaction = transaction;
        overCapacity.CommandText = """
            DELETE FROM chat_archive_messages
            WHERE id NOT IN (
                SELECT id FROM chat_archive_messages
                ORDER BY at_utc DESC, id DESC
                LIMIT $maximumRows);
            """;
        overCapacity.Parameters.AddWithValue("$maximumRows", maximumRows);
        deleted += overCapacity.ExecuteNonQuery();
        transaction.Commit();
        return deleted;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string NormalizePlatform(string platform)
    {
        if (string.Equals(platform, "Twitch", StringComparison.OrdinalIgnoreCase))
            return "Twitch";
        if (string.Equals(platform, "Kick", StringComparison.OrdinalIgnoreCase))
            return "Kick";
        throw new ArgumentOutOfRangeException(nameof(platform), "Solo se admiten mensajes públicos de Twitch o Kick.");
    }

    private static string ValidateRequired(string? value, int maximumLength, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw new ArgumentOutOfRangeException(parameterName, "El valor está vacío o supera el límite permitido.");
        return normalized;
    }

    private static void ValidateRetentionDays(int days)
    {
        if (days is < MinimumRetentionDays or > MaximumRetentionDays)
            throw new ArgumentOutOfRangeException(nameof(days), "La retención debe estar entre 1 y 365 días.");
    }
}
