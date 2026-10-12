using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private readonly ExternalApplicationManager externalApplicationManager =
        new(new SystemExternalApplicationRuntime());

    private UIElement BuildIntegrationsPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label(
            "Configurá y abrí herramientas especializadas que ya tengas instaladas. VTuberBot no las descarga ni las instala.",
            13, "#98A4BE"));
        root.Children.Add(BuildExternalApplicationCard(
            "PNGTuber Plus",
            "integrations.pngTuberPlus.path",
            "PNGTuberPlus.exe",
            "Seleccioná el ejecutable de PNGTuber Plus que ya está instalado en tu equipo."));
        root.Children.Add(BuildExternalApplicationCard(
            "Streamer.bot",
            "integrations.streamerBot.path",
            "Streamer.bot.exe",
            "Seleccioná el ejecutable de Streamer.bot. Su proceso activo no implica que exista una conexión API con VTuberBot."));
        var notice = Panel("Alcance de esta primera integración");
        notice.Children.Add(Label(
            "Esta página guarda rutas, abre aplicaciones y comprueba el proceso local. No simula una conexión ni controla acciones internas de terceros. Cada integración futura deberá verificar su propio protocolo y autenticación.",
            12, "#98A4BE"));
        notice.Children.Add(Label(
            "Las aplicaciones externas permanecen abiertas al cerrar VTuberBot. Cerrá cada una desde su propia ventana.",
            12, "#98A4BE"));
        root.Children.Add(notice);
        return root;
    }

    private UIElement BuildExternalApplicationCard(
        string displayName,
        string settingKey,
        string suggestedExecutableName,
        string description)
    {
        var panel = Panel(displayName);
        panel.Padding = new Thickness(16);
        panel.Children.Add(Label(description, 12, "#98A4BE"));

        var pathField = TextField("Ruta completa al ejecutable", store.GetSetting(settingKey));
        pathField.MinWidth = 360;
        pathField.TextWrapping = TextWrapping.NoWrap;
        panel.Children.Add(Label("Ruta del ejecutable", 12, "#98A4BE"));
        panel.Children.Add(pathField);

        var statusLabel = Label("Estado: comprobando…", 12, "#E7B56A", true);
        panel.Children.Add(statusLabel);

        void PersistPath()
        {
            var path = pathField.Text.Trim();
            store.SetSetting(settingKey, path);
            FooterStatus.Text = displayName + ": ruta guardada en SQLite.";
        }

        void DisplayStatus(ExternalApplicationStatus status)
        {
            var stateText = status.State switch
            {
                ExternalApplicationState.NotConfigured => "SIN CONFIGURAR",
                ExternalApplicationState.NotFound => "NO ENCONTRADO",
                ExternalApplicationState.Configured => "CONFIGURADO",
                ExternalApplicationState.Running => "EN EJECUCIÓN",
                ExternalApplicationState.Started => "INICIO SOLICITADO",
                ExternalApplicationState.Error => "ERROR",
                _ => status.State.ToString().ToUpperInvariant()
            };
            statusLabel.Text = "Estado: " + stateText + " — " + status.Message;
            statusLabel.Foreground = Brush(status.State switch
            {
                ExternalApplicationState.Running or ExternalApplicationState.Started => "#8FE6D0",
                ExternalApplicationState.Error or ExternalApplicationState.NotFound => "#F2A6A6",
                ExternalApplicationState.Configured => "#8DE6F8",
                _ => "#E7B56A"
            });
            FooterStatus.Text = displayName + ": " + status.Message;
        }

        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Seleccionar ejecutable…", (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar " + displayName,
                Filter = "Aplicación Windows (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                FileName = File.Exists(pathField.Text.Trim()) ? pathField.Text.Trim() : suggestedExecutableName
            };
            if (dialog.ShowDialog(this) != true)
                return;

            pathField.Text = dialog.FileName;
            PersistPath();
            DisplayStatus(externalApplicationManager.GetStatus(pathField.Text));
        }));

        actions.Children.Add(ActionButton("Guardar ruta", (_, _) =>
        {
            PersistPath();
            DisplayStatus(externalApplicationManager.GetStatus(pathField.Text));
        }));

        actions.Children.Add(ActionButton("Abrir aplicación", (_, _) =>
        {
            PersistPath();
            var status = externalApplicationManager.Launch(pathField.Text);
            DisplayStatus(status);
            if (status.State is ExternalApplicationState.Error or ExternalApplicationState.NotFound)
                store.Log("Integraciones", displayName + ": " + status.Message);
            else if (status.State == ExternalApplicationState.Started)
                store.Log("Integraciones", "Se solicitó abrir " + displayName + "; no se verificó conexión con VTuberBot.");
        }, true));

        actions.Children.Add(ActionButton("Actualizar estado", (_, _) =>
        {
            PersistPath();
            DisplayStatus(externalApplicationManager.GetStatus(pathField.Text));
        }));

        panel.Children.Add(actions);
        DisplayStatus(externalApplicationManager.GetStatus(pathField.Text));
        return panel;
    }
}
