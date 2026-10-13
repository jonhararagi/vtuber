using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ChatSpeakCommandPolicyTests
{
    [Theory]
    [InlineData("/angri", "Angry")]
    [InlineData("/angry", "Angry")]
    [InlineData("/happy", "Happy")]
    [InlineData("/surprised", "Surprised")]
    [InlineData("/idle", "Idle")]
    [InlineData("/talking", "Talking")]
    [InlineData("/blink", "Blink")]
    [InlineData("/left eye close", "LeftEyeClose")]
    [InlineData("/left-eye-close", "LeftEyeClose")]
    [InlineData("/left_eye_close", "LeftEyeClose")]
    public void OnlyExplicitlySupportedAvatarCommandsAreAccepted(string input, string expected)
    {
        Assert.True(ChatSpeakCommandPolicy.TryParseAvatarCommand(input, out var action));
        Assert.Equal(expected, action.Expression);
    }

    [Theory]
    [InlineData("/reset-chat")]
    [InlineData("/restart-chat")]
    [InlineData("/switch-chat")]
    [InlineData("/bot-on")]
    [InlineData("/bot-off")]
    [InlineData("/delete-files")]
    [InlineData("//happy")]
    [InlineData("/happy now")]
    public void AdministrativeOrUnknownCommandsAreNotAvatarActions(string input)
    {
        Assert.False(ChatSpeakCommandPolicy.TryParseAvatarCommand(input, out _));
    }

    [Fact]
    public void AssistantOutputStripsCommandsAndKeepsOnlyNaturalSpeech()
    {
        var parsed = ChatSpeakCommandPolicy.ParseAssistantOutput("¡Hola chat!\n/happy\n/switch-chat\n¿Todo bien?\n/bot-on");
        Assert.Equal("¡Hola chat! ¿Todo bien?", parsed.SpeechText);
        Assert.Single(parsed.Commands);
        Assert.Equal("Happy", parsed.Commands[0].Expression);
    }

    [Fact]
    public void MoreThanFourVisualDirectivesAreBounded()
    {
        var parsed = ChatSpeakCommandPolicy.ParseAssistantOutput("/happy\n/angri\n/surprised\n/blink\n/idle\nHola");
        Assert.Equal(4, parsed.Commands.Count);
        Assert.Equal("Hola", parsed.SpeechText);
    }

    [Fact]
    public void PromptInstructionsKeepAdministrativeActionsOutOfAiControl()
    {
        var instructions = ChatSpeakCommandPolicy.GetPromptInstructions();
        Assert.Contains("/left eye close", instructions);
        Assert.Contains("exclusivas del creador", instructions);
    }
}
