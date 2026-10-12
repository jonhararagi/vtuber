using System.Reflection;
using System.Runtime.InteropServices;

namespace VtuberBot.App;

public enum SpeechPriority
{
    High = 0,
    Normal = 1,
    Low = 2
}

public sealed record LocalSpeechVoice(string Id, string Name);

/// <summary>A bounded, priority-ordered SAPI queue that keeps speech off the WPF dispatcher.</summary>
public sealed class LocalSpeechQueue : IDisposable
{
    private sealed record SpeechItem(string Text, SpeechPriority Priority, long Sequence, string VoiceId, int Rate, int Volume);

    private sealed class SapiSession : IDisposable
    {
        private readonly object voice;
        private readonly Type voiceType;
        private object? defaultVoiceToken;
        private string? defaultVoiceId;
        private readonly Dictionary<string, object> tokens = new(StringComparer.OrdinalIgnoreCase);

        private SapiSession(object voice, Type voiceType)
        {
            this.voice = voice;
            this.voiceType = voiceType;
        }

        public static SapiSession Create()
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice")
                ?? throw new InvalidOperationException("Windows SAPI no está disponible en este equipo.");
            var voice = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("No se pudo crear el motor SAPI.");
            var session = new SapiSession(voice, type);
            try
            {
                session.defaultVoiceToken = type.InvokeMember("Voice", BindingFlags.GetProperty, null, voice, null);
                session.defaultVoiceId = ReadTokenId(session.defaultVoiceToken);
                var collection = type.InvokeMember(
                    "GetVoices",
                    BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
                    null, voice, new object[] { Type.Missing, Type.Missing })
                    ?? throw new InvalidOperationException("SAPI no devolvió la lista de voces.");
                try
                {
                    var collectionType = collection.GetType();
                    var count = Convert.ToInt32(collectionType.InvokeMember("Count", BindingFlags.GetProperty, null, collection, null),
                        System.Globalization.CultureInfo.InvariantCulture);
                    for (var index = 0; index < count; index++)
                    {
                        object? token = null;
                        try
                        {
                            token = collectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { index });
                            var id = ReadTokenId(token);
                            if (!string.IsNullOrWhiteSpace(id) && token is not null)
                            {
                                if (session.tokens.TryGetValue(id, out var oldToken) && !ReferenceEquals(oldToken, session.defaultVoiceToken))
                                    ReleaseVoice(oldToken);
                                session.tokens[id] = token;
                            }
                            else ReleaseVoice(token);
                        }
                        catch
                        {
                            ReleaseVoice(token);
                            throw;
                        }
                    }
                }
                finally { ReleaseVoice(collection); }
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        public bool Speak(string text, string requestedVoiceId, int rate, int volume)
        {
            object? token = defaultVoiceToken;
            var useFallback = false;
            if (!string.IsNullOrWhiteSpace(requestedVoiceId))
            {
                if (tokens.TryGetValue(requestedVoiceId, out var selectedToken))
                {
                    token = selectedToken;
                }
                else
                {
                    useFallback = true;
                }
            }

            if (token is not null)
                voiceType.InvokeMember("Voice", BindingFlags.SetProperty, null, voice, new[] { token });
            voiceType.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, new object[] { Math.Clamp(rate, -10, 10) });
            voiceType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { Math.Clamp(volume, 0, 100) });
            voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { text, 0 });
            return useFallback;
        }

        public static IReadOnlyList<LocalSpeechVoice> EnumerateVoices()
        {
            object? voice = null;
            object? collection = null;
            try
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice")
                    ?? throw new InvalidOperationException("Windows SAPI no está disponible en este equipo.");
                voice = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("No se pudo crear el motor SAPI.");
                collection = type.InvokeMember(
                    "GetVoices",
                    BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
                    null, voice, new object[] { Type.Missing, Type.Missing })
                    ?? throw new InvalidOperationException("SAPI no devolvió la lista de voces.");
                var collectionType = collection.GetType();
                var count = Convert.ToInt32(collectionType.InvokeMember("Count", BindingFlags.GetProperty, null, collection, null),
                    System.Globalization.CultureInfo.InvariantCulture);
                var voices = new List<LocalSpeechVoice>();
                for (var index = 0; index < count; index++)
                {
                    object? token = null;
                    try
                    {
                        token = collectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { index });
                        if (token is null) continue;
                        var tokenType = token.GetType();
                        var id = tokenType.InvokeMember("Id", BindingFlags.GetProperty, null, token, null) as string ?? string.Empty;
                        var name = tokenType.InvokeMember("GetDescription", BindingFlags.InvokeMethod, null, token, new object[] { 0 }) as string
                            ?? "Voz de Windows";
                        if (!string.IsNullOrWhiteSpace(id)) voices.Add(new LocalSpeechVoice(id, name));
                    }
                    finally { ReleaseVoice(token); }
                }
                return voices.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
            finally
            {
                ReleaseVoice(collection);
                ReleaseVoice(voice);
            }
        }

        private static string? ReadTokenId(object? token) =>
            token?.GetType().InvokeMember("Id", BindingFlags.GetProperty, null, token, null) as string;

        public void Dispose()
        {
            foreach (var token in tokens.Values)
                if (!ReferenceEquals(token, defaultVoiceToken)) ReleaseVoice(token);
            tokens.Clear();
            ReleaseVoice(defaultVoiceToken);
            defaultVoiceToken = null;
            ReleaseVoice(voice);
        }
    }

    private const int MaximumPending = 32;
    private readonly object gate = new();
    private readonly PriorityQueue<SpeechItem, (int Priority, long Sequence)> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private readonly Thread worker;
    private long sequence;
    private bool stopping;
    private bool speaking;

    public event Action<string, int>? StatusChanged;
    public int PendingCount { get { lock (gate) return queue.Count; } }

    public static IReadOnlyList<LocalSpeechVoice> GetInstalledVoices()
    {
        Exception? failure = null;
        IReadOnlyList<LocalSpeechVoice>? result = null;
        using var finished = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try { result = SapiSession.EnumerateVoices(); }
            catch (Exception exception) { failure = exception; }
            finally { finished.Set(); }
        })
        {
            IsBackground = true,
            Name = "VTuberBot SAPI voice enumeration"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!finished.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("La enumeración de voces de Windows superó los 5 segundos.");
        if (failure is not null)
            throw new InvalidOperationException("No se pudieron enumerar las voces SAPI instaladas.", failure);
        return result ?? Array.Empty<LocalSpeechVoice>();
    }

    public bool Enqueue(string text, SpeechPriority priority = SpeechPriority.Normal, string? voiceId = null, int rate = 0, int volume = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Notify("Escribí un texto antes de encolarlo.", PendingCount);
            return false;
        }
        if (rate is < -10 or > 10 || volume is < 0 or > 100)
        {
            Notify("La velocidad debe estar entre -10 y 10 y el volumen entre 0 y 100.", PendingCount);
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
            var item = new SpeechItem(text.Trim(), priority, ++sequence, voiceId?.Trim() ?? string.Empty, rate, volume);
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
        lock (gate) { queue.Clear(); pending = queue.Count; }
        signal.Set();
        Notify("Se vaciaron las frases pendientes. La frase que está sonando no se interrumpe.", pending);
    }

    private void WorkerLoop()
    {
        SapiSession? session = null;
        try
        {
            while (true)
            {
                SpeechItem? next = null;
                int pending;
                bool shouldStop;
                lock (gate)
                {
                    if (queue.Count > 0) { next = queue.Dequeue(); speaking = true; }
                    shouldStop = stopping && next is null;
                    pending = queue.Count;
                }

                if (shouldStop) break;
                if (next is null) { signal.WaitOne(); continue; }

                Notify("Reproduciendo frase encolada.", pending);
                try
                {
                    session ??= SapiSession.Create();
                    if (session.Speak(next.Text, next.VoiceId, next.Rate, next.Volume))
                        Notify("La voz elegida ya no está instalada; se usó la voz predeterminada de Windows.", PendingCount);
                    Notify("Reproducción finalizada.", PendingCount);
                }
                catch (Exception exception)
                {
                    session?.Dispose();
                    session = null;
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
            session?.Dispose();
            Notify("Cola de voz cerrada.", PendingCount);
        }
    }

    private static void ReleaseVoice(object? value)
    {
        if (value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); } catch { }
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
        if (worker.Join(TimeSpan.FromSeconds(2))) signal.Dispose();
    }

    public LocalSpeechQueue()
    {
        worker = new Thread(WorkerLoop) { IsBackground = true, Name = "VTuberBot local speech queue" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }
}
