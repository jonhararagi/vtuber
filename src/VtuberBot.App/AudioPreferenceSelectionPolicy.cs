namespace VtuberBot.App;

/// <summary>Preserves a saved device/voice choice until the installed-options catalog was loaded successfully.</summary>
public static class AudioPreferenceSelectionPolicy
{
    public static string Resolve(string? selectedOptionId, string? savedOptionId, bool optionCatalogLoaded) =>
        optionCatalogLoaded ? selectedOptionId ?? string.Empty : savedOptionId ?? string.Empty;
}
