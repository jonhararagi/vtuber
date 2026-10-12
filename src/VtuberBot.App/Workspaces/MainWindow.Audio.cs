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
    private UIElement BuildAudioPage()
    {
        var root = new StackPanel();
        var panel = Panel("Micrófono, detección de voz y TTS local");
        root.Children.Add(panel);

        var devices = new ComboBox
        {
            ItemsSource = Enumerable.Range(0, WaveIn.DeviceCount).Select(i => $"{i}: {WaveIn.GetCapabilities(i).ProductName}").ToList(),
            SelectedIndex = WaveIn.DeviceCount > 0 ? 0 : -1
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
        };
        panel.Children.Add(thresholdLabel);
        panel.Children.Add(thresholdSlider);

        var holdSlider = new Slider { Minimum = 100, Maximum = 1200, Value = voiceActivity.ReleaseHold.TotalMilliseconds, TickFrequency = 50 };
        var holdLabel = Label($"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms", 12, "#98A4BE");
        holdSlider.ValueChanged += (_, _) =>
        {
            voiceActivity.SetReleaseHold(TimeSpan.FromMilliseconds(holdSlider.Value));
            holdLabel.Text = $"Retención para evitar parpadeo: {voiceActivity.ReleaseHold.TotalMilliseconds:0} ms";
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

                voiceActivity.Reset();
                microphone = new WaveInEvent
                {
                    DeviceNumber = Math.Max(0, devices.SelectedIndex),
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                microphone.DataAvailable += (_, e) =>
                {
                    double sum = 0;
                    for (var i = 0; i < e.BytesRecorded - 1; i += 2)
                    {
                        short sample = BitConverter.ToInt16(e.Buffer, i);
                        sum += (double)sample * sample;
                    }
                    var rms = e.BytesRecorded > 0 ? Math.Sqrt(sum / (e.BytesRecorded / 2)) / 32768 : 0;
                    var rawLevel = Math.Clamp(rms * 450, 0, 100);
                    VoiceActivitySnapshot snapshot;
                    try { snapshot = voiceActivity.Process(rawLevel, DateTimeOffset.UtcNow); }
                    catch (ArgumentOutOfRangeException) { return; }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        meter.Value = snapshot.SmoothedLevelPercent;
                        meterLabel.Text = $"Nivel de entrada: {snapshot.SmoothedLevelPercent:0}% · " +
                            (snapshot.IsActive ? "actividad acústica detectada" : "silencio / debajo del umbral");
                        if (!snapshot.StateChanged) return;

                        var state = snapshot.IsActive ? "Talking" : "Idle";
                        store.SetSetting("avatar.state", state);
                        overlay?.SetState(state);
                        FooterStatus.Text = snapshot.IsActive
                            ? "Actividad acústica detectada; el overlay activa su estado Talking."
                            : "Actividad acústica finalizada; el overlay vuelve a Idle.";
                        store.Log("Audio", snapshot.IsActive
                            ? "Señal de entrada por encima del umbral; cue Talking activado."
                            : "Señal de entrada por debajo del umbral durante el tiempo de retención; cue Idle activado.");
                    }));
                };
                microphone.RecordingStopped += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                    meterLabel.Text = "Captura detenida."));
                microphone.StartRecording();
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
        var savedVoiceId = store.GetSetting("audio.tts.voiceId");
        var voicePicker = new ComboBox
        {
            ItemsSource = new List<LocalSpeechVoice> { new(string.Empty, "Voz predeterminada de Windows") },
            SelectedIndex = 0,
            DisplayMemberPath = nameof(LocalSpeechVoice.Name),
            MinWidth = 280,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var initialRate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var rateSetting) ? Math.Clamp(rateSetting, -10, 10) : 0;
        var initialVolume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var volumeSetting) ? Math.Clamp(volumeSetting, 0, 100) : 100;
        var rateSlider = new Slider { Minimum = -10, Maximum = 10, Value = initialRate, TickFrequency = 1 };
        var volumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = initialVolume, TickFrequency = 5 };
        var rateLabel = Label($"Velocidad SAPI: {initialRate}", 12, "#98A4BE");
        var volumeLabel = Label($"Volumen SAPI: {initialVolume}%", 12, "#98A4BE");
        rateSlider.ValueChanged += (_, _) => rateLabel.Text = $"Velocidad SAPI: {rateSlider.Value:0}";
        volumeSlider.ValueChanged += (_, _) => volumeLabel.Text = $"Volumen SAPI: {volumeSlider.Value:0}%";

        panel.Children.Add(Label("Síntesis de voz de Windows (SAPI)", 14, "#F8FAFC", true));
        panel.Children.Add(Label("Voces que ya están instaladas en Windows", 12, "#98A4BE"));
        panel.Children.Add(voicePicker);
        panel.Children.Add(ActionButton("Actualizar lista de voces", async (_, _) =>
        {
            try
            {
                var installed = await Task.Run(LocalSpeechQueue.GetInstalledVoices);
                var voices = new List<LocalSpeechVoice> { new(string.Empty, "Voz predeterminada de Windows") };
                voices.AddRange(installed);
                voicePicker.ItemsSource = voices;
                voicePicker.SelectedItem = voices.FirstOrDefault(voice => string.Equals(voice.Id, savedVoiceId, StringComparison.OrdinalIgnoreCase))
                    ?? voices[0];
                FooterStatus.Text = $"Voces SAPI encontradas: {installed.Count}.";
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudieron leer las voces SAPI: " + exception.Message;
                MessageBox.Show(this, "No se pudo enumerar las voces instaladas. Se mantiene la voz predeterminada de Windows.\n\n" + exception.Message);
            }
        }));
        panel.Children.Add(rateLabel);
        panel.Children.Add(rateSlider);
        panel.Children.Add(volumeLabel);
        panel.Children.Add(volumeSlider);
        panel.Children.Add(ActionButton("Guardar voz y ajustes", (_, _) =>
        {
            savedVoiceId = (voicePicker.SelectedItem as LocalSpeechVoice)?.Id ?? string.Empty;
            store.SetSetting("audio.tts.voiceId", savedVoiceId);
            store.SetSetting("audio.tts.rate", ((int)rateSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            store.SetSetting("audio.tts.volume", ((int)volumeSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            FooterStatus.Text = "Voz, velocidad y volumen guardados en SQLite.";
            store.Log("Audio", "Preferencias TTS locales guardadas.");
        }));
        panel.Children.Add(speech);
        panel.Children.Add(Label("Prioridad de reproducción", 12, "#98A4BE"));
        panel.Children.Add(priority);
        panel.Children.Add(ActionButton("＋ Encolar voz", (_, _) =>
        {
            var selectedPriority = priority.SelectedItem is SpeechPriority selected ? selected : SpeechPriority.Normal;
            var voiceId = (voicePicker.SelectedItem as LocalSpeechVoice)?.Id ?? savedVoiceId;
            if (speechQueue.Enqueue(speech.Text, selectedPriority, voiceId, (int)rateSlider.Value, (int)volumeSlider.Value))
                store.Log("Audio", "Frase agregada a la cola TTS local con prioridad " + selectedPriority + ".");
            else
                FooterStatus.Text = "No se pudo encolar: revisá el texto, velocidad, volumen o el límite de la cola.";
        }, true));
        panel.Children.Add(ActionButton("Vaciar frases pendientes", (_, _) =>
        {
            speechQueue.ClearPending();
            store.Log("Audio", "Cola de voz pendiente vaciada.");
        }));
        root.Children.Add(Label("La cola procesa una frase por vez en un hilo STA separado. Podés elegir una voz SAPI instalada, ajustar velocidad (-10 a 10) y volumen (0 a 100). Las alertas usan las preferencias guardadas. No se descarga ni se promete una voz anime: el resultado depende de las voces que tenga Windows. La detección de micrófono usa amplitud suavizada/retención, no reconocimiento semántico ni lip-sync por fonemas.", 12, "#98A4BE"));
        return root;
    }

    private bool EnqueueLocalSpeech(string text, SpeechPriority priority)
    {
        var voiceId = store.GetSetting("audio.tts.voiceId");
        var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var savedRate) ? Math.Clamp(savedRate, -10, 10) : 0;
        var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var savedVolume) ? Math.Clamp(savedVolume, 0, 100) : 100;
        return speechQueue.Enqueue(text, priority, voiceId, rate, volume);
    }

    private void StopMicrophone()
    {
        var capture = microphone;
        microphone = null;
        try { capture?.StopRecording(); } catch (InvalidOperationException) { }
        try { capture?.Dispose(); } catch (InvalidOperationException) { }
        if (voiceActivity.IsActive)
        {
            voiceActivity.Reset();
            store.SetSetting("avatar.state", "Idle");
            overlay?.SetState("Idle");
        }
    }
}
