using System.Text.Json;
using VtuberBot.App;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ChatReactionServiceTests
{
    private sealed class TempDb : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("vtuber-reactions-").FullName;
        public string DatabasePath => Path.Combine(Root, "data", "product.db");
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
    }

    private sealed class Effects
    {
        public bool Connected = true;
        public int Sends;
        public int Speeches;
        public int Expressions;
        public string LastMessage = "";
        public string LastExpression = "";
        public TaskCompletionSource<bool> SendEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondSendEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SpeechEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ExpressionEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SendRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SendCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool BlockSend;

        public Task<bool> Send(string message, CancellationToken token)
        {
            var call = Interlocked.Increment(ref Sends);
            LastMessage = message;
            SendEntered.TrySetResult(true);
            if (call == 2) SecondSendEntered.TrySetResult(true);
            return SendCoreAsync(token);
        }

        private async Task<bool> SendCoreAsync(CancellationToken token)
        {
            if (BlockSend)
            {
                try { await SendRelease.Task.WaitAsync(token); }
                catch (OperationCanceledException) { SendCancelled.TrySetResult(true); throw; }
            }
            return true;
        }

        public bool Speak(string text) { Interlocked.Increment(ref Speeches); SpeechEntered.TrySetResult(true); return true; }
        public bool Expression(string state, CancellationToken token) { if (token.IsCancellationRequested) return false; Interlocked.Increment(ref Expressions); LastExpression = state; ExpressionEntered.TrySetResult(true); return true; }
    }

    private static ChatReactionRule Rule(string name = "hola", string trigger = "hola",
        ChatReactionMatchType type = ChatReactionMatchType.WholeWord, bool enabled = false,
        bool speak = false, string expression = "", int global = 0, int user = 0, int perMinute = 60,
        params string[] responses) =>
        new(Guid.NewGuid().ToString("N"), name, trigger, type,
            responses.Length == 0 ? ["¡Hola {user}!"] : responses,
            speak, expression, global, user, perMinute, enabled);

    private static ChatReactionService Create(ProductStore store, Effects effects, int capacity = 64) =>
        new(store, effects.Send, effects.Speak, effects.Expression, () => effects.Connected, capacity);

    [Theory]
    [InlineData(ChatReactionMatchType.Exact, "¡HÓLA  CHAT!", "¡hola chat!", true)]
    [InlineData(ChatReactionMatchType.Exact, "hola", "hola chat", false)]
    [InlineData(ChatReactionMatchType.WholeWord, "HOLA, mundo", "hola", true)]
    [InlineData(ChatReactionMatchType.WholeWord, "holanda", "hola", false)]
    [InlineData(ChatReactionMatchType.Contains, "la palabra hola aparece", "hola", true)]
    [InlineData(ChatReactionMatchType.Contains, "saludos", "hola", false)]
    public void MatchingSupportsThreeTypesCaseFoldingAndAccentNormalization(ChatReactionMatchType type, string message, string trigger, bool expected)
    {
        var rule = Rule(trigger: trigger, type: type);
        Assert.Equal(expected, ChatReactionService.Matches(rule, message));
    }

    [Fact]
    public void EmptyOrOversizedMessagesNeverMatch()
    {
        var rule = Rule();
        Assert.False(ChatReactionService.Matches(rule, ""));
        Assert.False(ChatReactionService.Matches(rule, "   "));
        Assert.False(ChatReactionService.Matches(rule, new string('x', ChatReactionService.MaximumMessageLength + 1)));
    }

    [Fact]
    public async Task NewRulesStartDisabledAndPersistAcrossServiceRestart()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting(ChatReactionService.RulesSettingKey, "[]");
        store.SetSetting("chat.reactions.bilingual-defaults.v1", "true");
        var effects = new Effects();
        var rule = Rule(responses: ["Uno", "Dos"]);
        await using (var service = Create(store, effects))
        {
            Assert.True(service.SaveRule(rule));
            Assert.False(service.GetRules().Single().Enabled);
        }
        await using (var restored = Create(store, effects))
        {
            var loaded = Assert.Single(restored.GetRules());
            Assert.Equal(rule.Trigger, loaded.Trigger);
            Assert.Equal(new[] { "Uno", "Dos" }, loaded.Responses);
            Assert.False(loaded.Enabled);
        }
    }

    [Fact]
    public async Task FreshInstallSeedsEnabledBilingualAnimeCommands()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);

        var rules = service.GetRules();
        Assert.Equal(8, rules.Count);
        Assert.All(rules, rule => Assert.True(rule.Enabled));
        Assert.Contains(rules, rule => rule.Trigger == "/ohayo" && rule.Responses.Any(x => x.Contains("おはよう！") && x.Contains("Buenos días")));
        Assert.Contains(rules, rule => rule.Trigger == "/baka" && rule.Responses.Any(x => x.Contains("お兄ちゃんのばか！")));
        Assert.Contains(rules, rule => rule.Trigger == "/nani" && rule.Expression == "Surprised");
        Assert.Contains(rules, rule => rule.Trigger == "/okaerinasai" && rule.Responses.Any(x => x.Contains("{user}")));
        Assert.Contains(rules, rule => rule.Trigger == "!ohayo");
        Assert.Contains(rules, rule => rule.Trigger == "!baka");
        Assert.Contains(rules, rule => rule.Trigger == "!nani");
        Assert.Contains(rules, rule => rule.Trigger == "!okaerinasai");
        Assert.Equal(8, JsonSerializer.Deserialize<List<ChatReactionRule>>(store.GetSetting(ChatReactionService.RulesSettingKey))!.Count);
    }

    [Fact]
    public async Task ExistingReactionSettingsReceiveBilingualDefaultsOnlyOnce()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting(ChatReactionService.RulesSettingKey, "[]");
        var effects = new Effects();

        await using (var service = Create(store, effects))
        {
            Assert.Equal(8, service.GetRules().Count);
        }

        // Once migrated, deleting all rules remains a user choice after restart.
        store.SetSetting(ChatReactionService.RulesSettingKey, "[]");
        await using var restarted = Create(store, effects);
        Assert.Empty(restarted.GetRules());
    }

    [Fact]
    public async Task InvalidPersistedJsonRecoversToEmptyRuleSet()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting(ChatReactionService.RulesSettingKey, "{invalid");
        var effects = new Effects();
        await using var service = Create(store, effects);
        Assert.Empty(service.GetRules());
        Assert.Equal("[]", store.GetSetting(ChatReactionService.RulesSettingKey));
    }

    [Fact]
    public async Task LocalDryRunDoesNotSendChatVoiceOrExpression()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, speak: true, expression: "Happy", responses: ["Hola {user}"]);
        Assert.True(service.SaveRule(rule));
        Assert.True(service.SetRuleEnabled(rule.Id, true));

        var result = service.TestMessage("viewer", "hola");

        Assert.Equal("Simulación local", result.Outcome);
        Assert.True(result.Simulated);
        Assert.Contains("Hola viewer", result.Detail);
        Assert.Equal(0, effects.Sends);
        Assert.Equal(0, effects.Speeches);
        Assert.Equal(0, effects.Expressions);
    }

    [Fact]
    public async Task FirstMatchingRuleHasPriorityAndEffectsUseExistingCallbacks()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var first = Rule(name: "primera", trigger: "hola", type: ChatReactionMatchType.Contains, enabled: true,
            speak: true, expression: "Happy", responses: ["¡Hola {user}!"]);
        var second = Rule(name: "segunda", trigger: "hola chat", type: ChatReactionMatchType.Exact, enabled: true,
            responses: ["No debería ejecutarse"]);
        Assert.True(service.SaveRule(first)); Assert.True(service.SetRuleEnabled(first.Id, true));
        Assert.True(service.SaveRule(second)); Assert.True(service.SetRuleEnabled(second.Id, true));
        service.SetTwitchConnected(true);

        Assert.True(service.OnChatMessage("alice", "hola chat", "msg-1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        effects.SendRelease.TrySetResult(true);
        await effects.SpeechEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await effects.ExpressionEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, effects.Sends);
        Assert.Equal("¡Hola alice!", effects.LastMessage);
        Assert.Equal("Happy", effects.LastExpression);
        await WaitForOutcomeAsync(service, "Respuesta enviada");
        Assert.Equal("primera", Assert.Single(service.GetRecentActivity(), x => x.Outcome == "Respuesta enviada").RuleName);
    }

    [Fact]
    public async Task DuplicateMessageIdsAreRejectedAndPerMinuteLimitApplies()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, global: 0, user: 0, perMinute: 1);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);

        Assert.True(service.OnChatMessage("alice", "hola", "same-id"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(service.OnChatMessage("alice", "hola", "same-id"));
        Assert.False(service.OnChatMessage("bob", "hola", "different-id"));
        effects.SendRelease.TrySetResult(true);
        await WaitForOutcomeAsync(service, "Respuesta enviada");

        Assert.Equal(1, effects.Sends);
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Duplicado");
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Límite por minuto");
    }

    [Fact]
    public async Task DisconnectCancelsActiveSendAndOldQueuedWorkDoesNotReturnAfterReconnect()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects { BlockSend = true };
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);
        Assert.True(service.OnChatMessage("alice", "hola", "old-1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(service.OnChatMessage("bob", "hola", "old-2"));

        effects.Connected = false;
        service.SetTwitchConnected(false);
        await effects.SendCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        effects.Connected = true;
        service.SetTwitchConnected(true);
        Assert.Equal(0, service.PendingCount);
        Assert.False(service.OnChatMessage("bob", "hola", "old-2"));
        effects.BlockSend = false;
        Assert.True(service.OnChatMessage("carol", "hola", "new-1"));
        await effects.SecondSendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Cancelada");
    }

    [Fact]
    public async Task PausingInvalidatesQueuedWorkAndReenablingAcceptsNewMessages()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects { BlockSend = true };
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);
        Assert.True(service.OnChatMessage("alice", "hola", "first"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(service.OnChatMessage("bob", "hola", "pending"));
        service.SetPaused(true);
        await effects.SendCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.SetPaused(false);
        effects.BlockSend = false;
        Assert.True(service.OnChatMessage("carol", "hola", "fresh"));
        await effects.SecondSendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(service.GetRecentActivity(), x => x.User == "bob" && x.Outcome == "Respuesta enviada");
    }

    [Fact]
    public async Task BoundedQueueRejectsOverflowWithoutStartingDuplicateWork()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects { BlockSend = true };
        await using var service = Create(store, effects, capacity: 1);
        var rule = Rule(enabled: true, global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);
        Assert.True(service.OnChatMessage("a", "hola", "1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(service.OnChatMessage("b", "hola", "2"));
        Assert.False(service.OnChatMessage("c", "hola", "3"));
        Assert.Equal(1, service.PendingCount);
        effects.SendRelease.TrySetResult(true);
    }

    [Fact]
    public async Task GlobalAndUserCooldownsAreAppliedBeforeEffects()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, global: 30, user: 30, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);

        Assert.True(service.OnChatMessage("alice", "hola", "1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(service.OnChatMessage("bob", "hola", "2"));
        Assert.False(service.OnChatMessage("alice", "hola", "3"));
        effects.SendRelease.TrySetResult(true);
        await WaitForOutcomeAsync(service, "Respuesta enviada");
        Assert.Equal(1, effects.Sends);
    }

    private static async Task WaitForOutcomeAsync(ChatReactionService service, string outcome)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(ChatReactionResult result)
        {
            if (result.Outcome == outcome) completion.TrySetResult(true);
        }
        service.ResultChanged += Handler;
        try
        {
            if (service.GetRecentActivity().Any(x => x.Outcome == outcome)) return;
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally { service.ResultChanged -= Handler; }
    }

    [Fact]
    public async Task OwnBotAndChannelMessagesAreIgnoredToPreventEchoLoops()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("twitch.channel", "channelowner");
        store.SetSetting("twitch.bot.login", "mybot");
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(enabled: true, global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);

        Assert.False(service.OnChatMessage("mybot", "hola", "own-1"));
        Assert.False(service.OnChatMessage("channelowner", "hola", "own-2"));
        Assert.Equal(0, effects.Sends);
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Eco propio ignorado");
    }

    [Fact]
    public async Task PerUserCooldownAllowsAnotherViewerButBlocksSameViewer()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(global: 0, user: 30, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);

        Assert.True(service.OnChatMessage("alice", "hola", "u-1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(service.OnChatMessage("alice", "hola", "u-2"));
        Assert.True(service.OnChatMessage("bob", "hola", "u-3"));
        effects.SendRelease.TrySetResult(true);
        await effects.SecondSendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(2, effects.Sends);
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Cooldown usuario");
    }

    [Fact]
    public async Task ClosingServiceCancelsActiveSendAndStopsBackgroundWorker()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects { BlockSend = true };
        var service = Create(store, effects);
        var rule = Rule(global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);
        Assert.True(service.OnChatMessage("alice", "hola", "close-1"));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await effects.SendCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Cancelada");
    }

    [Fact]
    public async Task RepeatedEventsWithoutProviderIdsAreDeduplicatedBriefly()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var effects = new Effects();
        await using var service = Create(store, effects);
        var rule = Rule(global: 0, user: 0, perMinute: 60);
        Assert.True(service.SaveRule(rule)); Assert.True(service.SetRuleEnabled(rule.Id, true));
        service.SetTwitchConnected(true);

        Assert.True(service.OnChatMessage("alice", "hola chat", null));
        await effects.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(service.OnChatMessage("alice", "hola chat", null));
        effects.SendRelease.TrySetResult(true);
        await WaitForOutcomeAsync(service, "Respuesta enviada");

        Assert.Equal(1, effects.Sends);
        Assert.Contains(service.GetRecentActivity(), x => x.Outcome == "Duplicado");
    }
}
