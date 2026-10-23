using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace VtuberBot.App;

public partial class MainWindow
{
    private int obsAutoStartAttempted;

    /// <summary>
    /// Opens OBS on application startup when enabled and reconnects only when a password
    /// has already been saved by the user. A failed connection never blocks VTuberBot startup.
    /// </summary>
    private async Task AutoStartObsIfConfiguredAsync()
    {
        if (Interlocked.Exchange(ref obsAutoStartAttempted, 1) != 0 ||
            !string.Equals(store.GetSetting("obs.autoStart", "true"), "true", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (!IsObsRunning())
            {
                var executable = ResolveObsExecutable();
                if (executable is null)
                {
                    FooterStatus.Text = "OBS no encontrado. Elegí obs64.exe desde la página OBS.";
                    store.Log("OBS", "Inicio automático omitido: no se encontró obs64.exe; VTuberBot continúa funcionando.");
                    return;
                }

                StartObs(executable);
                await Task.Delay(800);
                if (!IsObsRunning())
                {
                    FooterStatus.Text = "Se solicitó iniciar OBS, pero el proceso todavía no aparece. VTuberBot sigue disponible.";
                    store.Log("OBS", "Inicio solicitado; el proceso OBS no se detectó en la primera comprobación.");
                    return;
                }
            }

            var password = LoadObsPassword();
            if (string.IsNullOrEmpty(password))
            {
                FooterStatus.Text = "OBS abierto. Guardá la contraseña WebSocket una vez para conectar automáticamente.";
                store.Log("OBS", "OBS está abierto; no hay contraseña WebSocket guardada, así que no se intentó autenticar.");
                return;
            }

            var host = store.GetSetting("obs.host", "127.0.0.1");
            if (!int.TryParse(store.GetSetting("obs.port", "4455"), out var port) || port is < 1 or > 65535)
                port = 4455;

            Exception? lastError = null;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                if (shutdownStarted || IsConnectedToObs())
                    return;

                try
                {
                    using var attemptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    await obs.ConnectAsync(host, port, password, attemptTimeout.Token);
                    var version = await obs.GetVersionAsync();
                    FooterStatus.Text = "OBS conectado automáticamente · versión " + version;
                    store.Log("OBS", "Conexión automática autenticada; versión " + version + ".");
                    return;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    await Task.Delay(1000);
                }
            }

            FooterStatus.Text = "OBS se abrió, pero no se pudo conectar. Revisá WebSocket y la contraseña en OBS.";
            store.Log("OBS", "Conexión automática agotó los reintentos (" + (lastError?.GetType().Name ?? "sin detalle") + ").");
        }
        catch (Exception exception)
        {
            FooterStatus.Text = "No se pudo iniciar OBS automáticamente; VTuberBot sigue disponible.";
            store.Log("OBS", "Falló el inicio automático (" + exception.GetType().Name + ").");
        }
    }

    private bool IsConnectedToObs() => obs.IsConnected;

    private static bool IsObsRunning()
    {
        foreach (var processName in new[] { "obs64", "obs32", "obs" })
        {
            try
            {
                using var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process is not null)
                    return true;
            }
            catch
            {
                // Process enumeration can fail under restrictive Windows policies.
            }
        }

        return false;
    }

    private string? ResolveObsExecutable()
    {
        var savedPath = store.GetSetting("obs.executablePath", "");
        if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
            return Path.GetFullPath(savedPath);

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "obs-studio", "bin", "64bit", "obs64.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void StartObs(string executable, bool forceWindowVisible = false)
    {
        if (!File.Exists(executable))
            throw new FileNotFoundException("No se encontró el ejecutable de OBS.", executable);

        var lightMode = string.Equals(store.GetSetting("obs.lightMode", "true"), "true", StringComparison.OrdinalIgnoreCase);
        var startMinimized = !forceWindowVisible &&
            string.Equals(store.GetSetting("obs.startMinimized", "true"), "true", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Arguments = ObsLaunchArgumentsPolicy.BuildArguments(lightMode, startMinimized)
        };

        Process.Start(startInfo);
        var startupMode = startMinimized ? "minimizado a la bandeja" : "con ventana normal";
        var pluginMode = lightMode ? "omitiendo plugins de terceros solo en este inicio" : "con los plugins configurados";
        store.Log("OBS", $"Inicio de OBS solicitado {startupMode}, {pluginMode}.");
    }

    private async Task OpenObsFromUiAsync()
    {
        try
        {
            if (IsObsRunning())
            {
                FooterStatus.Text = "OBS ya está abierto.";
                return;
            }

            var executable = ResolveObsExecutable();
            if (executable is null)
            {
                var picker = new OpenFileDialog
                {
                    Title = "Seleccioná el ejecutable de OBS Studio",
                    Filter = "OBS Studio (obs64.exe)|obs64.exe|Ejecutables (*.exe)|*.exe",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (picker.ShowDialog(this) != true)
                    return;

                executable = Path.GetFullPath(picker.FileName);
                store.SetSetting("obs.executablePath", executable);
            }

            StartObs(executable);
            FooterStatus.Text = "Inicio de OBS solicitado; comprobando el proceso y la sesión WebSocket.";
            await Task.Delay(1200);
            if (!IsObsRunning())
            {
                FooterStatus.Text = "Windows no detecta todavía el proceso de OBS. Revisá la ruta del ejecutable.";
                store.Log("OBS", "Inicio solicitado desde la interfaz, pero el proceso no se detectó.");
                return;
            }

            if (!obs.IsConnected && !string.IsNullOrEmpty(LoadObsPassword()))
                await AutoConnectObsFromUiAsync();

            FooterStatus.Text = obs.IsConnected
                ? "OBS abierto y conectado mediante WebSocket autenticado."
                : "Proceso de OBS detectado; la conexión WebSocket todavía no está autenticada.";
        }
        catch (Exception exception)
        {
            FooterStatus.Text = "No se pudo abrir OBS: " + exception.Message;
            store.Log("OBS", "No se pudo abrir OBS (" + exception.GetType().Name + ").");
        }
    }

    private async Task AutoConnectObsFromUiAsync()
    {
        if (obs.IsConnected)
            return;

        var password = LoadObsPassword();
        if (string.IsNullOrEmpty(password))
        {
            FooterStatus.Text = "OBS abierto. Guardá la contraseña WebSocket una vez para habilitar la conexión automática.";
            return;
        }

        var host = store.GetSetting("obs.host", "127.0.0.1");
        if (!int.TryParse(store.GetSetting("obs.port", "4455"), out var port) || port is < 1 or > 65535)
            port = 4455;

        try
        {
            await obs.ConnectAsync(host, port, password);
            var version = await obs.GetVersionAsync();
            FooterStatus.Text = "OBS autenticado · versión " + version;
            store.Log("OBS", "Conexión solicitada desde VTuberBot autenticada; versión " + version + ".");
        }
        catch (Exception exception)
        {
            FooterStatus.Text = "OBS no conectado: " + exception.Message;
            store.Log("OBS", "Conexión solicitada desde VTuberBot fallida (" + exception.GetType().Name + ").");
        }
    }
}
