using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarExpressionAvailabilityPolicyTests
{
    [Theory]
    [InlineData("Idle", true, false, true)]
    [InlineData("Idle", false, true, false)]
    [InlineData("Happy", true, false, false)]
    [InlineData("Happy", false, true, true)]
    [InlineData("Blink", true, false, false)]
    [InlineData("Angry", true, false, false)]
    [InlineData("Angry", false, true, true)]
    [InlineData("LeftEyeClose", true, false, false)]
    [InlineData("LeftEyeClose", false, true, true)]
    [InlineData("Unknown", true, true, false)]
    public void ExpressionButtonsOnlyEnableWhenRequiredResourceExists(string state, bool baseSprite, bool expressionSprite, bool expected)
    {
        Assert.Equal(expected, AvatarExpressionAvailabilityPolicy.IsAvailable(state, baseSprite, expressionSprite));
    }
}
