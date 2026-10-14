using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Channels;
using VtuberBot.Core;

namespace VtuberBot.App;

public enum ChatReactionMatchType { Exact, WholeWord, Contains }

public sealed record ChatReactionRule(
    string Id,
    string Name,
    string Trigger,
    ChatReactionMatchType MatchType,
    IReadOnlyList<string> Responses,
    bool Speak,
    string Expression,
    int GlobalCooldownSeconds,
    int UserCooldownSeconds,
    int MaxPerMinute,
    bool Enabled)
{
    public string Language { get; init; } = "auto";
    public string VoiceId { get; init; } = "";
    public string ClipAssetId { get; init; } = "";
}

public sealed record ChatReactionResult(
    DateTimeOffset At,
    string RuleName,
    string User,
    string Outcome,
    string Detail,
    bool Simulated);

/// <summary>
/// Bounded rule engine fed by the existing Twitch PRIVMSG pipeline. It never creates a
/// Twitch client, invokes AI, interprets regex, or runs user-supplied code.
/// </summary>
public sealed class ChatReactionService : IAsyncDisposable
{
    public const string RulesSettingKey = "chat.reactions.rules.v1";
    private const string BilingualDefaultsMigrationKey = "chat.reactions.bilingual-defaults.v1";
    public const string PausedSettingKey = "chat.reactions.paused";
    public const int MaximumRules = 100;
    public const int QueueCapacity = 64;
    public const int MaximumMessageLength = 1000;
    private readonly ProductStore store;
    private readonly Func<string, CancellationToken, Task<bool>> sendChat;
    private readonly Func<string, string, string, bool> enqueueSpeech;
    private readonly Func<string, CancellationToken, bool> setExpression;
    private readonly Func<string, CancellationToken, bool>? playClip;
    private readonly Func<bool> isConnected;
    private readonly Channel<WorkItem> queue;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private readonly object rulesGate = new();
    private readonly Dictionary<string, DateTimeOffset> globalCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> userCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<DateTimeOffset>> minuteRuns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> seenMessageIds = new(StringComparer.Ordinal);
    private readonly Queue<ChatReactionResult> activity = new();
    private CancellationTokenSource? activeAction;
    private Task worker;
    private List<ChatReactionRule> rules;
    private long connectionEpoch;
    private int responseIndex;
    private bool disposed;
    private bool twitchConnected;
    private bool paused;

    private sealed record WorkItem(long Epoch, string MessageId, string User, string Message, ChatReactionRule Rule, string Response, bool Simulated);

    public event Action<ChatReactionResult>? ResultChanged;
    public event Action<int>? QueueChanged;

    public ChatReactionService(ProductStore store, Func<string, CancellationToken, Task<bool>> sendChat,
        Func<string, string, string, bool> enqueueSpeech, Func<string, CancellationToken, bool> setExpression, Func<bool> isConnected,
        int queueCapacity = QueueCapacity, Func<string, CancellationToken, bool>? playClip = null)
    {
        this.store = store;
        this.sendChat = sendChat;
        this.enqueueSpeech = enqueueSpeech;
        this.setExpression = setExpression;
        this.isConnected = isConnected;
        this.playClip = playClip;
        rules = LoadRules();
        paused = store.GetSetting(PausedSettingKey, "false") == "true";
        queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(Math.Clamp(queueCapacity, 1, QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        worker = Task.Run(ProcessQueueAsync);
    }

    public bool IsPaused { get { lock (gate) return paused; } }
    public bool IsTwitchConnected { get { lock (gate) return twitchConnected; } }
    public int PendingCount => queue.Reader.Count;
    public IReadOnlyList<ChatReactionRule> GetRules() { lock (rulesGate) return rules.ToArray(); }
    public IReadOnlyList<ChatReactionResult> GetRecentActivity() { lock (rulesGate) return activity.ToArray(); }

    public bool SaveRule(ChatReactionRule rule, string? priorId = null)
    {
        if (!TryValidate(rule, out var validated, out var error))
        {
            Record(new(DateTimeOffset.UtcNow, rule.Name ?? "", "", "Regla inválida", error, true));
            return false;
        }
        lock (rulesGate)
        {
            var existingIndex = priorId is null ? -1 : rules.FindIndex(x => x.Id == priorId);
            if (existingIndex < 0 && rules.Count >= MaximumRules) return false;
            if (existingIndex < 0 && rules.Any(x => x.Id == validated.Id)) return false;
            if (existingIndex >= 0) rules[existingIndex] = validated with { Id = priorId! };
            else rules.Add(validated with { Enabled = false });
            PersistRulesLocked();
            return true;
        }
    }

    public bool SetRuleEnabled(string id, bool enabled)
    {
        lock (rulesGate)
        {
            var index = rules.FindIndex(x => x.Id == id);
            if (index < 0) return false;
            rules[index] = rules[index] with { Enabled = enabled };
            PersistRulesLocked();
            return true;
        }
    }

    public bool DeleteRule(string id)
    {
        lock (rulesGate)
        {
            var removed = rules.RemoveAll(x => x.Id == id) > 0;
            if (removed) PersistRulesLocked();
            return removed;
        }
    }

    public bool MoveRule(string id, int offset)
    {
        lock (rulesGate)
        {
            var index = rules.FindIndex(x => x.Id == id);
            var target = index + Math.Clamp(offset, -1, 1);
            if (index < 0 || target < 0 || target >= rules.Count) return false;
            (rules[index], rules[target]) = (rules[target], rules[index]);
            PersistRulesLocked();
            return true;
        }
    }

    public void SetPaused(bool value)
    {
        lock (gate) { paused = value; if (value) connectionEpoch++; }
        store.SetSetting(PausedSettingKey, value ? "true" : "false");
        if (value) InvalidatePending("reacciones pausadas");
    }

    public void SetTwitchConnected(bool connected)
    {
        lock (gate)
        {
            if (twitchConnected == connected) return;
            twitchConnected = connected;
            if (!connected) connectionEpoch++;
        }
        if (!connected) InvalidatePending("Twitch desconectado");
    }

    public bool OnChatMessage(string username, string message, string? messageId)
    {
        if (!TryAccept(username, message, messageId, false, out var work, out var result))
        {
            if (result is not null) Record(result);
            return false;
        }
        if (!queue.Writer.TryWrite(work!))
        {
            Record(new(DateTimeOffset.UtcNow, work!.Rule.Name, username, "Cola saturada", "Se descartó la reacción; la cola alcanzó su capacidad.", false));
            return false;
        }
        SafeEventDispatch.Invoke(QueueChanged, PendingCount);
        return true;
    }

    /// <summary>Offline dry-run: resolves matching and response variables, but performs no effects.</summary>
    public ChatReactionResult TestMessage(string username, string message)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength)
        {
            var invalid = new ChatReactionResult(DateTimeOffset.UtcNow, "", username ?? "", "Entrada inválida", "Usuario o mensaje vacío, o mensaje demasiado largo.", true);
            Record(invalid);
            return invalid;
        }
        ChatReactionRule? rule;
        lock (rulesGate) rule = rules.FirstOrDefault(x => x.Enabled && Matches(x, message)) ?? rules.FirstOrDefault(x => Matches(x, message));
        if (rule is null)
        {
            var unmatched = new ChatReactionResult(DateTimeOffset.UtcNow, "", username, "Sin coincidencia", "Ninguna regla activa coincide; no se envió nada a Twitch ni se activó voz o avatar.", true);
            Record(unmatched);
            return unmatched;
        }
        var response = ResolveResponse(rule, username);
        var result = new ChatReactionResult(DateTimeOffset.UtcNow, rule.Name, username, "Simulación local", $"Coincidiría con «{rule.Trigger}». Respuesta prevista: {response}. No se ejecutaron efectos.", true);
        Record(result);
        return result;
    }

    public static bool Matches(ChatReactionRule rule, string message)
    {
        if (string.IsNullOrWhiteSpace(rule.Trigger) || string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength)
            return false;
        var trigger = Normalize(rule.Trigger);
        var candidate = Normalize(message);
        return rule.MatchType switch
        {
            ChatReactionMatchType.Exact => string.Equals(candidate, trigger, StringComparison.Ordinal),
            ChatReactionMatchType.Contains => candidate.Contains(trigger, StringComparison.Ordinal),
            ChatReactionMatchType.WholeWord => ContainsWholeWord(candidate, trigger),
            _ => false
        };
    }

    private bool TryAccept(string username, string message, string? messageId, bool simulated,
        out WorkItem? work, out ChatReactionResult? result)
    {
        work = null; result = null;
        if (string.IsNullOrWhiteSpace(username) || username.Length > 25 || string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength)
        {
            result = new(DateTimeOffset.UtcNow, "", username ?? "", "Entrada inválida", "Se descartó un mensaje vacío o fuera de los límites.", simulated);
            return false;
        }
        var botLogin = store.GetSetting("twitch.bot.login", store.GetSetting("twitch.channel", "")).Trim();
        var channelLogin = store.GetSetting("twitch.channel", "").Trim();
        if (username.Equals(botLogin, StringComparison.OrdinalIgnoreCase) ||
            username.Equals(channelLogin, StringComparison.OrdinalIgnoreCase))
        {
            result = new(DateTimeOffset.UtcNow, "", username, "Eco propio ignorado", "El mensaje procede de la cuenta del bot o del canal; no se generan reacciones.", simulated);
            return false;
        }
        long epoch;
        lock (gate)
        {
            if (disposed || paused)
            {
                result = new(DateTimeOffset.UtcNow, "", username, "Pausado", "Las reacciones automáticas están pausadas.", simulated);
                return false;
            }
            if (!simulated && (!twitchConnected || !isConnected()))
            {
                result = new(DateTimeOffset.UtcNow, "", username, "Desconectado", "Twitch no está conectado; no se encoló la reacción.", false);
                return false;
            }
            epoch = connectionEpoch;
        }
        var now = DateTimeOffset.UtcNow;
        lock (rulesGate)
        {
            var hasProviderId = !string.IsNullOrWhiteSpace(messageId);
            var dedupeKey = hasProviderId
                ? "id:" + messageId
                : "fallback:" + username.ToLowerInvariant() + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(message))));
            var dedupeWindow = hasProviderId ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(2);
            if (seenMessageIds.TryGetValue(dedupeKey, out var previouslySeen) && now - previouslySeen <= dedupeWindow)
            {
                result = new(now, "", username, "Duplicado", "El evento de mensaje ya se procesó recientemente.", simulated);
                return false;
            }
            seenMessageIds[dedupeKey] = now;
            foreach (var old in seenMessageIds.Where(x =>
                now - x.Value > (x.Key.StartsWith("fallback:", StringComparison.Ordinal) ? TimeSpan.FromSeconds(2) : TimeSpan.FromMinutes(5)))
                .Select(x => x.Key).ToArray())
                seenMessageIds.Remove(old);
            while (seenMessageIds.Count > 2000) seenMessageIds.Remove(seenMessageIds.First().Key);
            var rule = rules.FirstOrDefault(x => x.Enabled && Matches(x, message));
            if (rule is null) return false;
            if (globalCooldowns.TryGetValue(rule.Id, out var lastGlobal) && now - lastGlobal < TimeSpan.FromSeconds(rule.GlobalCooldownSeconds))
            {
                result = new(now, rule.Name, username, "Cooldown global", "La regla sigue dentro de su cooldown global.", simulated);
                return false;
            }
            var userKey = rule.Id + ":" + username.ToLowerInvariant();
            if (userCooldowns.TryGetValue(userKey, out var lastUser) && now - lastUser < TimeSpan.FromSeconds(rule.UserCooldownSeconds))
            {
                result = new(now, rule.Name, username, "Cooldown usuario", "Este usuario sigue dentro del cooldown de la regla.", simulated);
                return false;
            }
            if (!minuteRuns.TryGetValue(rule.Id, out var executions)) minuteRuns[rule.Id] = executions = new Queue<DateTimeOffset>();
            while (executions.Count > 0 && now - executions.Peek() >= TimeSpan.FromMinutes(1)) executions.Dequeue();
            if (executions.Count >= rule.MaxPerMinute)
            {
                result = new(now, rule.Name, username, "Límite por minuto", "Se alcanzó el límite de ejecuciones de la regla.", simulated);
                return false;
            }
            foreach (var expired in userCooldowns.Where(x => now - x.Value > TimeSpan.FromHours(1)).Select(x => x.Key).ToArray())
                userCooldowns.Remove(expired);
            if (userCooldowns.Count >= 10000)
                foreach (var oldest in userCooldowns.OrderBy(x => x.Value).Take(userCooldowns.Count - 9000).Select(x => x.Key).ToArray())
                    userCooldowns.Remove(oldest);
            globalCooldowns[rule.Id] = now;
            userCooldowns[userKey] = now;
            executions.Enqueue(now);
            work = new(epoch, messageId ?? Guid.NewGuid().ToString("N"), username, message, rule, ResolveResponse(rule, username), simulated);
            return true;
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                CancellationTokenSource? operation = null;
                var stale = false;
                lock (gate)
                {
                    stale = disposed || paused || item.Epoch != connectionEpoch || (!item.Simulated && (!twitchConnected || !isConnected()));
                    if (!stale)
                    {
                        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        activeAction = operation;
                    }
                }
                if (stale)
                {
                    Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Descartada", "Trabajo obsoleto por pausa, cierre o cambio de sesión.", item.Simulated));
                    SafeEventDispatch.Invoke(QueueChanged, PendingCount);
                    continue;
                }
                var activeOperation = operation!;
                try
                {
                    var response = item.Response;
                    if (!item.Simulated && item.Rule.Responses.Count > 0)
                    {
                        lock (gate)
                            if (paused || item.Epoch != connectionEpoch || !twitchConnected || !isConnected())
                                throw new OperationCanceledException("sesión invalidada", activeOperation.Token);
                    }

                    if (item.Simulated)
                    {
                        Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Simulación local", "Trabajo simulado sin efectos externos.", true));
                        continue;
                    }

                    lock (gate)
                        if (paused || item.Epoch != connectionEpoch || !twitchConnected || !isConnected())
                            throw new OperationCanceledException("sesión invalidada antes del envío", activeOperation.Token);
                    var sent = await sendChat(response, activeOperation.Token).ConfigureAwait(false);
                    if (!sent)
                    {
                        Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Envío fallido", "Twitch rechazó el envío; no se activaron voz ni expresión.", false));
                        continue;
                    }

                    var details = new List<string> { "solicitud IRC enviada" };
                    lock (gate)
                    {
                        if (paused || item.Epoch != connectionEpoch || !twitchConnected || !isConnected())
                        {
                            Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Respuesta enviada", "La escritura IRC terminó antes de la pausa/desconexión; el mensaje no se puede retirar y se omitieron voz y expresión.", false));
                            continue;
                        }
                    }
                    if (item.Rule.Expression.Length > 0)
                    {
                        var expressionSet = setExpression(item.Rule.Expression, activeOperation.Token);
                        details.Add(expressionSet ? "expresión aplicada" : "expresión no aplicada (sprite, overlay o sesión no disponible)");
                    }
                    if (item.Rule.Speak)
                    {
                        bool audioQueued;
                        lock (gate)
                        {
                            audioQueued = !paused && item.Epoch == connectionEpoch && twitchConnected && isConnected() &&
                                !activeOperation.IsCancellationRequested &&
                                (string.IsNullOrWhiteSpace(item.Rule.ClipAssetId)
                                    ? enqueueSpeech(response, item.Rule.Language, item.Rule.VoiceId)
                                    : playClip is not null && playClip(item.Rule.ClipAssetId, activeOperation.Token));
                        }
                        details.Add(string.IsNullOrWhiteSpace(item.Rule.ClipAssetId)
                            ? (audioQueued ? "voz encolada con la voz de esta regla o la predeterminada" : "voz omitida/rechazada por pausa, sesión, voz o cola")
                            : (audioQueued ? "clip de audio encolado/reproducido" : "clip no disponible o rechazado; no se fingió una voz alternativa"));
                    }
                    Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Respuesta enviada", string.Join("; ", details), false));
                }
                catch (OperationCanceledException)
                {
                    Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Cancelada", "Trabajo cancelado por pausa, cierre o desconexión; no se registra el contenido.", item.Simulated));
                }
                catch (Exception ex)
                {
                    Record(new(DateTimeOffset.UtcNow, item.Rule.Name, item.User, "Error", "Ejecución fallida (" + ex.GetType().Name + ").", item.Simulated));
                }
                finally
                {
                    lock (gate) { if (ReferenceEquals(activeAction, activeOperation)) activeAction = null; }
                    activeOperation.Dispose();
                    SafeEventDispatch.Invoke(QueueChanged, PendingCount);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private void InvalidatePending(string reason)
    {
        CancellationTokenSource? active;
        lock (gate) active = activeAction;
        try { active?.Cancel(); } catch (ObjectDisposedException) { }
        Record(new(DateTimeOffset.UtcNow, "", "", "Invalidación", "Trabajos pendientes invalidados: " + reason + ".", false));
    }

    private void Record(ChatReactionResult result)
    {
        lock (rulesGate)
        {
            activity.Enqueue(result);
            while (activity.Count > 200) activity.Dequeue();
        }
        store.Log("Reacciones chat", result.Outcome + (result.RuleName.Length > 0 ? " · regla " + result.RuleName : "") + " · " + result.Detail);
        SafeEventDispatch.Invoke(ResultChanged, result);
    }

    private List<ChatReactionRule> LoadRules()
    {
        const string missingSetting = "__chat_reactions_not_configured__";
        var json = store.GetSetting(RulesSettingKey, missingSetting);
        var defaultsMigrated = store.GetSetting(BilingualDefaultsMigrationKey, "false") == "true";
        if (json == missingSetting)
        {
            var defaults = CreateBilingualAnimeCommands();
            store.SetSetting(RulesSettingKey, JsonSerializer.Serialize(defaults));
            store.SetSetting(BilingualDefaultsMigrationKey, "true");
            store.Log("Reacciones chat", "Se instalaron comandos anime bilingües iniciales; pueden editarse o desactivarse desde Reacciones.");
            return defaults;
        }
        try
        {
            var loaded = JsonSerializer.Deserialize<List<ChatReactionRule>>(json) ?? [];
            var valid = new List<ChatReactionRule>();
            foreach (var rule in loaded.Take(MaximumRules))
            {
                if (rule is not null && TryValidate(rule, out var clean, out _)) valid.Add(clean);
                else store.Log("Reacciones chat", "Se omitió una regla persistida inválida; no se ejecutó.");
            }

            if (!defaultsMigrated)
            {
                foreach (var builtIn in CreateBilingualAnimeCommands())
                {
                    if (valid.Count >= MaximumRules) break;
                    if (valid.All(existing => !string.Equals(existing.Id, builtIn.Id, StringComparison.OrdinalIgnoreCase)))
                        valid.Add(builtIn);
                }
                store.SetSetting(RulesSettingKey, JsonSerializer.Serialize(valid));
                store.SetSetting(BilingualDefaultsMigrationKey, "true");
                store.Log("Reacciones chat", "Se incorporaron comandos anime bilingües iniciales a la configuración existente.");
            }
            return valid;
        }
        catch (JsonException)
        {
            store.Log("Reacciones chat", "La configuración persistida no es JSON válido; se cargó una lista vacía.");
            store.SetSetting(RulesSettingKey, "[]");
            store.SetSetting(BilingualDefaultsMigrationKey, "true");
            return [];
        }
    }

    private static List<ChatReactionRule> CreateBilingualAnimeCommands()
    {
        // Slash aliases satisfy the requested command style. Bang aliases also work in
        // chat clients that reserve slash-prefixed input for their own local commands.
        return
        [
            new("anime-ohayo-slash", "Anime bilingüe: Ohayō", "/ohayo", ChatReactionMatchType.Exact,
                ["おはよう！ ¡Buenos días, chat!"], true, "Happy", 5, 10, 10, true),
            new("anime-ohayo-bang", "Anime bilingüe: Ohayō", "!ohayo", ChatReactionMatchType.Exact,
                ["おはよう！ ¡Buenos días, chat!"], true, "Happy", 5, 10, 10, true),
            new("anime-baka-slash", "Anime bilingüe: Baka", "/baka", ChatReactionMatchType.Exact,
                ["お兄ちゃんのばか！ ¡No seas malo conmigo!"], true, "Surprised", 5, 10, 10, true),
            new("anime-baka-bang", "Anime bilingüe: Baka", "!baka", ChatReactionMatchType.Exact,
                ["お兄ちゃんのばか！ ¡No seas malo conmigo!"], true, "Surprised", 5, 10, 10, true),
            new("anime-nani-slash", "Anime bilingüe: Nani", "/nani", ChatReactionMatchType.Exact,
                ["なに!? ¿¡Nani!? ¿Qué pasó?"], true, "Surprised", 5, 10, 10, true),
            new("anime-nani-bang", "Anime bilingüe: Nani", "!nani", ChatReactionMatchType.Exact,
                ["なに!? ¿¡Nani!? ¿Qué pasó?"], true, "Surprised", 5, 10, 10, true),
            new("anime-okaeri-slash", "Anime bilingüe: Okaerinasai", "/okaerinasai", ChatReactionMatchType.Exact,
                ["おかえりなさい！ ¡Bienvenido de vuelta, {user}!"], true, "Happy", 5, 10, 10, true),
            new("anime-okaeri-bang", "Anime bilingüe: Okaerinasai", "!okaerinasai", ChatReactionMatchType.Exact,
                ["おかえりなさい！ ¡Bienvenido de vuelta, {user}!"], true, "Happy", 5, 10, 10, true),
            new("anime-soybot-slash", "Español: Soy solo un bot", "/soybot", ChatReactionMatchType.Exact,
                ["Yo... soy solo un bot... jeje. ¡No me mires así!"], true, "Happy", 5, 10, 10, true),
            new("anime-soybot-bang", "Español: Soy solo un bot", "!soybot", ChatReactionMatchType.Exact,
                ["Yo... soy solo un bot... jeje. ¡No me mires así!"], true, "Happy", 5, 10, 10, true)
        ];
    }

    private void PersistRulesLocked() => store.SetSetting(RulesSettingKey, JsonSerializer.Serialize(rules));

    private static bool TryValidate(ChatReactionRule rule, out ChatReactionRule clean, out string error)
    {
        clean = rule; error = "";
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Trim().Length > 60 ||
            string.IsNullOrWhiteSpace(rule.Trigger) || rule.Trigger.Trim().Length > 80 ||
            rule.Trigger.Any(char.IsControl) || rule.Responses is null || rule.Responses.Count is < 1 or > 8 ||
            rule.Responses.Any(x => x is null || x.Trim().Length is < 1 or > 400 || x.Any(char.IsControl)) ||
            !Enum.IsDefined(rule.MatchType) ||
            rule.GlobalCooldownSeconds is < 0 or > 3600 || rule.UserCooldownSeconds is < 0 or > 3600 ||
            rule.MaxPerMinute is < 1 or > 60 ||
            (rule.Expression ?? "").Length > 30 || (!string.IsNullOrEmpty(rule.Expression) && rule.Expression is not ("Idle" or "Talking" or "Blink" or "Happy" or "Surprised")) ||
            (rule.Language ?? "auto") is not ("auto" or "es" or "ja") ||
            (rule.VoiceId ?? "").Length > 500 || (rule.ClipAssetId ?? "").Length > 64)
        {
            error = "Campos fuera de rango o tipo de coincidencia/expresión no permitido.";
            return false;
        }
        var id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
        if (id.Length > 64) { error = "ID de regla inválido."; return false; }
        clean = rule with { Id = id, Name = rule.Name.Trim(), Trigger = rule.Trigger.Trim(),
            Responses = rule.Responses.Select(x => x.Trim()).ToArray(), Expression = rule.Expression?.Trim() ?? "",
            Language = string.IsNullOrWhiteSpace(rule.Language) ? "auto" : rule.Language.Trim(),
            VoiceId = rule.VoiceId?.Trim() ?? "", ClipAssetId = rule.ClipAssetId?.Trim() ?? "" };
        return true;
    }

    private string ResolveResponse(ChatReactionRule rule, string username)
    {
        var response = rule.Responses.Count == 0 ? "" : rule.Responses[Math.Abs(Interlocked.Increment(ref responseIndex) - 1) % rule.Responses.Count];
        return response.Replace("{user}", username, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsWholeWord(string message, string trigger)
    {
        var start = 0;
        while ((start = message.IndexOf(trigger, start, StringComparison.Ordinal)) >= 0)
        {
            var before = start == 0 || !IsWordChar(message[start - 1]);
            var end = start + trigger.Length;
            var after = end == message.Length || !IsWordChar(message[end]);
            if (before && after) return true;
            start = end;
        }
        return false;
    }

    private static bool IsWordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var whitespace = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch))
            {
                if (!whitespace) builder.Append(' ');
                whitespace = true;
            }
            else { builder.Append(char.ToLowerInvariant(ch)); whitespace = false; }
        }
        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            connectionEpoch++;
        }
        InvalidatePending("cierre del servicio");
        queue.Writer.TryComplete();
        lifetime.Cancel();
        try { await worker.ConfigureAwait(false); } catch (OperationCanceledException) { }
        lifetime.Dispose();
    }
}
