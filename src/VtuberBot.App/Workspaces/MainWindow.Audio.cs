using System.Net.Http;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Wpf;
using NAudio.Wave;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private sealed record LocalSoundAsset(string Id, string Name, string Path);
    private Process? localVoicevoxEngineProcess;
    private int voicevoxEngineStartInProgress;
    private UIElement BuildAudioPage()
    {
        var root = new StackPanel();
        var panel = Panel("Micrófono, detección de voz y TTS local");
        root.Children.Add(panel);

        var deviceItems = Enumerable.Range(0, WaveIn.DeviceCount)
            .Select(i => $"{i}: {WaveIn.GetCapabilities(i).ProductName}")
            .ToList();
        var savedDeviceName = store.GetSetting("audio.microphone.deviceName");
        var savedDeviceIndex = deviceItems.FindIndex(item =>
        {
            var separator = item.IndexOf(':');
            return separator >= 0 && string.Equals(
                item[(separator + 1)..].Trim(),
                savedDeviceName,
                StringComparison.OrdinalIgnoreCase);
        });
        var devices = new ComboBox
        {
            ItemsSource = deviceItems,
            SelectedIndex = deviceItems.Count == 0 ? -1 : savedDeviceIndex >= 0 ? savedDeviceIndex : 0
        };
        panel.Children.Add(Label("Dispositivo de entrada", 12, "#98A4BE"));
        panel.Children.Add(devices);
        var meter = new ProgressBar { Minimum = 0, Maximum = 100, Height = 16, Margin = new Thickness(0, 8, 0, 12) };
        var meterLabel = Label("Nivel de entrada: N/D", 12, "#98A4BE");
        panel.Children.Add(meter);
        panel.Children.Add(meterLabel);

        var thresholdSlider = new Slider { Minimum = 2, Maximum = 30, Value = voiceActivity.ThresholdPercent, TickFrequency = 1 };
        var thresholdLabel = Label($"Umbral de actividad: {voiceActivity.ThresholdPercent:0}%", 12, "#98A4BE");
        thresholdSlider.ValueChanged += (_, _) =>
        {
            voiceActivity.SetThreshold(thresholdSlider.Value);
            thresholdLabel.Text = $"Umbral de actividad: {voiceActivity.ThresholdPercent:0}%";
            try
            {
                store.SetSetting("audio.vad.threshold",
                    voiceActivity.ThresholdPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo guardar el umbral: " + exception.Message;
            }
        };
        panel.Children.Add(thresholdLabel);
        panel.Children.Add(thresholdSlider);

        var holdSlider = new Slider { Minimum = 100, Maximum = 1200, Value = voiceActivity.ReleaseHold.TotalMilliseconds, TickFrequency = 50 };
        var holdLabel = Label($"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms", 12, "#98A4BE");
        holdSlider.ValueChanged += (_, _) =>
        {
            voiceActivity.SetReleaseHold(TimeSpan.FromMilliseconds(holdSlider.Value));
            holdLabel.Text = $"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms";
            try
            {
                store.SetSetting("audio.vad.releaseHoldMs",
                    voiceActivity.ReleaseHold.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo guardar la retención de voz: " + exception.Message;
            }
        };
        panel.Children.Add(holdLabel);
        panel.Children.Add(holdSlider);

        panel.Children.Add(ActionButton("Iniciar micrófono y detección", (_, _) =>
        {
            try
            {
                StopMicrophone();
                if (WaveIn.DeviceCount == 0)
                {
                    FooterStatus.Text = "No hay micrófonos disponibles.";
                    return;
                }
                if (devices.SelectedIndex < 0 || devices.SelectedIndex >= WaveIn.DeviceCount)
                {
                    FooterStatus.Text = "Elegí un dispositivo de entrada válido.";
                    return;
                }

                var chosenDevice = WaveIn.GetCapabilities(devices.SelectedIndex).ProductName;
                store.SetSetting("audio.microphone.deviceName", chosenDevice);
                voiceActivity.Reset();
                var capture = new WaveInEvent
                {
                    DeviceNumber = devices.SelectedIndex,
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                microphone = capture;
                capture.DataAvailable += (_, e) =>
                {
                    // The tested Core policy handles partial PCM16 frames and normalization.
                    var rawLevel = Pcm16LevelPolicy.CalculatePercent(e.Buffer.AsSpan(0, e.BytesRecorded));
                    VoiceActivitySnapshot snapshot;
                    try { snapshot = voiceActivity.Process(rawLevel, DateTimeOffset.UtcNow); }
                    catch (ArgumentOutOfRangeException) { return; }

                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            meter.Value = snapshot.SmoothedLevelPercent;
                            meterLabel.Text = $"Nivel de entrada: {snapshot.SmoothedLevelPercent:0}% · " +
                                (snapshot.IsActive ? "actividad acústica detectada" : "silencio / debajo del umbral");
                            if (!snapshot.StateChanged) return;

                            microphoneVoiceActive = snapshot.IsActive;
                            UpdateAvatarTalkState();
                            FooterStatus.Text = snapshot.IsActive
                                ? "Actividad acústica detectada; el overlay activa su estado Talking."
                                : speechPlaybackActive
                                    ? "El micrófono está en silencio, pero la voz del bot sigue hablando."
                                    : "Actividad acústica finalizada; el overlay vuelve a Idle.";
                            store.Log("Audio", snapshot.IsActive
                                ? "Señal de entrada por encima del umbral; cue Talking activado."
                                : "Señal de entrada por debajo del umbral durante el tiempo de retención; el avatar solo vuelve a Idle si no habla la voz sintética.");
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        // The audio callback may race with dispatcher shutdown; the app is closing.
                    }
                };
                capture.RecordingStopped += (_, args) =>
                {
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // Ignore a late stop callback from an older capture after a new
                            // device session has already started.
                            if (!ReferenceEquals(microphone, capture))
                                return;

                            microphone = null;
                            try { capture.Dispose(); } catch (InvalidOperationException) { }
                            voiceActivity.Reset();
                            meter.Value = 0;
                            meterLabel.Text = args.Exception is null
                                ? "Captura detenida."
                                : "Captura detenida por error: " + args.Exception.GetType().Name;
                            store.SetSetting("avatar.state", "Idle");
                            overlay?.SetState("Idle");

                            if (args.Exception is not null)
                            {
                                store.Log("Audio", "La captura del micrófono se detuvo por un error (" +
                                    args.Exception.GetType().Name + "). El detector se restableció.");
                                FooterStatus.Text = "El micrófono se detuvo; se restableció el avatar a Idle.";
                            }
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        // The Dispatcher is already shutting down; MainWindow.Close handles cleanup.
                    }
                };
                try
                {
                    capture.StartRecording();
                }
                catch
                {
                    if (ReferenceEquals(microphone, capture))
                        microphone = null;
                    try { capture.Dispose(); } catch (InvalidOperationException) { }
                    voiceActivity.Reset();
                    store.SetSetting("avatar.state", "Idle");
                    overlay?.SetState("Idle");
                    meter.Value = 0;
                    meterLabel.Text = "No se pudo iniciar la captura.";
                    throw;
                }
                store.Log("Audio", "Micrófono real y detección acústica iniciados.");
                FooterStatus.Text = "Micrófono activo. Ajustá umbral y retención para tu ambiente.";
            }
            catch (Exception ex)
            {
                FooterStatus.Text = $"Micrófono: {ex.Message}";
                MessageBox.Show(this, ex.Message);
            }
        }, true));

        panel.Children.Add(ActionButton("Detener micrófono", (_, _) =>
        {
            StopMicrophone();
            voiceActivity.Reset();
            meter.Value = 0;
            meterLabel.Text = "Captura detenida.";
            FooterStatus.Text = "Micrófono detenido; cue de avatar restablecido.";
        }));

        var speech = TextField("Texto para sintetizar en voz local");
        speech.Text = "Hola, chat. ¡Gracias por acompañarme!";
        var priority = new ComboBox
        {
            ItemsSource = Enum.GetValues<SpeechPriority>(),
            SelectedItem = SpeechPriority.Normal,
            Width = 180
        };
        var savedVoiceId = store.GetSetting("audio.voicevox.voiceId", store.GetSetting("audio.tts.voiceId"));
        var savedOutputId = store.GetSetting("audio.voicevox.outputId");
        var voicevoxBaseUrlField = TextField("http://127.0.0.1:50021",
            store.GetSetting("audio.voicevox.baseUrl", VoicevoxSpeechSession.DefaultBaseUrl));
        voicevoxBaseUrlField.MinWidth = 320;
        var voicevoxEnginePathField = TextField(
            "Seleccioná el ejecutable oficial vv-engine/run.exe",
            store.GetSetting("audio.voicevox.executablePath"));
        voicevoxEnginePathField.MinWidth = 320;
        var outputListLoaded = false;
        var voiceListLoaded = false;
        var voicePicker = new ComboBox
        {
            ItemsSource = new List<LocalSpeechVoice>
            {
                new(string.Empty, "VOICEVOX no comprobado — pulsá «Comprobar y actualizar voces»")
            },
            SelectedIndex = 0,
            DisplayMemberPath = nameof(LocalSpeechVoice.Name),
            MinWidth = 280,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var initialRate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var rateSetting) ? Math.Clamp(rateSetting, -10, 10) : 0;
        var initialVolume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var volumeSetting) ? Math.Clamp(volumeSetting, 0, 100) : 100;
        var initialPitch = double.TryParse(store.GetSetting("audio.voicevox.pitchScale", "0"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pitchSetting)
            ? VoicevoxSpeechSession.NormalizePitchScale(pitchSetting) : 0d;
        var initialIntonation = double.TryParse(store.GetSetting("audio.voicevox.intonationScale", "1"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var intonationSetting)
            ? VoicevoxSpeechSession.NormalizeIntonationScale(intonationSetting) : 1d;
        var rateSlider = new Slider { Minimum = -10, Maximum = 10, Value = initialRate, TickFrequency = 1 };
        var volumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = initialVolume, TickFrequency = 5 };
        var pitchSlider = new Slider { Minimum = -0.3, Maximum = 0.3, Value = initialPitch, TickFrequency = 0.01 };
        var intonationSlider = new Slider { Minimum = 0.5, Maximum = 1.5, Value = initialIntonation, TickFrequency = 0.05 };
        var rateLabel = Label($"Velocidad de voz: {initialRate}", 12, "#98A4BE");
        var volumeLabel = Label($"Volumen: {initialVolume}%", 12, "#98A4BE");
        var pitchLabel = Label($"Tono / pitch: {initialPitch:0.00}", 12, "#98A4BE");
        var intonationLabel = Label($"Variación de entonación: {initialIntonation:0.00}×", 12, "#98A4BE");
        rateSlider.ValueChanged += (_, _) => rateLabel.Text = $"Velocidad de voz: {rateSlider.Value:0}";
        volumeSlider.ValueChanged += (_, _) => volumeLabel.Text = $"Volumen: {volumeSlider.Value:0}%";
        pitchSlider.ValueChanged += (_, _) => pitchLabel.Text = $"Tono / pitch: {pitchSlider.Value:0.00}";
        intonationSlider.ValueChanged += (_, _) => intonationLabel.Text = $"Variación de entonación: {intonationSlider.Value:0.00}×";

        panel.Children.Add(Label("Síntesis de voz local · VOICEVOX Engine", 14, "#F8FAFC", true));
        panel.Children.Add(Label(
            "Esta pantalla usa únicamente estilos que VOICEVOX devuelve desde tu motor local. No cambia silenciosamente a Windows SAPI, Gemini ni a una API remota. Las voces VOICEVOX habituales están orientadas al japonés: el motor puede intentar sintetizar español, pero la pronunciación natural en español no está garantizada. El motor y las voces deben estar instalados según sus licencias.",
            12, "#98A4BE"));
        panel.Children.Add(Label("Dirección local de VOICEVOX Engine", 12, "#98A4BE"));
        panel.Children.Add(voicevoxBaseUrlField);
        panel.Children.Add(Label("Ejecutable de VOICEVOX Engine (no se incluye en VTuberBot)", 12, "#98A4BE"));
        panel.Children.Add(Label("En la instalación oficial suele estar en %LOCALAPPDATA%\\Programs\\VOICEVOX\\vv-engine\\run.exe. Seleccioná run.exe; VTuberBot lo iniciará sin exponer el servicio a la red local.", 11, "#98A4BE"));
        panel.Children.Add(voicevoxEnginePathField);
        panel.Children.Add(ActionButton("Elegir run.exe oficial", (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Elegí el ejecutable oficial de VOICEVOX Engine (run.exe)",
                Filter = "VOICEVOX Engine (run.exe)|run.exe|Programas ejecutables (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            if (!string.Equals(Path.GetFileName(dialog.FileName), "run.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Elegí el archivo run.exe de la carpeta vv-engine de una instalación oficial de VOICEVOX.",
                    "Ejecutable de VOICEVOX", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            voicevoxEnginePathField.Text = dialog.FileName;
            store.SetSetting("audio.voicevox.executablePath", dialog.FileName);
            FooterStatus.Text = "Ruta de VOICEVOX Engine guardada. Podés iniciarlo en segundo plano.";
        }));
        panel.Children.Add(ActionButton("Iniciar VOICEVOX Engine en segundo plano", async (_, _) =>
            await StartVoicevoxEngineAsync(), true));
        panel.Children.Add(Label("Voz / estilo", 12, "#98A4BE"));
        panel.Children.Add(voicePicker);

        var outputPicker = new ComboBox
        {
            ItemsSource = new List<LocalSpeechOutput>
            {
                new(string.Empty, string.IsNullOrWhiteSpace(savedOutputId)
                    ? "Salida predeterminada del sistema"
                    : "Se conservará la salida guardada hasta actualizar la lista")
            },
            SelectedIndex = 0,
            DisplayMemberPath = nameof(LocalSpeechOutput.Name),
            MinWidth = 280,
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(Label("Dispositivo de salida de audio VOICEVOX", 12, "#98A4BE"));
        panel.Children.Add(outputPicker);

        async Task<bool> LoadVoicevoxVoicesAsync(bool showError)
        {
            var configuredUrl = voicevoxBaseUrlField.Text.Trim();
            var previousSelection = (voicePicker.SelectedItem as LocalSpeechVoice)?.Id;
            try
            {
                // The adapter rejects non-loopback addresses before it makes any HTTP request.
                store.SetSetting("audio.voicevox.baseUrl", configuredUrl);
                var installedVoicevox = await Task.Run(() => VoicevoxSpeechSession.GetInstalledVoices(configuredUrl));
                if (installedVoicevox.Count == 0)
                    throw new InvalidOperationException("VOICEVOX respondió, pero no devolvió ningún estilo. Verificá que el Engine y al menos una voz sean compatibles.");

                voicePicker.ItemsSource = installedVoicevox;
                voiceListLoaded = true;
                var selected = installedVoicevox.FirstOrDefault(voice =>
                    string.Equals(voice.Id, previousSelection, StringComparison.OrdinalIgnoreCase))
                    ?? installedVoicevox.FirstOrDefault(voice =>
                        string.Equals(voice.Id, savedVoiceId, StringComparison.OrdinalIgnoreCase))
                    ?? installedVoicevox[0];
                voicePicker.SelectedItem = selected;
                savedVoiceId = selected.Id;
                FooterStatus.Text = $"VOICEVOX conectado: {installedVoicevox.Count} estilos disponibles.";
                store.Log("VOICEVOX", $"Motor local disponible; {installedVoicevox.Count} estilos detectados.");
                return true;
            }
            catch (Exception exception)
            {
                voicePicker.ItemsSource = new List<LocalSpeechVoice>
                {
                    new(string.Empty, "VOICEVOX no disponible — iniciá Engine y volvé a comprobar")
                };
                voicePicker.SelectedIndex = 0;
                voiceListLoaded = false;
                FooterStatus.Text = "VOICEVOX no disponible. Iniciá Engine, comprobá la dirección local y volvé a intentar.";
                store.Log("VOICEVOX", "No se pudo consultar el motor local (" + exception.GetType().Name + ").");
                if (showError)
                {
                    MessageBox.Show(this,
                        "No se pudo conectar con VOICEVOX Engine. No se generó audio ni se seleccionó otra voz.\n\n" +
                        exception.GetBaseException().Message +
                        "\n\nIniciá VOICEVOX Engine, comprobá la dirección local y elegí «Comprobar y actualizar voces».",
                        "VOICEVOX no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }
        }

        async Task<bool> IsVoicevoxApiReachableAsync(string baseUrl)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(750) };
                using var response = await client.GetAsync(baseUrl.TrimEnd('/') + "/version");
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException) { return false; }
            catch (TaskCanceledException) { return false; }
        }

        async Task StartVoicevoxEngineAsync()
        {
            if (Interlocked.CompareExchange(ref voicevoxEngineStartInProgress, 1, 0) != 0)
            {
                FooterStatus.Text = "El inicio de VOICEVOX Engine ya está en curso.";
                return;
            }

            try
            {
            var executable = voicevoxEnginePathField.Text.Trim();
            if (!File.Exists(executable) ||
                !string.Equals(Path.GetFileName(executable), "run.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Primero elegí el archivo run.exe de la carpeta vv-engine de una instalación oficial de VOICEVOX.",
                    "VOICEVOX Engine", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!VoicevoxEngineLaunchPolicy.TryParseBaseUrl(voicevoxBaseUrlField.Text.Trim(), out var endpoint))
            {
                MessageBox.Show(this,
                    "Para iniciar Engine desde VTuberBot, configurá una dirección HTTP local como http://127.0.0.1:50021. No se permite exponer Engine a una dirección remota.",
                    "Dirección local no válida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (await IsVoicevoxApiReachableAsync(endpoint.BaseUrl))
            {
                if (await LoadVoicevoxVoicesAsync(showError: false))
                    FooterStatus.Text = "VOICEVOX ya estaba activo. No se inició otra instancia.";
                else
                    FooterStatus.Text = "El Engine responde, pero no hay estilos disponibles. Revisá la instalación de las voces.";
                return;
            }

            if (localVoicevoxEngineProcess is { HasExited: false })
            {
                FooterStatus.Text = "VOICEVOX ya fue iniciado por VTuberBot, pero todavía no responde. Volvé a comprobar las voces en unos segundos.";
                return;
            }

            try
            {
                localVoicevoxEngineProcess?.Dispose();
                localVoicevoxEngineProcess = null;
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                startInfo.ArgumentList.Add("--host");
                startInfo.ArgumentList.Add(endpoint.Host);
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add(endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                startInfo.Environment["VV_OUTPUT_LOG_UTF8"] = "1";

                var launchedProcess = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Windows no pudo iniciar run.exe.");
                localVoicevoxEngineProcess = launchedProcess;
                voicevoxEnginePathField.Text = executable;
                voicevoxBaseUrlField.Text = endpoint.BaseUrl;
                store.SetSetting("audio.voicevox.executablePath", executable);
                store.SetSetting("audio.voicevox.baseUrl", endpoint.BaseUrl);
                FooterStatus.Text = "VOICEVOX Engine iniciado en segundo plano; esperando respuesta local…";

                var ready = false;
                using (var probeClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(750) })
                {
                    for (var attempt = 0; attempt < 30; attempt++)
                    {
                        await Task.Delay(500);
                        if (launchedProcess.HasExited)
                            throw new InvalidOperationException("VOICEVOX run.exe terminó antes de responder. Revisá la instalación oficial y sus requisitos.");

                        try
                        {
                            using var response = await probeClient.GetAsync(endpoint.BaseUrl + "/version");
                            if (response.IsSuccessStatusCode)
                            {
                                ready = true;
                                break;
                            }
                        }
                        catch (HttpRequestException) { }
                        catch (TaskCanceledException) { }
                    }
                }

                if (!ready)
                {
                    FooterStatus.Text = "El proceso de VOICEVOX sigue iniciado, pero la API local aún no responde. Podés volver a comprobarlo.";
                    store.Log("VOICEVOX", "run.exe se inició, pero la API no respondió dentro del plazo de comprobación.");
                    MessageBox.Show(this,
                        "Se inició run.exe, pero la API no respondió en 15 segundos. El proceso no se cerró automáticamente; revisá la instalación, la carga inicial de voces y el puerto configurado.",
                        "VOICEVOX todavía no responde", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (await LoadVoicevoxVoicesAsync(showError: false))
                {
                    FooterStatus.Text = "VOICEVOX Engine listo en segundo plano. Seleccioná un estilo y probá la voz.";
                    store.Log("VOICEVOX", "Engine local iniciado por VTuberBot y estilos disponibles.");
                }
                else
                {
                    FooterStatus.Text = "VOICEVOX Engine responde, pero no devolvió estilos utilizables.";
                }
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo iniciar VOICEVOX Engine: " + exception.GetBaseException().Message;
                store.Log("VOICEVOX", "No se pudo iniciar run.exe (" + exception.GetType().Name + ").");
                MessageBox.Show(this, exception.GetBaseException().Message, "No se pudo iniciar VOICEVOX Engine",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            }
            finally
            {
                Volatile.Write(ref voicevoxEngineStartInProgress, 0);
            }

        }

        panel.Children.Add(ActionButton("Actualizar dispositivos de salida", async (_, _) =>
        {
            try
            {
                var installed = await Task.Run(VoicevoxSpeechSession.GetInstalledOutputs);
                var outputs = new List<LocalSpeechOutput> { new(string.Empty, "Salida predeterminada del sistema") };
                outputs.AddRange(installed);
                outputPicker.ItemsSource = outputs;
                outputListLoaded = true;
                outputPicker.SelectedItem = outputs.FirstOrDefault(output =>
                    string.Equals(output.Id, savedOutputId, StringComparison.OrdinalIgnoreCase)) ?? outputs[0];
                savedOutputId = (outputPicker.SelectedItem as LocalSpeechOutput)?.Id ?? string.Empty;
                FooterStatus.Text = $"Dispositivos de salida de voz encontrados: {installed.Count}.";
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudieron enumerar los dispositivos de salida: " + exception.GetBaseException().Message;
                MessageBox.Show(this, exception.GetBaseException().Message, "Dispositivos de salida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        panel.Children.Add(ActionButton("Comprobar y actualizar voces VOICEVOX", async (_, _) =>
        {
            await LoadVoicevoxVoicesAsync(showError: true);
        }, true));

        panel.Children.Add(rateLabel);
        panel.Children.Add(rateSlider);
        panel.Children.Add(pitchLabel);
        panel.Children.Add(pitchSlider);
        panel.Children.Add(intonationLabel);
        panel.Children.Add(intonationSlider);
        panel.Children.Add(volumeLabel);
        panel.Children.Add(volumeSlider);
        panel.Children.Add(ActionButton("Guardar voz y ajustes", (_, _) =>
        {
            if (!voiceListLoaded ||
                voicePicker.SelectedItem is not LocalSpeechVoice selected ||
                !VoicevoxSpeechSession.IsVoiceId(selected.Id))
            {
                FooterStatus.Text = "Primero iniciá VOICEVOX Engine y comprobá la lista de voces.";
                return;
            }

            savedVoiceId = selected.Id;
            // If output enumeration was never requested in this UI session, preserve a previously
            // saved device instead of mistaking the placeholder item for an intentional reset.
            if (outputListLoaded)
                savedOutputId = (outputPicker.SelectedItem as LocalSpeechOutput)?.Id ?? string.Empty;
            store.SetSetting("audio.voicevox.baseUrl", voicevoxBaseUrlField.Text.Trim());
            store.SetSetting("audio.voicevox.voiceId", savedVoiceId);
            // Keep the generic local-queue preference aligned for internal consumers that already use it.
            store.SetSetting("audio.tts.voiceId", savedVoiceId);
            store.SetSetting("audio.voicevox.outputId", savedOutputId);
            store.SetSetting("audio.tts.rate", ((int)rateSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.voicevox.pitchScale", VoicevoxSpeechSession.NormalizePitchScale(pitchSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.voicevox.intonationScale", VoicevoxSpeechSession.NormalizeIntonationScale(intonationSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.tts.volume", ((int)volumeSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            FooterStatus.Text = "Preferencias VOICEVOX, salida, velocidad y volumen guardados en SQLite.";
            store.Log("VOICEVOX", "Preferencias de voz local guardadas.");
        }, true));
        panel.Children.Add(speech);
        panel.Children.Add(Label("Prioridad de reproducción", 12, "#98A4BE"));
        panel.Children.Add(priority);

        async Task QueueVoicevoxAsync(bool test)
        {
            var textToSpeak = speech.Text.Trim();
            if (string.IsNullOrWhiteSpace(textToSpeak) && test)
                textToSpeak = "こんにちは。これはVOICEVOXの音声テストです。";
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                FooterStatus.Text = "Escribí un texto para encolar en VOICEVOX.";
                return;
            }
            if (textToSpeak.Length > 2000)
            {
                FooterStatus.Text = "VOICEVOX admite un máximo de 2000 caracteres por frase.";
                return;
            }

            if (!await LoadVoicevoxVoicesAsync(showError: test))
                return;

            if (voicePicker.SelectedItem is not LocalSpeechVoice selectedVoice ||
                !VoicevoxSpeechSession.IsVoiceId(selectedVoice.Id))
            {
                FooterStatus.Text = "Seleccioná una voz VOICEVOX disponible antes de reproducir.";
                return;
            }

            var voiceId = selectedVoice.Id;
            var outputId = AudioPreferenceSelectionPolicy.Resolve(
                (outputPicker.SelectedItem as LocalSpeechOutput)?.Id,
                savedOutputId,
                outputListLoaded);
            var selectedPriority = test
                ? SpeechPriority.High
                : priority.SelectedItem is SpeechPriority selected ? selected : SpeechPriority.Normal;

            savedVoiceId = voiceId;
            savedOutputId = outputId;
            store.SetSetting("audio.voicevox.voiceId", voiceId);
            store.SetSetting("audio.tts.voiceId", voiceId);
            store.SetSetting("audio.voicevox.outputId", outputId);
            store.SetSetting("audio.voicevox.pitchScale", VoicevoxSpeechSession.NormalizePitchScale(pitchSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.voicevox.intonationScale", VoicevoxSpeechSession.NormalizeIntonationScale(intonationSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.tts.rate", ((int)rateSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.tts.volume", ((int)volumeSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (speechQueue.Enqueue(textToSpeak, selectedPriority, voiceId,
                    (int)rateSlider.Value, (int)volumeSlider.Value, outputId))
            {
                FooterStatus.Text = test
                    ? "Prueba VOICEVOX encolada con prioridad alta. El estado Talking se activa durante la reproducción real."
                    : "Texto encolado para síntesis VOICEVOX local.";
                store.Log("VOICEVOX", test
                    ? "Se encoló una prueba de audio VOICEVOX."
                    : "Se encoló texto para voz VOICEVOX.");
            }
            else
            {
                FooterStatus.Text = "No se pudo encolar VOICEVOX. Revisá que el texto sea válido y que la cola no esté llena.";
            }
        }

        panel.Children.Add(ActionButton("▶ Probar voz VOICEVOX", async (_, _) =>
            await QueueVoicevoxAsync(test: true), true));
        panel.Children.Add(ActionButton("＋ Encolar texto VOICEVOX", async (_, _) =>
            await QueueVoicevoxAsync(test: false), true));
        panel.Children.Add(ActionButton("Detener voz actual", (_, _) =>
        {
            if (speechQueue.StopCurrentSpeech())
                FooterStatus.Text = "Se solicitó detener la frase que está sonando.";
            else
                FooterStatus.Text = "No hay una frase de voz en reproducción.";
        }));
        panel.Children.Add(ActionButton("Vaciar frases pendientes", (_, _) =>
        {
            speechQueue.ClearPending();
            store.Log("Audio", "Cola de voz pendiente vaciada; la frase actual no se interrumpió.");
        }));
        var soundPanel = Panel("Biblioteca de sonidos locales");
        soundPanel.Margin = new Thickness(0, 14, 0, 0);
        soundPanel.Children.Add(Label("Importá efectos WAV o MP3 propios. Se copian a la carpeta local del usuario; reproducir y detener no requiere Twitch ni una API externa.", 12, "#98A4BE"));
        var soundDirectory = Path.Combine(dataDirectory, "sounds");
        Directory.CreateDirectory(soundDirectory);
        List<LocalSoundAsset> soundAssets;
        try
        {
            soundAssets = JsonSerializer.Deserialize<List<LocalSoundAsset>>(store.GetSetting("audio.sound.library", "[]")) ?? [];
            soundAssets = soundAssets.Where(asset => TryResolveSoundPath(asset.Path, out _)).ToList();
        }
        catch (Exception exception)
        {
            soundAssets = [];
            store.Log("Audio", "No se pudo leer la biblioteca de sonidos (" + exception.GetType().Name + ").");
        }
        var soundList = new ListBox { Height = 130, ItemsSource = soundAssets, DisplayMemberPath = nameof(LocalSoundAsset.Name) };
        soundPanel.Children.Add(soundList);
        void SaveSoundLibrary() => store.SetSetting("audio.sound.library", JsonSerializer.Serialize(soundAssets));
        soundPanel.Children.Add(ActionButton("Importar sonido WAV/MP3", (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "Importar efecto de sonido", Filter = "Audio WAV o MP3|*.wav;*.mp3", CheckFileExists = true, Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                foreach (var source in dialog.FileNames)
                {
                    if (!LocalSoundFormatPolicy.IsSupported(source))
                        throw new InvalidDataException("Solo se admiten archivos WAV y MP3.");
                    var id = Guid.NewGuid().ToString("N");
                    var destination = Path.Combine(soundDirectory, id + Path.GetExtension(source).ToLowerInvariant());
                    File.Copy(source, destination, false);
                    soundAssets.Add(new LocalSoundAsset(id, Path.GetFileNameWithoutExtension(source), destination));
                }
                SaveSoundLibrary();
                soundList.ItemsSource = null;
                soundList.ItemsSource = soundAssets;
                FooterStatus.Text = "Sonidos importados a la biblioteca local.";
                store.Log("Audio", "Se importaron efectos de sonido locales.");
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudieron importar los sonidos: " + exception.Message;
                MessageBox.Show(this, exception.Message, "Biblioteca de sonidos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        var soundActions = new WrapPanel();
        soundActions.Children.Add(ActionButton("Reproducir seleccionado", (_, _) =>
        {
            if (soundList.SelectedItem is not LocalSoundAsset selected || !TryResolveSoundPath(selected.Path, out var path))
            {
                MessageBox.Show(this, "Seleccioná un sonido local válido.", "Biblioteca de sonidos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                StopSoundPlayback();
                var reader = new AudioFileReader(path);
                var output = new WaveOutEvent();
                output.PlaybackStopped += (_, _) =>
                {
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (!ReferenceEquals(soundOutput, output)) return;
                            soundOutput = null;
                            soundReader = null;
                            output.Dispose();
                            reader.Dispose();
                        }));
                    }
                    catch (InvalidOperationException) { }
                };
                output.Init(reader);
                soundReader = reader;
                soundOutput = output;
                output.Play();
                FooterStatus.Text = "Reproduciendo sonido local: " + selected.Name;
                store.Log("Audio", "Reproducción local iniciada: " + selected.Name);
            }
            catch (Exception exception)
            {
                StopSoundPlayback();
                FooterStatus.Text = "No se pudo reproducir el sonido: " + exception.Message;
                MessageBox.Show(this, exception.Message, "Reproducción de audio", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        soundActions.Children.Add(ActionButton("Detener sonido", (_, _) => { StopSoundPlayback(); FooterStatus.Text = "Reproducción de sonido detenida."; }));
        soundActions.Children.Add(ActionButton("Quitar de biblioteca", (_, _) =>
        {
            if (soundList.SelectedItem is not LocalSoundAsset selected) return;
            if (MessageBox.Show(this, "Se quitará de la lista, pero el archivo local no se borrará. ¿Continuar?", "Biblioteca de sonidos", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            soundAssets.RemoveAll(asset => asset.Id == selected.Id);
            SaveSoundLibrary();
            soundList.ItemsSource = null;
            soundList.ItemsSource = soundAssets;
            FooterStatus.Text = "Sonido quitado de la biblioteca; el archivo original se conservó.";
        }));
        soundPanel.Children.Add(soundActions);
        root.Children.Add(soundPanel);
        
        root.Children.Add(Label("El dispositivo de micrófono, el umbral acústico y la retención se guardan automáticamente y se recuperan al reiniciar. La cola procesa una frase por vez en un hilo STA separado. Podés elegir una voz SAPI instalada, ajustar velocidad (-10 a 10) y volumen (0 a 100). Las alertas usan las preferencias guardadas. No se descarga ni se promete una voz anime: el resultado depende de las voces que tenga Windows. La salida SAPI se puede seleccionar entre los dispositivos que Windows exponga; si no está disponible, se usa la salida predeterminada. La detección de micrófono usa amplitud suavizada/retención, no reconocimiento semántico ni lip-sync por fonemas.", 12, "#98A4BE"));
        return root;
    }

    private bool TryResolveSoundPath(string candidate, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate) || !LocalSoundFormatPolicy.IsSupported(candidate)) return false;
        try
        {
            var root = Path.GetFullPath(Path.Combine(dataDirectory, "sounds")) + Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(candidate);
            if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved)) return false;
            fullPath = resolved;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private void StopSoundPlayback()
    {
        var output = soundOutput;
        var reader = soundReader;
        soundOutput = null;
        soundReader = null;
        try { output?.Stop(); } catch (InvalidOperationException) { }
        output?.Dispose();
        reader?.Dispose();
    }
    private bool EnqueueLocalSpeech(string text, SpeechPriority priority)
    {
        var voiceId = store.GetSetting("audio.tts.voiceId");
        var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var savedRate) ? Math.Clamp(savedRate, -10, 10) : 0;
        var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var savedVolume) ? Math.Clamp(savedVolume, 0, 100) : 100;
        var outputId = store.GetSetting("audio.tts.outputId");
        return speechQueue.Enqueue(text, priority, voiceId, rate, volume, outputId);
    }

    private void StopMicrophone()
    {
        var capture = microphone;
        microphone = null;
        try { capture?.StopRecording(); } catch (InvalidOperationException) { }
        try { capture?.Dispose(); } catch (InvalidOperationException) { }
        voiceActivity.Reset();
        microphoneVoiceActive = false;
        UpdateAvatarTalkState();
    }
}
