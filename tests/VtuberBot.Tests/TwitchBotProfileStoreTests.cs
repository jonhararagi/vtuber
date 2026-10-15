using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class TwitchBotProfileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "vtuberbot-twitch-profiles-" + Guid.NewGuid().ToString("N"));
    private readonly string dataDirectory;
    private readonly string secretDirectory;

    public TwitchBotProfileStoreTests()
    {
        dataDirectory = Path.Combine(root, "data");
        secretDirectory = Path.Combine(root, "secrets");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(secretDirectory);
    }

    [Fact]
    public void ProfileMetadataPersistsButOAuthTokensStayProtected()
    {
        var store = new TwitchBotProfileStore(dataDirectory, secretDirectory);
        var id = Guid.NewGuid().ToString("N");
        var credentials = new TwitchOAuthCredentials(
            "access-token-that-must-not-be-plain-text",
            "refresh-token-that-must-not-be-plain-text",
            DateTimeOffset.UtcNow.AddHours(1),
            "client-id",
            ["chat:read", "chat:edit"]);
        var profile = new TwitchBotProfile(id, "Mode-chan", "mode_chan", "my_channel", "client-id", DateTimeOffset.UtcNow);

        store.Save(profile, credentials);

        var loaded = Assert.Single(store.List());
        Assert.Equal("Mode-chan", loaded.Name);
        Assert.Equal("mode_chan", loaded.BotLogin);
        Assert.Equal("my_channel", loaded.Channel);
        var metadata = File.ReadAllText(Path.Combine(dataDirectory, "twitch-bot-profiles.json"));
        Assert.DoesNotContain(credentials.AccessToken, metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.RefreshToken!, metadata, StringComparison.Ordinal);

        var protectedFile = File.ReadAllBytes(Path.Combine(secretDirectory, "twitch-bot-" + id + ".bin"));
        var protectedText = Convert.ToBase64String(protectedFile);
        Assert.False(protectedText.Contains(credentials.AccessToken, StringComparison.Ordinal));
        Assert.False(protectedText.Contains(credentials.RefreshToken!, StringComparison.Ordinal));

        var reopened = new TwitchBotProfileStore(dataDirectory, secretDirectory);
        var restoredCredentials = reopened.LoadCredentials(loaded);
        Assert.Equal(credentials.AccessToken, restoredCredentials.AccessToken);
        Assert.Equal(credentials.RefreshToken, restoredCredentials.RefreshToken);
        Assert.Equal(credentials.ClientId, restoredCredentials.ClientId);

        reopened.Delete(loaded);
        Assert.Empty(reopened.List());
        Assert.False(File.Exists(Path.Combine(secretDirectory, "twitch-bot-" + id + ".bin")));
    }

    [Fact]
    public void CorruptProfileCatalogIsReportedAndNotSilentlyOverwritten()
    {
        var store = new TwitchBotProfileStore(dataDirectory, secretDirectory);
        var path = Path.Combine(dataDirectory, "twitch-bot-profiles.json");
        File.WriteAllText(path, "{");

        Assert.Throws<InvalidDataException>(() => store.List());
        Assert.Equal("{", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
