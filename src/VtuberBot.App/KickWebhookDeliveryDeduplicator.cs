namespace VtuberBot.App;

/// <summary>
/// Bounded, thread-safe webhook idempotency gate. An ID is remembered only after
/// the consumer accepts the event; a failed admission remains eligible for retry.
/// </summary>
public sealed class KickWebhookDeliveryDeduplicator
{
    private readonly object gate = new();
    private readonly HashSet<string> acceptedIds = new(StringComparer.Ordinal);
    private readonly Queue<string> acceptedOrder = new();
    private readonly int capacity;

    public KickWebhookDeliveryDeduplicator(int capacity = 4096)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe ser positiva.");
        this.capacity = capacity;
    }

    public int Capacity => capacity;

    public int RememberedCount
    {
        get { lock (gate) return acceptedIds.Count; }
    }

    /// <summary>
    /// Runs the admission callback exactly once for a remembered ID. If admission throws,
    /// the ID is not retained so the platform can retry the delivery.
    /// </summary>
    public bool TryDispatchOnce(string messageId, Action dispatch)
    {
        if (string.IsNullOrWhiteSpace(messageId) ||
            messageId.Length > 128 ||
            messageId.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new ArgumentException("El ID del evento webhook no es válido.", nameof(messageId));
        ArgumentNullException.ThrowIfNull(dispatch);

        lock (gate)
        {
            if (acceptedIds.Contains(messageId))
                return false;

            // Admission happens before deduplication is committed. The callback must be
            // a quick bounded enqueue, never an API call or other long-running operation.
            dispatch();

            acceptedIds.Add(messageId);
            acceptedOrder.Enqueue(messageId);
            while (acceptedOrder.Count > capacity)
                acceptedIds.Remove(acceptedOrder.Dequeue());

            return true;
        }
    }
}
