namespace VtuberBot.Core;

/// <summary>Truthful availability gate for native PNG avatar expression controls.</summary>
public static class AvatarExpressionAvailabilityPolicy
{
    public static bool IsAvailable(string state, bool baseSpriteAvailable, bool expressionSpriteAvailable)
    {
        if (string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase))
            return baseSpriteAvailable;
        if (state is null || !new[] { "Talking", "Blink", "Happy", "Surprised", "Angry", "LeftEyeClose" }
                .Contains(state, StringComparer.OrdinalIgnoreCase))
            return false;
        return expressionSpriteAvailable;
    }
}
