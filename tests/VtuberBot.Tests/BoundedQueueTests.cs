using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class BoundedQueueTests
{
    [Fact]
    public void PreservesFifoOrderAndRejectsItemsAboveCapacity()
    {
        var queue = new BoundedQueue<string>(2);

        Assert.True(queue.TryEnqueue("first"));
        Assert.True(queue.TryEnqueue("second"));
        Assert.False(queue.TryEnqueue("overflow"));
        Assert.Equal(2, queue.Count);

        Assert.True(queue.TryDequeue(out var first));
        Assert.True(queue.TryDequeue(out var second));
        Assert.Equal("first", first);
        Assert.Equal("second", second);
        Assert.False(queue.TryDequeue(out _));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ClearReturnsCapacityForSubsequentEvents()
    {
        var queue = new BoundedQueue<int>(1);
        Assert.True(queue.TryEnqueue(10));
        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.True(queue.TryEnqueue(20));
    }

    [Fact]
    public async Task ConcurrentProducersNeverExceedCapacity()
    {
        var queue = new BoundedQueue<int>(64);
        var accepted = new System.Collections.Concurrent.ConcurrentBag<int>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var item = 0; item < 100; item++)
            {
                var value = worker * 100 + item;
                if (queue.TryEnqueue(value))
                    accepted.Add(value);
            }
        })));

        Assert.Equal(64, accepted.Count);
        Assert.Equal(64, queue.Count);
        Assert.False(queue.TryEnqueue(9999));
    }

    [Fact]
    public void RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedQueue<int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedQueue<int>(-1));
    }
}
