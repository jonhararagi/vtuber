using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VtuberBot.App;

/// <summary>
/// Local, user-owned art catalog. License information is recorded as metadata, never inferred.
/// Files are copied into the app's avatar data directory and are not uploaded anywhere.
/// </summary>
public sealed record AvatarAssetEntry(
    string Id,
    string FileName,
    string RelativePath,
    string Category,
    string Tags,
    string SourceUrl,
    string LicenseStatus,
    bool IsFavorite,
    DateTime ImportedUtc)
{
    public string DisplayLabel => $"{(IsFavorite ? "★ " : string.Empty)}{FileName} · {Category} · {LicenseStatus}";
}

public sealed class AvatarAssetLibrary
{
    private const long MaxAssetBytes = 25L * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string rootDirectory;
    private readonly string assetsDirectory;
    private readonly string catalogPath;
    private readonly object sync = new();

    public AvatarAssetLibrary(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        assetsDirectory = Path.Combine(this.rootDirectory, "assets");
        catalogPath = Path.Combine(this.rootDirectory, "catalog.json");
    }

    public IReadOnlyList<AvatarAssetEntry> List(string? search = null, string? category = null, bool favoritesOnly = false)
    {
        lock (sync)
        {
            var normalizedSearch = search?.Trim() ?? string.Empty;
            var normalizedCategory = category?.Trim() ?? string.Empty;
            return ReadCatalog()
                .Where(entry => !favoritesOnly || entry.IsFavorite)
                .Where(entry => normalizedCategory.Length == 0 || normalizedCategory.Equals("Todos", StringComparison.OrdinalIgnoreCase)
                    || entry.Category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
                .Where(entry => normalizedSearch.Length == 0
                    || entry.FileName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entry.Category.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entry.Tags.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entry.LicenseStatus.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.IsFavorite)
                .ThenByDescending(entry => entry.ImportedUtc)
                .ToArray();
        }
    }

    public string GetAssetPath(AvatarAssetEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var path = Path.GetFullPath(Path.Combine(rootDirectory, entry.RelativePath));
        var prefix = rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? rootDirectory
            : rootDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La ruta del asset sale de la biblioteca local.");
        return path;
    }

    public AvatarAssetEntry Import(string sourcePath, string category, string? tags = null, string? sourceUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        var source = new FileInfo(sourcePath);
        if (!source.Exists || source.Length is <= 0 or > MaxAssetBytes)
            throw new InvalidDataException("Cada imagen debe existir y ocupar entre 1 byte y 25 MB.");
        var extension = source.Extension.ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            throw new InvalidDataException("Formato no admitido. Usá PNG, JPG, JPEG, BMP o WebP.");
        var cleanCategory = CleanField(category, 80);
        if (cleanCategory.Length == 0)
            throw new InvalidDataException("La categoría no puede quedar vacía.");
        var cleanTags = CleanField(tags, 500);
        var cleanSourceUrl = CleanField(sourceUrl, 1000);
        if (cleanSourceUrl.Length > 0 &&
            (!Uri.TryCreate(cleanSourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new InvalidDataException("La procedencia debe ser una URL HTTP o HTTPS válida.");

        lock (sync)
        {
            Directory.CreateDirectory(assetsDirectory);
            var id = Guid.NewGuid().ToString("N");
            var fileName = CleanFileName(source.Name);
            var storedName = id + extension;
            File.Copy(source.FullName, Path.Combine(assetsDirectory, storedName), overwrite: false);
            var entry = new AvatarAssetEntry(
                id,
                fileName,
                Path.Combine("assets", storedName),
                cleanCategory,
                cleanTags,
                cleanSourceUrl,
                "Pendiente de revisar",
                false,
                DateTime.UtcNow);
            var catalog = ReadCatalog().ToList();
            catalog.Add(entry);
            WriteCatalog(catalog);
            return entry;
        }
    }

    public AvatarAssetEntry UpdateMetadata(string id, string category, string? tags, string? sourceUrl, string licenseStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var cleanCategory = CleanField(category, 80);
        var cleanTags = CleanField(tags, 500);
        var cleanSourceUrl = CleanField(sourceUrl, 1000);
        var cleanLicense = CleanField(licenseStatus, 80);
        if (cleanCategory.Length == 0 || cleanLicense.Length == 0)
            throw new InvalidDataException("La categoría y el estado de licencia son obligatorios.");
        if (cleanSourceUrl.Length > 0 &&
            (!Uri.TryCreate(cleanSourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new InvalidDataException("La procedencia debe ser una URL HTTP o HTTPS válida.");

        lock (sync)
        {
            var catalog = ReadCatalog().ToList();
            var index = catalog.FindIndex(entry => entry.Id.Equals(id, StringComparison.Ordinal));
            if (index < 0)
                throw new KeyNotFoundException("El asset ya no existe en el catálogo.");
            var updated = catalog[index] with
            {
                Category = cleanCategory,
                Tags = cleanTags,
                SourceUrl = cleanSourceUrl,
                LicenseStatus = cleanLicense
            };
            catalog[index] = updated;
            WriteCatalog(catalog);
            return updated;
        }
    }

    public AvatarAssetEntry ToggleFavorite(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (sync)
        {
            var catalog = ReadCatalog().ToList();
            var index = catalog.FindIndex(entry => entry.Id.Equals(id, StringComparison.Ordinal));
            if (index < 0)
                throw new KeyNotFoundException("El asset ya no existe en el catálogo.");
            var updated = catalog[index] with { IsFavorite = !catalog[index].IsFavorite };
            catalog[index] = updated;
            WriteCatalog(catalog);
            return updated;
        }
    }

    private List<AvatarAssetEntry> ReadCatalog()
    {
        if (!File.Exists(catalogPath))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<AvatarAssetEntry>>(File.ReadAllText(catalogPath), JsonOptions) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El catálogo de arte está dañado. Conservá catalog.json antes de repararlo.", exception);
        }
    }

    private void WriteCatalog(IReadOnlyList<AvatarAssetEntry> entries)
    {
        Directory.CreateDirectory(rootDirectory);
        var temporaryPath = catalogPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temporaryPath, catalogPath, overwrite: true);
    }

    private static string CleanField(string? value, int maxLength)
    {
        var clean = (value ?? string.Empty).Trim();
        if (clean.Length > maxLength)
            throw new InvalidDataException($"El campo supera el límite de {maxLength} caracteres.");
        return clean;
    }

    private static string CleanFileName(string value)
    {
        var clean = Path.GetFileName(value.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
            clean = clean.Replace(invalid, '_');
        return clean.Length > 180 ? clean[..180] : clean;
    }
}

public partial class MainWindow
{
    private UIElement BuildAvatarAssetLibraryPanel()
    {
        var panel = Panel("Biblioteca local de arte · buscar, clasificar y reutilizar");
        panel.Margin = new Thickness(0, 0, 0, 14);
        panel.Children.Add(Label(
            "Guardá tus ilustraciones y resultados de PixAI u otras herramientas en una biblioteca local. Cada asset conserva etiquetas, procedencia y estado de licencia; VTuberBot no descarga arte ni deduce permisos automáticamente.",
            12, "#98A4BE"));

        var library = new AvatarAssetLibrary(Path.Combine(avatarDirectory, "art-library", "catalog"));
        var search = new TextBox
        {
            Width = 240, MinHeight = 32, Margin = new Thickness(0, 6, 8, 6),
            Padding = new Thickness(8, 4, 8, 4), Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"), BorderBrush = Brush("#34415E"),
            ToolTip = "Buscar por nombre, categoría, etiqueta o licencia"
        };
        var categoryFilter = new ComboBox
        {
            ItemsSource = new[] { "Todos", "Personaje base", "Fondo", "Objeto / capa", "Idle", "Talking", "Blink", "Happy", "Surprised", "Ropa", "Cabello", "Accesorio", "Otro" },
            SelectedIndex = 0, MinWidth = 160, Margin = new Thickness(0, 6, 8, 6)
        };
        var favoritesOnly = new CheckBox
        {
            Content = "Solo favoritos", Foreground = Brush("#F8FAFC"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
        };
        var toolbar = new WrapPanel();
        toolbar.Children.Add(search);
        toolbar.Children.Add(categoryFilter);
        toolbar.Children.Add(favoritesOnly);
        panel.Children.Add(toolbar);

        var assets = new ListBox
        {
            Height = 155, MinWidth = 260, Margin = new Thickness(0, 4, 8, 8),
            Background = Brush("#101525"), Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#34415E"), DisplayMemberPath = nameof(AvatarAssetEntry.DisplayLabel)
        };
        var preview = new Image
        {
            Width = 112, Height = 155, Stretch = System.Windows.Media.Stretch.Uniform,
            Margin = new Thickness(0, 4, 0, 8), ToolTip = "Vista previa del asset seleccionado"
        };
        var assetBrowser = new Grid();
        assetBrowser.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        assetBrowser.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(assets, 0);
        Grid.SetColumn(preview, 1);
        assetBrowser.Children.Add(assets);
        assetBrowser.Children.Add(preview);
        panel.Children.Add(assetBrowser);

        var details = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var category = new ComboBox
        {
            ItemsSource = new[] { "Personaje base", "Fondo", "Objeto / capa", "Idle", "Talking", "Blink", "Happy", "Surprised", "Ropa", "Cabello", "Accesorio", "Otro" },
            SelectedIndex = 0, Margin = new Thickness(0, 3, 0, 3)
        };
        var tags = new TextBox { MinHeight = 28, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(6, 3, 6, 3) };
        var sourceUrl = new TextBox { MinHeight = 28, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(6, 3, 6, 3) };
        var license = new ComboBox
        {
            ItemsSource = new[] { "Pendiente de revisar", "Uso personal permitido", "Uso comercial permitido", "No redistribuir", "No verificado", "Licencia propia" },
            SelectedIndex = 0, Margin = new Thickness(0, 3, 0, 3)
        };
        void AddDetailRow(int row, string label, UIElement input)
        {
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var caption = Label(label, 12, "#98A4BE");
            caption.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(caption, row);
            Grid.SetColumn(caption, 0);
            Grid.SetRow(input, row);
            Grid.SetColumn(input, 1);
            details.Children.Add(caption);
            details.Children.Add(input);
        }
        AddDetailRow(0, "Categoría", category);
        AddDetailRow(1, "Etiquetas", tags);
        AddDetailRow(2, "Procedencia URL", sourceUrl);
        AddDetailRow(3, "Licencia", license);
        panel.Children.Add(details);

        var status = Label("La biblioteca se guarda en los datos locales de VTuberBot.", 12, "#69E4D0");
        panel.Children.Add(status);
        var assignRole = new ComboBox
        {
            ItemsSource = new[] { "Personaje base", "Fondo", "Objeto / capa", "Idle", "Talking", "Blink", "Happy", "Surprised", "Angry", "LeftEyeClose" }, SelectedIndex = 0,
            MinWidth = 200, Margin = new Thickness(0, 4, 8, 4)
        };
        var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(ActionButton("＋ Importar imágenes", (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Importar assets a la biblioteca local",
                Filter = "Imágenes compatibles|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var selectedCategory = categoryFilter.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(selectedCategory) || selectedCategory == "Todos")
                    selectedCategory = "Otro";
                var count = 0;
                foreach (var path in dialog.FileNames)
                {
                    _ = LoadImage(path);
                    library.Import(path, selectedCategory, tags.Text, sourceUrl.Text);
                    count++;
                }
                RefreshList();
                status.Text = $"Se importaron {count} asset(s). La licencia queda pendiente hasta que la verifiques.";
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo importar el asset: " + exception.GetBaseException().Message,
                    "Biblioteca de arte", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        actions.Children.Add(ActionButton("Actualizar", (_, _) => RefreshList()));
        actions.Children.Add(ActionButton("★ Favorito", (_, _) =>
        {
            if (assets.SelectedItem is not AvatarAssetEntry entry) return;
            try { library.ToggleFavorite(entry.Id); RefreshList(entry.Id); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Biblioteca de arte", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        actions.Children.Add(ActionButton("Guardar metadatos", (_, _) =>
        {
            if (assets.SelectedItem is not AvatarAssetEntry entry) return;
            try
            {
                var updated = library.UpdateMetadata(entry.Id, category.SelectedItem as string ?? "Otro",
                    tags.Text, sourceUrl.Text, license.SelectedItem as string ?? "Pendiente de revisar");
                RefreshList(updated.Id);
                status.Text = "Metadatos guardados. Si no verificaste las condiciones originales, mantené la licencia pendiente.";
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Biblioteca de arte", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        actions.Children.Add(ActionButton("Asignar al avatar", (_, _) =>
        {
            if (assets.SelectedItem is not AvatarAssetEntry entry || assignRole.SelectedItem is not string role) return;
            try
            {
                var path = library.GetAssetPath(entry);
                if (!File.Exists(path)) throw new FileNotFoundException("El archivo de este asset ya no existe.", path);
                ImportArtAssetForRole(role, path);
                status.Text = $"Asset asignado a «{role}». La copia de biblioteca se conserva.";
                Navigate("Avatar Studio");
            }
            catch (Exception exception) { MessageBox.Show(this, exception.GetBaseException().Message, "Biblioteca de arte", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }, true));
        actions.Children.Add(assignRole);
        panel.Children.Add(actions);

        void RefreshList(string? selectedId = null)
        {
            try
            {
                var entries = library.List(search.Text, categoryFilter.SelectedItem as string, favoritesOnly.IsChecked == true);
                assets.ItemsSource = entries;
                if (selectedId is not null)
                    assets.SelectedItem = entries.FirstOrDefault(entry => entry.Id == selectedId);
                status.Text = $"{entries.Count} asset(s) en la vista. Archivos locales; no se suben a ningún servicio.";
            }
            catch (Exception exception)
            {
                assets.ItemsSource = Array.Empty<AvatarAssetEntry>();
                status.Text = "No se pudo leer el catálogo: " + exception.GetBaseException().Message;
            }
        }

        void LoadSelectedMetadata()
        {
            if (assets.SelectedItem is not AvatarAssetEntry entry)
            {
                preview.Source = null;
                return;
            }
            category.SelectedItem = entry.Category;
            if (category.SelectedIndex < 0) category.SelectedIndex = category.Items.Count - 1;
            tags.Text = entry.Tags;
            sourceUrl.Text = entry.SourceUrl;
            license.SelectedItem = entry.LicenseStatus;
            if (license.SelectedIndex < 0) license.SelectedIndex = 0;
            try { preview.Source = LoadImage(library.GetAssetPath(entry)); }
            catch { preview.Source = null; }
        }

        assets.SelectionChanged += (_, _) => LoadSelectedMetadata();
        search.TextChanged += (_, _) => RefreshList();
        categoryFilter.SelectionChanged += (_, _) => RefreshList();
        favoritesOnly.Checked += (_, _) => RefreshList();
        favoritesOnly.Unchecked += (_, _) => RefreshList();
        RefreshList();
        return panel;
    }
}
