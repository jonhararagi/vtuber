using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class CommandPermissionPolicyTests
{
    [Fact]
    public void AppliesConfiguredScopesAndModeratorInheritance()
    {
        Assert.True(CommandPermissionPolicy.IsAllowed("everyone", Array.Empty<string>()));
        Assert.False(CommandPermissionPolicy.IsAllowed("subscriber", Array.Empty<string>()));
        Assert.True(CommandPermissionPolicy.IsAllowed("subscriber", new[] { "subscriber" }));
        Assert.True(CommandPermissionPolicy.IsAllowed("subscriber", new[] { "moderator" }));
        Assert.False(CommandPermissionPolicy.IsAllowed("subscriber", new[] { "vip" }));
        Assert.True(CommandPermissionPolicy.IsAllowed("vip", new[] { "vip" }));
        Assert.True(CommandPermissionPolicy.IsAllowed("vip", new[] { "moderator" }));
        Assert.True(CommandPermissionPolicy.IsAllowed("moderator", new[] { "moderator" }));
        Assert.False(CommandPermissionPolicy.IsAllowed("moderator", new[] { "vip" }));
        Assert.False(CommandPermissionPolicy.IsAllowed("broadcaster", new[] { "moderator" }));
        Assert.True(CommandPermissionPolicy.IsAllowed("broadcaster", new[] { "broadcaster" }));
        Assert.False(CommandPermissionPolicy.IsAllowed("owner", new[] { "broadcaster" }));
        Assert.True(CommandPermissionPolicy.IsRecognizedScope("subscriber"));
        Assert.False(CommandPermissionPolicy.IsRecognizedScope("owner"));
    }

    [Fact]
    public void ResolvesRolesOnlyFromIrcTagsAndChannelIdentity()
    {
        const string raw = "@badges=moderator/1,subscriber/12;color=#00AAFF;mod=1;subscriber=1;vip=0;display-name=Test :testuser!testuser@testuser.tmi.twitch.tv PRIVMSG #channel :!premium";
        Assert.True(TwitchIrcMessage.TryParse(raw, out var parsed));
        Assert.NotNull(parsed);

        var roles = CommandPermissionPolicy.GetTwitchRoles(parsed, "channel");
        Assert.Contains("moderator", roles);
        Assert.Contains("subscriber", roles);
        Assert.DoesNotContain("vip", roles);
        Assert.DoesNotContain("broadcaster", roles);

        Assert.True(TwitchIrcMessage.TryParse(
            "@badges=broadcaster/1;mod=1 :channel!channel@channel.tmi.twitch.tv PRIVMSG #channel :!admin",
            out var broadcasterMessage));
        Assert.NotNull(broadcasterMessage);
        Assert.Contains("broadcaster", CommandPermissionPolicy.GetTwitchRoles(broadcasterMessage, "channel"));
    }
}
