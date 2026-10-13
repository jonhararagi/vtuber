using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private bool SetChatReactionExpression(string expression, CancellationToken cancellationToken)
    {
        try
        {
            return Dispatcher.Invoke(() =>
            {
                if (cancellationToken.IsCancellationRequested) return false;
                if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                    return false;
                var path = expression == "Idle" ? avatarPath : store.GetAvatarExpression(expression);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return false;
                store.SetSetting("avatar.state", expression);
                if (overlay is { IsVisible: true })
                    overlay.SetState(expression);
                return overlay is { IsVisible: true };
            });
        }
        catch (Exception exception)
        {
            store.Log("Avatar", "No se pudo aplicar una expresión de reacción (" + exception.GetType().Name + ").");
            return false;
        }
    }

    private void DetachLiveCenterHandlers()
    {
        if (liveCenterReactionHandler is not null) chatReactions.ResultChanged -= liveCenterReactionHandler;
        if (liveCenterQueueHandler is not null) chatReactions.QueueChanged -= liveCenterQueueHandler;
        liveCenterReactionHandler = null;
        liveCenterQueueHandler = null;
    }

    private BitmapSource? CaptureLiveCenterPreview(string state)
    {
        if (overlay is not null)
        {
            try { return overlay.CapturePreview(); }
            catch (Exception exception) { store.Log("Avatar", "Vista previa del renderizador no disponible (" + exception.GetType().Name + ")."); }
        }
        var expressionPath = store.GetAvatarExpression(state);
        var fallback = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath) ? expressionPath : avatarPath;
        if (string.IsNullOrWhiteSpace(fallback) || !File.Exists(fallback)) return null;
        try { return AvatarImageLoader.Load(fallback, 420); }
        catch (Exception exception) { store.Log("Avatar", "Vista previa de sprite no disponible (" + exception.GetType().Name + ")."); return null; }
    }

    private UIElement BuildLiveCenterWorkspace()
    {
        DetachLiveCenterHandlers();
        var root = new StackPanel();
        root.Children.Add(Label("Centro de directo", 22, "#F8FAFC", true));
        root.Children.Add(Label("La salida pública está en la ventana transparente del avatar. Esta página y sus controles son privados y no deben capturarse en OBS.", 12, "#98A4BE"));

        var publicPanel = Panel("A. Salida pública · captura de OBS");
        var publicGrid = new Grid { MinHeight = 250 };
        publicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        publicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var previewBorder = new Border { Background = Brush("#080B12"), BorderBrush = Brush("#34415E"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(0, 0, 12, 8) };
        var preview = new Image { Stretch = Stretch.Uniform, MaxHeight = 260, MaxWidth = 220 };
        var currentExpression = store.GetSetting("avatar.state", "Idle");
        var expressionPath = store.GetAvatarExpression(currentExpression);
        var previewPath = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath) ? expressionPath : avatarPath;
        if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
        {
            try { preview.Source = AvatarImageLoader.Load(previewPath, 420); }
            catch (Exception exception) { store.Log("Avatar", "Vista previa no disponible (" + exception.GetType().Name + ")."); }
        }
        previewBorder.Child = preview;
        publicGrid.Children.Add(previewBorder);
        var publicDetails = new StackPanel();
        publicDetails.Children.Add(Label("Ventana capturable", 14, "#F8FAFC", true));
        publicDetails.Children.Add(Label("VTuberBot Avatar Overlay · transparente · sin navegación ni controles privados.", 12, "#98A4BE"));
        var outputStatus = Label($"Estado solicitado: {currentExpression}", 12, "#F8FAFC");
        publicDetails.Children.Add(outputStatus);
        publicDetails.Children.Add(Label("La previsualización usa el sprite asignado; si no existe, se muestra la imagen base y no se simula una expresión.", 11, "#98A4BE"));
        liveCenterReactionHandler = result => Dispatcher.BeginInvoke(new Action(() =>
        {
            outputStatus.Text = $"Última reacción: {result.Outcome} · {result.Detail}";
            if (result.Outcome == "Respuesta enviada" && result.Detail.Contains("expresión aplicada", StringComparison.Ordinal))
            {
                var state = store.GetSetting("avatar.state", "Idle");
                var snapshot = CaptureLiveCenterPreview(state);
                if (snapshot is not null) { preview.Source = snapshot; currentExpression = state; }
            }
        }));
        chatReactions.ResultChanged += liveCenterReactionHandler;
        Grid.SetColumn(publicDetails, 1);
        publicGrid.Children.Add(publicDetails);
        publicPanel.Children.Add(publicGrid);
        publicPanel.Children.Add(ActionButton("Mostrar overlay público", (_, _) =>
        {
            ShowOverlay();
            if (overlay is { IsVisible: true })
            {
                preview.Source = CaptureLiveCenterPreview(currentExpression) ?? preview.Source;
                outputStatus.Text = "Overlay visible; puede seleccionarse como fuente de ventana en OBS.";
            }
            else outputStatus.Text = "No se pudo mostrar el overlay; comprobá el perfil del avatar.";
        }, true));
        publicPanel.Children.Add(ActionButton("Ocultar overlay público", (_, _) =>
        {
            overlay?.Hide();
            outputStatus.Text = "Overlay oculto.";
            store.Log("Avatar", "Overlay ocultado desde Centro de directo.");
        }));
        root.Children.Add(publicPanel);

        var privatePanel = Panel("B. Controles privados · solo creador");
        var connectionStatus = Label(twitch.IsConnected ? "Twitch: conectado" : "Twitch: desconectado", 12, "#98A4BE");
        var avatarStatus = Label(!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath) ? "Avatar: perfil con imagen base" : "Avatar: falta imagen base", 12, "#98A4BE");
        var queueStatus = Label($"Cola de reacciones: {chatReactions.PendingCount}", 12, "#98A4BE");
        liveCenterQueueHandler = pending => Dispatcher.BeginInvoke(new Action(() => queueStatus.Text = $"Cola de reacciones: {pending}"));
        chatReactions.QueueChanged += liveCenterQueueHandler;
        privatePanel.Children.Add(connectionStatus);
        privatePanel.Children.Add(avatarStatus);
        privatePanel.Children.Add(queueStatus);

        var expressionButtons = new WrapPanel();
        foreach (var state in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
        {
            var hasSprite = AvatarExpressionAvailabilityPolicy.IsAvailable(
                state,
                !string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath),
                File.Exists(store.GetAvatarExpression(state) ?? ""));
            var button = ActionButton(state, (_, _) =>
            {
                if (!SetChatReactionExpression(state, CancellationToken.None))
                {
                    outputStatus.Text = $"No se mostró {state}: el overlay debe estar visible y el sprite debe existir.";
                    return;
                }
                outputStatus.Text = $"Expresión {state} aplicada al renderizador público.";
                preview.Source = CaptureLiveCenterPreview(state) ?? preview.Source;
                currentExpression = state;
            });
            button.IsEnabled = hasSprite;
            button.ToolTip = hasSprite ? "Aplicar al overlay público" : "Deshabilitado: no hay recurso de imagen asignado.";
            expressionButtons.Children.Add(button);
        }
        privatePanel.Children.Add(Label("Expresiones disponibles", 13, "#F8FAFC", true));
        privatePanel.Children.Add(expressionButtons);

        var pause = new CheckBox { Content = "Pausar todas las reacciones automáticas", IsChecked = chatReactions.IsPaused, Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 8, 0, 8) };
        pause.Checked += (_, _) => chatReactions.SetPaused(true);
        pause.Unchecked += (_, _) => chatReactions.SetPaused(false);
        privatePanel.Children.Add(pause);
        var ttsEnabled = new CheckBox { Content = "Activar lectura TTS independiente del chat", IsChecked = store.GetSetting("social.tts.enabled", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 0, 0, 8) };
        ttsEnabled.Checked += (_, _) => store.SetSetting("social.tts.enabled", "true");
        ttsEnabled.Unchecked += (_, _) => store.SetSetting("social.tts.enabled", "false");
        privatePanel.Children.Add(ttsEnabled);

        var testPanel = Panel("Prueba local sin Twitch real");
        var testUser = TextField("Usuario de prueba", "viewer_demo");
        var testMessage = TextField("Mensaje de prueba", "¡Hola, VTuber!");
        var testResult = Label("La prueba solo evalúa coincidencia y muestra la respuesta prevista; no envía chat, voz ni expresiones.", 12, "#98A4BE");
        testPanel.Children.Add(testUser);
        testPanel.Children.Add(testMessage);
        testPanel.Children.Add(ActionButton("Probar regla local", (_, _) =>
        {
            var result = chatReactions.TestMessage(testUser.Text.Trim(), testMessage.Text);
            testResult.Text = result.Outcome + ": " + result.Detail;
        }, true));
        testPanel.Children.Add(testResult);
        privatePanel.Children.Add(testPanel);

        var editor = Panel("Editor de reglas de palabras y frases");
        var rulesList = new ListBox { MinHeight = 110, MaxHeight = 190, DisplayMemberPath = nameof(ChatReactionRule.Name) };
        var nameField = TextField("Nombre de regla", "");
        var triggerField = TextField("Palabra o frase activadora", "");
        var matchType = new ComboBox { ItemsSource = new[] { "Mensaje exacto", "Palabra completa", "Frase contenida" }, SelectedIndex = 1 };
        var responseField = new TextBox { Text = "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxHeight = 140, ToolTip = "Una respuesta alternativa por línea. Admite {user}." };
        var speak = new CheckBox { Content = "Encolar respuesta en voz local", IsChecked = false, Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 8) };
        var expression = new ComboBox { ItemsSource = new[] { "(Sin expresión)", "Idle", "Talking", "Blink", "Happy", "Surprised" }, SelectedIndex = 0 };
        var globalCooldown = TextField("Cooldown global en segundos (0–3600)", "5");
        var userCooldown = TextField("Cooldown por usuario en segundos (0–3600)", "15");
        var perMinute = TextField("Máximo de ejecuciones por minuto (1–60)", "6");
        editor.Children.Add(rulesList);
        editor.Children.Add(Label("Nombre", 12, "#98A4BE")); editor.Children.Add(nameField);
        editor.Children.Add(Label("Activador", 12, "#98A4BE")); editor.Children.Add(triggerField);
        editor.Children.Add(Label("Tipo de coincidencia", 12, "#98A4BE")); editor.Children.Add(matchType);
        editor.Children.Add(Label("Respuestas alternativas · una por línea", 12, "#98A4BE")); editor.Children.Add(responseField);
        editor.Children.Add(speak);
        editor.Children.Add(Label("Expresión del avatar", 12, "#98A4BE")); editor.Children.Add(expression);
        editor.Children.Add(globalCooldown); editor.Children.Add(userCooldown); editor.Children.Add(perMinute);

        void RefreshRules(string? selectedId = null)
        {
            rulesList.ItemsSource = null;
            rulesList.ItemsSource = chatReactions.GetRules();
            if (selectedId is not null)
                rulesList.SelectedItem = chatReactions.GetRules().FirstOrDefault(x => x.Id == selectedId);
            queueStatus.Text = $"Cola de reacciones: {chatReactions.PendingCount}";
            connectionStatus.Text = twitch.IsConnected ? "Twitch: conectado" : "Twitch: desconectado";
        }
        rulesList.SelectionChanged += (_, _) =>
        {
            if (rulesList.SelectedItem is not ChatReactionRule rule) return;
            nameField.Text = rule.Name;
            triggerField.Text = rule.Trigger;
            matchType.SelectedIndex = rule.MatchType switch { ChatReactionMatchType.Exact => 0, ChatReactionMatchType.WholeWord => 1, _ => 2 };
            responseField.Text = string.Join(Environment.NewLine, rule.Responses);
            speak.IsChecked = rule.Speak;
            expression.SelectedItem = string.IsNullOrEmpty(rule.Expression) ? "(Sin expresión)" : rule.Expression;
            globalCooldown.Text = rule.GlobalCooldownSeconds.ToString();
            userCooldown.Text = rule.UserCooldownSeconds.ToString();
            perMinute.Text = rule.MaxPerMinute.ToString();
        };
        editor.Children.Add(ActionButton("Nueva regla (nace desactivada)", (_, _) =>
        {
            rulesList.SelectedItem = null;
            nameField.Text = ""; triggerField.Text = ""; responseField.Text = "";
            matchType.SelectedIndex = 1; speak.IsChecked = false; expression.SelectedIndex = 0;
            globalCooldown.Text = "5"; userCooldown.Text = "15"; perMinute.Text = "6";
        }));
        editor.Children.Add(ActionButton("Guardar regla", (_, _) =>
        {
            if (!int.TryParse(globalCooldown.Text, out var global) || global is < 0 or > 3600 ||
                !int.TryParse(userCooldown.Text, out var user) || user is < 0 or > 3600 ||
                !int.TryParse(perMinute.Text, out var minute) || minute is < 1 or > 60)
            {
                MessageBox.Show(this, "Revisá los cooldowns y el límite por minuto.", "Regla no guardada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var selected = rulesList.SelectedItem as ChatReactionRule;
            var responses = responseField.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var selectedMatch = matchType.SelectedIndex switch { 0 => ChatReactionMatchType.Exact, 1 => ChatReactionMatchType.WholeWord, _ => ChatReactionMatchType.Contains };
            var selectedExpression = expression.SelectedItem?.ToString() ?? "(Sin expresión)";
            var rule = new ChatReactionRule(selected?.Id ?? Guid.NewGuid().ToString("N"), nameField.Text, triggerField.Text,
                selectedMatch, responses, speak.IsChecked == true, selectedExpression == "(Sin expresión)" ? "" : selectedExpression,
                global, user, minute, selected?.Enabled ?? false);
            if (!chatReactions.SaveRule(rule, selected?.Id))
            {
                MessageBox.Show(this, "No se guardó la regla. Revisá los campos, las alternativas y el máximo de 100 reglas.", "Regla no guardada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            RefreshRules(rule.Id);
            testResult.Text = selected is null ? "Regla guardada desactivada. Activala explícitamente para usarla." : "Regla actualizada.";
        }, true));
        editor.Children.Add(ActionButton("Activar regla seleccionada", (_, _) =>
        {
            if (rulesList.SelectedItem is ChatReactionRule rule && chatReactions.SetRuleEnabled(rule.Id, true)) RefreshRules(rule.Id);
        }));
        editor.Children.Add(ActionButton("Pausar regla seleccionada", (_, _) =>
        {
            if (rulesList.SelectedItem is ChatReactionRule rule && chatReactions.SetRuleEnabled(rule.Id, false)) RefreshRules(rule.Id);
        }));
        editor.Children.Add(ActionButton("Subir prioridad", (_, _) =>
        {
            if (rulesList.SelectedItem is ChatReactionRule rule && chatReactions.MoveRule(rule.Id, -1)) RefreshRules(rule.Id);
        }));
        editor.Children.Add(ActionButton("Bajar prioridad", (_, _) =>
        {
            if (rulesList.SelectedItem is ChatReactionRule rule && chatReactions.MoveRule(rule.Id, 1)) RefreshRules(rule.Id);
        }));
        editor.Children.Add(ActionButton("Eliminar regla seleccionada", (_, _) =>
        {
            if (rulesList.SelectedItem is ChatReactionRule rule && MessageBox.Show(this, $"¿Eliminar la regla «{rule.Name}»?", "Eliminar regla", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                chatReactions.DeleteRule(rule.Id);
                RefreshRules();
            }
        }));
        privatePanel.Children.Add(editor);
        root.Children.Add(privatePanel);
        RefreshRules();
        return root;
    }
}
