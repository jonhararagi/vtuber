using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private const string PngtuberExecutableSettingKey = "integrations.pngTuberPlus.path";

    private UIElement BuildPngtuberWorkspace()
    {
        var root = new StackPanel();
        root.Children.Add(Label(
            "Avatar avanzado gestionado por PNGTuber Plus. Tu Avatar Studio nativo de VTuberBot permanece intacto y usa perfiles y capas independientes.",
            13, "#98A4BE"));

        var appPanel = Panel("PNGTuber Plus · aplicación externa");
        var pathField = TextField("Ruta al ejecutable de PNGTuber Plus", store.GetSetting(PngtuberExecutableSettingKey));
        pathField.MinWidth = 380;
        appPanel.Children.Add(Label("Ejecutable", 12, "#98A4BE"));
        appPanel.Children.Add(pathField);
        var statusLabel = Label("Estado pendiente de comprobación", 12, "#E7B56A", true);
        appPanel.Children.Add(statusLabel);

        void SavePath()
        {
            var path = pathField.Text.Trim();
            store.SetSetting(PngtuberExecutableSettingKey, path);
            FooterStatus.Text = "Ruta de PNGTuber Plus guardada en SQLite.";
        }

        void RefreshStatus()
        {
            var status = externalApplicationManager.GetStatus(pathField.Text);
            statusLabel.Text = status.State switch
            {
                ExternalApplicationState.NotConfigured => "SIN CONFIGURAR — " + status.Message,
                ExternalApplicationState.NotFound => "EJECUTABLE NO ENCONTRADO — " + status.Message,
                ExternalApplicationState.Configured => "DISPONIBLE — " + status.Message,
                ExternalApplicationState.Running => "EN EJECUCIÓN — " + status.Message,
                ExternalApplicationState.Error => "ERROR — " + status.Message,
                _ => status.State.ToString().ToUpperInvariant() + " — " + status.Message
            };
            var packageWarning = GetPngtuberPackageWarning(pathField.Text);
            if (packageWarning is not null)
                statusLabel.Text += "\\nADVERTENCIA DE PAQUETE — " + packageWarning;
            statusLabel.Foreground = Brush(status.State switch
            {
                ExternalApplicationState.Running => "#8FE6D0",
                ExternalApplicationState.Configured => "#8DE6F8",
                ExternalApplicationState.Error or ExternalApplicationState.NotFound => "#F2A6A6",
                _ => "#E7B56A"
            });
        }

        var appActions = new WrapPanel();
        appActions.Children.Add(ActionButton("Seleccionar ejecutable…", (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar PNGTuber Plus",
                Filter = "Aplicación Windows (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                FileName = File.Exists(pathField.Text.Trim()) ? pathField.Text.Trim() : "PNGTuberPlus.exe"
            };
            if (dialog.ShowDialog(this) != true)
                return;
            pathField.Text = dialog.FileName;
            SavePath();
            RefreshStatus();
        }));
        appActions.Children.Add(ActionButton("Guardar ruta", (_, _) =>
        {
            SavePath();
            RefreshStatus();
        }));
        appActions.Children.Add(ActionButton("Abrir PNGTuber Plus", (_, _) =>
        {
            SavePath();
            var result = externalApplicationManager.Launch(pathField.Text);
            statusLabel.Text = result.State.ToString().ToUpperInvariant() + " — " + result.Message;
            statusLabel.Foreground = Brush(result.State == ExternalApplicationState.Error ||
                result.State == ExternalApplicationState.NotFound ? "#F2A6A6" : "#E7B56A");
            FooterStatus.Text = result.Message;
            store.Log("PNGTuber Plus", result.Message);
            RefreshStatus();
        }, true));
        appActions.Children.Add(ActionButton("Actualizar estado", (_, _) =>
        {
            SavePath();
            RefreshStatus();
        }));
        appPanel.Children.Add(appActions);

        var links = new WrapPanel();
        links.Children.Add(ActionButton("Portal de descarga y guía (independiente)", (_, _) => OpenExternalUrl("https://pngtuber.cc/es/")));
        links.Children.Add(ActionButton("Página del proyecto en GitHub", (_, _) => OpenExternalUrl("https://github.com/kaiakairos/PNGTuber-Plus")));
        
        appPanel.Children.Add(links);
        appPanel.Children.Add(Label(
            "Elegí la carpeta de la versión que instalaste y seleccioná su ejecutable real. Si el paquete oficial incluye un archivo .pck o bibliotecas junto al .exe, mantenelos juntos: no muevas solo el ejecutable. El nombre y la estructura pueden variar según el paquete.",
            12, "#98A4BE"));
        root.Children.Add(appPanel);
        RefreshStatus();

        root.Children.Add(BuildPngtuberSetupWizard(pathField));
        root.Children.Add(BuildPngtuberHotkeyPanel(pathField));

        var nativePanel = Panel("Dos modos de avatar, sin mezclar perfiles");
        nativePanel.Children.Add(Label(
            "• Avatar nativo VTuberBot: se configura en Avatar Studio con perfiles, imagen base, capas y expresiones propias.
• Avatar avanzado PNGTuber Plus: se edita y guarda dentro de PNGTuber Plus; esta página solo conserva la ruta, el progreso de instalación y el registro de atajos.
No se importa ni se convierte automáticamente un archivo .vtuberavatar. Los dos modos pueden coexistir y usan claves de configuración separadas.",
            12, "#98A4BE"));
        nativePanel.Children.Add(ActionButton("Abrir Avatar Studio nativo", (_, _) => Navigate("Avatar Studio")));
        root.Children.Add(nativePanel);
        return root;
    }

    private UIElement BuildPngtuberSetupWizard(TextBox pathField)
    {
        var setup = new PngtuberSetupService(store);
        var panel = Panel("Asistente de configuración · progreso guardado");
        panel.Children.Add(Label(
            "Los estados de ejecutable se comprueban automáticamente. Las casillas de los pasos siguientes solo guardan tu confirmación; VTuberBot no puede inspeccionar el micrófono, las expresiones ni la captura de OBS dentro de PNGTuber Plus.",
            12, "#98A4BE"));

        var steps = new[]
        {
            "1. Localizar PNGTuber Plus",
            "2. Abrir y comprobar la aplicación",
            "3. Crear o importar el avatar",
            "4. Probar micrófono y reacción al habla",
            "5. Preparar expresiones y disfraces",
            "6. Verificar avatar en OBS"
        };
        var stepList = new ListBox
        {
            ItemsSource = steps,
            MinHeight = 118,
            MaxHeight = 150,
            SelectedIndex = setup.Load().CurrentStep,
            Margin = new Thickness(0, 8, 0, 8)
        };
        panel.Children.Add(stepList);

        var detail = Label("", 13, "#F1F5F9", true);
        detail.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(detail);
        var confirmation = new CheckBox
        {
            Content = "Confirmo que completé este paso manualmente",
            Foreground = Brush("#F1F5F9"),
            Margin = new Thickness(0, 8, 0, 8)
        };
        panel.Children.Add(confirmation);

        var controls = new WrapPanel();
        var previous = ActionButton("← Paso anterior", (_, _) =>
        {
            var progress = setup.SetCurrentStep(Math.Max(0, stepList.SelectedIndex - 1));
            stepList.SelectedIndex = progress.CurrentStep;
            RefreshWizard();
        });
        var confirm = ActionButton("Guardar confirmación", (_, _) =>
        {
            if (stepList.SelectedIndex == 0)
            {
                FooterStatus.Text = "La ubicación se comprueba automáticamente al revisar la ruta.";
                return;
            }
            var step = (PngtuberSetupStep)stepList.SelectedIndex;
            var status = externalApplicationManager.GetStatus(pathField.Text);
            if ((step is PngtuberSetupStep.OpenApplication or PngtuberSetupStep.PrepareAvatar or
                PngtuberSetupStep.TestMicrophone or PngtuberSetupStep.PrepareExpressions or PngtuberSetupStep.VerifyObs) &&
                status.State != ExternalApplicationState.Running)
            {
                FooterStatus.Text = "Antes de confirmar, abre PNGTuber Plus y comprueba que su proceso esté en ejecución. El proceso por sí solo no confirma este paso.";
                return;
            }
            setup.Confirm(step, confirmation.IsChecked == true);
            FooterStatus.Text = "Confirmación manual guardada en SQLite.";
            RefreshWizard();
        }, true);
        var next = ActionButton("Siguiente paso →", (_, _) =>
        {
            var status = externalApplicationManager.GetStatus(pathField.Text);
            if (!IsPngtuberStepComplete(stepList.SelectedIndex, setup.Load(), status))
            {
                FooterStatus.Text = "Este paso sigue pendiente. Completalo o vuelve a uno anterior.";
                return;
            }
            var progress = setup.SetCurrentStep(Math.Min(5, stepList.SelectedIndex + 1));
            stepList.SelectedIndex = progress.CurrentStep;
            RefreshWizard();
        });
        controls.Children.Add(previous);
        controls.Children.Add(confirm);
        controls.Children.Add(next);
        panel.Children.Add(controls);

        void RefreshWizard()
        {
            var progress = setup.Load();
            var index = Math.Clamp(stepList.SelectedIndex, 0, 5);
            var status = externalApplicationManager.GetStatus(pathField.Text);
            stepList.SelectedIndex = index;
            confirmation.IsEnabled = index > 0;
            confirmation.IsChecked = IsPngtuberManualStepConfirmed(index, progress);
            previous.IsEnabled = index > 0;
            next.IsEnabled = index < 5 && IsPngtuberStepComplete(index, progress, status);
            confirm.IsEnabled = index > 0;
            detail.Text = index switch
            {
                0 => "COMPROBACIÓN AUTOMÁTICA\n" + status.Message +
                     (status.State is ExternalApplicationState.Configured or ExternalApplicationState.Running
                         ? "\nRuta válida. Ya puedes avanzar."
                         : "\nSelecciona un ejecutable existente antes de avanzar."),
                1 => "CONFIRMACIÓN MANUAL\nAbre PNGTuber Plus con el botón de arriba, comprueba que aparece su ventana y marca la casilla. El proceso detectado por sí solo no completa este paso.",
                2 => "CONFIRMACIÓN MANUAL\nImporta PNG transparentes, coloca las partes del avatar, ajusta posición/escala/capas y guarda el proyecto dentro de PNGTuber Plus. No importes aquí un perfil .vtuberavatar.",
                3 => "CONFIRMACIÓN MANUAL\nSelecciona el micrófono correcto en PNGTuber Plus. Habla y confirma que cambia entre silencio y habla. VTuberBot no puede leer esa selección interna.",
                4 => "CONFIRMACIÓN MANUAL\nPrepara sprites alternativos, expresiones o disfraces, asigna sus teclas dentro de PNGTuber Plus y prueba las combinaciones en la versión instalada.",
                _ => "CONFIRMACIÓN MANUAL\nAñade PNGTuber Plus a OBS con Captura de ventana o el método que funcione en tu equipo. Usa transparencia si la captura la conserva; si no, configura fondo verde y filtro Chroma Key. Verifica la escena final."
            };
        }

        stepList.SelectionChanged += (_, _) =>
        {
            if (stepList.SelectedIndex < 0) return;
            setup.SetCurrentStep(stepList.SelectedIndex);
            RefreshWizard();
        };
        RefreshWizard();
        return panel;
    }

    private static bool IsPngtuberManualStepConfirmed(int step, PngtuberSetupProgress progress) => step switch
    {
        1 => progress.AppOpenedConfirmed,
        2 => progress.AvatarPreparedConfirmed,
        3 => progress.MicrophoneReactionConfirmed,
        4 => progress.ExpressionsPreparedConfirmed,
        5 => progress.ObsCaptureConfirmed,
        _ => false
    };

    private static bool IsPngtuberStepComplete(int step, PngtuberSetupProgress progress, ExternalApplicationStatus status) =>
        PngtuberSetupStepCompletion.IsComplete(step, progress, status);

    private UIElement BuildPngtuberHotkeyPanel(TextBox pathField)
    {
        var registry = new PngtuberHotkeyRegistry(store, new WindowsPngtuberHotkeySender());
        var panel = Panel("Registro de atajos · PNGTuber Plus");
        panel.Children.Add(Label(
            "El código público de referencia usa teclas 1–0 para disfraces y también contempla alternar visibilidad de sprites. Este registro admite una sola tecla por atajo (0–9, A–Z, F1–F12). El envío de prueba es explícito y solo se intenta tras enfocar la ventana identificada de PNGTuber Plus. Windows no confirma que haya cambiado el avatar.",
            12, "#98A4BE"));

        var name = TextField("Nombre, por ejemplo Disfraz 1", "");
        var key = TextField("Pulsa una tecla", "");
        key.IsReadOnly = true;
        key.PreviewKeyDown += (_, e) =>
        {
            var pressed = e.Key == Key.System ? e.SystemKey : e.Key;
            if (pressed is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Escape)
            {
                e.Handled = true;
                if (pressed == Key.Escape) key.Clear();
                return;
            }
            var keyName = ToPngtuberKeyName(pressed);
            if (keyName is not null)
                key.Text = keyName;
            e.Handled = true;
        };
        var description = TextField("Descripción de la acción", "");
        panel.Children.Add(Label("Nombre", 12, "#98A4BE"));
        panel.Children.Add(name);
        panel.Children.Add(Label("Tecla (pulsa dentro del campo para asignarla)", 12, "#98A4BE"));
        panel.Children.Add(key);
        panel.Children.Add(Label("Descripción", 12, "#98A4BE"));
        panel.Children.Add(description);

        var entries = new ListBox { MinHeight = 80, MaxHeight = 140, Margin = new Thickness(0, 8, 0, 8) };
        void RefreshEntries()
        {
            entries.ItemsSource = null;
            entries.ItemsSource = registry.Load();
            entries.DisplayMemberPath = nameof(PngtuberHotkey.Name);
        }
        var resultLabel = Label("Estado del atajo: no probado", 12, "#E7B56A", true);
        panel.Children.Add(entries);
        panel.Children.Add(resultLabel);
        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Guardar atajo", (_, _) =>
        {
            try
            {
                var entry = new PngtuberHotkey(
                    Guid.NewGuid(), name.Text.Trim(), key.Text.Trim(), description.Text.Trim(), "PNGTuber Plus");
                registry.AddOrUpdate(entry);
                RefreshEntries();
                entries.SelectedItem = registry.Load().FirstOrDefault(item => item.Id == entry.Id);
                resultLabel.Text = "Configurado: " + entry.Name + " · tecla " + entry.Key +
                    ". Asigna la misma tecla dentro de PNGTuber Plus.";
                resultLabel.Foreground = Brush("#8DE6F8");
            }
            catch (Exception exception)
            {
                resultLabel.Text = "No se guardó el atajo: " + exception.Message;
                resultLabel.Foreground = Brush("#F2A6A6");
            }
        }, true));
        actions.Children.Add(ActionButton("Probar atajo seleccionado", (_, _) =>
        {
            if (entries.SelectedItem is not PngtuberHotkey entry)
            {
                resultLabel.Text = "Selecciona primero un atajo guardado.";
                return;
            }
            var result = registry.Test(entry.Id, pathField.Text.Trim());
            resultLabel.Text = result.Status switch
            {
                HotkeySendStatus.SentToForegroundApplication => "ENVIADO, NO VERIFICADO — " + result.Message,
                HotkeySendStatus.Cancelled => "CANCELADO — " + result.Message,
                HotkeySendStatus.TargetNotRunning => "APLICACIÓN NO ENCONTRADA — " + result.Message,
                HotkeySendStatus.NotConfigured => "SIN CONFIGURAR — " + result.Message,
                HotkeySendStatus.Unsupported => "NO COMPATIBLE — " + result.Message,
                _ => "ERROR — " + result.Message
            };
            resultLabel.Foreground = Brush(result.WasSent ? "#E7B56A" :
                result.Status == HotkeySendStatus.Error ? "#F2A6A6" : "#98A4BE");
            FooterStatus.Text = result.Message;
            store.Log("PNGTuber Plus / atajos", result.Status + ": " + result.Message);
        }));
        actions.Children.Add(ActionButton("Eliminar seleccionado", (_, _) =>
        {
            if (entries.SelectedItem is not PngtuberHotkey entry) return;
            registry.Remove(entry.Id);
            RefreshEntries();
            resultLabel.Text = "Atajo eliminado del registro local.";
        }));
        panel.Children.Add(actions);
        RefreshEntries();
        return panel;
    }

    private static string? GetPngtuberPackageWarning(string executablePath)
    {
        if (!File.Exists(executablePath))
            return null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            if (string.IsNullOrWhiteSpace(directory))
                return "No se pudo identificar la carpeta de instalación.";
            var hasPack = Directory.EnumerateFiles(directory, "*.pck", SearchOption.TopDirectoryOnly).Any();
            var hasLibrary = Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).Any();
            if (!hasPack || !hasLibrary)
                return "No se detectó " + (!hasPack ? "un archivo .pck" : "una biblioteca .dll") +
                    " junto al ejecutable. Verifica el paquete completo de la versión instalada; algunas distribuciones pueden variar.";
        }
        catch (Exception exception)
        {
            return "No se pudo revisar la carpeta del paquete: " + exception.Message;
        }
        return null;
    }

    private static string? ToPngtuberKeyName(Key key)
    {
        var value = (int)key;
        if (value >= (int)Key.D0 && value <= (int)Key.D9)
            return ((char)('0' + value - (int)Key.D0)).ToString();
        if (value >= (int)Key.A && value <= (int)Key.Z)
            return key.ToString();
        if (value >= (int)Key.F1 && value <= (int)Key.F12)
            return key.ToString();
        return null;
    }

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            FooterStatus.Text = "No se pudo abrir el enlace: " + exception.Message;
            store.Log("PNGTuber Plus", FooterStatus.Text);
        }
    }
}
