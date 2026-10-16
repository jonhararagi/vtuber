using System;

namespace VtuberBot.Core;

public sealed record VoicevoxEngineLaunchEndpoint(string Host, int Port, string BaseUrl);

/// <summary>Allows launching the local VOICEVOX Engine only with an HTTP loopback endpoint.</summary>
public static class VoicevoxEngineLaunchPolicy
{
    /// <summary>
    /// Keeps the local TTS Engine conservative on low-memory streaming PCs while allowing
    /// the user to trade synthesis speed for fewer occupied CPU threads.
    /// </summary>
    public static int NormalizeCpuThreads(int value) => Math.Clamp(value, 1, 6);

    public static bool TryParseBaseUrl(string? value, out VoicevoxEngineLaunchEndpoint endpoint)
    {
        endpoint = new VoicevoxEngineLaunchEndpoint("127.0.0.1", 50021, "http://127.0.0.1:50021");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsLoopback ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.Port is < 1 or > 65535)
        {
            return false;
        }

        // Always bind the launched server to IPv4 loopback. Never expose the engine on a LAN interface.
        var port = uri.Port;
        endpoint = new VoicevoxEngineLaunchEndpoint(
            "127.0.0.1",
            port,
            "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }
}
