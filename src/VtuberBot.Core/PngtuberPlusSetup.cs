using System.Text.Json;

namespace VtuberBot.Core;

/// <summary>Small persistence boundary backed by the existing local product_settings table.</summary>
public interface IProductSettingsStore
{
    string GetSetting(string key, string fallback = "");
    void SetSetting(string key, string value);
}

public sealed record PngtuberSetupProgress(
    int CurrentStep = 0,
    bool AppOpenedConfirmed = false,
    bool AvatarPreparedConfirmed = false,
    bool MicrophoneReactionConfirmed = false,
    bool ExpressionsPreparedConfirmed = false,
    bool ObsCaptureConfirmed = false);

public enum PngtuberSetupStep
{
    LocateApplication = 0,
    OpenApplication = 1,
    PrepareAvatar = 2,
    TestMicrophone = 3,
    PrepareExpressions = 4,
    VerifyObs = 5
}

/// <summary>Persists only user-confirmed setup milestones. It never infers them from process state.</summary>
public sealed class PngtuberSetupService
{
    public const string ProgressSettingKey = "integrations.pngTuberPlus.setupProgress";
    private readonly IProductSettingsStore settings;

    public PngtuberSetupService(IProductSettingsStore settings) => this.settings = settings;

    public PngtuberSetupProgress Load()
    {
        var json = settings.GetSetting(ProgressSettingKey);
        if (string.IsNullOrWhiteSpace(json))
            return new PngtuberSetupProgress();

        try
        {
            var progress = JsonSerializer.Deserialize<PngtuberSetupProgress>(json);
            return progress is null
                ? new PngtuberSetupProgress()
                : progress with { CurrentStep = Math.Clamp(progress.CurrentStep, 0, 5) };
        }
        catch (JsonException)
        {
            // A damaged local preference must not prevent the rest of the application from opening.
            return new PngtuberSetupProgress();
        }
    }

    public PngtuberSetupProgress SetCurrentStep(int step)
    {
        if (step is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(step), "El paso debe estar entre 0 y 5.");
        return Save(Load() with { CurrentStep = step });
    }

    public PngtuberSetupProgress Confirm(PngtuberSetupStep step, bool confirmed)
    {
        var progress = Load();
        progress = step switch
        {
            PngtuberSetupStep.OpenApplication => progress with { AppOpenedConfirmed = confirmed },
            PngtuberSetupStep.PrepareAvatar => progress with { AvatarPreparedConfirmed = confirmed },
            PngtuberSetupStep.TestMicrophone => progress with { MicrophoneReactionConfirmed = confirmed },
            PngtuberSetupStep.PrepareExpressions => progress with { ExpressionsPreparedConfirmed = confirmed },
            PngtuberSetupStep.VerifyObs => progress with { ObsCaptureConfirmed = confirmed },
            PngtuberSetupStep.LocateApplication => throw new ArgumentException(
                "La localización se comprueba automáticamente y no admite confirmación manual.", nameof(step)),
            _ => throw new ArgumentOutOfRangeException(nameof(step))
        };
        return Save(progress);
    }

    private PngtuberSetupProgress Save(PngtuberSetupProgress progress)
    {
        settings.SetSetting(ProgressSettingKey, JsonSerializer.Serialize(progress));
        return progress;
    }
}

public sealed record PngtuberHotkey(
    Guid Id,
    string Name,
    string Key,
    string Description,
    string TargetApplication,
    bool Enabled = true);

public enum HotkeySendStatus
{
    SentToForegroundApplication,
    Cancelled,
    NotConfigured,
    TargetNotRunning,
    TargetNotForeground,
    Error,
    Unsupported
}

public sealed record HotkeySendResult(HotkeySendStatus Status, string Message)
{
    public bool WasSent => Status == HotkeySendStatus.SentToForegroundApplication;
    public bool WasConfirmed => false;
}

/// <summary>Boundary for optional OS key injection. A send is never treated as proof of an avatar change.</summary>
public interface IPngtuberHotkeySender
{
    HotkeySendResult Send(string executablePath, string key);
}

public sealed class PngtuberHotkeyRegistry
{
    public const string RegistrySettingKey = "integrations.pngTuberPlus.hotkeys";
    private const int MaxEntries = 100;
    private readonly IProductSettingsStore settings;
    private readonly IPngtuberHotkeySender sender;

    public PngtuberHotkeyRegistry(IProductSettingsStore settings, IPngtuberHotkeySender sender)
    {
        this.settings = settings;
        this.sender = sender;
    }

    public IReadOnlyList<PngtuberHotkey> Load()
    {
        var json = settings.GetSetting(RegistrySettingKey);
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            var entries = JsonSerializer.Deserialize<List<PngtuberHotkey>>(json) ?? [];
            return entries.Take(MaxEntries).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public PngtuberHotkey AddOrUpdate(PngtuberHotkey entry)
    {
        Validate(entry);
        var entries = Load().ToList();
        var index = entries.FindIndex(item => item.Id == entry.Id);
        if (index >= 0)
            entries[index] = entry;
        else
        {
            if (entries.Count >= MaxEntries)
                throw new InvalidOperationException("El registro admite como máximo 100 atajos.");
            entries.Add(entry);
        }
        settings.SetSetting(RegistrySettingKey, JsonSerializer.Serialize(entries));
        return entry;
    }

    public bool Remove(Guid id)
    {
        var entries = Load().ToList();
        var removed = entries.RemoveAll(entry => entry.Id == id) > 0;
        if (removed)
            settings.SetSetting(RegistrySettingKey, JsonSerializer.Serialize(entries));
        return removed;
    }

    public HotkeySendResult Test(Guid id, string executablePath)
    {
        var entry = Load().FirstOrDefault(item => item.Id == id);
        if (entry is null || !entry.Enabled)
            return new(HotkeySendStatus.NotConfigured, "El atajo no existe o está desactivado.");
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return new(HotkeySendStatus.NotConfigured, "Configurá una ruta válida al ejecutable de PNGTuber Plus.");
        try
        {
            return sender.Send(executablePath, entry.Key);
        }
        catch (Exception exception)
        {
            return new(HotkeySendStatus.Error, "No se pudo solicitar el atajo: " + exception.Message);
        }
    }

    private static void Validate(PngtuberHotkey entry)
    {
        if (entry.Id == Guid.Empty)
            throw new ArgumentException("El atajo necesita un identificador.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Trim().Length > 64)
            throw new ArgumentException("El nombre debe tener entre 1 y 64 caracteres.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Description) || entry.Description.Trim().Length > 240)
            throw new ArgumentException("La descripción debe tener entre 1 y 240 caracteres.", nameof(entry));
        if (!string.Equals(entry.TargetApplication, "PNGTuber Plus", StringComparison.Ordinal))
            throw new ArgumentException("Esta versión solo admite PNGTuber Plus como aplicación objetivo.", nameof(entry));
        if (!PngtuberHotkeyKeyPolicy.IsSupported(entry.Key))
            throw new ArgumentException("Usá una sola tecla: 0–9, A–Z o F1–F12.", nameof(entry));
    }
}

public static class PngtuberHotkeyKeyPolicy
{
    public static bool IsSupported(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        var value = key.Trim().ToUpperInvariant();
        return (value.Length == 1 && (value[0] is >= '0' and <= '9' or >= 'A' and <= 'Z')) ||
               ((value.Length is 2 or 3) && value[0] == 'F' &&
                int.TryParse(value[1..], out var functionKey) && functionKey is >= 1 and <= 12);
    }
}
