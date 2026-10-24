using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using VtuberBot.Core;

namespace VtuberBot.App;

public sealed class ChatArchiveWindow : Window
{
    private sealed record ChatArchiveRow(string Platform, string Channel, string At, string Username, string Message);

    private readonly ProductStore settings;
    private readonly ChatArchiveService service;
    private readonly CheckBox enabledToggle;
    private readonly TextBox retentionInput;
    private readonly TextBox queryInput;
    private readonly ComboBox platformPicker;
    private readonly DataGrid resultsGrid;
    private readonly TextBlock summaryLabel;
    private bool changingToggle;

    public ChatArchiveWindow(ProductStore settings, ChatArchiveService service)
    {
        this.settings = settings;
        this.service = service;
        Title = "VTuberBot · Historial de chat";
        Width = 1_160;
        Height = 760;
        MinWidth = 850;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.WhiteSmoke;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var header = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(header, 0);
        root.Children.Add(header);
        header.Children.Add(new TextBlock
        {
            Text = "Historial local de Twitch y Kick",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Solo guarda mensajes públicos desde que lo activás. Los datos permanecen en este equipo; no se envían a IA ni a servicios externos. Activar el historial no activa respuestas ni moderación.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        enabledToggle = new CheckBox
        {
            Content = "Guardar mensajes nuevos de chat",
            IsChecked = settings.GetSetting("chat.archive.enabled", "false") == "true",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        enabledToggle.Checked += (_, _) => ChangeEnabled(true);
        enabledToggle.Unchecked += (_, _) => ChangeEnabled(false);
        header.Children.Add(enabledToggle);

        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        options.Children.Add(new TextBlock { Text = "Conservar durante (días):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        retentionInput = new TextBox
        {
            Text = settings.GetSetting("chat.archive.retentionDays", "30"),
            Width = 58,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        options.Children.Add(retentionInput);
        var saveRetention = new Button { Content = "Guardar y limpiar vencidos", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
        saveRetention.Click += async (_, _) => await SaveRetentionAsync();
        options.Children.Add(saveRetention);
        var deleteAll = new Button { Content = "Borrar todo el historial", Padding = new Thickness(10, 5, 10, 5) };
        deleteAll.Click += async (_, _) => await DeleteAllAsync();
        options.Children.Add(deleteAll);
        header.Children.Add(options);

        var filterRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(filterRow, 1);
        root.Children.Add(filterRow);
        var refreshButton = new Button { Content = "Buscar", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0) };
        refreshButton.Click += async (_, _) => await RefreshRowsAsync();
        DockPanel.SetDock(refreshButton, Dock.Right);
        filterRow.Children.Add(refreshButton);
        var exportButton = new Button { Content = "Exportar CSV", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0) };
        exportButton.Click += async (_, _) => await ExportCsvAsync();
        DockPanel.SetDock(exportButton, Dock.Right);
        filterRow.Children.Add(exportButton);
        platformPicker = new ComboBox
        {
            ItemsSource = new[] { "Todas", "Twitch", "Kick" },
            SelectedIndex = 0,
            Width = 100,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(platformPicker, Dock.Left);
        filterRow.Children.Add(platformPicker);
        queryInput = new TextBox
        {
            ToolTip = "Buscar en usuario, canal o texto del mensaje",
            VerticalContentAlignment = VerticalAlignment.Center,
            MinWidth = 160
        };
        queryInput.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                await RefreshRowsAsync();
            }
        };
        filterRow.Children.Add(queryInput);

        resultsGrid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        resultsGrid.Columns.Add(new DataGridTextColumn { Header = "Plataforma", Binding = new Binding(nameof(ChatArchiveRow.Platform)), Width = 85 });
        resultsGrid.Columns.Add(new DataGridTextColumn { Header = "Fecha local", Binding = new Binding(nameof(ChatArchiveRow.At)), Width = 145 });
        resultsGrid.Columns.Add(new DataGridTextColumn { Header = "Canal", Binding = new Binding(nameof(ChatArchiveRow.Channel)), Width = 110 });
        resultsGrid.Columns.Add(new DataGridTextColumn { Header = "Usuario", Binding = new Binding(nameof(ChatArchiveRow.Username)), Width = 120 });
        resultsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Mensaje",
            Binding = new Binding(nameof(ChatArchiveRow.Message)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        Grid.SetRow(resultsGrid, 2);
        root.Children.Add(resultsGrid);

        summaryLabel = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(summaryLabel, 3);
        root.Children.Add(summaryLabel);

        Closed += (_, _) =>
        {
            if (enabledToggle.IsChecked == true)
                settings.SetSetting("chat.archive.enabled", "true");
        };
        Loaded += async (_, _) => await RefreshRowsAsync();
    }

    private void ChangeEnabled(bool enabled)
    {
        if (changingToggle)
            return;

        if (enabled)
        {
            var choice = MessageBox.Show(this,
                "Desde ahora se guardarán mensajes públicos entrantes de Twitch y Kick en una base SQLite local. El historial puede contener nombres y texto publicados por espectadores. Podés desactivarlo, exportarlo o borrarlo desde esta ventana. ¿Activarlo?",
                "Confirmar almacenamiento local", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (choice != MessageBoxResult.Yes)
            {
                changingToggle = true;
                enabledToggle.IsChecked = false;
                changingToggle = false;
                settings.SetSetting("chat.archive.enabled", "false");
                return;
            }
        }

        settings.SetSetting("chat.archive.enabled", enabled ? "true" : "false");
        summaryLabel.Text = enabled
            ? "Guardado activado para mensajes nuevos. Los mensajes anteriores no se recuperan automáticamente."
            : "Guardado desactivado. El historial existente se conserva hasta que venza la retención o lo borres.";
    }

    private async Task SaveRetentionAsync()
    {
        if (!int.TryParse(retentionInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var days) ||
            days is < ChatArchiveStore.MinimumRetentionDays or > ChatArchiveStore.MaximumRetentionDays)
        {
            MessageBox.Show(this, "Elegí un número entre 1 y 365 días.", "Retención no válida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        settings.SetSetting("chat.archive.retentionDays", days.ToString(CultureInfo.InvariantCulture));
        try
        {
            var deleted = await Task.Run(() => service.Store.Prune(days));
            await RefreshRowsAsync();
            MessageBox.Show(this,
                "Configuración guardada. Se eliminaron " + deleted.ToString(CultureInfo.InvariantCulture) +
                " registro(s) vencidos o por encima del límite de capacidad.",
                "Retención actualizada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo limpiar el historial (" + exception.GetType().Name + ").",
                "Error de almacenamiento", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteAllAsync()
    {
        var choice = MessageBox.Show(this,
            "Se eliminarán todos los mensajes archivados. Esta operación no se puede deshacer y no afecta los comandos, puntos ni la configuración del bot. ¿Continuar?",
            "Borrar historial", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes)
            return;

        try
        {
            var deleted = await Task.Run(service.Store.DeleteAll);
            await RefreshRowsAsync();
            MessageBox.Show(this, "Se eliminaron " + deleted.ToString(CultureInfo.InvariantCulture) + " mensajes del historial.",
                "Historial borrado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo borrar el historial (" + exception.GetType().Name + ").",
                "Error de almacenamiento", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RefreshRowsAsync()
    {
        try
        {
            var query = queryInput.Text.Trim();
            var platform = platformPicker.SelectedItem as string ?? "Todas";
            var rows = await Task.Run(() => service.Store.Search(query, platform, 200));
            resultsGrid.ItemsSource = rows.Select(item => new ChatArchiveRow(
                item.Platform,
                item.Channel,
                item.AtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture),
                item.Username,
                item.Message)).ToArray();
            var stats = await Task.Run(service.Store.GetStats);
            summaryLabel.Text = "Mensajes archivados: " + stats.Total.ToString("N0", CultureInfo.CurrentCulture) +
                " (Twitch " + stats.Twitch.ToString("N0", CultureInfo.CurrentCulture) +
                ", Kick " + stats.Kick.ToString("N0", CultureInfo.CurrentCulture) + "). Mostrando hasta 200 resultados recientes. " +
                "Cola: " + service.PendingWrites.ToString(CultureInfo.CurrentCulture) +
                "; omitidos por sobrecarga: " + service.DroppedWrites.ToString(CultureInfo.CurrentCulture) +
                "; errores de escritura: " + service.FailedWrites.ToString(CultureInfo.CurrentCulture) +
                (string.IsNullOrWhiteSpace(service.LastError) ? string.Empty : "; último error: " + service.LastError) + ".";
        }
        catch (Exception exception)
        {
            summaryLabel.Text = "No se pudo consultar el archivo (" + exception.GetType().Name + ").";
        }
    }

    private async Task ExportCsvAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar historial de chat",
            FileName = "vtuberbot-chat-history-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".csv",
            Filter = "CSV (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var query = queryInput.Text.Trim();
            var platform = platformPicker.SelectedItem as string ?? "Todas";
            var rows = await Task.Run(() => service.Store.Search(query, platform, ChatArchiveStore.MaximumSearchResults));
            var csv = new StringBuilder();
            csv.AppendLine("Plataforma,Canal,Fecha UTC,Usuario,Mensaje,ID mensaje proveedor");
            foreach (var row in rows)
            {
                csv.Append(CsvCell(row.Platform)).Append(',')
                    .Append(CsvCell(row.Channel)).Append(',')
                    .Append(CsvCell(row.AtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))).Append(',')
                    .Append(CsvCell(row.Username)).Append(',')
                    .Append(CsvCell(row.Message)).Append(',')
                    .Append(CsvCell(row.ProviderMessageId ?? string.Empty)).AppendLine();
            }

            await File.WriteAllTextAsync(dialog.FileName, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show(this, "Se exportaron " + rows.Count.ToString(CultureInfo.InvariantCulture) +
                " mensajes (máximo " + ChatArchiveStore.MaximumSearchResults.ToString(CultureInfo.InvariantCulture) +
                " por exportación). El archivo CSV puede contener texto escrito por espectadores; guardalo en un lugar seguro.",
                "Exportación completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo exportar el historial (" + exception.GetType().Name + ").",
                "Error de exportación", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string CsvCell(string value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@')
            safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
