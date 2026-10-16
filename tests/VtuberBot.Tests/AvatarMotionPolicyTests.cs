using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarMotionPolicyTests
{
    [Fact]
    public void ShakeStrengthIsBoundedAndRejectsNonFiniteValues()
    {
        Assert.Equal(0, AvatarMotionPolicy.NormalizeShakeStrength(-2));
        Assert.Equal(8, AvatarMotionPolicy.NormalizeShakeStrength(8));
        Assert.Equal(18, AvatarMotionPolicy.NormalizeShakeStrength(50));
        Assert.Equal(0, AvatarMotionPolicy.NormalizeShakeStrength(double.NaN));
        Assert.Equal(0, AvatarMotionPolicy.NormalizeShakeStrength(double.PositiveInfinity));
    }

    [Fact]
    public void ShakeOffsetUsesRequestedIntensityAndCanBeDisabledWithZero()
    {
        Assert.Equal(0, AvatarMotionPolicy.GetShakeOffset(0, 8), precision: 8);
        Assert.True(AvatarMotionPolicy.GetShakeOffset(Math.PI / 5.2, 8) > 7.9);
        Assert.Equal(0, AvatarMotionPolicy.GetShakeOffset(1, 0));
        Assert.Equal(0, AvatarMotionPolicy.GetShakeOffset(double.NaN, 8));
    }

    [Fact]
    public void TalkingBounceCanBeDisabledAndIsBounded()
    {
        Assert.Equal(0, AvatarMotionPolicy.GetTalkingBounceOffset(1, enabled: false));
        Assert.InRange(Math.Abs(AvatarMotionPolicy.GetTalkingBounceOffset(3, enabled: true, strength: 40)), 0, 12);
        Assert.Equal(0, AvatarMotionPolicy.GetTalkingBounceOffset(double.NaN, enabled: true));
    }
}
