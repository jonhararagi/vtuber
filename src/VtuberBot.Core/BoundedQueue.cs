namespace VtuberBot.Core;

/// <summary>
/// Thread-safe bounded FIFO for asynchronous event ingress. Producers can reject work immediately
/// instead of creating an unbounded Dispatcher callback backlog during event bursts.
/// </summary>
public sealed class BoundedQueue<T>
{
    private readonly object gate = new();
    private Queue<T> items = new();
    private readonly int capacity;

    public BoundedQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad debe ser positiva.");
        this.capacity = capacity;
    }

    public int Capacity => capacity;
    public int Count { get { lock (gate) return items.Count; } }

    public bool TryEnqueue(T item)
    {
        lock (gate)
        {
            if (items.Count >= capacity)
                return false;
            items.Enqueue(item);
            return true;
        }
    }

    public bool TryDequeue(out T item)
    {
        lock (gate)
        {
            if (items.Count == 0)
            {
                item = default!;
                return false;
            }

            item = items.Dequeue();
            return true;
        }
    }

    /// <summary>
    /// Removes the newest matching item. Intended only to roll back an admission when the
    /// caller cannot schedule its consumer; normal processing should remain FIFO.
    /// </summary>
    public bool TryRemoveLast(T expected)
    {
        lock (gate)
        {
            if (items.Count == 0)
                return false;

            var snapshot = items.ToArray();
            if (!EqualityComparer<T>.Default.Equals(snapshot[^1], expected))
                return false;

            items = new Queue<T>(snapshot.Take(snapshot.Length - 1));
            return true;
        }
    }

    public void Clear()
    {
        lock (gate) items.Clear();
    }
}
