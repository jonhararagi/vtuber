using System.IO;
using System.Text.Json;

namespace VtuberBot.Core;

public sealed record StorageCategoryUsage(string Name, int FileCount, long SizeBytes);

public sealed record StorageCleanupCandidate(
    string FullPath,
    string RelativePath,
    string Category,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    string Reason)
{
    public string DisplayLabel => $"{RelativePath}  ·  {StorageMaintenanceService.FormatBytes(SizeBytes)}  ·  {Reason}";
}

public sealed record StorageInventorySnapshot(
    string RootPath,
    long TotalBytes,
    int FileCount,
    IReadOnlyList<StorageCategoryUsage> Categories,
    IReadOnlyList<StorageCleanupCandidate> CleanupCandidates);

public sealed record StorageCleanupResult(
    int DeletedCount,
    long BytesFreed,
    IReadOnlyList<string> DeletedRelativePaths,
    IReadOnlyList<string> SkippedRelativePaths);

/// <summary>
/// Conservative disk inventory and cleanup policy. It only offers individual files for
/// cleanup; active databases, sidecars, backups, encrypted secrets and reparse points are protected.
/// Nothing is deleted during Scan.
/// </summary>
public static class StorageMaintenanceService
{
    private const long MetadataFileLimitBytes = 2L * 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".wav", ".mp3" };
    private static readonly HashSet<string> MetadataExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".json", ".txt", ".md" };
    private static readonly HashSet<string> DatabaseExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".db", ".sqlite", ".sqlite3" };

    public static StorageInventorySnapshot Scan(
        string dataDirectory,
        string databasePath,
        IEnumerable<string>? referencedAvatarPaths = null,
        IEnumerable<string>? referencedSoundPaths = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var root = Path.GetFullPath(dataDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("No existe la carpeta local de datos: " + root);

        var database = Path.GetFullPath(databasePath);
        var now = (nowUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var files = EnumerateFilesWithoutFollowingLinks(root).ToArray();
        var referenced = new HashSet<string>(PathComparer);

        foreach (var path in referencedAvatarPaths ?? [])
            AddReference(referenced, path, root);
        foreach (var path in referencedSoundPaths ?? [])
            AddReference(referenced, path, root);

        var metadataTexts = new List<string>();
        foreach (var path in files)
        {
            if (IsProtectedLocation(root, path, database) || !MetadataExtensions.Contains(Path.GetExtension(path)))
                continue;

            try
            {
                var info = new FileInfo(path);
                if (info.Length is <= 0 or > MetadataFileLimitBytes)
                    continue;

                var text = File.ReadAllText(path);
                if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(text);
                        AddJsonReferences(document.RootElement, Path.GetDirectoryName(path)!, root, referenced);
                    }
                    catch (JsonException)
                    {
                        // Damaged metadata is not repaired or deleted. Treat unproven images conservatively below.
                        metadataTexts.Add(text);
                    }
                }
                else if (IsUnderNamedDirectory(root, path, "avatars") ||
                         IsUnderNamedDirectory(root, path, "waifuassets"))
                {
                    metadataTexts.Add(text);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // A file that could not be inspected does not prove that an asset is orphaned.
            }
        }

        var categorySizes = new Dictionary<string, (int Count, long Bytes)>(StringComparer.Ordinal)
        {
            ["Base SQLite activa"] = (0, 0),
            ["Imágenes y recursos de avatar"] = (0, 0),
            ["Sonidos"] = (0, 0),
            ["Registros"] = (0, 0),
            ["Caché y temporales"] = (0, 0),
            ["Credenciales protegidas"] = (0, 0),
            ["Copias de seguridad protegidas"] = (0, 0),
            ["Otros"] = (0, 0)
        };

        var candidates = new List<StorageCleanupCandidate>();
        long totalBytes = 0;
        var validFileCount = 0;

        foreach (var path in files)
        {
            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            var size = Math.Max(0, info.Length);
            var relative = NormalizeRelativePath(Path.GetRelativePath(root, path));
            var category = GetCategory(root, path, relative, database);
            var existing = categorySizes[category];
            categorySizes[category] = (existing.Count + 1, existing.Bytes + size);
            totalBytes += size;
            validFileCount++;

            var candidate = GetCleanupCandidate(
                root, path, relative, category, size, info.LastWriteTimeUtc,
                database, referenced, metadataTexts, now);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        var categories = categorySizes
            .Select(pair => new StorageCategoryUsage(pair.Key, pair.Value.Count, pair.Value.Bytes))
            .Where(item => item.FileCount > 0)
            .OrderBy(item => CategoryOrder(item.Name))
            .ToArray();

        return new StorageInventorySnapshot(
            root,
            totalBytes,
            validFileCount,
            categories,
            candidates.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>
    /// Deletes only candidates belonging to the supplied, previously displayed inventory.
    /// It checks that file size/timestamp are unchanged and re-protects the database and secrets.
    /// </summary>
    public static StorageCleanupResult DeleteSelected(
        StorageInventorySnapshot inventory,
        IEnumerable<StorageCleanupCandidate> selectedCandidates,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(selectedCandidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var database = Path.GetFullPath(databasePath);
        var approved = inventory.CleanupCandidates.ToDictionary(item => item.FullPath, PathComparer);
        var deleted = new List<string>();
        var skipped = new List<string>();
        long bytesFreed = 0;

        foreach (var selected in selectedCandidates)
        {
            if (!approved.TryGetValue(selected.FullPath, out var candidate) || candidate != selected)
            {
                skipped.Add(selected.RelativePath);
                continue;
            }

            try
            {
                var path = Path.GetFullPath(candidate.FullPath);
                if (!IsWithinRoot(inventory.RootPath, path) ||
                    IsProtectedLocation(inventory.RootPath, path, database) ||
                    !File.Exists(path))
                {
                    skipped.Add(candidate.RelativePath);
                    continue;
                }

                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skipped.Add(candidate.RelativePath);
                    continue;
                }

                var info = new FileInfo(path);
                if (info.Length != candidate.SizeBytes || info.LastWriteTimeUtc != candidate.LastWriteTimeUtc)
                {
                    // The file changed after preview. Require a fresh inventory before deleting it.
                    skipped.Add(candidate.RelativePath);
                    continue;
                }

                File.Delete(path);
                deleted.Add(candidate.RelativePath);
                bytesFreed += candidate.SizeBytes;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                skipped.Add(candidate.RelativePath);
            }
        }

        return new StorageCleanupResult(deleted.Count, bytesFreed, deleted, skipped);
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static StorageCleanupCandidate? GetCleanupCandidate(
        string root,
        string path,
        string relative,
        string category,
        long size,
        DateTime lastWriteUtc,
        string database,
        HashSet<string> referenced,
        IReadOnlyList<string> metadataTexts,
        DateTime nowUtc)
    {
        if (IsProtectedLocation(root, path, database))
            return null;

        var extension = Path.GetExtension(path);
        var age = nowUtc - DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc);

        if (IsUnderNamedDirectory(root, path, "sounds") &&
            SoundExtensions.Contains(extension) &&
            !referenced.Contains(Path.GetFullPath(path)))
        {
            return new(path, relative, category, size, lastWriteUtc, "Sonido fuera de la biblioteca; revisar antes de borrar");
        }

        if (IsAvatarImagePath(root, path, extension) &&
            !referenced.Contains(Path.GetFullPath(path)) &&
            !metadataTexts.Any(text => text.Contains(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
        {
            return new(path, relative, category, size, lastWriteUtc, "Imagen sin referencia en perfiles/catálogos; revisar antes de borrar");
        }

        if (IsTemporaryPath(root, path, relative, extension) && age >= TimeSpan.FromHours(24))
            return new(path, relative, category, size, lastWriteUtc, "Temporal sin cambios desde hace 24 horas");

        if (IsUnderNamedDirectory(root, path, "cache") && age >= TimeSpan.FromDays(7))
            return new(path, relative, category, size, lastWriteUtc, "Caché sin cambios desde hace 7 días");

        var isLog = extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                    IsUnderNamedDirectory(root, path, "logs");
        if (isLog && age >= TimeSpan.FromDays(30))
            return new(path, relative, category, size, lastWriteUtc, "Registro sin cambios desde hace 30 días");

        return null;
    }

    private static bool IsTemporaryPath(string root, string path, string relative, string extension) =>
        IsUnderNamedDirectory(root, path, "temp") ||
        IsUnderNamedDirectory(root, path, "tmp") ||
        extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".temp", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".part", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(path).Contains(".tmp-", StringComparison.OrdinalIgnoreCase);

    private static string GetCategory(string root, string path, string relative, string database)
    {
        if (IsActiveDatabaseOrSidecar(path, database))
            return "Base SQLite activa";
        if (IsUnderNamedDirectory(root, path, "secrets"))
            return "Credenciales protegidas";
        if (IsUnderNamedDirectory(root, path, "backups") || IsBackupFileName(path))
            return "Copias de seguridad protegidas";
        if (IsTemporaryPath(root, path, relative, Path.GetExtension(path)) ||
            IsUnderNamedDirectory(root, path, "cache"))
            return "Caché y temporales";
        if (IsUnderNamedDirectory(root, path, "sounds"))
            return "Sonidos";
        if (IsAvatarImagePath(root, path, Path.GetExtension(path)) ||
            IsUnderNamedDirectory(root, path, "avatars") ||
            IsUnderNamedDirectory(root, path, "waifuassets"))
            return "Imágenes y recursos de avatar";
        if (Path.GetExtension(path).Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            IsUnderNamedDirectory(root, path, "logs"))
            return "Registros";
        return "Otros";
    }

    private static bool IsAvatarImagePath(string root, string path, string extension) =>
        ImageExtensions.Contains(extension) &&
        (IsUnderNamedDirectory(root, path, "avatars") ||
         IsUnderNamedDirectory(root, path, "waifuassets"));

    private static bool IsActiveDatabaseOrSidecar(string path, string database)
    {
        var full = Path.GetFullPath(path);
        return PathComparer.Equals(full, database) ||
               PathComparer.Equals(full, database + "-wal") ||
               PathComparer.Equals(full, database + "-shm") ||
               PathComparer.Equals(full, database + "-journal");
    }

    private static bool IsProtectedLocation(string root, string path, string database)
    {
        var full = Path.GetFullPath(path);
        if (!IsWithinRoot(root, full) || IsActiveDatabaseOrSidecar(full, database))
            return true;

        if (IsUnderNamedDirectory(root, full, "secrets") ||
            IsUnderNamedDirectory(root, full, "backups") ||
            IsBackupFileName(full) ||
            DatabaseExtensions.Contains(Path.GetExtension(full)))
            return true;

        return false;
    }

    private static bool IsBackupFileName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Contains(".pre-restore-", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".vtuberbot.db", StringComparison.OrdinalIgnoreCase) ||
               name.Contains(".backup.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderNamedDirectory(string root, string path, string name)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts.Take(parts.Length - 1).Any(part => part.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWithinRoot(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, PathComparison) && !PathComparer.Equals(fullRoot, fullPath);
    }

    private static void AddReference(HashSet<string> references, string? path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096)
            return;

        try
        {
            var candidate = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(path, root);
            if (File.Exists(candidate))
                references.Add(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
        }
    }

    private static void AddJsonReferences(
        JsonElement element,
        string metadataDirectory,
        string root,
        HashSet<string> references)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                AddReference(references, element.GetString(), metadataDirectory);
                // Also allow a project/catalog to store a data-root-relative reference.
                AddReference(references, element.GetString(), root);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    AddJsonReferences(property.Value, metadataDirectory, root, references);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AddJsonReferences(item, metadataDirectory, root, references);
                break;
        }
    }

    private static IEnumerable<string> EnumerateFilesWithoutFollowingLinks(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(current); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            { continue; }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(entry); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException)
                { continue; }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(entry);
                else
                    yield return entry;
            }
        }
    }

    private static string NormalizeRelativePath(string value) =>
        value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Replace('\\', '/');

    private static int CategoryOrder(string name) => name switch
    {
        "Base SQLite activa" => 0,
        "Imágenes y recursos de avatar" => 1,
        "Sonidos" => 2,
        "Registros" => 3,
        "Caché y temporales" => 4,
        "Credenciales protegidas" => 5,
        "Copias de seguridad protegidas" => 6,
        _ => 7
    };

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
