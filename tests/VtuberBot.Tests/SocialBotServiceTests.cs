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
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Block { get; set; }
        public Task<ChatIntelligenceResult> CheckAsync(ChatIntelligenceOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new ChatIntelligenceResult(true, "", "Available"));
        public async Task<ChatIntelligenceResult> GenerateAsync(ChatIntelligenceOptions options, IReadOnlyList<ChatConversationMessage> messages, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls); Called.TrySetResult(true);
            if (Block) await Release.Task.WaitAsync(cancellationToken);
            return new(true, "Hola, chat.", "Generated");
        }
    }

    [Fact]
    public async Task AutomaticConversationIsDisabledByDefault()
    {
        using var temp = new TempDb();
        var store = new ProductStore(temp.DatabasePath);
        var speech = new LocalSpeechQueue();
        var twitch = new TwitchChatClient();
        var provider = new FakeProvider();
        await using (var service = new SocialBotService(store, speech, twitch, provider))
        {
            service.OnChatMessage("alice", "@VTuberBot hola", "channel");
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.Equal(0, Volatile.Read(ref provider.Calls));
        }
        speech.Dispose();
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
}
