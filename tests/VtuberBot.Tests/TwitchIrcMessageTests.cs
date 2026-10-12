using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class TwitchIrcMessageTests
{
    [Fact]
    public void ParsesTaggedPrivmsgIntoUsernameCommandAndTrailingText()
    {
        const string raw = "@badge-info=;badges=moderator/1;color=#00FF00;display-name=Cool\\sName;id=message-123 :coolname!coolname@coolname.tmi.twitch.tv PRIVMSG #mychannel :!hola mundo";

        Assert.True(TwitchIrcMessage.TryParse(raw, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("PRIVMSG", parsed.Command);
        Assert.Equal("coolname!coolname@coolname.tmi.twitch.tv", parsed.Prefix);
        Assert.Equal(new[] { "#mychannel" }, parsed.Parameters);
        Assert.Equal("!hola mundo", parsed.Trailing);
        Assert.Equal("message-123", parsed.Tags["id"]);
        Assert.Equal("Cool Name", parsed.Tags["display-name"]);
    }

    [Fact]
    public void UnescapesAllSupportedIrcv3TagEscapes()
    {
        const string raw = "@sample=one\\stwo\\:three\\\\four\\rfive\\nsix;empty= :user!user@host PRIVMSG #channel :hello";

        Assert.True(TwitchIrcMessage.TryParse(raw, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("one two;three\\four\rfive\nsix", parsed.Tags["sample"]);
        Assert.Equal(string.Empty, parsed.Tags["empty"]);
        Assert.Equal("hello", parsed.Trailing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@tags-without-value")]
    [InlineData(":prefix-only")]
    public void RejectsMalformedMessages(string? input)
    {
        Assert.False(TwitchIrcMessage.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }
}
