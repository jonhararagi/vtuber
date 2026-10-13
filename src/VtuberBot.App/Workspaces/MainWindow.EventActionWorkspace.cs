using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private EventActionCoordinator? eventActionCoordinator;
    private PngtuberHotkeyRegistry? eventActionHotkeys;
    private readonly List<StreamerBotAction> eventActionStreamerBotActions = [];
    private readonly SemaphoreSlim eventActionSubscriptionGate = new(1, 1);
    private IReadOnlyDictionary<string, string[]> appliedStreamerBotSubscriptions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    private ListBox? eventActionActivityList;

    private void InitializeEventActionCoordination()
    {
        var settings = new WindowProductSettingsStore(key => store.GetSetting(key), (key, value) => store.SetSetting(key, value));
        eventActionHotkeys = new PngtuberHotkeyRegistry(settings, new WindowsPngtuberHotkeySender());
        var dispatcher = new WindowEventActionDispatcher(streamerBot, settings, eventActionHotkeys,
            () => store.GetSetting("integrations.pngTuberPlus.path"));
        eventActionCoordinator = new EventActionCoordinator(settings, dispatcher);
        eventActionCoordinator.ActivityChanged += activity =>
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    store.Log("Automatizaciones", $"{activity.Outcome}: regla={activity.RuleId?.ToString("N") ?? "-"} fuente={activity.Source} evento={activity.EventType} destino={activity.Destination} correlación={activity.CorrelationId} · {activity.Message}");
                    if (currentPage == "Automatizaciones") RefreshEventActionActivity();
                }));
            }
            catch (InvalidOperationException) { }
        };
        eventActionCoordinator.Warning += warning =>
        {
            try { Dispatcher.BeginInvoke(new Action(() => { FooterStatus.Text = warning; store.Log("Automatizaciones", warning); })); }
            catch (InvalidOperationException) { }
        };
    }

    private UIElement BuildEventActionWorkspace()
    {
        var coordinator = eventActionCoordinator ?? throw new InvalidOperationException("El coordinador todavía no está inicializado.");
        var root = new StackPanel();
        root.Children.Add(Label("Reglas locales: evento real → condiciones → destino validado. Las reglas nuevas nacen desactivadas. No se crean acciones ni eventos ficticios.", 13, "#98A4BE"));

        var safety = Panel("Control general y seguridad");
        var paused = new CheckBox
        {
            Content = "Pausar todas las automatizaciones (recomendado al configurar)",
            IsChecked = coordinator.AutomationsPaused, Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 2, 0, 10)
        };
        paused.Checked += (_, _) => coordinator.AutomationsPaused = true;
        paused.Unchecked += (_, _) => coordinator.AutomationsPaused = false;
        safety.Children.Add(paused);
        var allowHotkeys = new CheckBox
        {
            Content = "AUTORIZAR explícitamente el envío automático de atajos de PNGTuber Plus",
            IsChecked = coordinator.AutomaticPngtuberHotkeysAllowed, Foreground = Brush("#E7B56A"), Margin = new Thickness(0, 2, 0, 6)
        };
        allowHotkeys.Checked += (_, _) =>
        {
            var accepted = MessageBox.Show("El envío automático puede poner PNGTuber Plus en primer plano y quitar el foco a OBS, un juego u otra aplicación. Solo se enviará una tecla registrada y el envío no confirma un cambio visual. ¿Autorizarlo?", "Riesgo de cambio de foco", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            coordinator.AutomaticPngtuberHotkeysAllowed = accepted == MessageBoxResult.Yes;
            allowHotkeys.IsChecked = accepted == MessageBoxResult.Yes;
        };
        allowHotkeys.Unchecked += (_, _) => coordinator.AutomaticPngtuberHotkeysAllowed = false;
        safety.Children.Add(allowHotkeys);
        var globalLimit = TextField("Máximo global por minuto", coordinator.GlobalMaxExecutionsPerMinute.ToString());
        globalLimit.Width = 120;
        safety.Children.Add(Label("Límite global de acciones automáticas por minuto (1–1000)", 12, "#98A4BE"));
        safety.Children.Add(globalLimit);
        safety.Children.Add(ActionButton("Guardar límite", (_, _) =>
        {
            if (!int.TryParse(globalLimit.Text, out var limit) || limit is < 1 or > 1000)
            { MessageBox.Show("Ingresá un límite entre 1 y 1000.", "Automatizaciones", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            coordinator.GlobalMaxExecutionsPerMinute = limit;
            FooterStatus.Text = "Límite global guardado en SQLite.";
        }));
        safety.Children.Add(Label("Advertencia: no se garantiza deduplicación entre Streamer.bot y EventSub cuando los proveedores no comparten un identificador común. Evitá conectar ambos orígenes a una misma regla.", 12, "#E7B56A"));
        root.Children.Add(safety);

        var editor = Panel("Crear o editar regla");
        var name = TextField("Nombre de la regla");
        var source = new ComboBox { MinWidth = 250, Margin = new Thickness(0, 0, 8, 8), ItemsSource = new[] { "Streamer.bot", "VTuberBot.EventSub" }, SelectedIndex = 0 };
        var category = TextField("Categoría real (por ejemplo, Twitch)");
        var eventType = TextField("Tipo de evento real");
        var destination = new ComboBox { MinWidth = 250, Margin = new Thickness(0, 0, 8, 8), ItemsSource = new[] { "Streamer.bot", "PNGTuber Plus" }, SelectedIndex = 0 };
        var target = new ComboBox { MinWidth = 360, Margin = new Thickness(0, 0, 8, 8), DisplayMemberPath = "Content" };
        var conditionsEditor = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 78,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ToolTip = "Una condición por línea: campo|eq|valor, campo|contains|texto o campo|present|",
            Background = Brush("#101525"), Foreground = Brush("#F8FAFC"), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8)
        };
        var cooldown = TextField("Cooldown en segundos", "0"); cooldown.Width = 110;
        var perRuleLimit = TextField("Máximo por minuto", "10"); perRuleLimit.Width = 110;
        var enabled = new CheckBox { Content = "Activada (solo al guardar expresamente)", IsChecked = false, Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 8) };
        var status = Label("Consultá el catálogo real para seleccionar un evento/acción existente.", 12, "#98A4BE");
        Guid? editingId = null;
        EventActionRule? selectedRule = null;

        editor.Children.Add(Label("Nombre", 12, "#98A4BE")); editor.Children.Add(name);
        editor.Children.Add(Label("Fuente (no se mezclan automáticamente proveedores)", 12, "#98A4BE")); editor.Children.Add(source);
        editor.Children.Add(Label("Categoría y tipo exactos del evento", 12, "#98A4BE")); editor.Children.Add(category); editor.Children.Add(eventType);
        var refreshRow = new WrapPanel();
        refreshRow.Children.Add(ActionButton("Consultar eventos reales de Streamer.bot", async (_, _) =>
        {
            try
            {
                var available = await streamerBot.GetEventsAsync();
                var choice = new ComboBox { MinWidth = 400, Margin = new Thickness(0, 0, 8, 8) };
                foreach (var item in available.OrderBy(x => x.Key).SelectMany(x => x.Value.Select(t => (Category: x.Key, Type: t))))
                    choice.Items.Add(new ComboBoxItem { Content = item.Category + "." + item.Type, Tag = item });
                if (choice.Items.Count > 0) choice.SelectedIndex = 0;
                var pick = new StackPanel(); pick.Children.Add(Label("Seleccioná un evento devuelto por GetEvents", 12, "#98A4BE")); pick.Children.Add(choice);
                var dialog = new Window { Title = "Catálogo real de Streamer.bot", Width = 540, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Content = pick, Background = Brush("#0B0E17"), Foreground = Brush("#F1F5F9"), ResizeMode = ResizeMode.NoResize };
                var chooseButton = ActionButton("Usar evento seleccionado", (_, _) => dialog.DialogResult = true, true);
                pick.Children.Add(chooseButton);
                if (dialog.ShowDialog() == true && choice.SelectedItem is ComboBoxItem { Tag: ValueTuple<string, string> picked })
                { source.SelectedItem = "Streamer.bot"; category.Text = picked.Item1; eventType.Text = picked.Item2; status.Text = "Evento seleccionado del catálogo real."; }
            }
            catch (Exception ex) { status.Text = "No se pudo consultar GetEvents: " + ex.GetType().Name; }
        }, true));
        refreshRow.Children.Add(ActionButton("Actualizar destinos reales", async (_, _) =>
        {
            try
            {
                target.Items.Clear();
                if (Equals(destination.SelectedItem, "Streamer.bot"))
                {
                    eventActionStreamerBotActions.Clear();
                    eventActionStreamerBotActions.AddRange(await streamerBot.GetActionsAsync());
                    foreach (var action in eventActionStreamerBotActions.Where(a => a.Enabled))
                        target.Items.Add(new ComboBoxItem { Content = $"{action.Name} · {action.Id}", Tag = action.Id });
                    status.Text = $"Catálogo real: {eventActionStreamerBotActions.Count} acciones; solo se ofrecen acciones habilitadas.";
                }
                else
                {
                    var entries = eventActionHotkeys?.Load() ?? [];
                    foreach (var hotkey in entries.Where(h => h.Enabled))
                        target.Items.Add(new ComboBoxItem { Content = $"{hotkey.Name} · tecla {hotkey.Key}", Tag = hotkey.Id.ToString("D") });
                    status.Text = $"Registro real de PNGTuber Plus: {entries.Count} atajos; no se han enviado teclas.";
                }
                if (target.Items.Count > 0) target.SelectedIndex = 0;
            }
            catch (Exception ex) { status.Text = "No se pudieron validar destinos: " + ex.GetType().Name + ". Conectá Streamer.bot o registrá un atajo primero."; }
        }));
        editor.Children.Add(refreshRow);
        editor.Children.Add(Label("Destino", 12, "#98A4BE"));
        destination.SelectionChanged += (_, _) => { target.Items.Clear(); status.Text = "Actualizá destinos para consultar el catálogo real."; };
        editor.Children.Add(destination); editor.Children.Add(target);
        editor.Children.Add(Label("Condiciones opcionales (una por línea; rutas JSON simples, sin expresiones ni código)", 12, "#98A4BE"));
        editor.Children.Add(Label("Formato: campo|eq|valor · campo|contains|texto · campo|present|", 11, "#98A4BE"));
        editor.Children.Add(conditionsEditor);
        var limitsRow = new WrapPanel(); limitsRow.Children.Add(cooldown); limitsRow.Children.Add(perRuleLimit); editor.Children.Add(limitsRow);
        editor.Children.Add(enabled); editor.Children.Add(status);

        var rulesList = new ListBox { MinHeight = 150, MaxHeight = 240, Background = Brush("#101525"), Foreground = Brush("#F1F5F9"), Margin = new Thickness(0, 0, 0, 10) };
        void ReloadRules()
        {
            rulesList.Items.Clear();
            foreach (var rule in coordinator.GetRules())
                rulesList.Items.Add(new ListBoxItem
                {
                    Tag = rule,
                    Content = $"{(rule.Enabled ? "●" : "○")} {rule.Name} · {rule.Source}/{rule.Category}.{rule.EventType} → {rule.Destination} · {(rule.Enabled ? "activada" : "desactivada")}",
                    Foreground = Brush("#F1F5F9")
                });
        }
        void ResetEditor()
        {
            editingId = null; selectedRule = null;
            name.Text = ""; source.SelectedItem = "Streamer.bot"; category.Text = ""; eventType.Text = "";
            destination.SelectedItem = "Streamer.bot"; target.Items.Clear(); conditionsEditor.Text = "";
            cooldown.Text = "0"; perRuleLimit.Text = "10"; enabled.IsChecked = false;
            status.Text = "Regla nueva desactivada. No se ejecutará hasta habilitarla expresamente.";
        }
        rulesList.SelectionChanged += (_, _) =>
        {
            if (rulesList.SelectedItem is not ListBoxItem { Tag: EventActionRule rule }) return;
            editingId = rule.Id; selectedRule = rule; name.Text = rule.Name;
            source.SelectedItem = rule.Source; category.Text = rule.Category; eventType.Text = rule.EventType;
            destination.SelectedItem = rule.Destination == EventActionDestination.StreamerBot ? "Streamer.bot" : "PNGTuber Plus";
            conditionsEditor.Text = string.Join(Environment.NewLine, rule.Conditions.Select(condition =>
                condition.FieldPath + "|" + condition.Operator + "|" + condition.ExpectedValue));
            cooldown.Text = rule.CooldownSeconds.ToString(); perRuleLimit.Text = rule.MaxExecutionsPerMinute.ToString();
            enabled.IsChecked = rule.Enabled; target.Items.Clear();
            target.Items.Add(new ComboBoxItem { Content = rule.TargetId + " · destino guardado (volvé a validar)", Tag = rule.TargetId });
            target.SelectedIndex = 0;
            status.Text = "Regla cargada. Antes de activarla, volvé a validar que el destino siga existiendo.";
        };
        editor.Children.Add(Label("Reglas guardadas", 12, "#98A4BE")); editor.Children.Add(rulesList);
        var ruleButtons = new WrapPanel();
        ruleButtons.Children.Add(ActionButton("Nueva regla", (_, _) => { rulesList.SelectedItem = null; ResetEditor(); }));
        ruleButtons.Children.Add(ActionButton("Guardar regla", async (_, _) =>
        {
            try
            {
                if (target.SelectedItem is not ComboBoxItem { Tag: string targetId })
                    throw new InvalidOperationException("Seleccioná un destino del catálogo real antes de guardar.");
                if (!int.TryParse(cooldown.Text, out var cooldownSeconds) || cooldownSeconds is < 0 or > 86400)
                    throw new ArgumentException("El cooldown debe estar entre 0 y 86400 segundos.");
                if (!int.TryParse(perRuleLimit.Text, out var maxPerMinute) || maxPerMinute is < 1 or > 100)
                    throw new ArgumentException("El límite por regla debe estar entre 1 y 100 por minuto.");
                var dest = Equals(destination.SelectedItem, "Streamer.bot") ? EventActionDestination.StreamerBot : EventActionDestination.PngtuberPlus;
                if (dest == EventActionDestination.StreamerBot)
                {
                    if (!Guid.TryParse(targetId, out var selectedActionId))
                        throw new InvalidOperationException("El destino Streamer.bot no tiene un GUID válido.");
                    var currentActions = await streamerBot.GetActionsAsync();
                    if (!currentActions.Any(a => a.Enabled && Guid.TryParse(a.Id, out var id) && id == selectedActionId))
                        throw new InvalidOperationException("La acción no está disponible/habilitada en el catálogo actual. Actualizá destinos.");
                }
                else
                {
                    if (!Guid.TryParse(targetId, out var selectedHotkeyId) ||
                        !(eventActionHotkeys?.Load() ?? []).Any(h => h.Enabled && h.Id == selectedHotkeyId))
                        throw new InvalidOperationException("El atajo no existe en el registro actual o está deshabilitado.");
                }
                var shouldEnable = enabled.IsChecked == true;
                var confirmed = selectedRule?.ExternalActionConfirmed == true &&
                    string.Equals(selectedRule.ConfirmedTargetId, targetId, StringComparison.OrdinalIgnoreCase);
                string? confirmedTargetId = confirmed ? targetId : null;
                if (shouldEnable && dest == EventActionDestination.StreamerBot && !confirmed)
                {
                    var targetName = target.SelectedItem is ComboBoxItem item ? item.Content?.ToString() : targetId;
                    var answer = MessageBox.Show($"Esta regla podrá solicitar una acción externa de Streamer.bot:\n\n{targetName}\n\nEl efecto posterior en OBS no está garantizado. ¿Confirmás este destino para automatización?", "Confirmar acción externa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) { enabled.IsChecked = false; return; }
                    confirmed = true;
                    confirmedTargetId = targetId;
                }
                var conditions = new List<EventActionCondition>();
                foreach (var line in conditionsEditor.Text.Split(new[] { '\\r', '\\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var fields = line.Split('|', 3, StringSplitOptions.None);
                    if (fields.Length < 2 || fields.Length == 2 && fields[1] != "present")
                        throw new ArgumentException("Cada condición debe ser campo|eq|valor, campo|contains|texto o campo|present|.");
                    var operation = fields[1].Trim();
                    var expected = fields.Length > 2 ? fields[2] : "";
                    if (operation is not ("eq" or "contains" or "present"))
                        throw new ArgumentException("Operador de condición no permitido: " + operation);
                    conditions.Add(new EventActionCondition(fields[0].Trim(), operation, expected));
                }
                var selectedSource = source.SelectedItem?.ToString() ?? "";
                if (selectedSource == "Streamer.bot")
                {
                    var available = await streamerBot.GetEventsAsync();
                    if (!available.TryGetValue(category.Text.Trim(), out var names) || !names.Contains(eventType.Text.Trim(), StringComparer.OrdinalIgnoreCase))
                        throw new InvalidOperationException("La categoría/tipo no aparece en GetEvents. Actualizá el catálogo y elegí un evento real.");
                }
                else if (!coordinator.GetRecentActivity().Any(a => a.Source == "VTuberBot.EventSub" &&
                    a.EventType.Equals(category.Text.Trim() + "." + eventType.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Este tipo interno todavía no se ha observado en un evento EventSub real; no se puede validar todavía.");
                var saved = coordinator.SaveRule(new EventActionRule(editingId ?? Guid.NewGuid(), name.Text,
                    shouldEnable, selectedSource, category.Text, eventType.Text, conditions,
                    dest, targetId, cooldownSeconds, maxPerMinute, confirmed, confirmedTargetId));
                selectedRule = saved; editingId = saved.Id;
                ReloadRules(); status.Text = "Regla guardada en SQLite. No se envió ninguna tecla ni se ejecutó una acción al guardar.";
                await ReconcileStreamerBotSubscriptionsAsync();
                FooterStatus.Text = "Regla guardada: " + saved.Name;
            }
            catch (Exception ex) { status.Text = "No se guardó la regla: " + ex.Message; }
        }, true));
        ruleButtons.Children.Add(ActionButton("Eliminar regla seleccionada", async (_, _) =>
        {
            if (rulesList.SelectedItem is not ListBoxItem { Tag: EventActionRule rule }) return;
            if (MessageBox.Show($"¿Eliminar la regla «{rule.Name}»?", "Eliminar regla", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            coordinator.RemoveRule(rule.Id); ResetEditor(); ReloadRules(); status.Text = "Regla eliminada.";
            await ReconcileStreamerBotSubscriptionsAsync();
        }));
        editor.Children.Add(ruleButtons);
        root.Children.Add(editor);

        var activityPanel = Panel("Actividad real de automatizaciones");
        var activityList = new ListBox { MinHeight = 180, MaxHeight = 300, Background = Brush("#101525"), Foreground = Brush("#F1F5F9") };
        eventActionActivityList = activityList;
        void FillActivity()
        {
            activityList.Items.Clear();
            foreach (var item in coordinator.GetRecentActivity().Reverse())
                activityList.Items.Add($"{item.At:HH:mm:ss} · {item.Outcome} · {item.Source}/{item.EventType} · {item.Destination} · {item.Message}");
        }
        activityPanel.Children.Add(activityList);
        activityPanel.Children.Add(ActionButton("Actualizar actividad", (_, _) => FillActivity()));
        root.Children.Add(activityPanel);
        ReloadRules(); FillActivity();
        return root;
    }

    private async Task SetManualStreamerBotSubscriptionsAsync(IReadOnlyDictionary<string, string[]> selected)
    {
        store.SetSetting("integrations.streamerBot.ws.manualSubscriptions", System.Text.Json.JsonSerializer.Serialize(selected));
        await ReconcileStreamerBotSubscriptionsAsync();
    }

    private async Task ReconcileStreamerBotSubscriptionsAsync()
    {
        if (eventActionCoordinator is null) return;
        await eventActionSubscriptionGate.WaitAsync();
        try
        {
            if (streamerBot.Snapshot.State != StreamerBotConnectionState.Connected)
            {
                appliedStreamerBotSubscriptions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var available = await streamerBot.GetEventsAsync();
            var requested = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            void Add(string category, string eventType)
            {
                if (!available.TryGetValue(category, out var validNames) ||
                    !validNames.Contains(eventType, StringComparer.OrdinalIgnoreCase))
                {
                    store.Log("Automatizaciones", $"Suscripción omitida: el evento {category}.{eventType} no existe en el catálogo actual.");
                    return;
                }
                if (!requested.TryGetValue(category, out var names)) requested[category] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                names.Add(validNames.First(x => x.Equals(eventType, StringComparison.OrdinalIgnoreCase)));
            }

            try
            {
                var manualJson = store.GetSetting("integrations.streamerBot.ws.manualSubscriptions", "");
                if (!string.IsNullOrWhiteSpace(manualJson))
                {
                    var manual = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(manualJson) ?? [];
                    foreach (var item in manual)
                        foreach (var eventType in item.Value ?? []) Add(item.Key, eventType);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                store.Log("Automatizaciones", "La selección manual de suscripciones está dañada; se ignoró sin detener el coordinador.");
            }

            foreach (var rule in eventActionCoordinator.GetRules().Where(r => r.Enabled &&
                r.Source.Equals("Streamer.bot", StringComparison.OrdinalIgnoreCase)))
                Add(rule.Category, rule.EventType);

            var desired = requested.ToDictionary(x => x.Key, x => x.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
            bool Same(IReadOnlyDictionary<string, string[]> left, IReadOnlyDictionary<string, string[]> right) =>
                left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var values) &&
                    pair.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).SequenceEqual(values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase));
            if (Same(appliedStreamerBotSubscriptions, desired)) return;

            if (appliedStreamerBotSubscriptions.Count > 0)
            {
                await streamerBot.UnsubscribeAsync(appliedStreamerBotSubscriptions);
                appliedStreamerBotSubscriptions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }
            if (desired.Count > 0)
            {
                var confirmed = await streamerBot.SubscribeAsync(desired);
                appliedStreamerBotSubscriptions = confirmed.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                store.Log("Automatizaciones", "Suscripciones reconciliadas con confirmación del servidor: " +
                    string.Join("; ", confirmed.Select(x => x.Key + ": " + string.Join(", ", x.Value))));
            }
            else
                store.Log("Automatizaciones", "No hay suscripciones manuales ni reglas habilitadas que requieran eventos de Streamer.bot.");
        }
        catch (Exception ex)
        {
            store.Log("Automatizaciones", "No se pudieron reconciliar las suscripciones (" + ex.GetType().Name + ").");
            FooterStatus.Text = "Suscripciones de automatización no confirmadas; revisá Streamer.bot.";
        }
        finally { eventActionSubscriptionGate.Release(); }
    }

    private void RefreshEventActionActivity()
    {
        if (eventActionActivityList is null || eventActionCoordinator is null) return;
        eventActionActivityList.Items.Clear();
        foreach (var item in eventActionCoordinator.GetRecentActivity().Reverse())
            eventActionActivityList.Items.Add($"{item.At:HH:mm:ss} · {item.Outcome} · {item.Source}/{item.EventType} · {item.Destination} · {item.Message}");
    }

    private sealed class WindowProductSettingsStore(
        Func<string, string> get,
        Action<string, string> set) : IProductSettingsStore
    {
        public string GetSetting(string key, string fallback = "") => get(key) is { Length: > 0 } value ? value : fallback;
        public void SetSetting(string key, string value) => set(key, value);
    }

    private sealed class WindowEventActionDispatcher(
        IStreamerBotClient streamerBot,
        IProductSettingsStore settings,
        PngtuberHotkeyRegistry hotkeys,
        Func<string> executablePath) : IEventActionDispatcher
    {
        public async Task<EventActionExecutionResult> DispatchAsync(EventActionRule rule, NormalizedBotEvent evt, CancellationToken cancellationToken)
        {
            if (rule.Destination == EventActionDestination.StreamerBot)
            {
                if (streamerBot.Snapshot.State != StreamerBotConnectionState.Connected)
                    return new(EventActionOutcome.InvalidDestination, "Streamer.bot no está conectado; no se solicitó ninguna acción.");
                if (!Guid.TryParse(rule.TargetId, out var id))
                    return new(EventActionOutcome.InvalidDestination, "El identificador guardado no es un GUID válido.");
                IReadOnlyList<StreamerBotAction> catalog;
                try { catalog = await streamerBot.GetActionsAsync(cancellationToken).ConfigureAwait(false); }
                catch (Exception ex) { return new(EventActionOutcome.InvalidDestination, "No se pudo verificar el catálogo de acciones (" + ex.GetType().Name + ")."); }
                var target = catalog.FirstOrDefault(a => Guid.TryParse(a.Id, out var catalogId) && catalogId == id && a.Enabled);
                if (target is null)
                    return new(EventActionOutcome.InvalidDestination, "La acción ya no existe o está deshabilitada; se rechazó el destino.");
                try
                {
                    var response = await streamerBot.DoActionAsync(id.ToString("D"), cancellationToken).ConfigureAwait(false);
                    return response.Success
                        ? new(EventActionOutcome.RequestAccepted, "Streamer.bot aceptó la solicitud. El efecto posterior en OBS no está confirmado.", response.Id)
                        : new(EventActionOutcome.ActionRejected, "Streamer.bot rechazó la solicitud: " + response.Message, response.Id);
                }
                catch (TimeoutException)
                {
                    return new(EventActionOutcome.OutcomeUnknown, "Timeout de Streamer.bot; la acción pudo haber comenzado y su efecto es desconocido.");
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.WebSockets.WebSocketException)
                {
                    return new(EventActionOutcome.OutcomeUnknown, "La conexión falló durante la solicitud; el resultado es desconocido (" + ex.GetType().Name + ").");
                }
            }

            if (settings.GetSetting(EventActionCoordinator.PngtuberAutomaticHotkeysSettingKey, "false") != "true")
                return new(EventActionOutcome.InvalidDestination, "El envío automático de atajos no está autorizado.");
            if (!Guid.TryParse(rule.TargetId, out var hotkeyId))
                return new(EventActionOutcome.InvalidDestination, "El identificador de atajo no es válido.");
            var entry = hotkeys.Load().FirstOrDefault(x => x.Id == hotkeyId && x.Enabled);
            if (entry is null)
                return new(EventActionOutcome.InvalidDestination, "El atajo no existe en el registro actual o está deshabilitado.");
            var path = executablePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new(EventActionOutcome.InvalidDestination, "El ejecutable configurado de PNGTuber Plus no existe.");
            cancellationToken.ThrowIfCancellationRequested();
            var result = hotkeys.Test(hotkeyId, path);
            return result.WasSent
                ? new(EventActionOutcome.HotkeySent, result.Message + " No se confirma un cambio visual.")
                : new(EventActionOutcome.ActionRejected, result.Message);
        }
    }
}
