namespace VtuberBot.App;

/// <summary>Builds only documented and explicitly selected OBS startup options.</summary>
public static class ObsLaunchArgumentsPolicy
{
    public static string BuildArguments(bool skipThirdPartyPlugins, bool minimizeToTray)
    {
        var arguments = new List<string>(2);
        if (skipThirdPartyPlugins)
            arguments.Add("--only-bundled-plugins");
        if (minimizeToTray)
            arguments.Add("--minimize-to-tray");
        return string.Join(" ", arguments);
    }
}
