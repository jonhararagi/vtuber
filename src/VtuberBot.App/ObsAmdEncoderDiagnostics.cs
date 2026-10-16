using Microsoft.Win32;
using System.IO;
using System.Security;

namespace VtuberBot.App;

public enum ObsAmdEncoderEvidenceLevel
{
    NoAmdAdapterDetected,
    AmdAdapterOnly,
    AmfPluginAndAmdAdapter,
    AmfMentionedInRecentLogs
}

public sealed record ObsAmdEncoderDiagnostic(
    IReadOnlyList<string> AmdAdapters,
    string? AmfPluginPath,
    bool AmfMentionedInRecentLogs,
    ObsAmdEncoderEvidenceLevel EvidenceLevel)
{
    public string Summary => EvidenceLevel switch
    {
        ObsAmdEncoderEvidenceLevel.NoAmdAdapterDetected =>
            "No se detectó adaptador AMD en las claves de controlador consultadas; no descarta hardware no enumerado.",
        ObsAmdEncoderEvidenceLevel.AmdAdapterOnly =>
            "GPU AMD detectada, pero no se encontraron indicios locales de AMF en el complemento inspeccionado o los logs recientes.",
        ObsAmdEncoderEvidenceLevel.AmfPluginAndAmdAdapter =>
            "Indicio favorable: GPU AMD y archivo del complemento AMF encontrados. La codificación real aún no está confirmada.",
        ObsAmdEncoderEvidenceLevel.AmfMentionedInRecentLogs =>
            "Los logs recientes mencionan AMF/AMD HW. Puede ser una inicialización correcta o un error; hay que revisar el log.",
        _ => "Diagnóstico AMD/AMF sin conclusión."
    };

    public string Details =>
        "Diagnóstico local AMD AMF\n\n" +
        "Adaptadores AMD detectados: " +
        (AmdAdapters.Count == 0 ? "ninguno en las claves revisadas" : string.Join("; ", AmdAdapters)) + "\n" +
        "Archivo del complemento AMF: " + (AmfPluginPath ?? "no encontrado en las rutas inspeccionadas") + "\n" +
        "Referencias AMF/AMD HW en logs recientes: " + (AmfMentionedInRecentLogs ? "sí" : "no") + "\n\n" +
        "Resultado: " + Summary + "\n\n" +
        "Límite importante: OBS WebSocket v5 no publica un inventario de encoders disponibles. Ninguno de estos indicios demuestra que el controlador pueda crear un encoder en este momento. Confirmación real: elegir «AMD HW (H.264) / AMF» en OBS, hacer una grabación local breve, detenerla, reproducir el archivo y comprobar el log de esa sesión sin errores de inicialización del encoder.";

    public string ToLogSummary() =>
        "Diagnóstico AMD/AMF (no concluyente): " + Summary +
        "; GPU=" + (AmdAdapters.Count == 0 ? "no detectada" : string.Join("; ", AmdAdapters)) +
        "; plugin=" + (AmfPluginPath ?? "no encontrado") +
        "; referencias en logs=" + (AmfMentionedInRecentLogs ? "sí" : "no") + ".";

    public static ObsAmdEncoderEvidenceLevel AssessEvidence(
        bool amdAdapterDetected,
        bool amfPluginFound,
        bool amfMentionedInRecentLogs)
    {
        if (amfMentionedInRecentLogs)
            return ObsAmdEncoderEvidenceLevel.AmfMentionedInRecentLogs;
        if (amdAdapterDetected && amfPluginFound)
            return ObsAmdEncoderEvidenceLevel.AmfPluginAndAmdAdapter;
        return amdAdapterDetected
            ? ObsAmdEncoderEvidenceLevel.AmdAdapterOnly
            : ObsAmdEncoderEvidenceLevel.NoAmdAdapterDetected;
    }
}

/// <summary>
/// Collects local hints without claiming encoder availability. Actual AMF support must be verified
/// by creating an encoder and completing a local recording using the installed OBS/AMD driver.
/// </summary>
public static class ObsAmdEncoderDiagnostics
{
    public static ObsAmdEncoderDiagnostic Inspect(
        string? obsExecutablePath,
        string? applicationDataDirectory = null,
        string? commonDataDirectory = null)
    {
        var adapters = FindAmdAdapterNames();
        var pluginPath = FindAmfPlugin(obsExecutablePath, commonDataDirectory);
        var hasAmfLogReferences = RecentLogsMentionAmf(applicationDataDirectory);
        return new ObsAmdEncoderDiagnostic(
            adapters,
            pluginPath,
            hasAmfLogReferences,
            ObsAmdEncoderDiagnostic.AssessEvidence(
                adapters.Count > 0,
                pluginPath is not null,
                hasAmfLogReferences));
    }

    private static IReadOnlyList<string> FindAmdAdapterNames()
    {
        var descriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var videoRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (videoRoot is null)
                return Array.Empty<string>();

            foreach (var adapterId in videoRoot.GetSubKeyNames())
            {
                using var adapter = videoRoot.OpenSubKey(adapterId);
                if (adapter is null)
                    continue;

                foreach (var instanceName in adapter.GetSubKeyNames())
                {
                    using var instance = adapter.OpenSubKey(instanceName);
                    if (instance is null)
                        continue;

                    var description = ReadRegistryText(instance, "DriverDesc");
                    var provider = ReadRegistryText(instance, "ProviderName");
                    var hardwareName = ReadRegistryText(instance, "HardwareInformation.AdapterString");
                    var combined = string.Join(" ", new[] { description, provider, hardwareName }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (combined.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        var display = !string.IsNullOrWhiteSpace(description)
                            ? description
                            : !string.IsNullOrWhiteSpace(hardwareName) ? hardwareName : provider;
                        if (!string.IsNullOrWhiteSpace(display))
                            descriptions.Add(display.Trim());
                    }
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            // Access to adapter registry metadata is best-effort; missing data must not claim a GPU is absent.
        }

        return descriptions.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ReadRegistryText(RegistryKey key, string valueName) =>
        key.GetValue(valueName) switch
        {
            string value => value,
            string[] values => string.Join(" ", values),
            byte[] bytes => System.Text.Encoding.Unicode.GetString(bytes).Trim('\0', ' '),
            var value => value?.ToString() ?? string.Empty
        };

    private static string? FindAmfPlugin(string? obsExecutablePath, string? commonDataDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(obsExecutablePath))
        {
            try
            {
                var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(obsExecutablePath));
                var installRoot = executableDirectory is null
                    ? null
                    : Directory.GetParent(executableDirectory)?.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(installRoot))
                    candidates.Add(Path.Combine(installRoot, "obs-plugins", "64bit", "obs-amd-encoder.dll"));
            }
            catch (ArgumentException)
            {
                // Ignore an invalid or stale configured path and inspect common plugin locations instead.
            }
        }

        var commonData = commonDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonData))
        {
            candidates.Add(Path.Combine(commonData, "obs-studio", "plugins", "obs-amd-encoder", "bin", "64bit", "obs-amd-encoder.dll"));
        }

        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "obs-studio", "obs-plugins", "64bit", "obs-amd-encoder.dll"));

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool RecentLogsMentionAmf(string? applicationDataDirectory)
    {
        var root = applicationDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var logsDirectory = Path.Combine(root, "obs-studio", "logs");
        try
        {
            if (!Directory.Exists(logsDirectory))
                return false;

            foreach (var file in Directory.GetFiles(logsDirectory, "*.txt")
                         .OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > 2 * 1024 * 1024)
                        continue;

                    var content = File.ReadAllText(file);
                    if (content.Contains("AMF", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("AMD HW", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("h264_texture_amf", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("hevc_texture_amf", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("av1_texture_amf", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or SecurityException)
                {
                    // A locked or unreadable log is not treated as positive or negative evidence.
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Diagnostics remain non-blocking if OBS logs are unavailable.
        }

        return false;
    }
}
