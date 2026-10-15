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
public sealed record LocalSpeechOutput(string Id, string Name);
public sealed record SpeechPlaybackResult(bool UsedFallback, bool Cancelled);

/// <summary>Abstraction for local speech playback; injectable so queue cancellation is testable without SAPI.</summary>
public interface ILocalSpeechSession : IDisposable
{
    SpeechPlaybackResult Speak(
        string text,
        string requestedVoiceId,
        string requestedOutputId,
        int rate,
        int volume,
        Func<bool> cancellationRequested);
}

/// <summary>A bounded, priority-ordered SAPI queue that keeps speech off the WPF dispatcher.</summary>
public sealed class LocalSpeechQueue : IDisposable
{
    private sealed record SpeechItem(string Text, SpeechPriority Priority, long Sequence, string VoiceId, string OutputId, int Rate, int Volume);

    private sealed class SapiSession : ILocalSpeechSession
    {
        private readonly object voice;
        private readonly Type voiceType;
        private object? defaultVoiceToken;
        private string? defaultVoiceId;
        private object? defaultOutputToken;
        private readonly Dictionary<string, object> outputTokens = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> tokens = new(StringComparer.OrdinalIgnoreCase);

        private SapiSession(object voice, Type voiceType)
        {
            this.voice = voice;
            this.voiceType = voiceType;
        }

        public static ILocalSpeechSession Create()
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
                try
                {
                    session.defaultOutputToken = type.InvokeMember("AudioOutput", BindingFlags.GetProperty, null, voice, null);
                    var outputCollection = type.InvokeMember(
                        "GetAudioOutputs",
                        BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
                        null, voice, new object[] { Type.Missing, Type.Missing })
                        ?? throw new InvalidOperationException("SAPI no devolvió la lista de salidas de audio.");
                    try
                    {
                        var outputCollectionType = outputCollection.GetType();
                        var outputCount = Convert.ToInt32(outputCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, outputCollection, null), System.Globalization.CultureInfo.InvariantCulture);
                        for (var index = 0; index < outputCount; index++)
                        {
                            object? token = null;
                            try
                            {
                                token = outputCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, outputCollection, new object[] { index });
                                var id = ReadTokenId(token);
                                if (!string.IsNullOrWhiteSpace(id) && token is not null) session.outputTokens[id] = token;
                                else ReleaseVoice(token);
                            }
                            catch { ReleaseVoice(token); throw; }
                        }
                    }
                    finally { ReleaseVoice(outputCollection); }
    
                }
                catch
                {
                    // Output enumeration is optional: retain SAPI's default device if it fails.
                    foreach (var outputToken in session.outputTokens.Values)
                        if (!ReferenceEquals(outputToken, session.defaultOutputToken)) ReleaseVoice(outputToken);
                    session.outputTokens.Clear();
                }
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

        public SpeechPlaybackResult Speak(
            string text,
            string requestedVoiceId,
            string requestedOutputId,
            int rate,
            int volume,
            Func<bool> cancellationRequested)
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
            object? outputToken = defaultOutputToken;
            if (!string.IsNullOrWhiteSpace(requestedOutputId))
            {
                if (outputTokens.TryGetValue(requestedOutputId, out var selectedOutput)) outputToken = selectedOutput;
                else useFallback = true;
            }
            if (outputToken is not null)
                voiceType.InvokeMember("AudioOutput", BindingFlags.SetProperty, null, voice, new[] { outputToken });
            voiceType.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, new object[] { Math.Clamp(rate, -10, 10) });
            voiceType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { Math.Clamp(volume, 0, 100) });
            if (cancellationRequested())
                return new SpeechPlaybackResult(useFallback, Cancelled: true);

            // Asynchronous SAPI playback lets the STA worker observe cancellation instead of
            // blocking inside Speak until the whole phrase has finished.
            voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { text, 1 });
            while (true)
            {
                if (cancellationRequested())
                {
                    voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { string.Empty, 2 });
                    _ = voiceType.InvokeMember("WaitUntilDone", BindingFlags.InvokeMethod, null, voice, new object[] { 1000 });
                    return new SpeechPlaybackResult(useFallback, Cancelled: true);
                }

                var completed = voiceType.InvokeMember("WaitUntilDone", BindingFlags.InvokeMethod, null, voice, new object[] { 100 });
                if (completed is bool done && done)
                    return new SpeechPlaybackResult(useFallback, Cancelled: false);
            }
        }

        public static IReadOnlyList<LocalSpeechOutput> EnumerateOutputs()
        {
            object? voice = null;
            object? collection = null;
            try
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice") ?? throw new InvalidOperationException("Windows SAPI no está disponible en este equipo.");
                voice = Activator.CreateInstance(type) ?? throw new InvalidOperationException("No se pudo crear el motor SAPI.");
                collection = type.InvokeMember("GetAudioOutputs", BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding, null, voice, new object[] { Type.Missing, Type.Missing })
                    ?? throw new InvalidOperationException("SAPI no devolvió la lista de salidas de audio.");
                var collectionType = collection.GetType();
                var count = Convert.ToInt32(collectionType.InvokeMember("Count", BindingFlags.GetProperty, null, collection, null), System.Globalization.CultureInfo.InvariantCulture);
                var outputs = new List<LocalSpeechOutput>();
                for (var index = 0; index < count; index++)
                {
                    object? token = null;
                    try
                    {
                        token = collectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, collection, new object[] { index });
                        if (token is null) continue;
                        var tokenType = token.GetType();
                        var id = tokenType.InvokeMember("Id", BindingFlags.GetProperty, null, token, null) as string ?? string.Empty;
                        var name = tokenType.InvokeMember("GetDescription", BindingFlags.InvokeMethod, null, token, new object[] { 0 }) as string ?? "Salida de Windows";
                        if (!string.IsNullOrWhiteSpace(id)) outputs.Add(new LocalSpeechOutput(id, name));
                    }
                    finally { ReleaseVoice(token); }
                }
                return outputs.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
            finally { ReleaseVoice(collection); ReleaseVoice(voice); }
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
            try
            {
                // Purge any asynchronous phrase before releasing COM references.
                voiceType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { string.Empty, 2 });
                _ = voiceType.InvokeMember("WaitUntilDone", BindingFlags.InvokeMethod, null, voice, new object[] { 1000 });
            }
            catch
            {
                // Best-effort cancellation during shutdown or after a SAPI device failure.
            }

            foreach (var token in tokens.Values)
                if (!ReferenceEquals(token, defaultVoiceToken)) ReleaseVoice(token);
            tokens.Clear();
            foreach (var token in outputTokens.Values)
                if (!ReferenceEquals(token, defaultOutputToken)) ReleaseVoice(token);
            outputTokens.Clear();
            ReleaseVoice(defaultOutputToken);
            defaultOutputToken = null;
            ReleaseVoice(defaultVoiceToken);
            defaultVoiceToken = null;
            ReleaseVoice(voice);
        }
    }

    private const int MaximumPending = 32;
    private readonly object gate = new();
    private readonly Func<ILocalSpeechSession> sessionFactory;
    private readonly PriorityQueue<SpeechItem, (int Priority, long Sequence)> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private readonly Thread worker;
    private long sequence;
    private bool stopping;
    private bool speaking;
    private bool cancelCurrentSpeech;
    private bool playbackStateActive;

    public event Action<string, int>? StatusChanged;
    /// <summary>Signals the lifetime of a queued speech request so the avatar follows synthetic speech.</summary>
    public event Action<bool>? PlaybackStateChanged;
    public int PendingCount { get { lock (gate) return queue.Count; } }
    public bool IsPlaybackActive { get { lock (gate) return playbackStateActive; } }

    public static IReadOnlyList<LocalSpeechVoice> GetInstalledVoices()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<LocalSpeechVoice>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(SapiSession.EnumerateVoices());
            }
            catch (Exception exception)
            {
                completion.TrySetException(new InvalidOperationException(
                    "No se pudieron enumerar las voces SAPI instaladas.", exception));
            }
        })
        {
            IsBackground = true,
            Name = "VTuberBot SAPI voice enumeration"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            // The STA worker may still be cleaning up COM after a timeout; a TCS avoids
            // disposing a wait handle that the worker could subsequently signal.
            throw new TimeoutException("La enumeración de voces de Windows superó los 5 segundos.");
        }
    }

    public static IReadOnlyList<LocalSpeechOutput> GetInstalledOutputs()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<LocalSpeechOutput>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(SapiSession.EnumerateOutputs()); }
            catch (Exception exception) { completion.TrySetException(new InvalidOperationException("No se pudieron enumerar las salidas SAPI instaladas.", exception)); }
        }) { IsBackground = true, Name = "VTuberBot SAPI output enumeration" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { return completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
        catch (TimeoutException) { throw new TimeoutException("La enumeración de salidas de Windows superó los 5 segundos."); }
    }

    public bool Enqueue(string text, SpeechPriority priority = SpeechPriority.Normal, string? voiceId = null, int rate = 0, int volume = 100, string? outputId = null)
    {
        try
        {
            text = VtuberBot.Core.SpeechRequestPolicy.NormalizeText(text);
        }
        catch (ArgumentException exception)
        {
            Notify(exception.Message, PendingCount);
            return false;
        }
        if (rate is < -10 or > 10 || volume is < 0 or > 100)
        {
            Notify("La velocidad debe estar entre -10 y 10 y el volumen entre 0 y 100.", PendingCount);
            return false;
        }

        int pending;
        string? rejection = null;
        lock (gate)
        {
            if (stopping || queue.Count + (speaking ? 1 : 0) >= MaximumPending)
            {
                pending = queue.Count;
                rejection = stopping
                    ? "La cola está cerrándose."
                    : "La cola llegó al límite de 32 frases.";
            }
            else
            {
                var item = new SpeechItem(text.Trim(), priority, ++sequence, voiceId?.Trim() ?? string.Empty, outputId?.Trim() ?? string.Empty, rate, volume);
                queue.Enqueue(item, ((int)priority, item.Sequence));
                pending = queue.Count;
            }
        }

        if (rejection is not null)
        {
            Notify(rejection, pending);
            return false;
        }

        signal.Set();
        Notify("Frase encolada con prioridad " + priority + ".", pending);
        return true;
    }

    public void ClearPending()
    {
        int pending;
        bool notifyPlaybackStopped = false;
        lock (gate)
        {
            queue.Clear();
            pending = queue.Count;
            if (!speaking && playbackStateActive)
            {
                playbackStateActive = false;
                notifyPlaybackStopped = true;
            }
        }
        signal.Set();
        if (notifyPlaybackStopped) NotifyPlaybackStateChanged(false);
        Notify("Se vaciaron las frases pendientes. La frase que está sonando no se interrumpe.", pending);
    }

    private void WorkerLoop()
    {
        ILocalSpeechSession? session = null;
        try
        {
            while (true)
            {
                SpeechItem? next = null;
                int pending;
                bool shouldStop;
                bool notifyPlaybackStarted = false;
                lock (gate)
                {
                    if (queue.Count > 0)
                    {
                        next = queue.Dequeue();
                        speaking = true;
                        if (!playbackStateActive)
                        {
                            playbackStateActive = true;
                            notifyPlaybackStarted = true;
                        }
                    }
                    shouldStop = stopping && next is null;
                    pending = queue.Count;
                }

                if (shouldStop) break;
                if (next is null) { signal.WaitOne(); continue; }
                if (notifyPlaybackStarted) NotifyPlaybackStateChanged(true);
                Notify("Reproduciendo frase encolada.", pending);
                try
                {
                    session ??= sessionFactory();
                    var result = session.Speak(next.Text, next.VoiceId, next.OutputId, next.Rate, next.Volume, IsSpeechCancellationRequested);
                    if (result.Cancelled) Notify("Reproducción cancelada.", PendingCount);
                    else
                    {
                        if (result.UsedFallback)
                            Notify("La voz o salida elegida ya no está disponible; se usó la configuración predeterminada de Windows.", PendingCount);
                        Notify("Reproducción finalizada.", PendingCount);
                    }
                }
                catch (Exception exception)
                {
                    try { session?.Dispose(); }
                    catch (Exception disposeException)
                    {
                        Notify("Error al liberar el motor TTS: " + disposeException.GetBaseException().Message, PendingCount);
                    }
                    session = null;
                    var actual = exception is TargetInvocationException invocation ? invocation.InnerException ?? exception : exception;
                    Notify("Error TTS local: " + actual.GetBaseException().Message, PendingCount);
                }
                finally
                {
                    bool notifyPlaybackStopped = false;
                    lock (gate)
                    {
                        speaking = false;
                        cancelCurrentSpeech = false;
                        if (playbackStateActive && queue.Count == 0)
                        {
                            playbackStateActive = false;
                            notifyPlaybackStopped = true;
                        }
                    }
                    if (notifyPlaybackStopped) NotifyPlaybackStateChanged(false);
                }
            }
        }
        finally
        {
            bool notifyPlaybackStopped = false;
            lock (gate)
            {
                speaking = false;
                if (playbackStateActive)
                {
                    playbackStateActive = false;
                    notifyPlaybackStopped = true;
                }
            }
            if (notifyPlaybackStopped) NotifyPlaybackStateChanged(false);
            try { session?.Dispose(); }
            catch (Exception exception)
            {
                Notify("Error al cerrar el motor TTS: " + exception.GetBaseException().Message, PendingCount);
            }
            Notify("Cola de voz cerrada.", PendingCount);
        }
    }

    private static void ReleaseVoice(object? value)
    {
        if (value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); } catch { }
    }

    private void Notify(string status, int pending) =>
        VtuberBot.Core.SafeEventDispatch.Invoke(StatusChanged, status, pending);

    private void NotifyPlaybackStateChanged(bool isActive) =>
        VtuberBot.Core.SafeEventDispatch.Invoke(PlaybackStateChanged, isActive);

    public bool StopCurrentSpeech()
    {
        int pending;
        lock (gate)
        {
            if (!speaking || stopping)
                return false;
            cancelCurrentSpeech = true;
            pending = queue.Count;
        }

        signal.Set();
        Notify("Se solicitó detener la frase actual.", pending);
        return true;
    }

    private bool IsSpeechCancellationRequested()
    {
        lock (gate)
            return stopping || cancelCurrentSpeech;
    }

    public void Dispose()
    {
        bool notifyPlaybackStopped = false;
        lock (gate)
        {
            if (stopping) return;
            stopping = true;
            cancelCurrentSpeech = true;
            queue.Clear();
            if (!speaking && playbackStateActive)
            {
                playbackStateActive = false;
                notifyPlaybackStopped = true;
            }
        }
        if (notifyPlaybackStopped) NotifyPlaybackStateChanged(false);
        signal.Set();
        if (worker.Join(TimeSpan.FromSeconds(2))) signal.Dispose();
    }

    internal static ILocalSpeechSession CreateSystemSpeechSession() => SapiSession.Create();

    public LocalSpeechQueue() : this(SapiSession.Create)
    {
    }

    public LocalSpeechQueue(Func<ILocalSpeechSession> sessionFactory)
    {
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        worker = new Thread(WorkerLoop) { IsBackground = true, Name = "VTuberBot local speech queue" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }
}
