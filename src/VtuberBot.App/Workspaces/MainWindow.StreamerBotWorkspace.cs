using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private readonly IStreamerBotClient streamerBot = new StreamerBotWebSocketClient();
    private readonly List<CheckBox> streamerBotEventChecks = [];
    private IReadOnlyList<StreamerBotAction> streamerBotActions = [];
    private TextBlock? streamerBotStateLabel;
    private TextBlock? streamerBotInfoLabel;
    private const string StreamerBotPasswordFile = "streamerbot-websocket.bin";

    private UIElement BuildStreamerBotWorkspace()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Conexión directa al servidor WebSocket de Streamer.bot. La presencia del ejecutable no indica conectividad. No se ejecutan acciones ni se activan atajos al recibir eventos.", 13, "#98A4BE"));

        var config = Panel("Servidor WebSocket");
        var host = TextField("127.0.0.1", store.GetSetting("integrations.streamerBot.ws.host", "127.0.0.1"));
        var port = TextField("8080", store.GetSetting("integrations.streamerBot.ws.port", "8080"));
        var endpoint = TextField("/", store.GetSetting("integrations.streamerBot.ws.endpoint", "/"));
        host.MinWidth = 220; port.Width = 100; endpoint.Width = 140;
        config.Children.Add(Label("Host", 12, "#98A4BE")); config.Children.Add(host);
        config.Children.Add(Label("Puerto", 12, "#98A4BE")); config.Children.Add(port);
        config.Children.Add(Label("Endpoint", 12, "#98A4BE")); config.Children.Add(endpoint);
        var auth = new CheckBox { Content = "El servidor exige autenticación", IsChecked = store.GetSetting("integrations.streamerBot.ws.auth", "false") == "true", Foreground = Brush("#D8DEEE"), Margin = new Thickness(0, 4, 0, 8) };
        config.Children.Add(auth);
        var password = new PasswordBox { Password = LoadStreamerBotPassword(), MinWidth = 260, Margin = new Thickness(0, 0, 0, 8) };
        config.Children.Add(Label("Contraseña WebSocket (guardada cifrada con DPAPI para este usuario de Windows)", 12, "#98A4BE"));
        config.Children.Add(password);
        var remoteWarning = Label(IsLoopbackHost(host.Text) ? "Host local. No se abrirán puertos ni se modificará el firewall." : "ADVERTENCIA: host remoto. WebSocket sin TLS no cifra el tráfico; usalo solo en una red confiable.", 12, "#E7B56A");
        config.Children.Add(remoteWarning);
        var configured = store.GetSetting("integrations.streamerBot.ws.configured", "false") == "true";
        var stateLabel = Label(configured ? "Estado: Configurado, desconectado — conectá para comprobar el servidor." : "Estado: Sin configurar — guardá los ajustes y conectá para comprobar el servidor.", 12, "#E7B56A", true);
        var infoLabel = Label("Instancia: aún no identificada", 12, "#98A4BE");
        streamerBotStateLabel = stateLabel;
        streamerBotInfoLabel = infoLabel;
        var errorLabel = Label("", 12, "#F2A6A6");
        config.Children.Add(stateLabel); config.Children.Add(infoLabel); config.Children.Add(errorLabel);

        void SaveConfiguration()
        {
            store.SetSetting("integrations.streamerBot.ws.host", host.Text.Trim());
            store.SetSetting("integrations.streamerBot.ws.port", port.Text.Trim());
            store.SetSetting("integrations.streamerBot.ws.endpoint", endpoint.Text.Trim());
            store.SetSetting("integrations.streamerBot.ws.auth", auth.IsChecked == true ? "true" : "false");
            store.SetSetting("integrations.streamerBot.ws.configured", "true");
            SaveStreamerBotPassword(password.Password);
            remoteWarning.Text = IsLoopbackHost(host.Text) ? "Host local. No se abren puertos ni se modifica el firewall." : "ADVERTENCIA: host remoto. ws:// no cifra el tráfico; verificá la red. No se abre ningún puerto.";
        }



        var connectionButtons = new WrapPanel();
        connectionButtons.Children.Add(ActionButton("Guardar configuración", (_, _) =>
        {
            try { SaveConfiguration(); errorLabel.Text = ""; FooterStatus.Text = "Ajustes guardados; contraseña protegida por DPAPI."; }
            catch (Exception ex) { errorLabel.Text = "No se pudo guardar: " + ex.GetType().Name; }
        }));
        connectionButtons.Children.Add(ActionButton("Conectar / probar", async (_, _) =>
        {
            try
            {
                SaveConfiguration();
                if (auth.IsChecked == true && string.IsNullOrEmpty(password.Password))
                    throw new InvalidOperationException("El servidor está marcado como autenticado, pero no se introdujo contraseña.");
                var portValue = int.TryParse(port.Text, out var parsedPort) ? parsedPort : -1;
                await streamerBot.ConnectAsync(new StreamerBotEndpoint(host.Text.Trim(), portValue, endpoint.Text.Trim()),
                    auth.IsChecked == true ? password.Password : null);
                errorLabel.Text = "";
            }
            catch (Exception ex) { errorLabel.Text = "Conexión no confirmada: " + ex.Message; }
        }, true));
        connectionButtons.Children.Add(ActionButton("Desconectar", async (_, _) =>
        {
            try { await streamerBot.DisconnectAsync(); errorLabel.Text = ""; }
            catch (Exception ex) { errorLabel.Text = "Desconexión: " + ex.GetType().Name; }
        }));
        config.Children.Add(connectionButtons);
        root.Children.Add(config);

        var actionsPanel = Panel("Acciones de la instancia");
        var actionPicker = new ComboBox { MinWidth = 420, DisplayMemberPath = nameof(StreamerBotAction.Name) };
        var actionStatus = Label("Todavía no se consultó GetActions.", 12, "#98A4BE");
        actionsPanel.Children.Add(actionPicker);
        var actionButtons = new WrapPanel();
        actionButtons.Children.Add(ActionButton("Consultar / actualizar acciones", async (_, _) =>
        {
            try
            {
                var loaded = await streamerBot.GetActionsAsync();
                streamerBotActions = loaded;
                actionPicker.ItemsSource = loaded;
                actionPicker.DisplayMemberPath = nameof(StreamerBotAction.Name);
                actionStatus.Text = loaded.Count == 0 ? "La instancia no devolvió acciones." : $"Acciones recibidas: {loaded.Count}. Seleccioná una para ver ID y estado.";
                errorLabel.Text = "";
            }
            catch (Exception ex) { actionStatus.Text = "GetActions falló: " + ex.Message; }
        }, true));
        actionButtons.Children.Add(ActionButton("Ejecutar acción seleccionada", async (_, _) =>
        {
            if (actionPicker.SelectedItem is not StreamerBotAction selected || !streamerBotActions.Any(a => a.Id == selected.Id))
            { actionStatus.Text = "Seleccioná una acción del catálogo recibido del servidor."; return; }
            actionStatus.Text = $"ID: {selected.Id} · habilitada: {selected.Enabled}";
            if (MessageBox.Show(this, $"Solicitar ejecución de «{selected.Name}»?\nID: {selected.Id}\n\nLa acción puede modificar el stream según su configuración.", "Confirmar acción", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                var result = await streamerBot.DoActionAsync(selected.Id);
                actionStatus.Text = result.Success ? "Respuesta OK: Streamer.bot aceptó la solicitud; el efecto final no está verificado." : "Streamer.bot rechazó la solicitud.";
                store.Log("Streamer.bot", "DoAction " + selected.Id + ": " + (result.Success ? "ok" : "error"));
            }
            catch (Exception ex) { actionStatus.Text = "Resultado no confirmado (timeout/desconexión/error): " + ex.Message; }
        }, true));
        actionsPanel.Children.Add(actionButtons); actionsPanel.Children.Add(actionStatus);
        actionPicker.SelectionChanged += (_, _) =>
        {
            if (actionPicker.SelectedItem is StreamerBotAction selected)
                actionStatus.Text = $"ID: {selected.Id} · habilitada: {selected.Enabled} · grupo: {selected.Group}";
        };
        root.Children.Add(actionsPanel);

        var eventsPanel = Panel("Eventos y suscripciones");
        eventsPanel.Children.Add(Label("Consultá GetEvents para descubrir las categorías admitidas por esta instancia. Después marcá eventos concretos y enviá Subscribe. Los eventos solo se registran; no activan acciones.", 12, "#98A4BE"));
        var eventsList = new StackPanel();
        eventsPanel.Children.Add(eventsList);
        var eventsStatus = Label("No se consultó GetEvents.", 12, "#98A4BE");
        eventsPanel.Children.Add(eventsStatus);
        var eventButtons = new WrapPanel();
        eventButtons.Children.Add(ActionButton("Consultar eventos disponibles", async (_, _) =>
        {
            try
            {
                var available = await streamerBot.GetEventsAsync();
                eventsList.Children.Clear(); streamerBotEventChecks.Clear();
                var initial = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "twitch.Follow", "twitch.Raid", "twitch.Cheer", "twitch.Sub", "twitch.ReSub", "twitch.GiftSub", "twitch.GiftBomb", "twitch.ChatMessage"
                };
                foreach (var category in available.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var group = Panel(category.Key);
                    foreach (var name in category.Value)
                    {
                        var check = new CheckBox { Content = name, Tag = category.Key + "." + name, IsChecked = initial.Contains(category.Key + "." + name), Foreground = Brush("#D8DEEE"), Margin = new Thickness(0, 2, 12, 2) };
                        streamerBotEventChecks.Add(check); group.Children.Add(check);
                    }
                    eventsList.Children.Add(group);
                }
                eventsStatus.Text = $"GetEvents devolvió {available.Sum(x => x.Value.Length)} eventos en {available.Count} categorías.";
            }
            catch (Exception ex) { eventsStatus.Text = "GetEvents falló: " + ex.Message; }
        }, true));
        eventButtons.Children.Add(ActionButton("Suscribir eventos seleccionados", async (_, _) =>
        {
            var selected = streamerBotEventChecks.Where(c => c.IsChecked == true && c.Tag is string).Select(c => ((string)c.Tag!).Split('.', 2)).Where(p => p.Length == 2).GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(p => p[1]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 0) { eventsStatus.Text = "No seleccionaste eventos."; return; }
            try
            {
                var confirmed = await streamerBot.SubscribeAsync(selected);
                eventsStatus.Text = "Respuesta Subscribe confirmada: " + string.Join("; ", confirmed.Select(kv => kv.Key + ": " + string.Join(", ", kv.Value)));
            }
            catch (Exception ex) { eventsStatus.Text = "Subscribe no confirmado: " + ex.Message; }
        }, true));
        eventButtons.Children.Add(ActionButton("Desmarcar todos", (_, _) => { foreach (var check in streamerBotEventChecks) check.IsChecked = false; }));
        eventsPanel.Children.Add(eventButtons);
        root.Children.Add(eventsPanel);
        return root;
    }

    private void HandleStreamerBotStateChanged(StreamerBotConnectionSnapshot snapshot) => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (streamerBotStateLabel is not null)
        {
            streamerBotStateLabel.Text = "Estado: " + snapshot.State + " — " + snapshot.Message;
            streamerBotStateLabel.Foreground = Brush(snapshot.State switch
            {
                StreamerBotConnectionState.Connected => "#8FE6D0",
                StreamerBotConnectionState.AuthenticationError or StreamerBotConnectionState.ProtocolError or StreamerBotConnectionState.ServerInaccessible or StreamerBotConnectionState.UnexpectedDisconnect => "#F2A6A6",
                _ => "#E7B56A"
            });
        }
        if (streamerBotInfoLabel is not null && snapshot.Instance is { } instance)
            streamerBotInfoLabel.Text = $"Instancia: {instance.Name} · versión {instance.Version} · ID {instance.InstanceId} · {instance.OperatingSystem}";
        FooterStatus.Text = "Streamer.bot: " + snapshot.Message;
    }));

    private void HandleStreamerBotEventReceived(StreamerBotEvent evt) => Dispatcher.BeginInvoke(new Action(() =>
    {
        var normalized = $"Streamer.bot/{evt.Source}/{evt.Type}";
        store.Log("Streamer.bot event", normalized);
        FooterStatus.Text = "Evento recibido: " + normalized;
    }));

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1";

    private string LoadStreamerBotPassword()
    {
        var path = Path.Combine(secretDirectory, StreamerBotPasswordFile);
        if (!File.Exists(path)) return string.Empty;
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); }
        catch { FooterStatus.Text = "No se pudo descifrar la contraseña de Streamer.bot; introducila de nuevo."; return string.Empty; }
    }

    private void SaveStreamerBotPassword(string value)
    {
        var path = Path.Combine(secretDirectory, StreamerBotPasswordFile);
        if (string.IsNullOrEmpty(value)) { if (File.Exists(path)) File.Delete(path); return; }
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }
}
