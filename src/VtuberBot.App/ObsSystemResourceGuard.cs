using System.Net;
using System.Runtime.InteropServices;

namespace VtuberBot.App;

public enum ObsSystemResourceRisk
{
    Normal,
    Low,
    Critical
}

public readonly record struct ObsSystemMemorySnapshot(
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes)
{
    public ulong TotalPhysicalMiB => TotalPhysicalBytes / (1024UL * 1024UL);
    public ulong AvailablePhysicalMiB => AvailablePhysicalBytes / (1024UL * 1024UL);
}

/// <summary>
/// Conservative preflight for the beginner OBS flow. It never kills processes and never
/// claims to enforce a whole-system CPU/GPU/RAM limit.
/// </summary>
public static class ObsSystemResourceGuard
{
    public const ulong CriticalAvailableMemoryBytes = 1024UL * 1024UL * 1024UL;
    public const ulong LowAvailableMemoryBytes = 1536UL * 1024UL * 1024UL;

    public static ObsSystemResourceRisk ClassifyAvailableMemory(ulong availableBytes)
    {
        if (availableBytes < CriticalAvailableMemoryBytes)
            return ObsSystemResourceRisk.Critical;

        return availableBytes < LowAvailableMemoryBytes
            ? ObsSystemResourceRisk.Low
            : ObsSystemResourceRisk.Normal;
    }

    public static bool CanStartNewObsInstance(ulong availableBytes) =>
        ClassifyAvailableMemory(availableBytes) != ObsSystemResourceRisk.Critical;

    public static bool CanApplyLightProfile(bool isConnected, bool isStreaming, bool isRecording) =>
        isConnected && !isStreaming && !isRecording;

    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var normalized = host.Trim().Trim('[', ']');
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(normalized, out var address) && IPAddress.IsLoopback(address));
    }

    public static bool TryReadSystemMemory(out ObsSystemMemorySnapshot snapshot)
    {
        snapshot = default;
        try
        {
            var status = new MemoryStatusEx
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };
            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysicalBytes == 0)
                return false;

            snapshot = new ObsSystemMemorySnapshot(status.TotalPhysicalBytes, status.AvailablePhysicalBytes);
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}