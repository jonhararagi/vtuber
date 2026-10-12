using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class PngtuberPlusSetupTests
{
    [Fact]
    public void WizardProgressSurvivesServiceRecreation()
    {
        var settings = new FakeSettings();
        var first = new PngtuberSetupService(settings);
        first.SetCurrentStep(4);
        first.Confirm(PngtuberSetupStep.PrepareAvatar, true);
        first.Confirm(PngtuberSetupStep.TestMicrophone, true);

        var restartedService = new PngtuberSetupService(settings);
        var recovered = restartedService.Load();

        Assert.Equal(4, recovered.CurrentStep);
        Assert.True(recovered.AvatarPreparedConfirmed);
        Assert.True(recovered.MicrophoneReactionConfirmed);
        Assert.False(recovered.ExpressionsPreparedConfirmed);
        Assert.False(recovered.ObsCaptureConfirmed);
    }

    [Fact]
    public void SqlitePersistsExecutableAndWizardProgressAcrossStoreRecreation()
    {
        var root = Path.Combine(Path.GetTempPath(), "vtuberbot-pngtuber-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var database = Path.Combine(root, "settings.sqlite3");
        var executable = Path.Combine(root, "PNGTuberPlus.exe");
        try
        {
            File.WriteAllText(executable, "test placeholder");
            var firstStore = new ProductStore(database);
            firstStore.SetSetting("integrations.pngTuberPlus.path", executable);
            var firstSetup = new PngtuberSetupService(firstStore);
            firstSetup.SetCurrentStep(3);
            firstSetup.Confirm(PngtuberSetupStep.TestMicrophone, true);

            var reopenedStore = new ProductStore(database);
            Assert.Equal(executable, reopenedStore.GetSetting("integrations.pngTuberPlus.path"));
            var recovered = new PngtuberSetupService(reopenedStore).Load();
            Assert.Equal(3, recovered.CurrentStep);
            Assert.True(recovered.MicrophoneReactionConfirmed);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void ProcessPresenceDoesNotAutomaticallyConfirmWizardSteps()
    {
        var service = new PngtuberSetupService(new FakeSettings());
        var progress = service.Load();
        var running = new ExternalApplicationStatus(
            ExternalApplicationState.Running, @"C:\\Apps\\PNGTuberPlus.exe",
            "Proceso presente; conexión no verificada.", true);

        Assert.False(progress.AppOpenedConfirmed);
        Assert.False(PngtuberSetupStepCompletion.IsComplete(1, progress, running));
    }

    [Fact]
    public void WizardRejectsAutomaticLocationAsManualConfirmation()
    {
        var service = new PngtuberSetupService(new FakeSettings());
        Assert.Throws<ArgumentException>(() => service.Confirm(PngtuberSetupStep.LocateApplication, true));
    }

    [Fact]
    public void WizardCanGoBackAndPersistsManualConfirmationRemoval()
    {
        var settings = new FakeSettings();
        var service = new PngtuberSetupService(settings);
        service.Confirm(PngtuberSetupStep.VerifyObs, true);
        service.SetCurrentStep(1);
        service.Confirm(PngtuberSetupStep.VerifyObs, false);

        var recovered = new PngtuberSetupService(settings).Load();
        Assert.Equal(1, recovered.CurrentStep);
        Assert.False(recovered.ObsCaptureConfirmed);
    }

    [Fact]
    public void HotkeyRegistryPersistsAndValidatesEntriesAcrossInstances()
    {
        var settings = new FakeSettings();
        var first = new PngtuberHotkeyRegistry(settings, new FakeSender());
        var entry = new PngtuberHotkey(Guid.NewGuid(), "Disfraz 1", "1", "Cambiar al disfraz uno", "PNGTuber Plus");
        first.AddOrUpdate(entry);

        var restarted = new PngtuberHotkeyRegistry(settings, new FakeSender());
        Assert.Equal(entry, Assert.Single(restarted.Load()));
        Assert.Throws<ArgumentException>(() => restarted.AddOrUpdate(entry with { Key = "CTRL+1" }));
        Assert.Throws<ArgumentException>(() => restarted.AddOrUpdate(entry with { TargetApplication = "Streamer.bot" }));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("9", true)]
    [InlineData("A", true)]
    [InlineData("F12", true)]
    [InlineData("F13", false)]
    [InlineData("CTRL+1", false)]
    [InlineData("", false)]
    public void HotkeyKeyPolicyAcceptsOnlySupportedSingleKeys(string key, bool expected)
    {
        Assert.Equal(expected, PngtuberHotkeyKeyPolicy.IsSupported(key));
    }

    [Fact]
    public void MissingExecutablePreventsSendingHotkey()
    {
        var settings = new FakeSettings();
        var sender = new FakeSender();
        var registry = new PngtuberHotkeyRegistry(settings, sender);
        var entry = new PngtuberHotkey(Guid.NewGuid(), "Costume", "1", "Switch costume", "PNGTuber Plus");
        registry.AddOrUpdate(entry);

        var result = registry.Test(entry.Id, Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"));

        Assert.Equal(HotkeySendStatus.NotConfigured, result.Status);
        Assert.Equal(0, sender.SendCount);
    }

    [Fact]
    public void SenderCancellationAndErrorsAreNotReportedAsSuccess()
    {
        using var executable = new TemporaryExecutable();
        var settings = new FakeSettings();
        var sender = new FakeSender
        {
            Result = new HotkeySendResult(HotkeySendStatus.Cancelled, "PNGTuber Plus no está en primer plano.")
        };
        var registry = new PngtuberHotkeyRegistry(settings, sender);
        var entry = new PngtuberHotkey(Guid.NewGuid(), "Costume", "1", "Switch costume", "PNGTuber Plus");
        registry.AddOrUpdate(entry);

        var cancelled = registry.Test(entry.Id, executable.Path);
        Assert.Equal(HotkeySendStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.WasSent);
        Assert.False(cancelled.WasConfirmed);

        sender.Exception = new InvalidOperationException("injected failure");
        var error = registry.Test(entry.Id, executable.Path);
        Assert.Equal(HotkeySendStatus.Error, error.Status);
        Assert.Contains("injected failure", error.Message);
        Assert.False(error.WasConfirmed);
    }

    [Fact]
    public void SentHotkeyIsStillNotConfirmedAsAvatarChange()
    {
        using var executable = new TemporaryExecutable();
        var settings = new FakeSettings();
        var sender = new FakeSender
        {
            Result = new HotkeySendResult(HotkeySendStatus.SentToForegroundApplication, "Tecla enviada.")
        };
        var registry = new PngtuberHotkeyRegistry(settings, sender);
        var entry = new PngtuberHotkey(Guid.NewGuid(), "Costume", "1", "Switch costume", "PNGTuber Plus");
        registry.AddOrUpdate(entry);

        var result = registry.Test(entry.Id, executable.Path);
        Assert.True(result.WasSent);
        Assert.False(result.WasConfirmed);
    }

    [Fact]
    public void SavingExternalSettingsDoesNotOverwriteNativeAvatarProfilesOrLayers()
    {
        var root = Path.Combine(Path.GetTempPath(), "vtuberbot-pngtuber-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProductStore(Path.Combine(root, "test.sqlite3"));
            var profileBefore = store.GetActiveAvatarProfile();
            var profilesBefore = store.GetAvatarProfiles().ToArray();
            var layersBefore = store.GetAvatarLayers().ToArray();

            store.SetSetting("integrations.pngTuberPlus.path", Path.Combine(root, "PNGTuberPlus.exe"));
            new PngtuberSetupService(store).Confirm(PngtuberSetupStep.PrepareAvatar, true);
            new PngtuberHotkeyRegistry(store, new FakeSender()).AddOrUpdate(
                new PngtuberHotkey(Guid.NewGuid(), "Disfraz", "1", "Alternar disfraz", "PNGTuber Plus"));

            Assert.Equal(profileBefore, store.GetActiveAvatarProfile());
            Assert.Equal(profilesBefore, store.GetAvatarProfiles().ToArray());
            Assert.Equal(layersBefore, store.GetAvatarLayers().ToArray());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class FakeSettings : IProductSettingsStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public string GetSetting(string key, string fallback = "") => values.TryGetValue(key, out var value) ? value : fallback;
        public void SetSetting(string key, string value) => values[key] = value;
    }

    private sealed class FakeSender : IPngtuberHotkeySender
    {
        public HotkeySendResult Result { get; init; } = new(HotkeySendStatus.SentToForegroundApplication, "sent");
        public Exception? Exception { get; set; }
        public int SendCount { get; private set; }
        public HotkeySendResult Send(string executablePath, string key)
        {
            SendCount++;
            if (Exception is not null) throw Exception;
            return Result;
        }
    }

    private sealed class TemporaryExecutable : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".exe");
        public TemporaryExecutable() => File.WriteAllText(Path, "test-only placeholder");
        public void Dispose() { try { File.Delete(Path); } catch (IOException) { } }
    }
}
