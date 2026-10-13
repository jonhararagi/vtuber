using System.Text.Json;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class EventActionCoordinatorTests
{
    [Fact]
    public async Task RulesPersistAcrossCoordinatorInstancesAndNewRulesAreNotImplicitlyEnabled()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        var first = new EventActionCoordinator(settings, dispatcher);
        var saved = first.SaveRule(Rule(enabled: false));
        Assert.False(saved.Enabled);
        await first.DisposeAsync();

        var second = new EventActionCoordinator(settings, dispatcher);
        Assert.Contains(second.GetRules(), x => x.Id == saved.Id && !x.Enabled);
        Assert.True(second.RemoveRule(saved.Id));
        Assert.Empty(second.GetRules());
        await second.DisposeAsync();
    }

    [Fact]
    public async Task RealNormalizedEventSelectsOnlyMatchingSourceAndTypeAndChecksFields()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.AutomationsPaused = false;
        coordinator.SaveRule(Rule(enabled: true, conditions: [new("userName", "eq", "alice")]));

        using var payload = JsonDocument.Parse("""{"userName":"alice","message":"hello"}""");
        Assert.True(coordinator.TryPublish(NormalizedBotEvent.Create("Streamer.bot", "Twitch", "Follow",
            payload.RootElement, providerEventId: "evt-1")));
        Assert.True(await dispatcher.WaitForCountAsync(1));
        Assert.Equal("action-1", dispatcher.Targets.Single());

        using var wrongSourcePayload = JsonDocument.Parse("""{"userName":"alice"}""");
        coordinator.TryPublish(NormalizedBotEvent.Create("VTuberBot.EventSub", "Twitch", "Follow",
            wrongSourcePayload.RootElement, providerEventId: "evt-2"));
        using var missingField = JsonDocument.Parse("""{"userName":null}""");
        coordinator.TryPublish(NormalizedBotEvent.Create("Streamer.bot", "Twitch", "Follow",
            missingField.RootElement, providerEventId: "evt-3"));
        await Task.Delay(150);
        Assert.Single(dispatcher.Targets);
        await AssertActivityAsync(coordinator, EventActionOutcome.ConditionNotMet);
    }

    [Fact]
    public async Task GlobalPauseAndDisabledRulesPreventAutomaticExecution()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.SaveRule(Rule(enabled: false));
        Publish(coordinator, "one");
        await Task.Delay(100);
        Assert.Empty(dispatcher.Targets);

        coordinator.SaveRule(Rule(enabled: true));
        coordinator.AutomationsPaused = true;
        Publish(coordinator, "two");
        await Task.Delay(100);
        Assert.Empty(dispatcher.Targets);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == EventActionOutcome.AutomationsPaused);
    }

    [Fact]
    public async Task DuplicateProviderDeliveryIsDiscardedButDistinctIdenticalMessagesAreAllowed()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.AutomationsPaused = false;
        coordinator.SaveRule(Rule(enabled: true));

        Publish(coordinator, "provider-same", "same text");
        Publish(coordinator, "provider-same", "same text");
        Assert.True(await dispatcher.WaitForCountAsync(1));
        await Task.Delay(100);
        Assert.Single(dispatcher.Targets);

        Publish(coordinator, "provider-different-1", "same text");
        Publish(coordinator, "provider-different-2", "same text");
        Assert.True(await dispatcher.WaitForCountAsync(3));
        Assert.Equal(3, dispatcher.Targets.Count);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == EventActionOutcome.DuplicateDiscarded);
    }

    [Fact]
    public async Task CooldownAndGlobalLimitAreEnforced()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.AutomationsPaused = false;
        coordinator.GlobalMaxExecutionsPerMinute = 1;
        coordinator.SaveRule(Rule(enabled: true, cooldown: 30));
        Publish(coordinator, "first");
        Assert.True(await dispatcher.WaitForCountAsync(1));
        Publish(coordinator, "second");
        await Task.Delay(150);
        Assert.Single(dispatcher.Targets);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome is EventActionOutcome.Cooldown or EventActionOutcome.RateLimited);
    }

    [Fact]
    public async Task DispatcherFailureIsIsolatedAndDoesNotKillWorker()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher { FailFirst = true };
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.AutomationsPaused = false;
        coordinator.SaveRule(Rule(enabled: true));
        Publish(coordinator, "first");
        Publish(coordinator, "second");
        Assert.True(await dispatcher.WaitForCountAsync(2));
        Assert.Equal(2, dispatcher.Targets.Count);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == EventActionOutcome.InternalError);
    }

    [Fact]
    public async Task AutomaticPngtuberDestinationRequiresExplicitGlobalAuthorization()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        coordinator.AutomationsPaused = false;
        coordinator.SaveRule(Rule(enabled: true, destination: EventActionDestination.PngtuberPlus, confirmed: true));
        Publish(coordinator, "png-1");
        await Task.Delay(100);
        Assert.Empty(dispatcher.Targets);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == EventActionOutcome.InvalidDestination);
    }

    [Fact]
    public async Task InvalidRulesAndMalformedEventsAreRejectedWithoutCrashingCoordinator()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        await using var coordinator = new EventActionCoordinator(settings, dispatcher);
        Assert.Throws<ArgumentException>(() => coordinator.SaveRule(Rule(enabled: true, conditions: [new("../secret", "eq", "x")])));
        Assert.False(coordinator.TryPublish(new NormalizedBotEvent("", "", "", null, DateTimeOffset.UtcNow,
            null, default, "", NormalizedEventOrigin.External)));
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == EventActionOutcome.InternalError);
    }

    [Fact]
    public async Task ShutdownStopsWorkerAndRejectsNewEvents()
    {
        var settings = new MemorySettings();
        var dispatcher = new RecordingDispatcher();
        var coordinator = new EventActionCoordinator(settings, dispatcher);
        await coordinator.DisposeAsync();
        using var payload = JsonDocument.Parse("""{"ok":true}""");
        Assert.False(coordinator.TryPublish(NormalizedBotEvent.Create("Streamer.bot", "Twitch", "Follow", payload.RootElement)));
    }

    private static EventActionRule Rule(bool enabled, IReadOnlyList<EventActionCondition>? conditions = null,
        int cooldown = 0, EventActionDestination destination = EventActionDestination.StreamerBot, bool confirmed = true) =>
        new(Guid.NewGuid(), "test rule", enabled, "Streamer.bot", "Twitch", "Follow",
            conditions ?? [], destination, destination == EventActionDestination.StreamerBot ? "action-1" : Guid.NewGuid().ToString("D"),
            cooldown, 10, confirmed);

    private static void Publish(EventActionCoordinator coordinator, string id, string message = "hello")
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(new { userName = "alice", message }));
        coordinator.TryPublish(NormalizedBotEvent.Create("Streamer.bot", "Twitch", "Follow",
            payload.RootElement, providerEventId: id));
    }

    private static async Task AssertActivityAsync(EventActionCoordinator coordinator, EventActionOutcome outcome)
    {
        await Task.Delay(100);
        Assert.Contains(coordinator.GetRecentActivity(), x => x.Outcome == outcome);
    }

    private sealed class MemorySettings : IProductSettingsStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public string GetSetting(string key, string fallback = "") => values.TryGetValue(key, out var value) ? value : fallback;
        public void SetSetting(string key, string value) => values[key] = value;
    }

    private sealed class RecordingDispatcher : IEventActionDispatcher
    {
        private readonly object gate = new();
        private readonly List<string> targets = [];
        private int calls;
        public bool FailFirst { get; set; }
        public IReadOnlyList<string> Targets { get { lock (gate) return targets.ToArray(); } }

        public Task<bool> WaitForCountAsync(int expected)
        {
            return Task.Run(async () =>
            {
                var until = DateTimeOffset.UtcNow.AddSeconds(3);
                while (DateTimeOffset.UtcNow < until)
                {
                    if (Targets.Count >= expected) return true;
                    await Task.Delay(10);
                }
                return false;
            });
        }

        public Task<EventActionExecutionResult> DispatchAsync(EventActionRule rule, NormalizedBotEvent evt, CancellationToken cancellationToken)
        {
            lock (gate) targets.Add(rule.TargetId);
            var current = Interlocked.Increment(ref calls);
            if (FailFirst && current == 1) throw new InvalidOperationException("test failure");
            return Task.FromResult(new EventActionExecutionResult(EventActionOutcome.RequestAccepted, "accepted"));
        }
    }
}
