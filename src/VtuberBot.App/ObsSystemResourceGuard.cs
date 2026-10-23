using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace VtuberBot.App;

public enum ObsSystemResourceRisk
{
    Normal,
    Low,
    Critical
}

public enum ObsSystemCpuRisk
{
    Unavailable,
    Normal,
    High,
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

    public static ObsSystemCpuRisk ClassifySystemCpuUsage(double usagePercent)
    {
        if (!double.IsFinite(usagePercent) || usagePercent < 0 || usagePercent > 100)
            return ObsSystemCpuRisk.Unavailable;
        if (usagePercent >= 95)
            return ObsSystemCpuRisk.Critical;
        return usagePercent >= 80 ? ObsSystemCpuRisk.High : ObsSystemCpuRisk.Normal;
    }

    /// <summary>
    /// Samples whole-system CPU utilization for half a second. This is a preflight warning/block,
    /// not a CPU quota; Windows and external processes can change their load immediately afterward.
    /// </summary>
    public static async Task<double?> TryReadSystemCpuUsagePercentAsync(CancellationToken cancellationToken = default)
    {
        if (!TryReadSystemCpuTimes(out var idleBefore, out var kernelBefore, out var userBefore))
            return null;

        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

        if (!TryReadSystemCpuTimes(out var idleAfter, out var kernelAfter, out var userAfter) ||
            idleAfter < idleBefore || kernelAfter < kernelBefore || userAfter < userBefore)
            return null;

        return CalculateSystemCpuUsagePercent(
            idleAfter - idleBefore,
            kernelAfter - kernelBefore,
            userAfter - userBefore);
    }

    /// <summary>Pure calculation for the Windows GetSystemTimes counters, useful for regression tests.</summary>
    public static double? CalculateSystemCpuUsagePercent(
        ulong idleDelta,
        ulong kernelDelta,
        ulong userDelta)
    {
        if (kernelDelta > ulong.MaxValue - userDelta)
            return null;

        var totalDelta = kernelDelta + userDelta;
        if (totalDelta == 0 || idleDelta > totalDelta)
            return null;

        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0d, 100d);
    }

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

    private static bool TryReadSystemCpuTimes(out ulong idle, out ulong kernel, out ulong user)
    {
        idle = kernel = user = 0;
        try
        {
            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                return false;

            idle = ToUInt64(idleTime);
            kernel = ToUInt64(kernelTime);
            user = ToUInt64(userTime);
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static ulong ToUInt64(SystemFileTime value) =>
        ((ulong)value.HighDateTime << 32) | value.LowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetSystemTimes", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out SystemFileTime idleTime,
        out SystemFileTime kernelTime,
        out SystemFileTime userTime);

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