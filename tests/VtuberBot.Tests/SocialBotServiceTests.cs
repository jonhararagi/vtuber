using System.Reflection;
using VtuberBot.App;
using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class SocialBotServiceTests
{
    private sealed class TempDb : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("vtuber-social-").FullName;
        public string DatabasePath => Path.Combine(Root, "data", "product.db");
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
    }

    private sealed class FakeProvider : IChatIntelligenceProvider
    {
        public int Calls;
        public int ThrowOnCall { get; set; }
        public TaskCompletionSource<bool> SecondCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Block { get; set; }
        public Task<ChatIntelligenceResult> CheckAsync(ChatIntelligenceOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new ChatIntelligenceResult(true, "", "Available"));
        public async Task<ChatIntelligenceResult> GenerateAsync(ChatIntelligenceOptions options, IReadOnlyList<ChatConversationMessage> messages, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref Calls);
            Called.TrySetResult(true);
            if (call == 2) SecondCalled.TrySetResult(true);
            if (ThrowOnCall == call) throw new InvalidOperationException("simulated provider failure");
            if (Block)
            {
                try { await Release.Task.WaitAsync(cancellationToken); }
                catch (OperationCanceledException) { CancellationObserved.TrySetResult(true); throw; }
            }
            return new(true, "Hola, chat.", "Generated");
        }
    }

    [Fact]
    public async Task AutomaticConversationIsDisabledByDefault()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        using var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider();
        await using (var service = new SocialBotService(store, speech, twitch, provider))
        {
            service.OnChatMessage("alice", "@VTuberBot hola", "channel");
            Assert.Equal(0, Volatile.Read(ref provider.Calls));
        }
    }

    [Fact]
    public async Task OnlyConfiguredMentionOrCommandTriggersConversationAndRunsOffCaller()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        store.SetSetting("social.chat.command", "!bot");
        var speech = new LocalSpeechQueue();
        var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using (var service = new SocialBotService(store, speech, twitch, provider))
        {
            service.OnChatMessage("alice", "mensaje normal", "channel");
            Assert.Equal(0, Volatile.Read(ref provider.Calls));
            service.OnChatMessage("alice", "!bot hola", "channel");
            await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(1, Volatile.Read(ref provider.Calls));
            provider.Release.TrySetResult(true);
        }
        speech.Dispose();
    }

    [Fact]
    public async Task BotIdentityIsExcludedToPreventSelfReplyLoop()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        store.SetSetting("twitch.bot.login", "mybotlogin");
        var speech = new LocalSpeechQueue();
        var twitch = new TwitchChatClient();
        var provider = new FakeProvider();
        await using (var service = new SocialBotService(store, speech, twitch, provider))
        {
            service.OnChatMessage("mybotlogin", "@VTuberBot no te respondas", "channel");
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.Equal(0, Volatile.Read(ref provider.Calls));
        }
        speech.Dispose();
    }

    [Fact]
    public async Task DisablingAutomaticRepliesCancelsBlockedGeneration()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using var service = new SocialBotService(store, speech, twitch, provider);

        service.OnChatMessage("alice", "!bot hola", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.SetAutomaticRepliesEnabled(false);
        await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal("false", store.GetSetting("social.chat.enabled", "false"));
        Assert.Equal(1, Volatile.Read(ref provider.Calls));
        speech.Dispose();
    }

    [Fact]
    public async Task DisablingAutomaticRepliesDropsQueuedConversations()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        store.SetSetting("social.chat.globalCooldownSeconds", "1");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using var service = new SocialBotService(store, speech, twitch, provider);

        service.OnChatMessage("alice", "!bot primero", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.OnChatMessage("bob", "!bot segundo", "channel");
        Assert.Equal(1, service.PendingConversations);
        service.SetAutomaticRepliesEnabled(false);
        Assert.Equal(0, service.PendingConversations);
        await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref provider.Calls));
        speech.Dispose();
    }

    [Fact]
    public async Task DisablingAiDoesNotDisableIndependentChatTts()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "false");
        store.SetSetting("social.tts.enabled", "true");
        store.SetSetting("social.tts.readAll", "true");
        store.SetSetting("social.tts.intervalMs", "0");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider();
        await using var service = new SocialBotService(store, speech, twitch, provider);
        var speechQueued = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StatusChanged += status =>
        {
            if (status.State == "TTS" && status.Detail.Contains("Mensaje de chat encolado", StringComparison.Ordinal))
                speechQueued.TrySetResult(true);
        };

        service.OnChatMessage("alice", "mensaje independiente", "channel");

        await speechQueued.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref provider.Calls));
        speech.Dispose();
    }

    [Fact]
    public async Task PausedGenerationDoesNotEnqueueConversationalSpeech()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        store.SetSetting("social.chat.speakReplies", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using var service = new SocialBotService(store, speech, twitch, provider);

        service.OnChatMessage("alice", "!bot hola", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.SetAutomaticRepliesEnabled(false);
        await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(0, speech.PendingCount);
        speech.Dispose();
    }

    [Fact]
    public async Task SaturatedConversationQueueStaysBoundedAndReportsOverflow()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using var service = new SocialBotService(store, speech, twitch, provider);
        var overflow = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StatusChanged += status =>
        {
            if (status.Detail.Contains("Cola de conversaciones llena", StringComparison.Ordinal))
                overflow.TrySetResult(true);
        };

        service.OnChatMessage("first", "!bot bloqueá", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        for (var i = 0; i < 65; i++)
            service.OnChatMessage("viewer" + i, "!bot hola", "channel");

        await overflow.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(64, service.PendingConversations);
        speech.Dispose();
    }

    [Fact]
    public async Task ProviderFailureDoesNotConsumeCooldownOrBlockNextConversation()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { ThrowOnCall = 1 };
        await using var service = new SocialBotService(store, speech, twitch, provider);

        service.OnChatMessage("alice", "!bot primero", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.OnChatMessage("bob", "!bot segundo", "channel");
        await provider.SecondCalled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref provider.Calls));
        speech.Dispose();
    }

    [Fact]
    public async Task TtsFiltersRejectLongLinksCommandsAndIgnoredUsers()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.tts.enabled", "true");
        store.SetSetting("social.tts.readAll", "true");
        store.SetSetting("social.tts.ignoreCommands", "true");
        store.SetSetting("social.tts.ignoreLinks", "true");
        store.SetSetting("social.tts.ignoredUsers", "baduser");
        store.SetSetting("social.tts.maxChars", "20");
        store.SetSetting("social.tts.intervalMs", "0");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider();
        await using var service = new SocialBotService(store, speech, twitch, provider);
        var accepted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StatusChanged += status =>
        {
            if (status.State == "TTS" && status.Detail.Contains("Mensaje de chat encolado", StringComparison.Ordinal))
                accepted.TrySetResult(true);
        };

        service.OnChatMessage("alice", "!bot comando", "channel");
        service.OnChatMessage("alice", "https://example.com", "channel");
        service.OnChatMessage("baduser", "hola mundo", "channel");
        service.OnChatMessage("alice", "este mensaje supera veinte", "channel");
        Assert.False(accepted.Task.IsCompleted);

        service.OnChatMessage("alice", "hola chat", "channel");
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref provider.Calls));
        speech.Dispose();
    }

    [Fact]
    public async Task DisposingServiceCancelsActiveGenerationAndStopsWorker()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        var service = new SocialBotService(store, speech, twitch, provider);

        service.OnChatMessage("alice", "!bot hola", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await service.DisposeAsync();
        await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, service.PendingConversations);
        speech.Dispose();
    }

    [Fact]
    public async Task TwitchDisconnectCancelsOldSessionAndReconnectAcceptsOnlyNewWork()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        store.SetSetting("social.chat.enabled", "true");
        var speech = new LocalSpeechQueue();
        await using var twitch = new TwitchChatClient();
        var provider = new FakeProvider { Block = true };
        await using var service = new SocialBotService(store, speech, twitch, provider);

        SetTwitchConnection(twitch, true);
        service.OnChatMessage("alice", "!bot sesión vieja activa", "channel");
        await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        service.OnChatMessage("bob", "!bot sesión vieja en cola", "channel");
        Assert.Equal(1, service.PendingConversations);

        SetTwitchConnection(twitch, false);
        await provider.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, service.PendingConversations);

        SetTwitchConnection(twitch, true);
        service.OnChatMessage("carol", "!bot sesión nueva", "channel");
        await provider.SecondCalled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(2, Volatile.Read(ref provider.Calls));
        provider.Release.TrySetResult(true);
        speech.Dispose();
    }

    private static void SetTwitchConnection(TwitchChatClient client, bool connected)
    {
        var setter = typeof(TwitchChatClient).GetMethod("SetConnectionState", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Connection state seam was not found.");
        setter.Invoke(client, [connected]);
    }
}
