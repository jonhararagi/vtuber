namespace VtuberBot.Core;

/// <summary>Resolves persisted AI provider preferences with privacy-preserving local defaults.</summary>
public static class ProviderPreferenceDefaults
{
    /// <summary>Only an explicit saved "remote" value enables a remote provider; missing or unknown values stay local.</summary>
    public static string Resolve(string? savedValue) =>
        string.Equals(savedValue?.Trim(), "remote", StringComparison.OrdinalIgnoreCase) ? "remote" : "local";

    public static bool IsRemote(string? savedValue) => Resolve(savedValue) == "remote";
}
