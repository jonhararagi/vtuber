using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VtuberBot.App;

public enum SpeechPriority
{
    High = 0,
    Normal = 1,
    Low = 2
}

/// <summary>
/// A bounded, priority-ordered local speech queue. SAPI calls run sequentially on a
/// dedicated STA worker so a long utterance cannot freeze the WPF dispatcher.
/// </summary>
public sealed class LocalSpeechQueue : IDisposable
{
    private sealed record SpeechItem(string Text, SpeechPriority Priority, long Sequence);

    private const int MaximumPending = 32;
    private readonly object gate = new();
    private readonly PriorityQueue<SpeechItem, (int Priority, long Sequence)> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private readonly Thread worker;
    private long sequence;
    private bool stopping;
    private bool speaking;

    public event Action<string, int>? StatusChanged;

    public int PendingCount
    {
        get { lock (gate) return queue.Count; }
    }

    public bool Enqueue(string text, SpeechPriority priority = SpeechPriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Notify("Escribí un texto antes de encolarlo.", PendingCount);
            return false;
        }

        int pending;
        lock (gate)
        {
            if (stopping || queue.Count + (speaking ? 1 : 0) >= MaximumPending)
            {
                pending = queue.Count;
                Notify(stopping ? "La cola está cerrándose." : "La cola llegó al límite de 32 frases.", pending);
                return false;
            }

            var item = new SpeechItem(text.Trim(), priority, ++sequence);
            queue.Enqueue(item, ((int)priority, item.Sequence));
            pending = queue.Count;
        }

        signal.Set();
        Notify("Frase encolada con prioridad " + priority + ".", pending);
        return true;
    }

    public void ClearPending()
    {
        int pending;
        lock (gate)
        {
            queue.Clear();
            pending = queue.Count;
        }

        signal.Set();
        Notify("Se vaciaron las frases pendientes. La frase que está sonando no se interrumpe.", pending);
    }

    private void WorkerLoop()
    {
        object? voice = null;
        Type? voiceType = null;
        try
        {
            while (true)
            {
                SpeechItem? next = null;
                int pending;
                bool shouldStop;
                lock (gate)
                {
                    if (queue.Count > 0)
                    {
                        next = queue.Dequeue();
                        speaking = true;
                    }
                    shouldStop = stopping && next is null;
                    pending = queue.Count;
                }

                if (shouldStop)
                    break;
                if (next is null)
                {
                    signal.WaitOne();
                    continue;
                }

                Notify("Reproduciendo frase encolada.", pending);
                try
                {
                    voiceType ??= Type.GetTypeFromProgID("SAPI.SpVoice")
                        ?? throw new InvalidOperationException("Windows SAPI no está disponible en este equipo.");
                    voice ??= Activator.CreateInstance(voiceType)
                        ?? throw new InvalidOperationException("No se pudo crear el motor SAPI.");
                    voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { next.Text, 0 });
                    Notify("Reproducción finalizada.", PendingCount);
                }
                catch (Exception exception)
                {
                    ReleaseVoice(voice);
                    voice = null;
                    voiceType = null;
                    var actual = exception is TargetInvocationException invocation
                        ? invocation.InnerException ?? exception
                        : exception;
                    Notify("Error TTS local: " + actual.Message, PendingCount);
                }
                finally
                {
                    lock (gate) speaking = false;
                }
            }
        }
        finally
        {
            ReleaseVoice(voice);
            Notify("Cola de voz cerrada.", PendingCount);
        }
    }

    private static void ReleaseVoice(object? voice)
    {
        if (voice is null || !Marshal.IsComObject(voice))
            return;
        try { Marshal.FinalReleaseComObject(voice); }
        catch { }
    }

    private void Notify(string status, int pending)
    {
        try { StatusChanged?.Invoke(status, pending); }
        catch { /* UI notification failures must not terminate the speech worker. */ }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (stopping) return;
            stopping = true;
            queue.Clear();
        }

        signal.Set();
        if (worker.Join(TimeSpan.FromSeconds(2)))
            signal.Dispose();
    }

    public LocalSpeechQueue()
    {
        worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "VTuberBot local speech queue"
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }
}
