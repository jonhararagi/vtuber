using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>Non-secret metadata for one real Twitch bot account and its target channel.</summary>
public sealed record TwitchBotProfile(
    string Id,
    string Name,
    string BotLogin,
    string Channel,
    string ClientId,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Stores bot profile metadata separately from DPAPI-protected OAuth credentials.
/// A profile is a saved real account configuration, not a fabricated Twitch identity.
/// </summary>
public sealed class TwitchBotProfileStore
{
    private readonly string metadataPath;
    private readonly string secretDirectory;
    private readonly object gate = new();

    public TwitchBotProfileStore(string dataDirectory, string secretDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretDirectory);
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(secretDirectory);
        metadataPath = Path.Combine(dataDirectory, "twitch-bot-profiles.json");
        this.secretDirectory = secretDirectory;
    }

    public IReadOnlyList<TwitchBotProfile> List()
    {
        lock (gate)
        {
            if (!File.Exists(metadataPath))
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<TwitchBotProfile>>(File.ReadAllText(metadataPath)) ?? [];
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("El catálogo de perfiles de bots Twitch está dañado. No se sobrescribió.", exception);
            }
        }
    }

    public void Save(TwitchBotProfile profile, TwitchOAuthCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(credentials);
        if (!Guid.TryParseExact(profile.Id, "N", out _))
            throw new InvalidDataException("El ID del perfil no es válido.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
            throw new ArgumentException("El nombre del perfil debe tener entre 1 y 80 caracteres.");
        if (string.IsNullOrWhiteSpace(profile.BotLogin) || string.IsNullOrWhiteSpace(profile.Channel))
            throw new ArgumentException("El login real del bot y el canal de destino son obligatorios.");
        if (string.IsNullOrWhiteSpace(profile.ClientId) ||
            !string.Equals(credentials.ClientId, profile.ClientId, StringComparison.Ordinal))
            throw new InvalidDataException("Las credenciales OAuth no coinciden con el Client ID del perfil.");
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
            throw new InvalidDataException("El perfil no contiene un token OAuth válido.");

        lock (gate)
        {
            var entries = ReadUnsafe().Where(item => !string.Equals(item.Id, profile.Id, StringComparison.Ordinal)).ToList();
            entries.Add(profile with { Name = profile.Name.Trim(), BotLogin = profile.BotLogin.Trim(), Channel = profile.Channel.Trim() });
            WriteAtomic(metadataPath, JsonSerializer.Serialize(entries));
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials)),
                Encoding.UTF8.GetBytes("VTuberBot Twitch bot profile " + profile.Id),
                DataProtectionScope.CurrentUser);
            WriteAtomicBytes(GetSecretPath(profile.Id), protectedBytes);
        }
    }

    public TwitchOAuthCredentials LoadCredentials(TwitchBotProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Guid.TryParseExact(profile.Id, "N", out _))
            throw new InvalidDataException("El ID del perfil no es válido.");
        var path = GetSecretPath(profile.Id);
        if (!File.Exists(path))
            throw new FileNotFoundException("Este perfil no tiene credenciales guardadas. Volvé a autorizar la cuenta y guardalo otra vez.", path);
        try
        {
            var plaintext = ProtectedData.Unprotect(
                File.ReadAllBytes(path),
                Encoding.UTF8.GetBytes("VTuberBot Twitch bot profile " + profile.Id),
                DataProtectionScope.CurrentUser);
            var credentials = JsonSerializer.Deserialize<TwitchOAuthCredentials>(Encoding.UTF8.GetString(plaintext));
            if (credentials is null || string.IsNullOrWhiteSpace(credentials.AccessToken) ||
                !string.Equals(credentials.ClientId, profile.ClientId, StringComparison.Ordinal))
                throw new InvalidDataException("Las credenciales guardadas no coinciden con el perfil.");
            return credentials;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("Windows no pudo descifrar las credenciales de este perfil.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Las credenciales de este perfil están dañadas.", exception);
        }
    }

    public void Delete(TwitchBotProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Guid.TryParseExact(profile.Id, "N", out _))
            throw new InvalidDataException("El ID del perfil no es válido.");
        lock (gate)
        {
            var entries = ReadUnsafe().Where(item => !string.Equals(item.Id, profile.Id, StringComparison.Ordinal)).ToList();
            WriteAtomic(metadataPath, JsonSerializer.Serialize(entries));
            var path = GetSecretPath(profile.Id);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private List<TwitchBotProfile> ReadUnsafe()
    {
        if (!File.Exists(metadataPath))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<TwitchBotProfile>>(File.ReadAllText(metadataPath)) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El catálogo de perfiles de bots Twitch está dañado. No se sobrescribió.", exception);
        }
    }

    private string GetSecretPath(string id) => Path.Combine(secretDirectory, "twitch-bot-" + id + ".bin");

    private static void WriteAtomic(string path, string content) =>
        WriteAtomicBytes(path, Encoding.UTF8.GetBytes(content));

    private static void WriteAtomicBytes(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
