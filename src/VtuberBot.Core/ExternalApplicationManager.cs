using System.Diagnostics;

namespace VtuberBot.Core;

public enum ExternalApplicationState
{
    NotConfigured,
    NotFound,
    Configured,
    Running,
    Started,
    Error
}

public sealed record ExternalApplicationStatus(
    ExternalApplicationState State,
    string ExecutablePath,
    string Message,
    bool IsRunning = false);

public interface IExternalApplicationRuntime
{
    bool FileExists(string executablePath);
    bool IsRunning(string executablePath);
    void Start(string executablePath);
}

public sealed class SystemExternalApplicationRuntime : IExternalApplicationRuntime
{
    public bool FileExists(string executablePath) => File.Exists(executablePath);

    public bool IsRunning(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var imagePath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(imagePath) &&
                        string.Equals(Path.GetFullPath(imagePath), Path.GetFullPath(executablePath),
                            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                        return true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Never infer a match when Windows hides the executable identity.
                }
                catch (InvalidOperationException)
                {
                    // The process exited while being inspected.
                }
            }
        }

        return false;
    }

    public void Start(string executablePath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true
        });

        if (process is null)
            throw new InvalidOperationException("Windows no devolvió un proceso al solicitar el inicio.");
        process.Dispose();
    }
}

/// <summary>
/// Reports local process state only. A running process is not evidence of an authenticated
/// or connected integration; connection health must be verified by each integration protocol.
/// </summary>
public sealed class ExternalApplicationManager(IExternalApplicationRuntime runtime)
{
    public ExternalApplicationStatus GetStatus(string? executablePath)
    {
        var path = Normalize(executablePath);
        if (path.Length == 0)
            return new(ExternalApplicationState.NotConfigured, string.Empty, "Elegí el ejecutable para configurar esta aplicación.");

        try
        {
            if (!runtime.FileExists(path))
                return new(ExternalApplicationState.NotFound, path, "El ejecutable configurado no existe en esa ruta.");

            if (runtime.IsRunning(path))
                return new(ExternalApplicationState.Running, path, "El proceso está en ejecución; la conexión con VTuberBot no está verificada.", true);

            return new(ExternalApplicationState.Configured, path, "Ejecutable encontrado; la aplicación no está en ejecución.");
        }
        catch (Exception exception) when (IsOperationalException(exception))
        {
            return new(ExternalApplicationState.Error, path, "No se pudo comprobar el estado: " + exception.Message);
        }
    }

    public ExternalApplicationStatus Launch(string? executablePath)
    {
        var path = Normalize(executablePath);
        if (path.Length == 0)
            return new(ExternalApplicationState.NotConfigured, string.Empty, "Elegí un ejecutable antes de abrir la aplicación.");

        try
        {
            if (!runtime.FileExists(path))
                return new(ExternalApplicationState.NotFound, path, "No se inició: el ejecutable configurado no existe.");

            if (runtime.IsRunning(path))
                return new(ExternalApplicationState.Running, path, "La aplicación ya está en ejecución; no se inició una segunda instancia.", true);

            runtime.Start(path);
            return new(ExternalApplicationState.Started, path, "Windows aceptó la solicitud de inicio. Esto no confirma una conexión con VTuberBot.");
        }
        catch (Exception exception) when (IsOperationalException(exception))
        {
            return new(ExternalApplicationState.Error, path, "No se pudo iniciar la aplicación: " + exception.Message);
        }
    }

    private static string Normalize(string? executablePath) => executablePath?.Trim() ?? string.Empty;

    private static bool IsOperationalException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or
        System.ComponentModel.Win32Exception or ArgumentException or NotSupportedException;
}
