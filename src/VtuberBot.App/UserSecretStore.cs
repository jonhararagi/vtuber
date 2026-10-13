using System.Security.Cryptography;
using System.Text;

namespace VtuberBot.App;

/// <summary>DPAPI-backed per-Windows-user secret storage, outside SQLite and application logs.</summary>
public sealed class UserSecretStore
{
    private readonly string directory;
    public UserSecretStore(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
    }

    public void Save(string name, string secret)
    {
        ValidateName(name);
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 8192)
            throw new ArgumentException("La clave debe contener entre 1 y 8192 caracteres.", nameof(secret));
        var path = GetPath(name);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret.Trim()), null, DataProtectionScope.CurrentUser);
        try
        {
            File.WriteAllBytes(temporary, protectedBytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
    }

    public string Read(string name)
    {
        ValidateName(name);
        var path = GetPath(name);
        if (!File.Exists(path)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); }
        catch (CryptographicException) { return ""; }
        catch (IOException) { return ""; }
    }

    public bool HasSecret(string name) => !string.IsNullOrWhiteSpace(Read(name));
    public void Delete(string name) { ValidateName(name); var path = GetPath(name); if (File.Exists(path)) File.Delete(path); }
    private string GetPath(string name) => Path.Combine(directory, name + ".dpapi");

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 ||
            name.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-'))
            throw new ArgumentException("Identificador de secreto no válido.", nameof(name));
    }
}
