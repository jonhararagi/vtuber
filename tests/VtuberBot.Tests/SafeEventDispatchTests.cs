using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class SafeEventDispatchTests
{
    [Fact]
    public void OneFailingSingleArgumentSubscriberDoesNotBlockOtherSubscribers()
    {
        var delivered = new List<string>();
        Action<string> subscribers = _ => throw new InvalidOperationException("view disposed");
        subscribers += value => delivered.Add(value);

        SafeEventDispatch.Invoke(subscribers, "network update");

        Assert.Equal(new[] { "network update" }, delivered);
    }

    [Fact]
    public void OneFailingTwoArgumentSubscriberDoesNotBlockOtherSubscribers()
    {
        var delivered = new List<(string Type, string Summary)>();
        Action<string, string> subscribers = (_, _) => throw new InvalidOperationException("dispatcher stopped");
        subscribers += (type, summary) => delivered.Add((type, summary));

        SafeEventDispatch.Invoke(subscribers, "channel.follow", "Nuevo follow: luna");

        Assert.Equal(new[] { ("channel.follow", "Nuevo follow: luna") }, delivered);
    }

    [Fact]
    public void NullSubscribersAreIgnored()
    {
        SafeEventDispatch.Invoke<string>(null, "event");
        SafeEventDispatch.Invoke<string, string>(null, "event", "summary");
    }
}
