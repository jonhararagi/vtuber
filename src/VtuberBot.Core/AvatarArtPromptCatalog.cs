namespace VtuberBot.Core;

/// <summary>
/// Stable role names and local setting keys for art prompts. Prompt settings are scoped to an
/// avatar profile so editing one character never overwrites another character's art direction.
/// </summary>
public static class AvatarArtPromptCatalog
{
    private static readonly IReadOnlyDictionary<string, string> RoleSlugs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Personaje base"] = "personaje_base",
            ["Fondo"] = "fondo",
            ["Objeto / capa"] = "objeto_capa",
            ["Idle"] = "idle",
            ["Talking"] = "talking",
            ["Blink"] = "blink",
            ["Happy"] = "happy",
            ["Surprised"] = "surprised"
        };

    public static IReadOnlyList<string> Roles { get; } =
        Array.AsReadOnly(new[]
        {
            "Personaje base", "Fondo", "Objeto / capa", "Idle",
            "Talking", "Blink", "Happy", "Surprised"
        });

    public static string GetSettingKey(long profileId, string role)
    {
        if (profileId <= 0)
            throw new ArgumentOutOfRangeException(nameof(profileId), "El ID del perfil debe ser positivo.");
        var slug = GetRoleSlug(role);
        return "avatar.profile." + profileId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ".art_prompt." + slug;
    }

    public static string GetLegacySettingKey(string role) =>
        "avatar.art_prompt." + GetRoleSlug(role);

    private static string GetRoleSlug(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || !RoleSlugs.TryGetValue(role.Trim(), out var slug))
            throw new ArgumentException("Rol de arte desconocido: " + role, nameof(role));
        return slug;
    }
}
