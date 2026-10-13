namespace VtuberBot.Core;

/// <summary>Truthful availability gate for native PNG avatar expression controls.</summary>
public static class AvatarExpressionAvailabilityPolicy
{
    public static bool IsAvailable(string state, bool baseSpriteAvailable, bool expressionSpriteAvailable)
    {
        if (string.Equals(state, "Idle", StringComparison.Ordinal))
            return baseSpriteAvailable;
        if (state is not ("Talking" or "Blink" or "Happy" or "Surprised"))
            return false;
        return expressionSpriteAvailable;
    }
}
