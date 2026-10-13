using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace VtuberBot.Core;

public enum NormalizedEventOrigin { Internal, External }
public enum EventActionDestination { StreamerBot, PngtuberPlus }
public enum EventActionOutcome
{
    EventReceived, DuplicateDiscarded, RuleDisabled, RuleChanged, AutomationsPaused, ConditionNotMet,
    ActionQueued, ActionSent, RequestAccepted, ActionRejected, OutcomeUnknown, HotkeySent,
    InternalError, QueueSaturated, InvalidDestination, Cooldown, RateLimited, CausalDepthLimit
}

public sealed record NormalizedBotEvent(
    string Source,
    string Category,
    string Type,
    string? ProviderEventId,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? OriginalTimestamp,
    JsonElement Payload,
    string CorrelationId,
    NormalizedEventOrigin Origin,
    string? CausationId = null,
    int CausalDepth = 0)
{
    public static NormalizedBotEvent Create(
        string source, string category, string type, JsonElement payload,
        string? providerEventId = null, DateTimeOffset? originalTimestamp = null,
        NormalizedEventOrigin origin = NormalizedEventOrigin.External,
        string? correlationId = null, string? causationId = null, int causalDepth = 0)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(type) || payload.ValueKind is JsonValueKind.Undefined)
            throw new ArgumentException("La fuente, categoría, tipo y payload JSON son obligatorios.");
        return new(source.Trim(), category.Trim(), type.Trim(), string.IsNullOrWhiteSpace(providerEventId) ? null : providerEventId.Trim(),
            DateTimeOffset.UtcNow, originalTimestamp, payload.Clone(), correlationId ?? Guid.NewGuid().ToString("N"),
            origin, causationId, Math.Clamp(causalDepth, 0, 32));
    }
}

public sealed record EventActionCondition(string FieldPath, string Operator, string ExpectedValue);

public sealed record EventActionRule(
    Guid Id,
    string Name,
    bool Enabled,
    string Source,
    string Category,
    string EventType,
    IReadOnlyList<EventActionCondition> Conditions,
    EventActionDestination Destination,
    string TargetId,
    int CooldownSeconds = 0,
    int MaxExecutionsPerMinute = 10,
    bool ExternalActionConfirmed = false,
    string? ConfirmedTargetId = null,
    int SchemaVersion = 1);

public sealed record EventActionExecutionResult(
    EventActionOutcome Outcome,
    string Message,
    string? ProviderRequestId = null,
    bool RequestSent = false);

public sealed record EventActionActivity(
    DateTimeOffset At,
    EventActionOutcome Outcome,
    Guid? RuleId,
    string Source,
    string EventType,
    string Destination,
    string Message,
    string CorrelationId,
    long Sequence = 0);

public interface IEventActionDispatcher
{
    Task<EventActionExecutionResult> DispatchAsync(EventActionRule rule, NormalizedBotEvent evt, CancellationToken cancellationToken);
}

public interface INormalizedEventPublisher
{
    bool TryPublish(NormalizedBotEvent evt);
}

public sealed record EventActionCoordinatorOptions(
    int QueueCapacity = 256,
    int GlobalMaxExecutionsPerMinute = 60,
    int MaxCausalDepth = 4,
    TimeSpan? DeduplicationWindow = null);

/// <summary>
/// Single-reader, bounded event coordinator. It stores rule/config JSON through the existing
/// product settings adapter, serializes rule execution, and never executes payload-supplied targets.
/// </summary>
public sealed class EventActionCoordinator : INormalizedEventPublisher, IAsyncDisposable
{
    public const string RulesSettingKey = "automation.eventActions.rules.v1";
    public const string PausedSettingKey = "automation.eventActions.paused";
    public const string PngtuberAutomaticHotkeysSettingKey = "automation.eventActions.pngtuberHotkeysAllowed";
    public const string GlobalLimitSettingKey = "automation.eventActions.globalPerMinute";
    private const int MaxRules = 200;
    private const int MaxActivity = 500;
    private const int MaxPayloadCharacters = 65536;
    private readonly IProductSettingsStore settings;
    private readonly IEventActionDispatcher dispatcher;
    private readonly EventActionCoordinatorOptions options;
    private readonly Channel<NormalizedBotEvent> queue;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task worker;
    private readonly object rulesGate = new();
    private readonly Dictionary<Guid, EventActionRule> rules = [];
    private readonly ConcurrentDictionary<string, DateTimeOffset> seenEvents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> ruleEventRuns = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, DateTimeOffset> lastExecution = [];
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> ruleExecutions = [];
    private readonly Queue<DateTimeOffset> globalExecutions = new();
    private readonly Queue<EventActionActivity> activity = new();
    private readonly List<string> startupWarnings = [];
    private int automationsPaused;
    private int automaticPngtuberHotkeysAllowed;
    private int globalMaxExecutionsPerMinute;
    private int queueSaturationWarningPending;
    private long activitySequence;
    private int disposed;

    public event Action<EventActionActivity>? ActivityChanged;
    public event Action<string>? Warning;
    public IReadOnlyList<string> StartupWarnings { get { lock (startupWarnings) return startupWarnings.ToArray(); } }

    private void AddStartupWarning(string message)
    {
        lock (startupWarnings) startupWarnings.Add(message);
        try { Warning?.Invoke(message); } catch { }
    }

    public EventActionCoordinator(IProductSettingsStore settings, IEventActionDispatcher dispatcher,
        EventActionCoordinatorOptions? options = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.options = options ?? new EventActionCoordinatorOptions();
        if (this.options.QueueCapacity is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(options), "La capacidad de cola debe estar entre 1 y 10000.");
        if (this.options.GlobalMaxExecutionsPerMinute is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), "El límite global debe estar entre 1 y 1000.");
        automationsPaused = settings.GetSetting(PausedSettingKey, "true") != "false" ? 1 : 0;
        automaticPngtuberHotkeysAllowed = settings.GetSetting(PngtuberAutomaticHotkeysSettingKey, "false") == "true" ? 1 : 0;
        globalMaxExecutionsPerMinute = int.TryParse(settings.GetSetting(GlobalLimitSettingKey,
            this.options.GlobalMaxExecutionsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture)), out var configuredLimit)
            ? Math.Clamp(configuredLimit, 1, 1000) : this.options.GlobalMaxExecutionsPerMinute;
        queue = Channel.CreateBounded<NormalizedBotEvent>(new BoundedChannelOptions(this.options.QueueCapacity)
        {
            SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        LoadRulesSafely();
        worker = Task.Run(ProcessQueueAsync);
    }

    public bool AutomationsPaused
    {
        get => Volatile.Read(ref automationsPaused) != 0;
        set
        {
            settings.SetSetting(PausedSettingKey, value ? "true" : "false");
            Volatile.Write(ref automationsPaused, value ? 1 : 0);
        }
    }

    public bool AutomaticPngtuberHotkeysAllowed
    {
        get => Volatile.Read(ref automaticPngtuberHotkeysAllowed) != 0;
        set
        {
            settings.SetSetting(PngtuberAutomaticHotkeysSettingKey, value ? "true" : "false");
            Volatile.Write(ref automaticPngtuberHotkeysAllowed, value ? 1 : 0);
        }
    }

    public int GlobalMaxExecutionsPerMinute
    {
        get => Volatile.Read(ref globalMaxExecutionsPerMinute);
        set
        {
            if (value is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(value));
            settings.SetSetting(GlobalLimitSettingKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Volatile.Write(ref globalMaxExecutionsPerMinute, value);
        }
    }

    public IReadOnlyList<EventActionRule> GetRules()
    {
        lock (rulesGate) return rules.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<EventActionActivity> GetRecentActivity()
    {
        lock (activity) return activity.OrderBy(item => item.Sequence).ToArray();
    }

    public EventActionRule SaveRule(EventActionRule rule)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(rule);
        ValidateRule(rule);
        lock (rulesGate)
        {
            if (!rules.ContainsKey(rule.Id) && rules.Count >= MaxRules)
                throw new InvalidOperationException("El coordinador admite como máximo 200 reglas.");
            rules[rule.Id] = rule with { Name = rule.Name.Trim(), Source = rule.Source.Trim(),
                Category = rule.Category.Trim(), EventType = rule.EventType.Trim(), TargetId = rule.TargetId.Trim(),
                Conditions = rule.Conditions.ToArray(), SchemaVersion = 1 };
            PersistRulesLocked();
            return rules[rule.Id];
        }
    }

    public bool RemoveRule(Guid id)
    {
        ThrowIfDisposed();
        lock (rulesGate)
        {
            if (!rules.Remove(id)) return false;
            PersistRulesLocked();
            return true;
        }
    }

    public bool TryPublish(NormalizedBotEvent evt)
    {
        if (Volatile.Read(ref disposed) != 0) return false;
        try
        {
            ValidateEvent(evt);
            if (queue.Writer.TryWrite(evt))
            {
                Interlocked.Exchange(ref queueSaturationWarningPending, 0);
                Record(new(DateTimeOffset.UtcNow, EventActionOutcome.EventReceived, null,
                    evt.Source, evt.Category + "." + evt.Type, "", "Evento recibido y encolado.", evt.CorrelationId));
                return true;
            }
            // Report once per saturation burst; repeated drops must not flood SQLite or the UI dispatcher.
            if (Interlocked.Exchange(ref queueSaturationWarningPending, 1) == 0)
            {
                Record(new(DateTimeOffset.UtcNow, EventActionOutcome.QueueSaturated, null, evt.Source,
                    evt.Category + "." + evt.Type, "", "Cola saturada; se descartaron eventos sin bloquear al productor.", evt.CorrelationId));
                try { Warning?.Invoke("Cola de automatizaciones saturada; se descartaron eventos. Revisá los límites y la actividad."); } catch { }
            }
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
        {
            Record(new(DateTimeOffset.UtcNow, EventActionOutcome.InternalError, null, evt?.Source ?? "unknown",
                evt is null ? "unknown" : evt.Category + "." + evt.Type, "", "Evento malformado rechazado (" + ex.GetType().Name + ").",
                evt?.CorrelationId ?? ""));
            return false;
        }
    }

    public static bool MatchesConditions(NormalizedBotEvent evt, IReadOnlyList<EventActionCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!TryReadField(evt.Payload, condition.FieldPath, out var value)) return false;
            var actual = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => "null",
                _ => value.GetRawText()
            };
            var expected = condition.ExpectedValue;
            var matched = condition.Operator switch
            {
                "eq" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "contains" when value.ValueKind == JsonValueKind.String => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
                "present" => true,
                _ => false
            };
            if (!matched) return false;
        }
        return true;
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var evt in queue.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                try { await ProcessEventAsync(evt, lifetime.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Record(new(DateTimeOffset.UtcNow, EventActionOutcome.InternalError, null, evt.Source,
                        evt.Category + "." + evt.Type, "", "Error aislado al procesar evento (" + ex.GetType().Name + ").", evt.CorrelationId));
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private async Task ProcessEventAsync(NormalizedBotEvent evt, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        Prune(now);
        var eventKey = BuildEventKey(evt);
        var dedupeWindow = options.DeduplicationWindow ?? TimeSpan.FromSeconds(3);
        if (seenEvents.TryGetValue(eventKey, out var seenAt) && now - seenAt <= dedupeWindow)
        {
            Record(new(now, EventActionOutcome.DuplicateDiscarded, null, evt.Source, evt.Category + "." + evt.Type, "",
                "Reentrega del mismo evento descartada dentro de la ventana de deduplicación.", evt.CorrelationId));
            return;
        }
        seenEvents[eventKey] = now;

        if (evt.CausalDepth >= options.MaxCausalDepth)
        {
            Record(new(now, EventActionOutcome.CausalDepthLimit, null, evt.Source, evt.Category + "." + evt.Type, "",
                "Cadena causal limitada para evitar ciclos.", evt.CorrelationId));
            return;
        }

        EventActionRule[] snapshot;
        lock (rulesGate) snapshot = rules.Values.ToArray();
        foreach (var rule in snapshot)
        {
            if (!string.Equals(rule.Source, evt.Source, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rule.Category, evt.Category, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rule.EventType, evt.Type, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!rule.Enabled)
            {
                RecordActivity(rule, evt, EventActionOutcome.RuleDisabled, "Regla desactivada; no se ejecutó ninguna acción.");
                continue;
            }
            if (AutomationsPaused)
            {
                RecordActivity(rule, evt, EventActionOutcome.AutomationsPaused, "Automatizaciones pausadas globalmente.");
                continue;
            }
            if (rule.Conditions.Count > 0 && !MatchesConditions(evt, rule.Conditions))
            {
                RecordActivity(rule, evt, EventActionOutcome.ConditionNotMet, "Las condiciones configuradas no se cumplieron.");
                continue;
            }
            if (rule.Destination == EventActionDestination.PngtuberPlus && !AutomaticPngtuberHotkeysAllowed)
            {
                RecordActivity(rule, evt, EventActionOutcome.InvalidDestination,
                    "Atajo automático bloqueado: el envío de teclas no está autorizado globalmente.");
                continue;
            }
            if (rule.Destination == EventActionDestination.StreamerBot &&
                (!rule.ExternalActionConfirmed || !string.Equals(rule.ConfirmedTargetId, rule.TargetId, StringComparison.OrdinalIgnoreCase)))
            {
                RecordActivity(rule, evt, EventActionOutcome.InvalidDestination,
                    "Acción externa bloqueada: falta la confirmación explícita del destino.");
                continue;
            }

            // Rules are immutable snapshots. If the user edits/deletes one while this event is queued,
            // do not dispatch using a stale destination or a half-obsolete rule.
            lock (rulesGate)
            {
                if (!rules.TryGetValue(rule.Id, out var currentRule) || currentRule != rule)
                {
                    RecordActivity(rule, evt, EventActionOutcome.RuleChanged, "La regla cambió o se eliminó mientras el evento esperaba; se omitió esta ejecución.");
                    continue;
                }
            }
            if (AutomationsPaused)
            {
                RecordActivity(rule, evt, EventActionOutcome.AutomationsPaused, "Las automatizaciones se pausaron antes del despacho.");
                continue;
            }

            var ruleEventKey = rule.Id.ToString("N") + ":" + eventKey;
            if (ruleEventRuns.ContainsKey(ruleEventKey))
            {
                RecordActivity(rule, evt, EventActionOutcome.DuplicateDiscarded, "Esta regla ya procesó esta entrega del evento.");
                continue;
            }
            if (lastExecution.TryGetValue(rule.Id, out var last) && now - last < TimeSpan.FromSeconds(rule.CooldownSeconds))
            {
                RecordActivity(rule, evt, EventActionOutcome.Cooldown, "La regla sigue dentro de su cooldown.");
                continue;
            }
            var perRule = GetRuleQueue(rule.Id);
            while (perRule.Count > 0 && now - perRule.Peek() >= TimeSpan.FromMinutes(1)) perRule.Dequeue();
            if (perRule.Count >= rule.MaxExecutionsPerMinute)
            {
                RecordActivity(rule, evt, EventActionOutcome.RateLimited, "Se alcanzó el límite por minuto de esta regla.");
                continue;
            }
            while (globalExecutions.Count > 0 && now - globalExecutions.Peek() >= TimeSpan.FromMinutes(1)) globalExecutions.Dequeue();
            if (globalExecutions.Count >= GlobalMaxExecutionsPerMinute)
            {
                RecordActivity(rule, evt, EventActionOutcome.RateLimited, "Se alcanzó el límite global por minuto.");
                continue;
            }

            ruleEventRuns[ruleEventKey] = now;
            RecordActivity(rule, evt, EventActionOutcome.ActionQueued, "Acción validada y encolada para ejecución.");
            EventActionExecutionResult result;
            try { result = await dispatcher.DispatchAsync(rule, evt, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (TimeoutException)
            {
                result = new(EventActionOutcome.OutcomeUnknown, "Tiempo de espera agotado; el efecto remoto puede ser desconocido.");
            }
            catch (Exception ex)
            {
                result = new(EventActionOutcome.InternalError, "El adaptador falló (" + ex.GetType().Name + ").");
            }
            if (result.RequestSent)
                RecordActivity(rule, evt, EventActionOutcome.ActionSent, "El adaptador envió la solicitud; todavía no implica que el efecto posterior haya ocurrido.");
            if (result.Outcome is EventActionOutcome.RequestAccepted or EventActionOutcome.ActionRejected or
                EventActionOutcome.OutcomeUnknown or EventActionOutcome.HotkeySent)
            {
                lastExecution[rule.Id] = now;
                perRule.Enqueue(now);
                globalExecutions.Enqueue(now);
            }
            RecordActivity(rule, evt, result.Outcome, result.Message);
        }
    }

    private void RecordActivity(EventActionRule rule, NormalizedBotEvent evt, EventActionOutcome outcome, string message) =>
        Record(new(DateTimeOffset.UtcNow, outcome, rule.Id, evt.Source, evt.Category + "." + evt.Type,
            rule.Destination.ToString(), message, evt.CorrelationId));

    private void Record(EventActionActivity entry)
    {
        entry = entry with { Sequence = Interlocked.Increment(ref activitySequence) };
        lock (activity)
        {
            activity.Enqueue(entry);
            while (activity.Count > MaxActivity) activity.Dequeue();
        }
        try { ActivityChanged?.Invoke(entry); } catch { /* UI observers cannot break event processing. */ }
    }

    private Queue<DateTimeOffset> GetRuleQueue(Guid id)
    {
        if (!ruleExecutions.TryGetValue(id, out var value)) ruleExecutions[id] = value = new Queue<DateTimeOffset>();
        return value;
    }

    private void Prune(DateTimeOffset now)
    {
        var dedupeWindow = options.DeduplicationWindow ?? TimeSpan.FromSeconds(3);
        foreach (var pair in seenEvents)
            if (now - pair.Value > dedupeWindow && seenEvents.TryRemove(pair.Key, out _)) { }
        foreach (var pair in ruleEventRuns)
            if (now - pair.Value > dedupeWindow && ruleEventRuns.TryRemove(pair.Key, out _)) { }
        // A burst can contain many unique IDs inside the expiry window; cap the dedupe memory as well.
        if (seenEvents.Count > 5000)
            foreach (var pair in seenEvents.OrderBy(x => x.Value).Take(seenEvents.Count - 5000))
                seenEvents.TryRemove(pair.Key, out _);
        if (ruleEventRuns.Count > 10000)
            foreach (var pair in ruleEventRuns.OrderBy(x => x.Value).Take(ruleEventRuns.Count - 10000))
                ruleEventRuns.TryRemove(pair.Key, out _);
        HashSet<Guid> activeRuleIds;
        lock (rulesGate) activeRuleIds = rules.Keys.ToHashSet();
        foreach (var id in lastExecution.Keys.Where(id => !activeRuleIds.Contains(id)).ToArray()) lastExecution.Remove(id);
        foreach (var id in ruleExecutions.Keys.Where(id => !activeRuleIds.Contains(id)).ToArray()) ruleExecutions.Remove(id);
        while (globalExecutions.Count > 0 && now - globalExecutions.Peek() >= TimeSpan.FromMinutes(1)) globalExecutions.Dequeue();
    }

    private static string BuildEventKey(NormalizedBotEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.ProviderEventId))
            return "id:" + evt.Source.ToLowerInvariant() + ":" + evt.ProviderEventId;
        // Chat messages may legitimately repeat with identical text and coarse timestamps. Without a
        // provider ID, keep them distinct rather than silently dropping a real viewer message.
        if (evt.Type.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
            evt.Type.Equals("message", StringComparison.OrdinalIgnoreCase))
            return "unique:" + evt.CorrelationId;
        // For non-chat events, only timestamped events with identical payloads use the fallback key.
        if (evt.OriginalTimestamp is not { } timestamp)
            return "unique:" + evt.CorrelationId;
        var stable = evt.Source.ToLowerInvariant() + "|" + evt.Category.ToLowerInvariant() + "|" +
            evt.Type.ToLowerInvariant() + "|" + timestamp.ToUniversalTime().ToString("O") + "|" + evt.Payload.GetRawText();
        return "fallback:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stable)));
    }

    private static bool TryReadField(JsonElement payload, string fieldPath, out JsonElement value)
    {
        value = payload;
        foreach (var segment in fieldPath.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var child))
                return false;
            value = child;
        }
        return true;
    }

    private static void ValidateEvent(NormalizedBotEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (string.IsNullOrWhiteSpace(evt.Source) || evt.Source.Length > 80 ||
            string.IsNullOrWhiteSpace(evt.Category) || evt.Category.Length > 80 ||
            string.IsNullOrWhiteSpace(evt.Type) || evt.Type.Length > 120 ||
            evt.Payload.ValueKind != JsonValueKind.Object || evt.CausalDepth is < 0 or > 32)
            throw new ArgumentException("El evento normalizado debe incluir fuente, categoría, tipo y un payload JSON objeto.");
        if (evt.Payload.GetRawText().Length > MaxPayloadCharacters)
            throw new ArgumentException("El payload del evento supera el límite de 64 KiB.");
    }

    private static void ValidateRule(EventActionRule rule)
    {
        if (rule.Id == Guid.Empty || string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(rule.Source) || rule.Source.Length > 80 ||
            string.IsNullOrWhiteSpace(rule.Category) || rule.Category.Length > 80 ||
            string.IsNullOrWhiteSpace(rule.EventType) || rule.EventType.Length > 120 ||
            string.IsNullOrWhiteSpace(rule.TargetId) || rule.TargetId.Length > 200 ||
            rule.CooldownSeconds is < 0 or > 86400 || rule.MaxExecutionsPerMinute is < 1 or > 100 ||
            rule.SchemaVersion != 1 || rule.Conditions.Count > 12)
            throw new ArgumentException("La regla contiene campos fuera de los límites admitidos.");
        foreach (var condition in rule.Conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.FieldPath) || condition.FieldPath.Length > 180 ||
                !Regex.IsMatch(condition.FieldPath, @"^[A-Za-z0-9_.-]+$") ||
                condition.FieldPath.Split('.').Any(x => x.Length == 0) ||
                condition.Operator is not ("eq" or "contains" or "present") ||
                condition.ExpectedValue is null || condition.ExpectedValue.Length > 500)
                throw new ArgumentException("Una condición contiene una ruta, operador o valor no permitido.");
        }
        if (!Enum.IsDefined(rule.Destination)) throw new ArgumentException("El destino de la regla no es válido.");
    }

    private void LoadRulesSafely()
    {
        var json = settings.GetSetting(RulesSettingKey);
        if (string.IsNullOrWhiteSpace(json)) return;
        var foundInvalidRule = false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Se esperaba una lista de reglas.");
            foreach (var element in document.RootElement.EnumerateArray().Take(MaxRules))
            {
                try
                {
                    var rule = element.Deserialize<EventActionRule>();
                    if (rule is null) throw new JsonException("Regla vacía.");
                    ValidateRule(rule);
                    rules[rule.Id] = rule;
                }
                catch (Exception ex) when (ex is ArgumentException or JsonException or NullReferenceException or NotSupportedException)
                {
                    foundInvalidRule = true;
                    AddStartupWarning("Una regla guardada dañada/no compatible se omitió. Por seguridad, todas las automatizaciones quedaron pausadas; la configuración original se conserva en SQLite.");
                }
            }
            if (foundInvalidRule)
                PauseAfterDamagedConfiguration();
        }
        catch (JsonException)
        {
            PauseAfterDamagedConfiguration();
            AddStartupWarning("La configuración de reglas está dañada; las automatizaciones se pausaron y no se cargaron reglas. La configuración original se conserva en SQLite.");
        }
    }

    private void PauseAfterDamagedConfiguration()
    {
        // Fail closed even if persisting the safety switch itself fails.
        Volatile.Write(ref automationsPaused, 1);
        try
        {
            settings.SetSetting(PausedSettingKey, "true");
        }
        catch (Exception ex)
        {
            AddStartupWarning("No se pudo persistir la pausa global tras detectar configuración dañada (" + ex.GetType().Name + "); la pausa sigue activa durante esta sesión.");
        }
    }

    private void PersistRulesLocked() => settings.SetSetting(RulesSettingKey, JsonSerializer.Serialize(rules.Values.ToArray()));

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        queue.Writer.TryComplete();
        lifetime.Cancel();
        try { await worker.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        lifetime.Dispose();
    }
}
