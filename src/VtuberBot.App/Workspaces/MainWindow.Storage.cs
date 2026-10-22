using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildStorageManagerPanel()
    {
        var panel = Panel("Administrador de almacenamiento");
        panel.Margin = new Thickness(0, 16, 0, 0);
        panel.Children.Add(Label(
            "Este panel mide el directorio local y prepara una lista de posibles residuos. Analizar no borra nada. Los elementos propuestos requieren selección y confirmación; la base activa, sus archivos auxiliares, las copias de seguridad y las credenciales protegidas nunca se ofrecen para borrar. La limpieza de imágenes es conservadora y puede dejar archivos sin referencia dudosa para evitar eliminar arte original.",
            12, "#98A4BE"));

        var limitBox = new TextBox
        {
            Text = store.GetSetting("storage.warningLimitMb", "4096"),
            Width = 110,
            MinHeight = 30,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#34415E"),
            ToolTip = "Límite de aviso de almacenamiento en MiB (256 a 65536)"
        };
        var summary = Label("Todavía no se analizó el almacenamiento.", 12, "#F8FAFC");
        var categories = new ListBox { Height = 115, Margin = new Thickness(0, 0, 0, 10) };
        var candidateList = new ListBox
        {
            Height = 190,
            SelectionMode = SelectionMode.Extended,
            DisplayMemberPath = nameof(StorageCleanupCandidate.DisplayLabel),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var candidateCount = Label("No hay vista previa de limpieza todavía.", 11, "#98A4BE");
        StorageInventorySnapshot? lastInventory = null;
        var busy = false;

        List<string> ReadReferencedSoundPathsSafely()
        {
            var json = store.GetSetting("audio.sound.library", "[]");
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("La biblioteca de sonidos no contiene una lista.");

                var referenced = new List<string>();
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    foreach (var property in item.EnumerateObject())
                    {
                        if (property.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) &&
                            property.Value.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        {
                            referenced.Add(property.Value.GetString()!);
                            break;
                        }
                    }
                }
                return referenced;
            }
            catch (JsonException)
            {
                // If the library metadata is damaged, protect every sound file rather than
                // guessing which imports are unused.
                var soundDirectory = Path.Combine(dataDirectory, "sounds");
                if (!Directory.Exists(soundDirectory)) return [];
                try { return Directory.EnumerateFiles(soundDirectory, "*", SearchOption.AllDirectories).ToList(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { return []; }
            }
        }

        async Task<StorageInventorySnapshot> ScanAsync()
        {
            // The service reads files on a worker thread; snapshots and UI updates remain on the dispatcher.
            var avatarPaths = store.GetReferencedAvatarAssetPaths();
            var soundPaths = ReadReferencedSoundPathsSafely();
            var root = dataDirectory;
            var databasePath = store.DatabasePath;
            return await Task.Run(() => StorageMaintenanceService.Scan(root, databasePath, avatarPaths, soundPaths));
        }

        void DisplaySnapshot(StorageInventorySnapshot snapshot)
        {
            lastInventory = snapshot;
            categories.ItemsSource = snapshot.Categories.Select(category =>
                $"{category.Name} · {category.FileCount} archivos · {StorageMaintenanceService.FormatBytes(category.SizeBytes)}").ToArray();
            candidateList.ItemsSource = snapshot.CleanupCandidates;
            var quotaMbText = limitBox.Text.Trim();
            var quotaMb = long.TryParse(quotaMbText, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedLimit) &&
                parsedLimit is >= 256 and <= 65536 ? parsedLimit : 4096;
            var quotaBytes = quotaMb * 1024L * 1024L;
            var quotaStatus = snapshot.TotalBytes > quotaBytes
                ? $"AVISO: supera el umbral de {quotaMb} MiB."
                : $"Por debajo del umbral de aviso de {quotaMb} MiB.";
            summary.Text =
                $"Carpeta analizada: {snapshot.RootPath}\n" +
                $"Uso total: {StorageMaintenanceService.FormatBytes(snapshot.TotalBytes)} en {snapshot.FileCount} archivos. {quotaStatus}\n" +
                $"Archivos propuestos para limpieza: {snapshot.CleanupCandidates.Count}; espacio candidato (estimado): " +
                StorageMaintenanceService.FormatBytes(snapshot.CleanupCandidates.Aggregate(0L, (total, item) => total + item.SizeBytes)) + ".";
            candidateCount.Text = snapshot.CleanupCandidates.Count == 0
                ? "No se encontraron residuos que cumplan las reglas seguras actuales."
                : "Revisá las rutas y motivos antes de seleccionar archivos. Los elementos dudosos se conservan.";
        }

        async Task RefreshStorageInventoryAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                summary.Text = "Analizando archivos y tamaños… No se está borrando nada.";
                var snapshot = await ScanAsync();
                DisplaySnapshot(snapshot);
                FooterStatus.Text = "Análisis de almacenamiento completado; no se modificaron archivos.";
            }
            catch (Exception exception)
            {
                summary.Text = "No se pudo completar el análisis. No se eliminaron archivos.";
                FooterStatus.Text = "Error al analizar almacenamiento: " + exception.GetBaseException().Message;
            }
            finally { busy = false; }
        }

        var thresholdActions = new WrapPanel();
        thresholdActions.Children.Add(limitBox);
        thresholdActions.Children.Add(ActionButton("Guardar umbral de aviso", (_, _) =>
        {
            if (!long.TryParse(limitBox.Text.Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var mb) || mb is < 256 or > 65536)
            {
                MessageBox.Show(this, "Ingresá un límite de aviso entre 256 y 65536 MiB. Es un aviso, no una cuota que bloquee la aplicación.",
                    "Límite de almacenamiento", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            store.SetSetting("storage.warningLimitMb", mb.ToString(System.Globalization.CultureInfo.InvariantCulture));
            FooterStatus.Text = $"Umbral de aviso guardado: {mb} MiB. No bloquea operaciones ni borra datos automáticamente.";
            if (lastInventory is not null) DisplaySnapshot(lastInventory);
        }, true));
        panel.Children.Add(Label("Umbral para avisar de uso elevado (MiB, no es una cuota obligatoria)", 12, "#98A4BE"));
        panel.Children.Add(thresholdActions);
        panel.Children.Add(summary);
        panel.Children.Add(Label("Uso por categoría", 12, "#98A4BE", true));
        panel.Children.Add(categories);
        panel.Children.Add(Label("Posibles residuos · seleccioná solo los que quieras eliminar", 12, "#98A4BE", true));
        panel.Children.Add(candidateList);
        panel.Children.Add(candidateCount);

        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Analizar almacenamiento", async (_, _) => await RefreshStorageInventoryAsync(), true));
        actions.Children.Add(ActionButton("Seleccionar todos", (_, _) => candidateList.SelectAll()));
        actions.Children.Add(ActionButton("Eliminar seleccionados…", async (_, _) =>
        {
            if (busy) return;
            var originallySelected = candidateList.SelectedItems.Cast<StorageCleanupCandidate>().ToArray();
            if (originallySelected.Length == 0)
            {
                MessageBox.Show(this, "Primero analizá el almacenamiento y seleccioná al menos un archivo de la lista.",
                    "Limpieza de almacenamiento", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            busy = true;
            try
            {
                var fresh = await ScanAsync();
                var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var freshByPath = fresh.CleanupCandidates.ToDictionary(item => item.FullPath, comparer);
                var stillEligible = originallySelected
                    .Where(item => freshByPath.TryGetValue(item.FullPath, out var current) &&
                                   current.SizeBytes == item.SizeBytes &&
                                   current.LastWriteTimeUtc == item.LastWriteTimeUtc)
                    .Select(item => freshByPath[item.FullPath])
                    .ToArray();

                if (stillEligible.Length == 0)
                {
                    DisplaySnapshot(fresh);
                    MessageBox.Show(this, "Los archivos seleccionados cambiaron o ya no cumplen las reglas. Volvé a revisar la nueva vista previa.",
                        "Vista previa actualizada", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var estimated = stillEligible.Aggregate(0L, (total, item) => total + item.SizeBytes);
                var paths = string.Join(Environment.NewLine, stillEligible.Take(12)
                    .Select(item => $"• {item.RelativePath} — {StorageMaintenanceService.FormatBytes(item.SizeBytes)}"));
                if (stillEligible.Length > 12)
                    paths += Environment.NewLine + $"• y {stillEligible.Length - 12} archivos más.";
                var confirm = MessageBox.Show(this,
                    $"Se eliminarán {stillEligible.Length} archivos seleccionados (estimación: {StorageMaintenanceService.FormatBytes(estimated)}).\n\n{paths}\n\n" +
                    "La base activa y sus archivos auxiliares, copias de seguridad, credenciales, enlaces de avatar detectados y archivos fuera de la carpeta local están protegidos. " +
                    "No se borrará automáticamente el contenido de una carpeta completa. ¿Confirmás esta eliminación?",
                    "Confirmar limpieza de almacenamiento", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    DisplaySnapshot(fresh);
                    return;
                }

                var result = await Task.Run(() => StorageMaintenanceService.DeleteSelected(fresh, stillEligible, store.DatabasePath));
                store.Log("Almacenamiento", $"Limpieza confirmada: {result.DeletedCount} archivos, {StorageMaintenanceService.FormatBytes(result.BytesFreed)} liberados estimados.");
                DisplaySnapshot(await ScanAsync());
                FooterStatus.Text = $"Limpieza terminada: {result.DeletedCount} archivos eliminados; {StorageMaintenanceService.FormatBytes(result.BytesFreed)} recuperados. Omitidos: {result.SkippedRelativePaths.Count}.";
                MessageBox.Show(this,
                    $"Archivos eliminados: {result.DeletedCount}\nEspacio recuperado estimado: {StorageMaintenanceService.FormatBytes(result.BytesFreed)}\nOmitidos por cambios, protección o permisos: {result.SkippedRelativePaths.Count}.",
                    "Limpieza completada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo completar la limpieza: " + exception.GetBaseException().Message;
                MessageBox.Show(this, "No se completó toda la limpieza. Los archivos que no pudieron verificarse se conservan.\n\n" +
                    exception.GetBaseException().Message, "Limpieza de almacenamiento", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { busy = false; }
        }));

        actions.Children.Add(ActionButton("Compactar SQLite…", async (_, _) =>
        {
            if (busy) return;
            if (twitchChatDesired || twitchEventSubDesired || twitch.IsConnected || eventSub.IsConnected)
            {
                MessageBox.Show(this, "Desconectá primero Twitch chat y EventSub para reducir escrituras durante la compactación.",
                    "Desconectar Twitch primero", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (Volatile.Read(ref automationProcessing) != 0)
            {
                MessageBox.Show(this, "Hay una automatización en proceso. Reintentá cuando termine.",
                    "Automatización en curso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                "SQLite creará una copia verificada en la carpeta backups y ejecutará VACUUM. Los ajustes, comandos, cuentas, reglas, perfiles y referencias se conservan. " +
                "El archivo puede no reducirse si no hay páginas libres. ¿Crear la copia y compactar ahora?",
                "Compactar la base SQLite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes) return;

            busy = true;
            automationTimer.Stop();
            var backupPath = Path.Combine(dataDirectory, "backups",
                "VTuberBot-before-vacuum-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
                Guid.NewGuid().ToString("N")[..8] + ".vtuberbot.db");
            try
            {
                summary.Text = "Creando una copia verificada y compactando SQLite…";
                var result = await Task.Run(() => store.CompactDatabase(backupPath));
                store.Log("Almacenamiento", "SQLite compactada tras copia verificada: " + result.BackupPath);
                FooterStatus.Text = $"Compactación terminada. Tamaño: {StorageMaintenanceService.FormatBytes(result.SizeBeforeBytes)} → {StorageMaintenanceService.FormatBytes(result.SizeAfterBytes)}.";
                MessageBox.Show(this,
                    "Compactación terminada.\n\nAntes: " + StorageMaintenanceService.FormatBytes(result.SizeBeforeBytes) +
                    "\nDespués: " + StorageMaintenanceService.FormatBytes(result.SizeAfterBytes) +
                    "\nCopia verificada conservada en:\n" + result.BackupPath,
                    "Compactación de SQLite", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se completó la compactación de SQLite.";
                MessageBox.Show(this,
                    "SQLite no confirmó la compactación. La copia de seguridad, si llegó a completarse, queda conservada. Revisá el mensaje antes de reintentarlo.\n\n" +
                    exception.GetBaseException().Message, "Compactación no completada", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                automationTimer.Start();
                busy = false;
            }
            await RefreshStorageInventoryAsync();
        }));
        panel.Children.Add(actions);

        _ = RefreshStorageInventoryAsync();
        return panel;
    }
}
