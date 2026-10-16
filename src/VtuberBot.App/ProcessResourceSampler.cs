using System.ComponentModel;
using System.Diagnostics;

namespace VtuberBot.App;

public sealed record ProcessResourceSnapshot(
    int ProcessId,
    string ProcessName,
    long WorkingSetBytes,
    double CpuPercent)
{
    public double WorkingSetMiB => WorkingSetBytes / (1024d * 1024d);

    public string ToDisplayString() =>
        $"{ProcessName} (PID {ProcessId}): CPU {CpuPercent:0.0}% del total de CPU disponible · RAM {WorkingSetMiB:0.0} MiB";
}

/// <summary>
/// Samples one Windows process's working set and CPU independently of OBS WebSocket.
/// CPU is normalized to total machine capacity (0-100%), not to a single logical core.
/// </summary>
public static class ProcessResourceSampler
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(650);

    public static double CalculateCpuPercent(TimeSpan cpuTimeDelta, TimeSpan elapsed, int processorCount)
    {
        if (cpuTimeDelta <= TimeSpan.Zero || elapsed <= TimeSpan.Zero || processorCount <= 0)
            return 0d;

        return Math.Clamp(
            cpuTimeDelta.TotalMilliseconds / elapsed.TotalMilliseconds / processorCount * 100d,
            0d,
            100d);
    }

    public static async Task<ProcessResourceSnapshot?> MeasureAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0)
            return null;

        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        using (process)
        {
            try
            {
                var processName = process.ProcessName;
                var initialCpu = process.TotalProcessorTime;
                var startTimestamp = Stopwatch.GetTimestamp();
                await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);

                process.Refresh();
                if (process.HasExited)
                    return null;

                var workingSet = process.WorkingSet64;
                var cpuDelta = process.TotalProcessorTime - initialCpu;
                var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                return new ProcessResourceSnapshot(
                    process.Id,
                    processName,
                    workingSet,
                    CalculateCpuPercent(cpuDelta, elapsed, Environment.ProcessorCount));
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public static int? FindFirstProcessId(params string[] processNames)
    {
        if (processNames is null)
            throw new ArgumentNullException(nameof(processNames));

        foreach (var processName in processNames)
        {
            if (string.IsNullOrWhiteSpace(processName))
                continue;

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception or UnauthorizedAccessException)
            {
                continue;
            }

            try
            {
                foreach (var process in processes)
                {
                    if (!process.HasExited)
                        return process.Id;
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception or UnauthorizedAccessException)
            {
                // Continue to the next candidate process name.
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        return null;
    }
}
