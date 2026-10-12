using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AutomaticModerationPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-12T12:00:00Z");
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [Fact]
    public void RuleActionsRequireTheGlobalOptIn()
    {
        Assert.Equal(AutomaticModerationDecision.Disabled,
            AutomaticModerationPolicy.Evaluate("ban", false, Array.Empty<string>(), "message-id", null, Now, Cooldown));
        Assert.Equal(AutomaticModerationDecision.LogOnly,
            AutomaticModerationPolicy.Evaluate("flag", false, Array.Empty<string>(), "", null, Now, Cooldown));
    }

    [Theory]
    [InlineData("broadcaster")]
    [InlineData("moderator")]
    [InlineData("MODERATOR")]
    public void AutomaticSanctionsNeverTargetBroadcasterOrModerators(string role)
    {
        Assert.True(AutomaticModerationPolicy.IsProtectedUser(new[] { role }));
        Assert.Equal(AutomaticModerationDecision.ProtectedUser,
            AutomaticModerationPolicy.Evaluate("timeout", true, new[] { role }, "message-id", null, Now, Cooldown));
    }

    [Fact]
    public void DeleteRequiresTheServerSuppliedMessageId()
    {
        Assert.Equal(AutomaticModerationDecision.MissingMessageId,
            AutomaticModerationPolicy.Evaluate("delete", true, Array.Empty<string>(), "", null, Now, Cooldown));
        Assert.Equal(AutomaticModerationDecision.Execute,
            AutomaticModerationPolicy.Evaluate("delete", true, Array.Empty<string>(), "irc-message-id", null, Now, Cooldown));
    }

    [Fact]
    public void CooldownStartsOnlyFromTheLastSuccessfulActionTimestamp()
    {
        Assert.Equal(AutomaticModerationDecision.Cooldown,
            AutomaticModerationPolicy.Evaluate("timeout", true, Array.Empty<string>(), "id", Now.AddMinutes(-2), Now, Cooldown));
        Assert.Equal(AutomaticModerationDecision.Execute,
            AutomaticModerationPolicy.Evaluate("timeout", true, Array.Empty<string>(), "id", Now.AddMinutes(-5), Now, Cooldown));
        Assert.Equal(AutomaticModerationDecision.Execute,
            AutomaticModerationPolicy.Evaluate("ban", true, Array.Empty<string>(), "id", null, Now, Cooldown));
    }

    [Theory]
    [InlineData("warn")]
    [InlineData("delete")]
    [InlineData("timeout")]
    [InlineData("ban")]
    public void SupportsTheConfiguredNonFlagActions(string action)
    {
        Assert.True(AutomaticModerationPolicy.IsSupportedAction(action));
        Assert.Equal(AutomaticModerationDecision.Execute,
            AutomaticModerationPolicy.Evaluate(action, true, Array.Empty<string>(), "message-id", null, Now, Cooldown));
    }

    [Theory]
    [InlineData(AutomaticModerationDecision.Execute, true)]
    [InlineData(AutomaticModerationDecision.Cooldown, true)]
    [InlineData(AutomaticModerationDecision.MissingMessageId, true)]
    [InlineData(AutomaticModerationDecision.LogOnly, false)]
    [InlineData(AutomaticModerationDecision.Disabled, false)]
    [InlineData(AutomaticModerationDecision.ProtectedUser, false)]
    [InlineData(AutomaticModerationDecision.UnsupportedAction, false)]
    public void OnlyMatchedSanctionDecisionsSuppressCommandAndPointSideEffects(
        AutomaticModerationDecision decision,
        bool expected) =>
        Assert.Equal(expected, AutomaticModerationPolicy.ShouldSuppressMessageSideEffects(decision));

    [Fact]
    public void UnknownActionIsRejectedRatherThanExecuted()
    {
        Assert.Equal(AutomaticModerationDecision.UnsupportedAction,
            AutomaticModerationPolicy.Evaluate("purge-everything", true, Array.Empty<string>(), "id", null, Now, Cooldown));
    }
}
