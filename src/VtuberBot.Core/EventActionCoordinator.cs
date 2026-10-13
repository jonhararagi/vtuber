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
    EventReceived, DuplicateDiscarded, RuleDisabled, AutomationsPaused, ConditionNotMet,
    ActionQueued, RequestAccepted, ActionRejected, OutcomeUnknown, HotkeySent,
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
    string? ProviderRequestId = null);

public sealed record EventActionActivity(
    DateTimeOffset At,
    EventActionOutcome Outcome,
    Guid? RuleId,
    string Source,
    string EventType,
    string Destination,
    string Message,
    string CorrelationId);

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
    private int disposed;

    public event Action<EventActionActivity>? ActivityChanged;
    public event Action<string>? Warning;

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
        get => settings.GetSetting(PausedSettingKey, "true") != "false";
        set => settings.SetSetting(PausedSettingKey, value ? "true" : "false");
    }

    public bool AutomaticPngtuberHotkeysAllowed
    {
        get => settings.GetSetting(PngtuberAutomaticHotkeysSettingKey, "false") == "true";
        set => settings.SetSetting(PngtuberAutomaticHotkeysSettingKey, value ? "true" : "false");
    }

    public int GlobalMaxExecutionsPerMinute
    {
        get => int.TryParse(settings.GetSetting(GlobalLimitSettingKey, options.GlobalMaxExecutionsPerMinute.ToString()), out var value)
            ? Math.Clamp(value, 1, 1000) : options.GlobalMaxExecutionsPerMinute;
        set
        {
            if (value is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(value));
            settings.SetSetting(GlobalLimitSettingKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public IReadOnlyList<EventActionRule> GetRules()
    {
        lock (rulesGate) return rules.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<EventActionActivity> GetRecentActivity()
    {
        lock (activity) return activity.ToArray();
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
            lastExecution.Remove(id);
            ruleExecutions.Remove(id);
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
                Record(new(DateTimeOffset.UtcNow, EventActionOutcome.EventReceived, null,
                    evt.Source, evt.Category + "." + evt.Type, "", "Evento recibido y encolado.", evt.CorrelationId));
                return true;
            }
            Record(new(DateTimeOffset.UtcNow, EventActionOutcome.QueueSaturated, null, evt.Source,
                evt.Category + "." + evt.Type, "", "Cola saturada; el evento se descartó sin bloquear al productor.", evt.CorrelationId));
            Warning?.Invoke("Cola de automatizaciones saturada; se descartó un evento. Revisá los límites y la actividad.");
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
                "present" => value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined,
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
        while (globalExecutions.Count > 0 && now - globalExecutions.Peek() >= TimeSpan.FromMinutes(1)) globalExecutions.Dequeue();
    }

    private static string BuildEventKey(NormalizedBotEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.ProviderEventId))
            return "id:" + evt.Source.ToLowerInvariant() + ":" + evt.ProviderEventId;
        // Without a reliable provider ID, only timestamped events with identical payloads are deduplicated.
        // Chat-like events lacking a timestamp remain distinct, even if user and text are identical.
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
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }
        return true;
    }

    private static void ValidateEvent(NormalizedBotEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (string.IsNullOrWhiteSpace(evt.Source) || evt.Source.Length > 80 ||
            string.IsNullOrWhiteSpace(evt.Category) || evt.Category.Length > 80 ||
            string.IsNullOrWhiteSpace(evt.Type) || evt.Type.Length > 120 ||
            evt.Payload.ValueKind == JsonValueKind.Undefined || evt.CausalDepth is < 0 or > 32)
            throw new ArgumentException("El evento normalizado no cumple el contrato.");
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
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Se esperaba una lista.");
            var loaded = JsonSerializer.Deserialize<List<EventActionRule>>(json) ?? [];
            foreach (var rule in loaded.Take(MaxRules))
            {
                try
                {
                    ValidateRule(rule);
                    rules[rule.Id] = rule;
                }
                catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
                {
                    Warning?.Invoke("Una regla guardada no es válida y fue omitida/desactivada de forma segura.");
                }
            }
        }
        catch (JsonException)
        {
            Warning?.Invoke("La configuración de reglas está dañada; se conservó en SQLite y el coordinador inició sin reglas.");
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
