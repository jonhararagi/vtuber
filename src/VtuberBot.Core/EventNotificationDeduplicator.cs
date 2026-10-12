namespace VtuberBot.Core;

/// <summary>
/// Remembers recently handled external event IDs so a reconnect/redelivery cannot replay
/// visible alerts or TTS twice. Capacity is bounded to keep long-running streams predictable.
/// </summary>
public sealed class EventNotificationDeduplicator
{
    private readonly object gate = new();
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly Queue<string> insertionOrder = new();

    public int Capacity { get; }

    public EventNotificationDeduplicator(int capacity = 10_000)
    {
        if (capacity is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe estar entre 1 y 100.000 IDs.");
        Capacity = capacity;
    }

    /// <summary>
    /// Returns false for an ID already handled in the current cache window.
    /// A missing ID is accepted because there is no stable key available for deduplication.
    /// </summary>
    public bool TryAccept(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return true;

        var id = messageId.Trim();
        lock (gate)
        {
            if (!seen.Add(id))
                return false;

            insertionOrder.Enqueue(id);
            while (insertionOrder.Count > Capacity)
                seen.Remove(insertionOrder.Dequeue());
            return true;
        }
    }
}
