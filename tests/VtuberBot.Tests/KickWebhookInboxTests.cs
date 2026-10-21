using Microsoft.Data.Sqlite;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class KickWebhookInboxTests
{
    private sealed class TempDb : IDisposable
    {
        public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("vtuber-kick-inbox-").FullName;
        public string DatabasePath => Path.Combine(DirectoryPath, "data", "product.db");

        public void Dispose()
        {
            try { Directory.Delete(DirectoryPath, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void VerifiedEventIsStoredBeforeAckAndDuplicateIsRejectedAcrossStoreInstances()
    {
        using var temp = new TempDb();
        const string eventId = "kick-webhook-0001";
        const string payload = "{\"EventType\":\"chat.message.sent\",\"EventMessageId\":\"kick-webhook-0001\"}";

        var firstStore = new ProductStore(temp.DatabasePath);
        Assert.Equal(KickWebhookInboxAdmissionResult.Stored, firstStore.TryStoreKickWebhookEvent(eventId, payload));

        var reopenedStore = new ProductStore(temp.DatabasePath);
        Assert.Equal(KickWebhookInboxAdmissionResult.Duplicate, reopenedStore.TryStoreKickWebhookEvent(eventId, payload));
        Assert.Equal(new KickWebhookInboxSummary(1, 0, 0, 0, 0), reopenedStore.GetKickWebhookInboxSummary());

        var item = Assert.IsType<KickWebhookInboxItem>(reopenedStore.GetKickWebhookInboxItem(eventId));
        Assert.Equal(payload, item.PayloadJson);
        Assert.Equal("pending", item.Status);
    }

    [Fact]
    public void ClaimedEventSurvivesReopenAndInterruptedProcessingIsHeldForReview()
    {
        using var temp = new TempDb();
        const string eventId = "kick-webhook-interrupted";
        const string payload = "{\"sensitiveTestBody\":\"preserved for review\"}";

        var store = new ProductStore(temp.DatabasePath);
        Assert.Equal(KickWebhookInboxAdmissionResult.Stored, store.TryStoreKickWebhookEvent(eventId, payload));

        var claimed = Assert.Single(store.ClaimPendingKickWebhookEvents());
        Assert.Equal("processing", claimed.Status);
        Assert.Equal(1, claimed.Attempts);

        var reopened = new ProductStore(temp.DatabasePath);
        Assert.Equal(1, reopened.RecoverInterruptedKickWebhookEvents());
        var summary = reopened.GetKickWebhookInboxSummary();
        Assert.Equal(0, summary.Pending);
        Assert.Equal(0, summary.Processing);
        Assert.Equal(1, summary.Failed);

        var held = Assert.IsType<KickWebhookInboxItem>(reopened.GetKickWebhookInboxItem(eventId));
        Assert.Equal("failed", held.Status);
        Assert.Equal(payload, held.PayloadJson);
        var recoveryError = Assert.IsType<string>(held.LastError);
        Assert.Contains("no se repite automáticamente", recoveryError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KickWebhookInboxAdmissionResult.Duplicate, reopened.TryStoreKickWebhookEvent(eventId, payload));
    }

    [Fact]
    public void SuccessfulProcessingClearsPayloadButRetainsIdempotencyRecord()
    {
        using var temp = new TempDb();
        const string eventId = "kick-webhook-complete";
        const string payload = "{\"message\":\"private viewer text\"}";

        var store = new ProductStore(temp.DatabasePath);
        Assert.Equal(KickWebhookInboxAdmissionResult.Stored, store.TryStoreKickWebhookEvent(eventId, payload));
        Assert.Single(store.ClaimPendingKickWebhookEvents());

        store.MarkKickWebhookEventProcessed(eventId);

        var processed = Assert.IsType<KickWebhookInboxItem>(store.GetKickWebhookInboxItem(eventId));
        Assert.Equal("processed", processed.Status);
        Assert.Equal(string.Empty, processed.PayloadJson);
        Assert.Null(processed.LastError);
        Assert.Equal(KickWebhookInboxAdmissionResult.Duplicate, store.TryStoreKickWebhookEvent(eventId, payload));
        Assert.Equal(new KickWebhookInboxSummary(0, 0, 0, 1, 0), store.GetKickWebhookInboxSummary());
    }

    [Fact]
    public void FailedProcessingRedactsSecretsAndPreservesPayloadForReview()
    {
        using var temp = new TempDb();
        const string eventId = "kick-webhook-failed";
        const string payload = "{\"message\":\"preserve for local review\"}";

        var store = new ProductStore(temp.DatabasePath);
        Assert.Equal(KickWebhookInboxAdmissionResult.Stored, store.TryStoreKickWebhookEvent(eventId, payload));
        Assert.Single(store.ClaimPendingKickWebhookEvents());

        store.MarkKickWebhookEventFailed(eventId, "request failed token=super-secret-value");

        var failed = Assert.IsType<KickWebhookInboxItem>(store.GetKickWebhookInboxItem(eventId));
        Assert.Equal("failed", failed.Status);
        Assert.Equal(payload, failed.PayloadJson);
        var safeError = Assert.IsType<string>(failed.LastError);
        Assert.DoesNotContain("super-secret-value", safeError);
        Assert.Contains("[REDACTED]", safeError);
    }

    [Fact]
    public void FullInboxRejectsNewDeliveryWithoutDeletingExistingEvents()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var now = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp.DatabasePath }.ConnectionString))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO kick_webhook_inbox(event_message_id,payload_json,received_utc,updated_utc,status,attempts)
                VALUES($id,'{}',$now,$now,'pending',0)
                """;
            var id = insert.Parameters.Add("$id", SqliteType.Text);
            var timestamp = insert.Parameters.Add("$now", SqliteType.Text);
            timestamp.Value = now;
            for (var i = 0; i < ProductStore.MaximumPendingKickWebhookEvents; i++)
            {
                id.Value = "pending-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                insert.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        Assert.Equal(KickWebhookInboxAdmissionResult.CapacityExceeded,
            store.TryStoreKickWebhookEvent("new-delivery", "{\"new\":true}"));
        Assert.Equal(ProductStore.MaximumPendingKickWebhookEvents, store.GetKickWebhookInboxSummary().Pending);
        Assert.Null(store.GetKickWebhookInboxItem("new-delivery"));
        Assert.NotNull(store.GetKickWebhookInboxItem("pending-0"));
    }

    [Fact]
    public void RowCapEvictsOnlyOldestCompletedIdAndKeepsRecentDeduplicationRecords()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp.DatabasePath }.ConnectionString))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO kick_webhook_inbox(event_message_id,payload_json,received_utc,updated_utc,status,attempts)
                VALUES($id,'',$now,$now,'processed',1)
                """;
            var id = insert.Parameters.Add("$id", SqliteType.Text);
            var timestamp = insert.Parameters.Add("$now", SqliteType.Text);
            for (var i = 0; i < 2_000; i++)
            {
                id.Value = "completed-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                timestamp.Value = baseTime.AddMilliseconds(i).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                insert.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        Assert.Equal(KickWebhookInboxAdmissionResult.Stored,
            store.TryStoreKickWebhookEvent("new-live-delivery", "{\"message\":\"fresh\"}"));
        Assert.Null(store.GetKickWebhookInboxItem("completed-0"));
        Assert.NotNull(store.GetKickWebhookInboxItem("completed-1999"));
        Assert.Equal(KickWebhookInboxAdmissionResult.Duplicate,
            store.TryStoreKickWebhookEvent("completed-1999", "{}"));
        Assert.Equal(KickWebhookInboxAdmissionResult.Stored,
            store.TryStoreKickWebhookEvent("completed-0", "{}"));
    }

    [Fact]
    public void InboxRejectsOversizedPayloadAndInvalidIdentifiers()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);

        Assert.Throws<ArgumentException>(() => store.TryStoreKickWebhookEvent("bad\nid", "{}"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.TryStoreKickWebhookEvent("too-large", new string('x', 1_048_577)));
        Assert.Throws<ArgumentException>(() => store.GetKickWebhookInboxItem(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ClaimPendingKickWebhookEvents(101));
    }

    [Fact]
    public void RetentionExpiresFailedPayloadAfterThirtyDaysAndThenRemovesOldIds()
    {
        using var temp = new TempDb();
        const string failedId = "kick-webhook-expired-payload";
        const string processedId = "kick-webhook-old-processed";
        var store = new ProductStore(temp.DatabasePath);

        Assert.Equal(KickWebhookInboxAdmissionResult.Stored,
            store.TryStoreKickWebhookEvent(failedId, "{\"message\":\"failed payload\"}"));
        Assert.Single(store.ClaimPendingKickWebhookEvents());
        store.MarkKickWebhookEventFailed(failedId, "test error");

        Assert.Equal(KickWebhookInboxAdmissionResult.Stored,
            store.TryStoreKickWebhookEvent(processedId, "{\"message\":\"processed payload\"}"));
        Assert.Single(store.ClaimPendingKickWebhookEvents());
        store.MarkKickWebhookEventProcessed(processedId);

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp.DatabasePath }.ConnectionString))
        {
            connection.Open();
            using var ageRows = connection.CreateCommand();
            ageRows.CommandText = "UPDATE kick_webhook_inbox SET updated_utc=$old WHERE event_message_id IN ($failed,$processed)";
            ageRows.Parameters.AddWithValue("$old", DateTimeOffset.UtcNow.AddDays(-31).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            ageRows.Parameters.AddWithValue("$failed", failedId);
            ageRows.Parameters.AddWithValue("$processed", processedId);
            Assert.Equal(2, ageRows.ExecuteNonQuery());
        }

        Assert.Equal(1, store.PruneKickWebhookInboxRetention());
        var expired = Assert.IsType<KickWebhookInboxItem>(store.GetKickWebhookInboxItem(failedId));
        Assert.Equal("expired", expired.Status);
        Assert.Equal(string.Empty, expired.PayloadJson);
        Assert.Null(store.GetKickWebhookInboxItem(processedId));
        Assert.Equal(KickWebhookInboxAdmissionResult.Duplicate,
            store.TryStoreKickWebhookEvent(failedId, "{\"message\":\"failed payload\"}"));
    }

    [Fact]
    public void ProductSchemaUpgradeCreatesInboxWithoutDroppingExistingSettings()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("inbox.migration.keep", "preserved");

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp.DatabasePath }.ConnectionString))
        {
            connection.Open();
            using var downgrade = connection.CreateCommand();
            downgrade.CommandText = "UPDATE product_schema_meta SET version=2 WHERE id=1";
            Assert.Equal(1, downgrade.ExecuteNonQuery());
        }

        var upgraded = new ProductStore(temp.DatabasePath);
        Assert.Equal("preserved", upgraded.GetSetting("inbox.migration.keep"));

        using var verify = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp.DatabasePath }.ConnectionString);
        verify.Open();
        using var version = verify.CreateCommand();
        version.CommandText = "SELECT version FROM product_schema_meta WHERE id=1";
        Assert.Equal(ProductStore.CurrentProductSchemaVersion,
            Convert.ToInt32(version.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));

        using var table = verify.CreateCommand();
        table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='kick_webhook_inbox'";
        Assert.Equal(1L, Convert.ToInt64(table.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
    }
}
