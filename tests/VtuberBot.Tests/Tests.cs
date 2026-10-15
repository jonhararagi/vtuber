using Xunit;
using LocalSpeechQueue = VtuberBot.App.LocalSpeechQueue;
using ILocalSpeechSession = VtuberBot.App.ILocalSpeechSession;
using SpeechPlaybackResult = VtuberBot.App.SpeechPlaybackResult;
using CoreTaskScheduler = VtuberBot.Core.TaskScheduler;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using VtuberBot.Core;

namespace VtuberBot.Tests;

public sealed class Tests
{

    [Theory]
    [InlineData(null, "local")]
    [InlineData("", "local")]
    [InlineData("unknown", "local")]
    [InlineData("local", "local")]
    [InlineData("remote", "remote")]
    [InlineData(" REMOTE ", "remote")]
    public void ProviderPreferenceDefaultsUseLocalUnlessRemoteWasExplicitlySaved(string? savedValue, string expected)
    {
        Assert.Equal(expected, ProviderPreferenceDefaults.Resolve(savedValue));
        Assert.Equal(expected == "remote", ProviderPreferenceDefaults.IsRemote(savedValue));
    }


    [Fact]
    public void Pcm16LevelPolicyReturnsZeroForIncompleteSample()
    {
        Assert.Equal(0, Pcm16LevelPolicy.CalculatePercent(ReadOnlySpan<byte>.Empty));
        Assert.Equal(0, Pcm16LevelPolicy.CalculatePercent(new byte[] { 0x7F }));
    }

    [Fact]
    public void Pcm16LevelPolicyNormalizesSilenceAndClampsLoudInput()
    {
        Assert.Equal(0, Pcm16LevelPolicy.CalculatePercent(new byte[] { 0, 0, 0, 0 }));
        Assert.Equal(100, Pcm16LevelPolicy.CalculatePercent(new byte[] { 0xFF, 0x7F, 0x00, 0x80 }));
    }

    [Fact]
    public void Pcm16LevelPolicyIgnoresTrailingIncompleteByte()
    {
        var complete = Pcm16LevelPolicy.CalculatePercent(new byte[] { 0x00, 0x20 });
        var withTrailingByte = Pcm16LevelPolicy.CalculatePercent(new byte[] { 0x00, 0x20, 0xFF });

        Assert.Equal(complete, withTrailingByte);
    }

    [Fact]
    public void ConfigMissingFileUsesDefaults()
    {
        using var directory = new TempDirectory();
        var config = AppConfig.Load(Path.Combine(directory.Path, "missing.toml"));

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
        Assert.Null(config.ConfigPath);
    }

    [Fact]
    public void ConfigEmptyTomlUsesDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, string.Empty);

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyDatabaseUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[database]\npath = \"data/custom.sqlite3\"");

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "custom.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyAppUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[app]\nname = \"Configured Bot\"");

        var config = AppConfig.Load(path);

        Assert.Equal("Configured Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyLoggingUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[logging]\nlevel = \"DEBUG\"");

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(LogLevel.Debug, config.LogLevel);
    }

    [Fact]
    public void ConfigInvalidTomlThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[app\nname = \"broken\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigInvalidLoggingLevelThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[logging]\nlevel = \"NOPE\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigRelativePathUsesConfigDirectory()
    {
        using var directory = new TempDirectory();
        var path = directory.File("nested", "config.toml");
        File.WriteAllText(path, "[database]\npath = \"db/vtuber.sqlite3\"");

        var config = AppConfig.Load(path);

        Assert.Equal(
            Path.Combine(directory.Path, "nested", "db", "vtuber.sqlite3"),
            config.DatabasePath);
    }

    [Fact]
    public void ConfigWrongAppSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "app = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigWrongDatabaseSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "database = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigWrongLoggingSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "logging = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigEmptyDatabasePathThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[database]\npath = \"\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigDefaultBaseDirectory()
    {
        var config = AppConfig.Load();

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VtuberBot"),
            config.BaseDirectory);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(config.BaseDirectory, "data", "vtuber.sqlite3")),
            config.DatabasePath);
    }


    [Fact]
    public void DatabaseHasNoSharedPublicConnection()
    {
        Assert.Null(typeof(SqliteDatabase).GetProperty("Connection"));

        Assert.DoesNotContain(
            typeof(SqliteDatabase).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => field.FieldType == typeof(SqliteConnection));
    }

    [Fact]
    public void DatabaseCreatesSchemaAndConfiguresRequiredSettings()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();

        Assert.Equal(1L, database.QueryScalar("PRAGMA foreign_keys"));
        Assert.Equal(2L, database.QueryScalar("PRAGMA synchronous"));
        Assert.Equal("wal", database.QueryScalar("PRAGMA journal_mode"));
        Assert.Equal(
            SqliteDatabase.SchemaVersion.ToString(),
            database.QueryScalar("SELECT value FROM schema_meta WHERE key = 'schema_version'"));
    }

    [Fact]
    public void DatabaseExecuteAndQueryScalarUseParameters()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.Execute("CREATE TABLE sample(value TEXT NOT NULL)");
        Assert.Equal(
            1,
            database.Execute(
                "INSERT INTO sample(value) VALUES ($value)",
                ("$value", "hello")));

        Assert.Equal(
            "hello",
            database.QueryScalar(
                "SELECT value FROM sample WHERE value = $value",
                ("$value", "hello")));
    }

    [Fact]
    public void DatabaseOpenAndCloseAreIdempotentAndReopenWorks()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.Open();
        database.InitializeSchema();
        database.Close();
        database.Close();

        Assert.Throws<InvalidOperationException>(() => database.Execute("SELECT 1"));

        database.Open();
        Assert.Equal(1L, database.QueryScalar("PRAGMA foreign_keys"));
        Assert.Equal(2L, database.QueryScalar("PRAGMA synchronous"));
    }

    [Fact]
    public void DatabaseRepeatedOpenInitializeCloseCyclesAreStable()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        for (var cycle = 1; cycle <= 40; cycle++)
        {
            database.Open();
            database.InitializeSchema();

            Assert.Equal(
                SqliteDatabase.SchemaVersion.ToString(),
                database.QueryScalar(
                    "SELECT value FROM schema_meta WHERE key = 'schema_version'"));

            database.Close();

            Assert.Throws<InvalidOperationException>(
                () => database.QueryScalar("SELECT 1"));
        }
    }

    [Fact]
    public void DatabaseSqlErrorDoesNotPoisonSubsequentOperations()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();

        Assert.Throws<SqliteException>(() => database.Execute("THIS IS NOT SQL"));
        Assert.Equal(1L, database.QueryScalar("SELECT 1"));
    }

    [Fact]
    public void DatabaseInitializeSchemaIsIdempotent()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.InitializeSchema();

        Assert.Equal(
            SqliteDatabase.SchemaVersion.ToString(),
            database.QueryScalar("SELECT value FROM schema_meta WHERE key = 'schema_version'"));
    }


    [Fact]
    public async Task DatabaseSupportsConcurrentReaders()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();

        var tasks = Enumerable.Range(0, 24)
            .Select(_ => System.Threading.Tasks.Task.Run(
                () => database.QueryScalar("SELECT 1"),
                TestContext.Current.CancellationToken))
            .ToArray();

        var results = await System.Threading.Tasks.Task.WhenAll(tasks);

        Assert.All(results, result => Assert.Equal(1L, result));
    }

    [Fact]
    public async Task DatabaseSerializesConcurrentWriters()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var tasks = Enumerable.Range(0, 32)
            .Select(value => System.Threading.Tasks.Task.Run(
                () => database.Execute(
                    "INSERT INTO sample(value) VALUES ($value)",
                    ("$value", value)),
                TestContext.Current.CancellationToken))
            .ToArray();

        await System.Threading.Tasks.Task.WhenAll(tasks);

        Assert.Equal(32L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public async Task DatabaseAllowsReadersWhileWriterIsActive()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var writerTask = System.Threading.Tasks.Task.Run(
            () =>
            {
                database.ExecuteTransaction((connection, transaction) =>
                {
                    entered.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO sample(value) VALUES (1)";
                    command.ExecuteNonQuery();
                });
            },
            TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(0L, database.QueryScalar("SELECT COUNT(*) FROM sample"));

        release.TrySetResult(true);
        await writerTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public async Task DatabaseWriterGateBlocksSecondWriterUntilFirstTransactionCompletes()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWriter = System.Threading.Tasks.Task.Run(
            () =>
            {
                database.ExecuteTransaction((connection, transaction) =>
                {
                    firstEntered.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO sample(value) VALUES (1)";
                    command.ExecuteNonQuery();
                });
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var secondWriter = System.Threading.Tasks.Task.Run(
            () =>
            {
                secondStarted.TrySetResult(true);
                return database.Execute(
                    "INSERT INTO sample(value) VALUES (2)");
            },
            TestContext.Current.CancellationToken);

        await secondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        Assert.False(secondWriter.IsCompleted);

        release.TrySetResult(true);

        await System.Threading.Tasks.Task.WhenAll(
            firstWriter.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken),
            secondWriter.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        Assert.Equal(2L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public async Task DatabaseWriterGateBlocksExecuteScalarWriteUntilFirstTransactionCompletes()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWriter = System.Threading.Tasks.Task.Run(
            () =>
                database.ExecuteTransaction((connection, transaction) =>
                {
                    firstEntered.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO sample(value) VALUES (1)";
                    command.ExecuteNonQuery();
                }),
            TestContext.Current.CancellationToken);

        await firstEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var scalarWriter = System.Threading.Tasks.Task.Run(
            () => database.ExecuteScalar(
                "INSERT INTO sample(value) VALUES (2) RETURNING value"),
            TestContext.Current.CancellationToken);

        Assert.False(scalarWriter.IsCompleted);

        release.TrySetResult(true);

        await firstWriter.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var scalarResult = await scalarWriter.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(2L, scalarResult);
        Assert.Equal(2L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public void DatabaseTransactionRollsBackOnFailure()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        Assert.Throws<InvalidOperationException>(() =>
            database.ExecuteTransaction((connection, transaction) =>
            {
                using var first = connection.CreateCommand();
                first.Transaction = transaction;
                first.CommandText = "INSERT INTO sample(value) VALUES (1)";
                first.ExecuteNonQuery();

                using var second = connection.CreateCommand();
                second.Transaction = transaction;
                second.CommandText = "INSERT INTO sample(value) VALUES (2)";
                second.ExecuteNonQuery();

                throw new InvalidOperationException("expected rollback");
            }));

        Assert.Equal(0L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public async Task DatabaseCloseDoesNotInterruptAlreadyAcquiredTransaction()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var transactionTask = System.Threading.Tasks.Task.Run(
            () =>
                database.ExecuteTransaction((connection, transaction) =>
                {
                    entered.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO sample(value) VALUES (7)";
                    command.ExecuteNonQuery();
                }),
            TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        database.Close();
        Assert.Throws<InvalidOperationException>(() => database.QueryScalar("SELECT 1"));

        release.TrySetResult(true);
        await transactionTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        database.Open();
        Assert.Equal(1L, database.QueryScalar("SELECT COUNT(*) FROM sample"));
    }

    [Fact]
    public async Task DatabaseCloseRejectsNewWriterWithoutWaiting()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");
        database.Close();

        var writerTask = System.Threading.Tasks.Task.Run(
            () =>
            {
                database.Execute("INSERT INTO sample(value) VALUES (1)");
                return true;
            },
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writerTask.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));

        Assert.Contains("not open", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabaseRejectsFutureSchemaVersionWithoutDowngrade()
    {
        using var directory = new TempDirectory();
        var path = directory.File("a.db");

        using (var connection = new SqliteConnection(
            $"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE schema_meta(key TEXT PRIMARY KEY, value TEXT NOT NULL); " +
                "INSERT INTO schema_meta(key, value) VALUES ('schema_version', '99');";
            command.ExecuteNonQuery();
        }

        using var database = new SqliteDatabase(path);
        database.Open();

        var exception = Assert.Throws<SqliteSchemaException>(
            () => database.InitializeSchema());

        Assert.Contains("newer than supported", exception.Message);
        Assert.Equal("99", database.QueryScalar(
            "SELECT value FROM schema_meta WHERE key = 'schema_version'"));
    }

    [Fact]
    public async Task DatabaseCloseRechecksLifecycleAfterWaitingForWriterGate()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstWriter = System.Threading.Tasks.Task.Run(
            () =>
                database.ExecuteTransaction((connection, transaction) =>
                {
                    firstEntered.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO sample(value) VALUES (1)";
                    command.ExecuteNonQuery();
                }),
            TestContext.Current.CancellationToken);

        await firstEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var secondWriter = System.Threading.Tasks.Task.Run(
            () =>
            {
                secondStarted.TrySetResult(true);
                database.Execute("INSERT INTO sample(value) VALUES (2)");
            },
            TestContext.Current.CancellationToken);

        await secondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        Assert.False(secondWriter.IsCompleted);

        database.Close();
        release.TrySetResult(true);

        await firstWriter.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => secondWriter.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));

        Assert.Contains("not open", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabaseRejectsInvalidSchemaVersion()
    {
        using var directory = new TempDirectory();
        var path = directory.File("a.db");

        using (var connection = new SqliteConnection(
            $"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE schema_meta(key TEXT PRIMARY KEY, value TEXT NOT NULL); " +
                "INSERT INTO schema_meta(key, value) VALUES ('schema_version', 'abc');";
            command.ExecuteNonQuery();
        }

        using var database = new SqliteDatabase(path);
        database.Open();

        Assert.Throws<SqliteSchemaException>(() => database.InitializeSchema());
        Assert.Equal("abc", database.QueryScalar(
            "SELECT value FROM schema_meta WHERE key = 'schema_version'"));
    }

    [Fact]
    public void DatabaseSchemaInitializationFailureRollsBackAndCanRecover()
    {
        using var directory = new TempDirectory();
        var path = directory.File("a.db");

        using (var connection = new SqliteConnection(
            $"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE schema_meta(" +
                "key TEXT PRIMARY KEY, " +
                "value TEXT NOT NULL CHECK(value = 'never-valid'));";
            command.ExecuteNonQuery();
        }

        using var database = new SqliteDatabase(path);
        database.Open();

        Assert.Throws<SqliteException>(() => database.InitializeSchema());

        Assert.Null(database.QueryScalar(
            "SELECT value FROM schema_meta WHERE key = 'schema_version'"));

        database.Close();

        using (var connection = new SqliteConnection(
            $"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE schema_meta";
            command.ExecuteNonQuery();
        }

        database.Open();
        database.InitializeSchema();

        Assert.Equal(
            SqliteDatabase.SchemaVersion.ToString(),
            database.QueryScalar("SELECT value FROM schema_meta WHERE key = 'schema_version'"));
    }

    [Fact]
    public async Task DatabaseRetriesBusyUntilExternalLockIsReleased()
    {
        using var directory = new TempDirectory();
        var path = directory.File("a.db");
        using var database = new SqliteDatabase(path);

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        using var locker = new SqliteConnection(
            $"Data Source={path};Pooling=False;Default Timeout=5");
        locker.Open();
        using var transaction = locker.BeginTransaction(deferred: false);

        var writeTask = System.Threading.Tasks.Task.Run(
            () => database.Execute(
                "INSERT INTO sample(value) VALUES (1)"),
            TestContext.Current.CancellationToken);

        var completedEarly = await System.Threading.Tasks.Task.WhenAny(
            writeTask,
            System.Threading.Tasks.Task.Delay(
                TimeSpan.FromMilliseconds(250),
                TestContext.Current.CancellationToken));

        Assert.NotSame(writeTask, completedEarly);

        transaction.Rollback();

        Assert.Equal(
            1,
            await writeTask.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DatabaseBusyEventuallyFailsAtBoundedTimeoutAndRecovers()
    {
        using var directory = new TempDirectory();
        var path = directory.File("a.db");
        using var database = new SqliteDatabase(path);

        database.Open();
        database.InitializeSchema();
        database.Execute("CREATE TABLE sample(value INTEGER NOT NULL)");

        using var locker = new SqliteConnection(
            $"Data Source={path};Pooling=False;Default Timeout=5");
        locker.Open();
        using var transaction = locker.BeginTransaction(deferred: false);

        var start = Stopwatch.GetTimestamp();
        var exception = Assert.Throws<SqliteException>(
            () => database.Execute("INSERT INTO sample(value) VALUES (1)"));
        var elapsed = Stopwatch.GetElapsedTime(start);

        Assert.True(exception.SqliteErrorCode is 5 or 6);
        Assert.InRange(elapsed, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8));

        transaction.Rollback();

        Assert.Equal(
            1,
            database.Execute("INSERT INTO sample(value) VALUES (2)"));
    }

    [Fact]
    public void EventsRejectInvalidNamesAndDuplicateHandlers()
    {
        var manager = new EventManager(new TestLogger());
        var calls = 0;
        Action<AppEvent> handler = _ => calls++;

        Assert.Throws<ArgumentException>(() => new AppEvent(" "));
        Assert.Throws<ArgumentException>(() => manager.Subscribe("", handler));

        manager.Subscribe("event", handler);
        manager.Subscribe("event", handler);
        manager.Emit(new AppEvent("event"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void EventsUnsubscribeAndSnapshotAreSafe()
    {
        var manager = new EventManager(new TestLogger());
        var calls = 0;
        Action<AppEvent> handler = null!;
        handler = _ =>
        {
            calls++;
            manager.Unsubscribe("event", handler!);
        };

        manager.Subscribe("event", handler);
        manager.Emit(new AppEvent("event"));
        manager.Emit(new AppEvent("event"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void EventsIsolateHandlerFailure()
    {
        var manager = new EventManager(new TestLogger());
        var calls = new List<string>();

        manager.Subscribe("event", _ =>
        {
            calls.Add("bad");
            throw new InvalidOperationException("expected");
        });
        manager.Subscribe("event", _ => calls.Add("good"));

        manager.Emit(new AppEvent("event"));

        Assert.Equal(["bad", "good"], calls);
    }

    [Fact]
    public void ResourcesCleanupInReverseOrderAndContinueAfterFailure()
    {
        var manager = new ResourceManager(new TestLogger());
        var calls = new List<string>();

        manager.Register("a", () => calls.Add("a"));
        manager.Register("b", () =>
        {
            calls.Add("b");
            throw new InvalidOperationException("expected");
        });
        manager.Register("c", () => calls.Add("c"));

        manager.CleanupAll();

        Assert.Equal(["c", "b", "a"], calls);
    }

    [Fact]
    public void ResourcesDuplicateNamesReplaceCleanup()
    {
        var manager = new ResourceManager(new TestLogger());
        var calls = new List<string>();

        manager.Register("same", () => calls.Add("first"));
        manager.Register("same", () => calls.Add("second"));
        manager.CleanupAll();

        Assert.Equal(["second"], calls);
    }

    [Fact]
    public void ResourcesRejectInvalidNamesAndAreIdempotent()
    {
        var manager = new ResourceManager(new TestLogger());
        Assert.Throws<ArgumentException>(() => manager.Register(" ", () => { }));

        var calls = 0;
        manager.Register("a", () => calls++);
        manager.CleanupAll();
        manager.CleanupAll();

        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => manager.Register("b", () => { }));
    }

    [Fact]
    public void SchedulerExecutesNormalCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), signal.Set);

        Assert.True(signal.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerCancellationPreventsCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        var id = scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(200), signal.Set);

        Assert.True(scheduler.Cancel(id));
        Assert.False(scheduler.Cancel(id));
        Assert.False(signal.Wait(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerSupportsMultipleTasks()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new CountdownEvent(3);

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(10), () => signal.Signal());
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), () => signal.Signal());
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(30), () => signal.Signal());

        Assert.True(signal.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerIsolatesCallbackFailure()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(10), () => throw new InvalidOperationException("expected"));
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), signal.Set);

        // This is a worker-liveness assertion; allow a contended Windows CI runner time to schedule it.
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerShutdownDiscardsPendingTasks()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromSeconds(1), signal.Set);
        scheduler.Shutdown();

        Assert.False(signal.IsSet);
        Assert.Throws<InvalidOperationException>(
            () => scheduler.ScheduleOnce(TimeSpan.Zero, signal.Set));
    }

    [Fact]
    public void SchedulerShutdownWaitsForRunningCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.Zero, () =>
        {
            started.Set();
            Thread.Sleep(100);
            finished.Set();
        });

        Assert.True(started.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        scheduler.Shutdown();

        Assert.True(finished.IsSet);
    }

    [Fact]
    public async Task TaskSchedulerCallbackCanShutdownWithoutDeadlock()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());

        var callbackEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        scheduler.ScheduleOnce(
            TimeSpan.Zero,
            () =>
            {
                callbackEntered.TrySetResult(true);
                scheduler.Shutdown();
                callbackReturned.TrySetResult(true);
            });

        await callbackEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        await callbackReturned.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Throws<InvalidOperationException>(
            () => scheduler.ScheduleOnce(TimeSpan.Zero, () => { }));

        var workerField = typeof(CoreTaskScheduler).GetField(
            "worker",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.True(
            SpinWait.SpinUntil(
                () => workerField?.GetValue(scheduler) is null,
                TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task SchedulerConcurrentShutdownWaitsForRunningCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());

        var callbackEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackFinished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var selfShutdownReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        scheduler.ScheduleOnce(
            TimeSpan.Zero,
            () =>
            {
                callbackEntered.TrySetResult(true);
                scheduler.Shutdown();
                selfShutdownReturned.TrySetResult(true);
                releaseCallback.Task.GetAwaiter().GetResult();
                callbackFinished.TrySetResult(true);
            });

        await callbackEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await selfShutdownReturned.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var secondCallerStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondShutdown = System.Threading.Tasks.Task.Run(
            () =>
            {
                secondCallerStarted.TrySetResult(true);
                scheduler.Shutdown();
            },
            TestContext.Current.CancellationToken);

        try
        {
            await secondCallerStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            // The callback is held by an explicit gate. A concurrent external
            // Shutdown must not return until that callback is released and exits.
            var completedBeforeRelease = await System.Threading.Tasks.Task.WhenAny(
                secondShutdown,
                System.Threading.Tasks.Task.Delay(
                    TimeSpan.FromMilliseconds(150),
                    TestContext.Current.CancellationToken));

            Assert.NotSame(secondShutdown, completedBeforeRelease);
        }
        finally
        {
            releaseCallback.TrySetResult(true);
        }

        await secondShutdown.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await callbackFinished.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ApplicationLifecycleAndDoubleShutdown()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        Assert.Equal(AppState.Running, application.State);
        Assert.Equal(1, application.InternalTestEventsProcessed);

        application.Shutdown();
        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void ApplicationRepeatedInitializeShutdownCyclesAreStable()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);

        for (var cycle = 1; cycle <= 20; cycle++)
        {
            application.Initialize();

            Assert.Equal(AppState.Running, application.State);
            Assert.Equal(cycle, application.InternalTestEventsProcessed);
            Assert.True(application.PerformanceMonitor.IsRunning);

            var monitor = application.PerformanceMonitor;
            application.Shutdown();

            Assert.Equal(AppState.Stopped, application.State);
            Assert.False(monitor.IsRunning);
        }
    }

    [Fact]
    public void ApplicationStartupFailureEntersErrorAndCanShutdown()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[logging]\nlevel = \"NOPE\"");

        var application = new VtuberApplication(configPath);

        Assert.Throws<ApplicationException>(() => application.Initialize());
        Assert.Equal(AppState.Error, application.State);

        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void ApplicationDatabaseStartupFailureCleansPartialResources()
    {
        using var directory = new TempDirectory();
        var databaseDirectory = directory.Directory("database");
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, $"[database]\npath = \"{databaseDirectory.Replace("\\", "\\\\")}\"");

        var application = new VtuberApplication(configPath);

        Assert.Throws<ApplicationException>(() => application.Initialize());
        Assert.Equal(AppState.Error, application.State);

        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void VoiceDefaultsAndValidation()
    {
        var request = new SpeechRequest("hello");

        Assert.Null(request.Voice);
        Assert.Equal(SpeechPriority.Normal, request.Priority);
        Assert.Throws<ArgumentException>(() => new SpeechRequest(" "));
    }

    [Fact]
    public void CommandRequestParsesSimpleWhitespaceArguments()
    {
        var request = CommandRequest.Parse("help foo bar");

        Assert.Equal("help", request.Name);
        Assert.Equal(["foo", "bar"], request.Arguments);
    }

    [Fact]
    public void CommandRouterRegistersCommand()
    {
        var router = new CommandRouter(new TestLogger());
        router.Register(new CommandDefinition(
            "ping",
            "Check the router.",
            _ => CommandResult.Succeeded("pong")));

        var result = router.Execute("ping");

        Assert.True(result.Success);
        Assert.Equal("pong", result.Message);
    }

    [Fact]
    public void CommandRouterRejectsDuplicate()
    {
        var router = new CommandRouter(new TestLogger());
        router.Register(new CommandDefinition(
            "ping",
            "Check the router.",
            _ => CommandResult.Succeeded("pong")));

        Assert.Throws<InvalidOperationException>(
            () => router.Register(new CommandDefinition(
                "PING",
                "Duplicate.",
                _ => CommandResult.Succeeded("duplicate"))));
    }

    [Fact]
    public void CommandRouterUnknownCommandReturnsFailure()
    {
        var router = new CommandRouter(new TestLogger());

        var result = router.Execute("missing");

        Assert.False(result.Success);
        Assert.Contains("Unknown command: missing", result.Message);
    }

    [Fact]
    public void CommandRouterExecutesHandler()
    {
        var router = new CommandRouter(new TestLogger());
        IReadOnlyList<string> received = [];

        router.Register(new CommandDefinition(
            "echo",
            "Echo an argument.",
            request =>
            {
                received = request.Arguments;
                return CommandResult.Succeeded(request.Arguments[0]);
            }));

        var result = router.Execute("echo hello");

        Assert.True(result.Success);
        Assert.Equal("hello", result.Message);
        Assert.Equal(["hello"], received);
    }

    [Fact]
    public void CommandRouterReportsHandlerFailure()
    {
        var router = new CommandRouter(new TestLogger());
        router.Register(new CommandDefinition(
            "fail",
            "Fail intentionally.",
            _ => throw new InvalidOperationException("expected")));

        var result = router.Execute("fail");

        Assert.False(result.Success);
        Assert.Contains("Command failed: expected", result.Message);
    }

    [Fact]
    public void CommandRouterLookupIsCaseInsensitive()
    {
        var router = new CommandRouter(new TestLogger());
        router.Register(new CommandDefinition(
            "Ping",
            "Check the router.",
            _ => CommandResult.Succeeded("pong")));

        var result = router.Execute("pInG");

        Assert.True(result.Success);
        Assert.Equal("pong", result.Message);
    }

    [Fact]
    public void CommandRouterHelpOrderingIsDeterministic()
    {
        var router = new CommandRouter(new TestLogger());
        router.Register(new CommandDefinition("status", "Status.", _ => CommandResult.Succeeded("status")));
        router.Register(new CommandDefinition("help", "Help.", _ => CommandResult.Succeeded("help")));
        router.Register(new CommandDefinition("ping", "Ping.", _ => CommandResult.Succeeded("ping")));

        Assert.Equal(
            "help — Help." + Environment.NewLine +
            "ping — Ping." + Environment.NewLine +
            "status — Status.",
            router.GetHelpText());
    }

    [Fact]
    public void CommandHelpWorks()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            var result = application.ExecuteCommand("help");

            Assert.True(result.Success);
            Assert.Contains("help — List available commands.", result.Message);
            Assert.Contains("ping — Check that the command core is responding.", result.Message);
            Assert.Contains("status — Show the current application status.", result.Message);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void CommandStatusWorks()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            var result = application.ExecuteCommand("STATUS");

            Assert.True(result.Success);
            Assert.Contains("Name: VTuber Bot", result.Message);
            Assert.Contains($"Version: {VtuberApplication.Version}", result.Message);
            Assert.Contains("State: Running", result.Message);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void ApplicationCreatesMissingParentDirectoryForConfiguredDatabase()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        var databasePath = Path.Combine(directory.Path, "nested", "state.sqlite3");
        File.WriteAllText(configPath, "[database]\npath = \"nested/state.sqlite3\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)));
            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void CommandPingWorks()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            var result = application.ExecuteCommand("ping");

            Assert.True(result.Success);
            Assert.Equal("pong", result.Message);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public async Task WpfExecutableSmokeStartsAndClosesCleanly()
    {
        using var dataDirectory = new TempDirectory();
        var executablePath = GetWpfExecutablePath();
        Assert.True(File.Exists(executablePath), $"WPF executable was not found: {executablePath}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment = { ["VTUBERBOT_HOME"] = dataDirectory.Path }
            }
        };

        Assert.True(process.Start());
        var standardOutput = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var standardError = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                standardOutput.Enqueue(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                standardError.Enqueue(eventArgs.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            // Windows CI can be heavily loaded; keep the smoke test bounded but allow WPF startup to settle.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            var token = TestContext.Current.CancellationToken;

            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    break;

                process.Refresh();
                if (FindVisibleMainWindowHandle(process) != IntPtr.Zero)
                    break;

                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            }

            Assert.False(
                process.HasExited,
                "WPF executable exited before presenting its main window." +
                BuildWpfStartupDiagnostics(dataDirectory.Path, standardOutput, standardError, process.Id));

            process.Refresh();
            var mainWindowHandle = FindVisibleMainWindowHandle(process);
            Assert.True(
                mainWindowHandle != IntPtr.Zero,
                "WPF executable remained alive but did not expose a visible VTuberBot window within 30 seconds." +
                BuildWpfStartupDiagnostics(dataDirectory.Path, standardOutput, standardError, process.Id));

            Assert.True(
                PostMessage(mainWindowHandle, WmClose, IntPtr.Zero, IntPtr.Zero),
                "WPF executable did not accept the main-window close request." +
                BuildWpfStartupDiagnostics(dataDirectory.Path, standardOutput, standardError, process.Id));

            await process.WaitForExitAsync(token).WaitAsync(
                TimeSpan.FromSeconds(10),
                token);

            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"WPF executable exited with code {process.ExitCode}." +
                BuildWpfStartupDiagnostics(dataDirectory.Path, standardOutput, standardError, process.Id));
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                try
                {
                    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
                        .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                }
                catch (TimeoutException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }

    [Fact]
    public void ApplicationCommandIntegrationEmitsExecutionEvent()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            var calls = 0;
            AppEvent? received = null;
            application.EventManager.Subscribe(
                VtuberApplication.CommandExecutedEvent,
                appEvent =>
                {
                    calls++;
                    received = appEvent;
                });

            var result = application.ExecuteCommand("ping");

            Assert.True(result.Success);
            Assert.Equal(1, calls);
            Assert.NotNull(received);
            Assert.Equal(VtuberApplication.CommandExecutedEvent, received!.Name);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void ApplicationCommandBeforeInitializeReturnsFailure()
    {
        var application = new VtuberApplication();

        var result = application.ExecuteCommand("ping");

        Assert.False(result.Success);
        Assert.Equal("Application is not running.", result.Message);
    }

    [Fact]
    public void ApplicationCommandAfterShutdownReturnsFailure()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();
        application.Shutdown();

        var result = application.ExecuteCommand("ping");

        Assert.False(result.Success);
        Assert.Equal("Application is not running.", result.Message);
    }

    [Fact]
    public void PerformanceProfilesUseExpectedIntervals()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), PerformancePolicy.GetSamplingInterval(PerformanceProfile.Ahorro));
        Assert.Equal(TimeSpan.FromSeconds(2), PerformancePolicy.GetSamplingInterval(PerformanceProfile.Equilibrado));
        Assert.Equal(TimeSpan.FromSeconds(1), PerformancePolicy.GetSamplingInterval(PerformanceProfile.Calidad));
    }

    [Fact]
    public void PerformancePolicyRejectsInvalidProfile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerformancePolicy.GetSamplingInterval((PerformanceProfile)99));
    }

    [Fact]
    public async Task MonitorRejectsInvalidProfileAndRetainsPreviousPolicy()
    {
        var clock = new FakeHealthClock(DateTimeOffset.UnixEpoch);
        var timers = new FakePeriodicTimerFactory();
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100),
            new ProcessPerformanceSample(TimeSpan.FromSeconds(2), 110));
        using var monitor = CreateMonitor(process, clock: clock, timers: timers);

        monitor.SetProfile(PerformanceProfile.Ahorro);
        var previousPolicy = monitor.Policy;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => monitor.SetProfile((PerformanceProfile)99));

        Assert.Equal(PerformanceProfile.Ahorro, monitor.Profile);
        Assert.Equal(previousPolicy, monitor.Policy);

        var secondSnapshot = new TaskCompletionSource<HealthSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotPublished += snapshot =>
        {
            if (snapshot.Timestamp > DateTimeOffset.UnixEpoch)
                secondSnapshot.TrySetResult(snapshot);
        };

        monitor.Start();
        clock.Advance(TimeSpan.FromSeconds(5));
        timers.Last.Signal();

        var snapshot = await secondSnapshot.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PerformanceProfile.Ahorro, snapshot.Profile);
        Assert.Equal(TimeSpan.FromSeconds(5), timers.Last.Period);
    }

    [Fact]
    public void HealthSnapshotCopiesIssues()
    {
        var issues = new List<HealthIssue> { new("GPU", "Unavailable", Optional: true) };
        var snapshot = new HealthSnapshot(
            DateTimeOffset.UtcNow,
            null,
            123,
            null,
            null,
            PerformanceProfile.Equilibrado,
            HealthState.DEGRADED,
            issues);

        issues.Clear();

        Assert.Single(snapshot.Issues);
        Assert.True(snapshot.Issues[0].Optional);
    }

    [Fact]
    public void CpuCalculationIsDeterministic()
    {
        var previous = new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100);
        var current = new ProcessPerformanceSample(TimeSpan.FromSeconds(11), 120);

        var result = PerformanceMonitor.CalculateCpuPercent(
            previous,
            current,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(2),
            processorCount: 4);

        Assert.Equal(12.5, result);
    }

    [Fact]
    public void CpuCalculationReturnsNullForInvalidElapsedTime()
    {
        var sample = new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100);

        Assert.Null(
            PerformanceMonitor.CalculateCpuPercent(
                sample,
                sample,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                processorCount: 4));
    }

    [Fact]
    public void CpuCalculationReturnsNullForNegativeCpuDelta()
    {
        var previous = new ProcessPerformanceSample(TimeSpan.FromSeconds(11), 100);
        var current = new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100);

        Assert.Null(
            PerformanceMonitor.CalculateCpuPercent(
                previous,
                current,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddSeconds(2),
                processorCount: 4));
    }

    [Fact]
    public void CpuCalculationRejectsNonPositiveProcessorCount()
    {
        var sample = new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerformanceMonitor.CalculateCpuPercent(
                sample,
                sample,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                processorCount: 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerformanceMonitor.CalculateCpuPercent(
                sample,
                sample,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                processorCount: -1));
    }

    [Fact]
    public void CpuCalculationClampsUpperBound()
    {
        var previous = new ProcessPerformanceSample(TimeSpan.Zero, 100);
        var current = new ProcessPerformanceSample(TimeSpan.FromSeconds(20), 100);

        var result = PerformanceMonitor.CalculateCpuPercent(
            previous,
            current,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(2),
            processorCount: 1);

        Assert.Equal(100, result);
    }

    [Fact]
    public void CpuCalculationReachesLowerBoundAtZero()
    {
        var sample = new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100);

        var result = PerformanceMonitor.CalculateCpuPercent(
            sample,
            sample,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(2),
            processorCount: 4);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CurrentProcessProbeRefreshesBeforeReading()
    {
        using var reader = new FakeProcessPerformanceReader(
            TimeSpan.FromSeconds(4),
            456_789_123);
        using var probe = new CurrentProcessPerformanceProbe(reader);

        var sample = probe.Sample();

        Assert.Equal(1, reader.RefreshCount);
        Assert.Equal(TimeSpan.FromSeconds(4), sample.TotalProcessorTime);
        Assert.Equal(456_789_123, sample.WorkingSetBytes);
    }

    [Fact]
    public void MonitorDoesNotSampleBeforeStart()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process);

        Assert.False(monitor.IsRunning);
        Assert.Equal(0, process.CallCount);
        Assert.Null(monitor.LatestSnapshot);
    }

    [Fact]
    public void MonitorFirstSampleHasRamAndNoCpu()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process);

        HealthSnapshot? snapshot = null;
        monitor.SnapshotPublished += value => snapshot = value;

        monitor.Start();

        Assert.True(monitor.IsRunning);
        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.CpuPercent);
        Assert.Equal(100, snapshot.WorkingSetBytes);
        Assert.Equal(HealthState.OK, snapshot.Health);
    }

    [Fact]
    public async Task MonitorSecondSampleCalculatesCpu()
    {
        var clock = new FakeHealthClock(DateTimeOffset.UnixEpoch);
        var timers = new FakePeriodicTimerFactory();
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(10), 100),
            new ProcessPerformanceSample(TimeSpan.FromSeconds(11), 120));
        using var monitor = CreateMonitor(process, clock: clock, timers: timers);

        var secondSnapshot = new TaskCompletionSource<HealthSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        monitor.SnapshotPublished += snapshot =>
        {
            if (++count == 2)
                secondSnapshot.TrySetResult(snapshot);
        };

        monitor.Start();

        clock.Advance(TimeSpan.FromSeconds(2));
        timers.Last.Signal();

        var snapshot = await secondSnapshot.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(120, snapshot.WorkingSetBytes);
        Assert.Equal(12.5, snapshot.CpuPercent);
    }

    [Fact]
    public async Task MonitorProfileChangesPolicyAndSnapshot()
    {
        var clock = new FakeHealthClock(DateTimeOffset.UnixEpoch);
        var timers = new FakePeriodicTimerFactory();
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100),
            new ProcessPerformanceSample(TimeSpan.FromSeconds(2), 110));
        using var monitor = CreateMonitor(process, clock: clock, timers: timers);

        var secondSnapshot = new TaskCompletionSource<HealthSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotPublished += snapshot =>
        {
            if (snapshot.Timestamp > DateTimeOffset.UnixEpoch)
                secondSnapshot.TrySetResult(snapshot);
        };

        monitor.Start();
        monitor.SetProfile(PerformanceProfile.Ahorro);

        Assert.Equal(PerformanceProfile.Ahorro, monitor.Profile);
        Assert.Equal(TimeSpan.FromSeconds(5), monitor.Policy.SamplingInterval);

        clock.Advance(TimeSpan.FromSeconds(5));
        timers.Last.Signal();

        var snapshot = await secondSnapshot.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PerformanceProfile.Ahorro, snapshot.Profile);
        Assert.Equal(TimeSpan.FromSeconds(5), timers.Last.Period);
    }

    [Fact]
    public void MonitorStopStopsWorker()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process);

        monitor.Start();
        monitor.Stop();

        Assert.False(monitor.IsRunning);
        var calls = process.CallCount;
        Assert.Equal(calls, process.CallCount);
    }

    [Fact]
    public void MonitorRestartCreatesFreshCycle()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100),
            new ProcessPerformanceSample(TimeSpan.FromSeconds(2), 200));
        var timers = new FakePeriodicTimerFactory();
        using var monitor = CreateMonitor(process, timers: timers);

        var snapshots = new List<HealthSnapshot>();
        monitor.SnapshotPublished += snapshots.Add;

        monitor.Start();
        monitor.Stop();
        monitor.Start();

        Assert.Equal(2, process.CallCount);
        Assert.Equal(2, snapshots.Count);
        Assert.Null(snapshots[0].CpuPercent);
        Assert.Null(snapshots[1].CpuPercent);
        Assert.Equal(2, timers.Created.Count);
    }

    [Fact]
    public async Task MonitorRequiredProbeFailurePublishesUnavailableAndContinues()
    {
        var clock = new FakeHealthClock(DateTimeOffset.UnixEpoch);
        var timers = new FakePeriodicTimerFactory();
        var process = new FailFirstProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process, clock: clock, timers: timers);

        var snapshots = new List<HealthSnapshot>();
        var secondSnapshot = new TaskCompletionSource<HealthSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotPublished += snapshot =>
        {
            snapshots.Add(snapshot);
            if (snapshots.Count == 2)
                secondSnapshot.TrySetResult(snapshot);
        };

        monitor.Start();

        Assert.Equal(HealthState.UNAVAILABLE, snapshots[0].Health);
        Assert.Contains(snapshots[0].Issues, issue => issue.Source == "CPU/RAM");

        clock.Advance(TimeSpan.FromSeconds(2));
        timers.Last.Signal();

        var recovered = await secondSnapshot.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HealthState.OK, recovered.Health);
        Assert.Equal(100, recovered.WorkingSetBytes);
    }

    [Fact]
    public void MonitorGpuFailureProducesDegradedHealth()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        var gpu = new DelegateGpuProbe(
            () => throw new InvalidOperationException("gpu unavailable"));
        using var monitor = CreateMonitor(process, gpu: gpu);

        HealthSnapshot? snapshot = null;
        monitor.SnapshotPublished += value => snapshot = value;

        monitor.Start();

        Assert.Equal(HealthState.DEGRADED, snapshot!.Health);
        Assert.Contains(snapshot.Issues, issue => issue.Source == "GPU");
    }

    [Fact]
    public void MonitorFpsFailureProducesDegradedHealth()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        var fps = new DelegateFpsSource(
            () => throw new InvalidOperationException("fps unavailable"));
        using var monitor = CreateMonitor(process, fps: fps);

        HealthSnapshot? snapshot = null;
        monitor.SnapshotPublished += value => snapshot = value;

        monitor.Start();

        Assert.Equal(HealthState.DEGRADED, snapshot!.Health);
        Assert.Contains(snapshot.Issues, issue => issue.Source == "FPS");
    }

    [Fact]
    public async Task MonitorListenerFailureDoesNotStopTheMonitor()
    {
        var clock = new FakeHealthClock(DateTimeOffset.UnixEpoch);
        var timers = new FakePeriodicTimerFactory();
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100),
            new ProcessPerformanceSample(TimeSpan.FromSeconds(2), 100));
        using var monitor = CreateMonitor(process, clock: clock, timers: timers);

        var secondSnapshot = new TaskCompletionSource<HealthSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotPublished += _ => throw new InvalidOperationException("listener failed");
        var count = 0;
        monitor.SnapshotPublished += snapshot =>
        {
            if (++count == 2)
                secondSnapshot.TrySetResult(snapshot);
        };

        monitor.Start();
        Assert.True(monitor.IsRunning);

        clock.Advance(TimeSpan.FromSeconds(2));
        timers.Last.Signal();

        _ = await secondSnapshot.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(monitor.IsRunning);
    }

    [Fact]
    public async Task MonitorSnapshotCallbackCanStopMonitorWithoutDeadlock()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process);

        var callbackEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.SnapshotPublished += _ =>
        {
            callbackEntered.TrySetResult(true);
            monitor.Stop();
            callbackReturned.TrySetResult(true);
        };

        var startTask = System.Threading.Tasks.Task.Run(() => monitor.Start(), TestContext.Current.CancellationToken);

        await callbackEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await callbackReturned.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await startTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public async Task MonitorSnapshotCallbackCanDisposeMonitorWithoutDeadlock()
    {
        var process = new SequenceProcessProbe(
            new ProcessPerformanceSample(TimeSpan.FromSeconds(1), 100));
        using var monitor = CreateMonitor(process);

        var callbackEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.SnapshotPublished += _ =>
        {
            callbackEntered.TrySetResult(true);
            monitor.Dispose();
            callbackReturned.TrySetResult(true);
        };

        var startTask = System.Threading.Tasks.Task.Run(() => monitor.Start(), TestContext.Current.CancellationToken);

        await callbackEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await callbackReturned.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        await startTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.False(monitor.IsRunning);
        Assert.Throws<ObjectDisposedException>(
            () => monitor.SetProfile(PerformanceProfile.Calidad));
    }

    [Fact]
    public async Task ApplicationShutdownFromHealthEventDoesNotDeadlock()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();
        var monitor = application.PerformanceMonitor;

        var callbackEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackReturned = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        application.EventManager.Subscribe(
            VtuberApplication.HealthUpdatedEvent,
            _ =>
            {
                callbackEntered.TrySetResult(true);
                application.Shutdown();
                callbackReturned.TrySetResult(true);
            });

        application.SetPerformanceProfile(PerformanceProfile.Calidad);

        await callbackEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await callbackReturned.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        monitor.Stop();

        Assert.Equal(AppState.Stopped, application.State);
        Assert.False(monitor.IsRunning);
        Assert.Null(application.LatestHealth);
    }

    [Fact]
    public void ApplicationIntegratesPerformanceMonitor()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            Assert.Equal(AppState.Running, application.State);
            Assert.True(application.PerformanceMonitor.IsRunning);
            Assert.NotNull(application.LatestHealth);
            Assert.Equal(PerformanceProfile.Equilibrado, application.PerformanceProfile);
            Assert.Equal("0.2.3", VtuberApplication.Version);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void ApplicationShutdownStopsPerformanceMonitor()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();
        var monitor = application.PerformanceMonitor;

        application.Shutdown();

        Assert.False(monitor.IsRunning);
        Assert.Null(application.LatestHealth);
    }

    [Fact]
    public void ApplicationRestartCreatesFreshPerformanceMonitor()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();
        var first = application.PerformanceMonitor;

        application.Shutdown();
        application.Initialize();

        try
        {
            Assert.NotSame(first, application.PerformanceMonitor);
            Assert.True(application.PerformanceMonitor.IsRunning);
            Assert.NotNull(application.LatestHealth);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public async Task PerformanceMonitorRepeatedStartStopCyclesAreStable()
    {
        var process = new SequenceProcessProbe(
            Enumerable.Range(1, 20)
                .Select(value => new ProcessPerformanceSample(
                    TimeSpan.FromSeconds(value),
                    100))
                .ToArray());
        var timers = new FakePeriodicTimerFactory();
        using var monitor = CreateMonitor(process, timers: timers);

        TaskCompletionSource<HealthSnapshot>? nextSnapshot = null;
        monitor.SnapshotPublished += snapshot => nextSnapshot?.TrySetResult(snapshot);

        for (var cycle = 1; cycle <= 20; cycle++)
        {
            nextSnapshot = new TaskCompletionSource<HealthSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            monitor.Start();

            var snapshot = await nextSnapshot.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            Assert.NotNull(snapshot);
            Assert.True(monitor.IsRunning);

            monitor.Stop();

            Assert.False(monitor.IsRunning);
        }

        nextSnapshot = null;

        Assert.Equal(20, process.CallCount);
        Assert.Equal(20, timers.Created.Count);
        Assert.All(timers.Created, waiter => Assert.True(waiter.IsDisposed));
    }

    [Fact]
    public void ApplicationPerformanceProfileUsesRealPolicy()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            application.SetPerformanceProfile(PerformanceProfile.Calidad);

            Assert.Equal(PerformanceProfile.Calidad, application.PerformanceProfile);
            Assert.Equal(TimeSpan.FromSeconds(1), application.PerformanceMonitor.Policy.SamplingInterval);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public void ApplicationStaysRunningWithoutGpuProvider()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        try
        {
            Assert.Equal(AppState.Running, application.State);
            Assert.NotNull(application.LatestHealth);
            Assert.Null(application.LatestHealth!.Gpu);
        }
        finally
        {
            application.Shutdown();
        }
    }

    [Fact]
    public async Task ProcessWaitAndStopCanRunConcurrently()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());
        Assert.True(
            SpinWait.SpinUntil(
                () => GetActiveWaiters(process) > 0,
                TimeSpan.FromSeconds(5)));

        var stopStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopTask = System.Threading.Tasks.Task.Run(
            () =>
            {
                stopStarted.SetResult();
                return process.Stop();
            },
            TestContext.Current.CancellationToken);

        await stopStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        await Task.WhenAll(
            stopTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken),
            waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        Assert.False(process.IsRunning);
        Assert.Null(process.Pid);
    }

    [Fact]
    public async Task ProcessStartWhileWaitIsActiveDoesNotBlockIndefinitely()
    {
        var process = CreateLongRunningProcess();
        try
        {
            process.Start();
            var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());

            Assert.True(
                SpinWait.SpinUntil(
                    () => GetActiveWaiters(process) > 0,
                    TimeSpan.FromSeconds(3)));

            var startTask = System.Threading.Tasks.Task.Run(
                () =>
                {
                    Assert.Throws<ProcessManagerException>(() => process.Start());
                },
                TestContext.Current.CancellationToken);

            await startTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            process.Stop();
            await waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            process.Stop();
        }
    }

    [Fact]
    public async Task ProcessWaitAndDisposeCanRunConcurrently()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());
        Assert.True(
            SpinWait.SpinUntil(
                () => GetActiveWaiters(process) > 0,
                TimeSpan.FromSeconds(3)));

        var disposeStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeTask = System.Threading.Tasks.Task.Run(
            () =>
            {
                disposeStarted.SetResult();
                process.Dispose();
            },
            TestContext.Current.CancellationToken);

        await disposeStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

        await Task.WhenAll(
            disposeTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken),
            waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        Assert.False(process.IsRunning);
        Assert.Null(process.Pid);
    }

    [Fact]
    public async Task ProcessWaitAndRestartCanRunConcurrently()
    {
        using var process = CreateLongRunningProcess();
        var firstPid = process.Start();
        var firstProcess = GetOwnedProcessInstance(process);

        var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());
        Assert.True(
            SpinWait.SpinUntil(
                () => GetActiveWaiters(process) > 0,
                TimeSpan.FromSeconds(1)));

        var restartStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var restartTask = System.Threading.Tasks.Task.Run(
            () =>
            {
                restartStarted.SetResult();
                return process.Restart();
            },
            TestContext.Current.CancellationToken);

        await restartStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        var secondPid = await restartTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        var secondProcess = GetOwnedProcessInstance(process);

        await waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.NotEqual(firstPid, secondPid);
        Assert.NotSame(firstProcess, secondProcess);
        Assert.Equal(secondPid, process.Pid);
        Assert.True(process.IsRunning);

        process.Stop();
    }

    [Fact]
    public async Task ProcessWaitAndStopRepeatedlyDoesNotDisposeProcessEarly()
    {
        using var process = CreateLongRunningProcess();

        for (var iteration = 0; iteration < 12; iteration++)
        {
            process.Start();

            var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());
            Assert.True(
                SpinWait.SpinUntil(
                    () => GetActiveWaiters(process) > 0,
                    TimeSpan.FromSeconds(1)));

            var stopStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var stopTask = System.Threading.Tasks.Task.Run(
                () =>
                {
                    stopStarted.SetResult();
                    return process.Stop();
                },
                TestContext.Current.CancellationToken);

            await stopStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

            await Task.WhenAll(
                stopTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken),
                waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

            Assert.False(process.IsRunning);
            Assert.Null(process.Pid);
        }
    }


    [Fact]
    public void ProcessMissingCwd()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe"], "Z:\\missing-vtuber-dir"),
            new TestLogger());

        Assert.Throws<ProcessManagerException>(() => process.Start());
    }

    [Fact]
    public void ProcessDoubleStartIsRejected()
    {
        using var process = CreateLongRunningProcess();

        process.Start();
        Assert.Throws<ProcessManagerException>(() => process.Start());

        process.Stop();
    }

    [Fact]
    public async Task ProcessConcurrentStopIsSafe()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        var stops = Enumerable.Range(0, 2)
            .Select(_ => System.Threading.Tasks.Task.Run(() => process.Stop()))
            .ToArray();

        await System.Threading.Tasks.Task.WhenAll(stops);

        Assert.False(process.IsRunning);
        Assert.All(stops, task => Assert.Null(task.Exception));
    }

    [Fact]
    public void ProcessWaitAndNaturalExitReleaseProcess()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "7"]),
            new TestLogger());

        process.Start();

        Assert.Equal(7, process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
        Assert.False(process.IsRunning);
    }

    [Fact]
    public void ProcessStopAfterNaturalExitIsSafe()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        process.Start();
        Assert.True(SpinWait.SpinUntil(() => !process.IsRunning, 1000));

        Assert.Equal(0, process.Stop());
        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessRestartAfterExitWorks()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        process.Start();
        Assert.True(SpinWait.SpinUntil(() => !process.IsRunning, 1000));

        process.Restart();

        Assert.NotNull(process.Pid);
        Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ProcessExternalTerminationIsHandled()
    {
        using var process = CreateLongRunningProcess();
        var pid = process.Start();

        using (var external = Process.GetProcessById(pid))
            external.Kill();

        Assert.NotNull(process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessRepeatedStartWaitDoesNotRetainPreviousInstances()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        for (var i = 0; i < 20; i++)
        {
            process.Start();
            Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
        }

        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessEnvironmentOverridesArePassedToChild()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(
                ["cmd.exe", "/c", "if \"%VTUBER_TEST_ENV%\"==\"expected\" (exit /b 0) else (exit /b 9)"],
                Environment: new Dictionary<string, string>
                {
                    ["VTUBER_TEST_ENV"] = "expected"
                }),
            new TestLogger());

        process.Start();

        Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
    }

    [Fact]
    public void StopRejectsNegativeTimeout()
    {
        using var process = CreateLongRunningProcess();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => process.Stop(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WaitRejectsNegativeTimeout()
    {
        using var process = CreateLongRunningProcess();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => process.Wait(TimeSpan.FromSeconds(-1)));
    }


    [Fact]
    public void ProcessDisposeIsIdempotent()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        process.Dispose();
        process.Dispose();

        Assert.False(process.IsRunning);
        Assert.Null(process.Pid);
    }

    [Fact]
    public void LoggingWritesFileAndDisposes()
    {
        using var directory = new TempDirectory();
        var logger = new Logger(directory.Path, LogLevel.Info);

        logger.Info("hello");
        logger.Warning("warning");
        logger.Dispose();
        logger.Dispose();

        var path = Path.Combine(directory.Path, "vtuber.log");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("hello", text);
        Assert.Contains("warning", text);
    }

    [Fact]
    public void LoggingRotatesAtApproximatelyOneMegabyte()
    {
        using var directory = new TempDirectory();
        using var logger = new Logger(directory.Path, LogLevel.Info);
        var payload = new string('x', 1200);

        for (var i = 0; i < 1100; i++)
            logger.Info("{0}", payload);

        var active = new FileInfo(Path.Combine(directory.Path, "vtuber.log"));
        var backup = new FileInfo(Path.Combine(directory.Path, "vtuber.log.1"));

        Assert.True(active.Exists);
        Assert.True(backup.Exists);
        Assert.True(backup.Length >= 1_000_000);
    }

    private static string GetWpfExecutablePath()
    {
        var repositoryRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.Parent?.FullName;

        if (repositoryRoot is null)
            throw new InvalidOperationException("Repository root could not be resolved.");

        return Path.Combine(
            repositoryRoot,
            "src",
            "VtuberBot.App",
            "bin",
            "Release",
            "net10.0-windows",
            "VtuberBot.App.exe");
    }

    private static PerformanceMonitor CreateMonitor(
        IPerformanceProbe process,
        IGpuPerformanceProbe? gpu = null,
        IFpsSource? fps = null,
        FakeHealthClock? clock = null,
        FakePeriodicTimerFactory? timers = null)
    {
        return new PerformanceMonitor(
            process,
            gpu ?? new NullGpuPerformanceProbe(),
            fps ?? new NullFpsSource(),
            clock ?? new FakeHealthClock(DateTimeOffset.UnixEpoch),
            timers ?? new FakePeriodicTimerFactory(),
            new TestLogger());
    }

    private sealed class SequenceProcessProbe : IPerformanceProbe
    {
        private readonly Queue<ProcessPerformanceSample> samples;

        public SequenceProcessProbe(params ProcessPerformanceSample[] samples) =>
            this.samples = new Queue<ProcessPerformanceSample>(samples);

        public int CallCount { get; private set; }

        public ProcessPerformanceSample Sample()
        {
            CallCount++;
            if (samples.Count == 0)
                throw new InvalidOperationException("No more process samples.");
            return samples.Dequeue();
        }
    }

    private sealed class FailFirstProcessProbe : IPerformanceProbe
    {
        private readonly ProcessPerformanceSample recoverySample;
        private bool failed = true;

        public FailFirstProcessProbe(ProcessPerformanceSample recoverySample) =>
            this.recoverySample = recoverySample;

        public ProcessPerformanceSample Sample()
        {
            if (failed)
            {
                failed = false;
                throw new InvalidOperationException("process unavailable");
            }

            return recoverySample;
        }
    }

    private sealed class DelegateGpuProbe : IGpuPerformanceProbe
    {
        private readonly Func<GpuPerformanceSample?> sample;

        public DelegateGpuProbe(Func<GpuPerformanceSample?> sample) =>
            this.sample = sample;

        public GpuPerformanceSample? Sample() => sample();
    }

    private sealed class DelegateFpsSource : IFpsSource
    {
        private readonly Func<double?> sample;

        public DelegateFpsSource(Func<double?> sample) =>
            this.sample = sample;

        public double? Sample() => sample();
    }

    private sealed class FakeHealthClock : IHealthClock
    {
        public FakeHealthClock(DateTimeOffset current) => UtcNow = current;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan amount) => UtcNow += amount;
    }

    private sealed class FakePeriodicTimerFactory : IPeriodicWaiterFactory
    {
        public List<FakePeriodicWaiter> Created { get; } = [];

        public FakePeriodicWaiter Last =>
            Created[^1];

        public IPeriodicWaiter Create(TimeSpan interval)
        {
            var waiter = new FakePeriodicWaiter(interval);
            Created.Add(waiter);
            return waiter;
        }
    }

    private sealed class FakePeriodicWaiter : IPeriodicWaiter
    {
        private readonly object gate = new();
        private TaskCompletionSource<bool> next = CreateSource();
        private bool disposed;

        public FakePeriodicWaiter(TimeSpan period) => Period = period;

        public TimeSpan Period { get; set; }

        public bool IsDisposed
        {
            get
            {
                lock (gate)
                    return disposed;
            }
        }

        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            Task<bool> task;
            lock (gate)
            {
                if (disposed)
                    return false;
                task = next.Task;
            }

            try
            {
                return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public void Signal()
        {
            TaskCompletionSource<bool> current;
            lock (gate)
            {
                if (disposed)
                    return;
                current = next;
                next = CreateSource();
            }

            current.TrySetResult(true);
        }

        public void Dispose()
        {
            TaskCompletionSource<bool> current;
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                current = next;
            }

            current.TrySetResult(false);
        }

        private static TaskCompletionSource<bool> CreateSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FakeProcessPerformanceReader : IProcessPerformanceReader
    {
        private readonly TimeSpan totalProcessorTime;
        private readonly long workingSetBytes;
        private bool refreshed;

        public FakeProcessPerformanceReader(TimeSpan totalProcessorTime, long workingSetBytes)
        {
            this.totalProcessorTime = totalProcessorTime;
            this.workingSetBytes = workingSetBytes;
        }

        public int RefreshCount { get; private set; }

        public void Refresh()
        {
            RefreshCount++;
            refreshed = true;
        }

        public TimeSpan TotalProcessorTime =>
            refreshed ? totalProcessorTime : throw new InvalidOperationException("Refresh was not called.");

        public long WorkingSet64 =>
            refreshed ? workingSetBytes : throw new InvalidOperationException("Refresh was not called.");

        public void Dispose()
        {
        }
    }

    private const uint WmClose = 0x0010;

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record ProcessWindow(IntPtr Handle, string Title, bool IsVisible);

    private static IReadOnlyList<ProcessWindow> GetProcessWindows(int processId)
    {
        var windows = new List<ProcessWindow>();
        EnumWindows((windowHandle, lParam) =>
        {
            _ = GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
            if (ownerProcessId != (uint)processId)
                return true;

            var title = new StringBuilder(512);
            _ = GetWindowText(windowHandle, title, title.Capacity);
            windows.Add(new ProcessWindow(windowHandle, title.ToString(), IsWindowVisible(windowHandle)));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static IntPtr FindVisibleMainWindowHandle(Process process)
    {
        var window = GetProcessWindows(process.Id)
            .FirstOrDefault(candidate => candidate.IsVisible && candidate.Title == "VTuberBot");
        return window?.Handle ?? IntPtr.Zero;
    }

    private static string BuildWpfStartupDiagnostics(
        string dataDirectory,
        IEnumerable<string> standardOutput,
        IEnumerable<string> standardError,
        int processId)
    {
        var logFiles = Directory.Exists(dataDirectory)
            ? Directory.GetFiles(dataDirectory, "*.log", SearchOption.AllDirectories)
            : [];
        var logSummary = string.Join(
            Environment.NewLine,
            logFiles.Select(logPath =>
            {
                string content;
                try
                {
                    content = File.ReadAllText(logPath);
                }
                catch (Exception exception)
                {
                    content = "[No se pudo leer el log: " + exception.GetType().Name + "]";
                }

                return $"--- {Path.GetRelativePath(dataDirectory, logPath)} ---{Environment.NewLine}{content}";
            }));
        var windowSummary = string.Join(
            Environment.NewLine,
            GetProcessWindows(processId).Select(window =>
                $"Handle={window.Handle}, Visible={window.IsVisible}, Title='{window.Title}'"));

        return Environment.NewLine +
            $"PROCESS WINDOWS:{Environment.NewLine}{windowSummary}{Environment.NewLine}" +
            $"STDOUT:{Environment.NewLine}{string.Join(Environment.NewLine, standardOutput)}{Environment.NewLine}" +
            $"STDERR:{Environment.NewLine}{string.Join(Environment.NewLine, standardError)}{Environment.NewLine}" +
            $"APPLICATION LOGS:{Environment.NewLine}{logSummary}";
    }

    private static int GetActiveWaiters(OwnedProcess process)
    {
        var field = typeof(OwnedProcess).GetField(
            "activeWaiters",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (int)field!.GetValue(process)!;
    }

    private static Process GetOwnedProcessInstance(OwnedProcess process)
    {
        var field = typeof(OwnedProcess).GetField(
            "process",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (Process)field!.GetValue(process)!;
    }

    private static OwnedProcess CreateLongRunningProcess() =>
        new(
            new ProcessSpec(
                ["cmd.exe", "/c", "timeout", "/t", "5", "/nobreak", ">nul"]),
            new TestLogger());

    [Fact]
    public async Task LocalSpeechQueueCanCancelTheActivePhrase()
    {
        var session = new BlockingSpeechSession();
        using var queue = new LocalSpeechQueue(() => session);

        Assert.True(queue.Enqueue("Frase de prueba que debe poder detenerse."));
        await session.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(queue.StopCurrentSpeech());
        await session.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LocalSpeechQueueDisposalCancelsTheActivePhrase()
    {
        var session = new BlockingSpeechSession();
        var queue = new LocalSpeechQueue(() => session);

        Assert.True(queue.Enqueue("Frase de prueba durante el cierre."));
        await session.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        queue.Dispose();
        await session.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    private sealed class BlockingSpeechSession : ILocalSpeechSession
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SpeechPlaybackResult Speak(
            string text,
            string requestedVoiceId,
            string requestedOutputId,
            int rate,
            int volume,
            Func<bool> cancellationRequested)
        {
            Started.TrySetResult(true);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (!cancellationRequested() && DateTime.UtcNow < deadline)
                Thread.Sleep(10);

            var cancelled = cancellationRequested();
            if (cancelled)
                Cancelled.TrySetResult(true);
            return new SpeechPlaybackResult(UsedFallback: false, Cancelled: cancelled);
        }

        public void Dispose()
        {
        }
    }

    [Theory]
    [InlineData("Naofumi753", "naofumi753")]
    [InlineData("Iwatani_753", "iwatani_753")]
    [InlineData("ABC123", "abc123")]
    public void TwitchLoginPolicyNormalizesValidLogins(string login, string expected)
    {
        Assert.True(TwitchLoginPolicy.TryNormalize(login, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad user")]
    [InlineData("bad;user")]
    [InlineData("bad!user")]
    [InlineData("abcdefghijklmnopqrstuvwxyz")]
    [InlineData("bad\r\nPRIVMSG")]
    public void TwitchLoginPolicyRejectsInvalidLogins(string login)
    {
        Assert.False(TwitchLoginPolicy.TryNormalize(login, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TwitchLoginPolicyRejectsMissingLogin()
    {
        Assert.False(TwitchLoginPolicy.TryNormalize(null, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    private sealed class TestLogger : ILogger
    {
        public void Info(string message, params object?[] args)
        {
        }

        public void Warning(string message, params object?[] args)
        {
        }

        public void Error(Exception exception, string message, params object?[] args)
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Directory.CreateTempSubdirectory("vtuber-tests-").FullName;
        }

        public string Path { get; }

        public string File(params string[] parts)
        {
            var path = System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            return path;
        }

        public string Directory(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
