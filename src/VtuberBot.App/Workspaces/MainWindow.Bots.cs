using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildBotsPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Bots y plataformas", 22, "#F8FAFC", true));
        root.Children.Add(Label(
            "VTuberBot es el centro de reglas y respuestas. Cada plataforma conserva sus cuentas, permisos y funciones propias; OBS sigue siendo el motor de transmisión. Los estados se basan en conexiones o respuestas reales, nunca en indicadores simulados.",
            12, "#98A4BE"));

        var summary = Panel("Estado real de conexiones");
        var twitchState = Label(
            twitch.IsConnected ? "Twitch chat: conectado" : "Twitch chat: desconectado",
            12, twitch.IsConnected ? "#8FE6D0" : "#E7B56A", true);
        var eventSubState = Label(
            eventSub.IsConnected ? "Twitch EventSub: conectado" : "Twitch EventSub: desconectado",
            12, eventSub.IsConnected ? "#8FE6D0" : "#98A4BE");
        var kickState = Label(
            secretStore.HasSecret("kick-access-token")
                ? "Kick: existe un token cifrado localmente; todavía no verificado en esta vista."
                : "Kick: sin autorización guardada.",
            12, "#E7B56A");
        summary.Children.Add(twitchState);
        summary.Children.Add(eventSubState);
        summary.Children.Add(kickState);
        summary.Children.Add(ActionButton("Actualizar estados", (_, _) =>
        {
            twitchState.Text = twitch.IsConnected ? "Twitch chat: conectado" : "Twitch chat: desconectado";
            twitchState.Foreground = Brush(twitch.IsConnected ? "#8FE6D0" : "#E7B56A");
            eventSubState.Text = eventSub.IsConnected ? "Twitch EventSub: conectado" : "Twitch EventSub: desconectado";
            eventSubState.Foreground = Brush(eventSub.IsConnected ? "#8FE6D0" : "#98A4BE");
            kickState.Text = secretStore.HasSecret("kick-access-token")
                ? "Kick: existe un token cifrado localmente; todavía no verificado en esta vista."
                : "Kick: sin autorización guardada.";
            kickState.Foreground = Brush(secretStore.HasSecret("kick-access-token") ? "#E7B56A" : "#98A4BE");
            FooterStatus.Text = "Estados locales actualizados; Kick solo se marca como verificado tras una consulta real a su API.";
        }));

        var tabs = new TabControl { MinHeight = 640, Margin = new Thickness(0, 0, 0, 10) };
        tabs.Items.Add(new TabItem { Header = "Twitch", Content = BuildTwitchPage() });
        tabs.Items.Add(new TabItem { Header = "Kick", Content = BuildKickBotPage() });
        tabs.Items.Add(new TabItem { Header = "Comportamiento del bot", Content = BuildSocialBotPage() });
        tabs.Items.Add(new TabItem { Header = "Streamer.bot (opcional)", Content = BuildStreamerBotWorkspace() });

        root.Children.Add(summary);
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildKickBotPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Bot de Kick · API oficial", 20, "#F8FAFC", true));
        root.Children.Add(Label(
            "Esta versión autoriza tu propia aplicación Kick, protege los tokens con DPAPI y puede verificar el canal y enviar un mensaje de prueba real. La recepción automática de mensajes/eventos todavía no se declara conectada: Kick los entrega por webhooks con URL pública y firma verificable.",
            12, "#98A4BE"));

        var config = Panel("1. Cuenta y aplicación");
        var clientId = TextField("Client ID de tu aplicación en Kick Developer", store.GetSetting("kick.client_id"));
        var clientSecret = new PasswordBox
        {
            Password = secretStore.Read("kick-client-secret"),
            MinWidth = 260,
            MaxLength = 8192,
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#34415E")
        };
        var channel = TextField("Canal de Kick (slug o URL kick.com/canal)", store.GetSetting("kick.channel_slug"));
        config.Children.Add(Label("Client ID", 12, "#98A4BE"));
        config.Children.Add(clientId);
        config.Children.Add(Label("Client Secret (se guarda cifrado para tu usuario de Windows)", 12, "#98A4BE"));
        config.Children.Add(clientSecret);
        config.Children.Add(Label("Canal destino", 12, "#98A4BE"));
        config.Children.Add(channel);
        config.Children.Add(Label(
            "En Kick Developer registrá exactamente esta Redirect URL: " + KickOAuthClient.RedirectUri +
            " · Scopes mínimos solicitados: user:read, channel:read y chat:write. No pegues el secreto en GitHub ni en mensajes de soporte.",
            11, "#98A4BE"));

        var credentialStatus = Label(
            secretStore.HasSecret("kick-access-token")
                ? "Hay una sesión guardada cifrada; todavía no se validó su token."
                : "Sin token de Kick guardado. Autorizá tu cuenta para continuar.",
            12, secretStore.HasSecret("kick-access-token") ? "#E7B56A" : "#98A4BE", true);
        config.Children.Add(credentialStatus);

        void SaveKickSettings()
        {
            var normalizedSlug = KickApiClient.NormalizeChannelSlug(channel.Text);
            var configuredClientId = clientId.Text.Trim();
            if (string.IsNullOrWhiteSpace(configuredClientId))
                throw new InvalidOperationException("Ingresá el Client ID de tu aplicación Kick.");
            store.SetSetting("kick.client_id", configuredClientId);
            store.SetSetting("kick.channel_slug", normalizedSlug);
            if (string.IsNullOrWhiteSpace(clientSecret.Password))
                secretStore.Delete("kick-client-secret");
            else
                secretStore.Save("kick-client-secret", clientSecret.Password);
        }

        var credentialActions = new WrapPanel();
        credentialActions.Children.Add(ActionButton("Guardar configuración segura", (_, _) =>
        {
            try
            {
                SaveKickSettings();
                credentialStatus.Text = secretStore.HasSecret("kick-access-token")
                    ? "Configuración guardada. El token existe, pero el canal debe verificarse al usar «Verificar canal»."
                    : "Configuración guardada localmente; todavía no hay sesión Kick.";
                credentialStatus.Foreground = Brush("#E7B56A");
                FooterStatus.Text = "Configuración Kick guardada; Client Secret cifrado con DPAPI y sin escribirlo en logs.";
                store.Log("Kick", "Se guardó configuración local sin registrar Client Secret ni tokens.");
            }
            catch (Exception exception)
            {
                credentialStatus.Text = "No se guardó: " + exception.Message;
                FooterStatus.Text = "Configuración Kick no guardada.";
            }
        }, true));
        credentialActions.Children.Add(ActionButton("Autorizar / reautorizar Kick", async (_, _) =>
        {
            try
            {
                SaveKickSettings();
                if (string.IsNullOrWhiteSpace(clientSecret.Password))
                    throw new InvalidOperationException("Ingresá el Client Secret de tu aplicación Kick antes de autorizar.");
                FooterStatus.Text = "Esperando autorización en el navegador de Kick…";
                credentialStatus.Text = "Esperando autorización de Kick; no se considera conectado hasta recibir el token.";
                var credentials = await KickOAuthClient.AuthorizeAsync(clientId.Text.Trim(), clientSecret.Password);
                SaveKickCredentials(credentials);
                credentialStatus.Text = "OAuth completado. Token y refresh token guardados cifrados con DPAPI. Verificá el canal antes de enviar mensajes.";
                credentialStatus.Foreground = Brush("#8FE6D0");
                FooterStatus.Text = "Kick OAuth completado; la sesión está guardada, pero todavía no se validó envío de chat.";
                store.Log("Kick", "OAuth completado; tokens y secretos no se registraron.");
            }
            catch (Exception exception)
            {
                credentialStatus.Text = "OAuth no completado: " + exception.Message;
                credentialStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "Kick OAuth falló; revisá el detalle y la Redirect URL registrada.";
                store.Log("Kick OAuth", "Falló la autorización (" + exception.GetType().Name + "); no se guardó contenido secreto.");
                MessageBox.Show(this,
                    exception.Message + "\n\nConfirmá que la Redirect URL sea exactamente " + KickOAuthClient.RedirectUri +
                    " y que Client ID/Secret correspondan a la misma aplicación. Si Kick devuelve HTTP 400 con cuerpo vacío, puede ser un fallo del propio endpoint OAuth.",
                    "Autorización Kick", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        credentialActions.Children.Add(ActionButton("Abrir Kick Developer", (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://kick.com/settings/developer") { UseShellExecute = true }); }
            catch (Exception exception) { FooterStatus.Text = "No se pudo abrir Kick Developer: " + exception.GetType().Name; }
        }));
        config.Children.Add(credentialActions);
        root.Children.Add(config);

        var actionPanel = Panel("2. Verificar canal y probar el chat");
        var channelStatus = Label("Canal: aún no consultado a Kick.", 12, "#98A4BE", true);
        var eventStatus = Label(
            "Eventos entrantes: no conectados. La API de Kick exige un webhook accesible desde Internet y validación de firma; no se finge una escucha local.",
            12, "#E7B56A");
        var messageType = new ComboBox
        {
            ItemsSource = new[] { "Usuario (canal indicado)", "Bot (canal asociado al token)" },
            SelectedIndex = store.GetSetting("kick.chat.messageType", "user") == "bot" ? 1 : 0,
            MinWidth = 270,
            Margin = new Thickness(0, 0, 8, 8)
        };
        var testMessage = TextField("Mensaje de prueba para el chat", "¡Hola chat! Prueba de VTuberBot.");
        actionPanel.Children.Add(channelStatus);
        actionPanel.Children.Add(Label("Tipo de envío", 12, "#98A4BE"));
        actionPanel.Children.Add(messageType);
        actionPanel.Children.Add(Label(
            "Recomendado para empezar: Usuario. Para aparecer como Bot, autorizá al dueño del canal destino y creá/activá el bot de esa app desde Kick Developer. El reporte público #343 documenta errores HTTP 500 cuando falta esa configuración o se usa el token de otra cuenta.",
            11, "#E7B56A"));
        actionPanel.Children.Add(Label("Mensaje de prueba (máximo oficial: 500 caracteres percibidos)", 12, "#98A4BE"));
        actionPanel.Children.Add(testMessage);
        actionPanel.Children.Add(eventStatus);

        async Task<KickOAuthCredentials> GetValidKickCredentialsAsync()
        {
            var accessToken = secretStore.Read("kick-access-token");
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("Primero elegí «Autorizar / reautorizar Kick».");
            if (!DateTimeOffset.TryParse(
                    store.GetSetting("kick.token.expiresAtUtc"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var expiresAt))
                expiresAt = DateTimeOffset.MinValue;

            var refreshToken = secretStore.Read("kick-refresh-token");
            if (expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                var scopes = store.GetSetting("kick.token.scopes").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new KickOAuthCredentials(accessToken, refreshToken, expiresAt, scopes);
            }

            var clientIdValue = store.GetSetting("kick.client_id").Trim();
            var clientSecretValue = secretStore.Read("kick-client-secret");
            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientSecretValue))
                throw new InvalidOperationException("El token de Kick venció o no tiene refresh token. Volvé a autorizar la cuenta desde el panel.");

            var refreshed = await KickOAuthClient.RefreshAsync(clientIdValue, clientSecretValue, refreshToken);
            SaveKickCredentials(refreshed);
            return refreshed;
        }

        actionPanel.Children.Add(ActionButton("Verificar canal", async (_, _) =>
        {
            try
            {
                var slug = KickApiClient.NormalizeChannelSlug(channel.Text);
                SaveKickSettings();
                var credentials = await GetValidKickCredentialsAsync();
                var result = await new KickApiClient().GetChannelAsync(credentials.AccessToken, slug);
                channelStatus.Text = $"API Kick verificada: {result.Slug} · ID {result.BroadcasterUserId}" +
                    (result.IsLive.HasValue ? (result.IsLive.Value ? " · EN VIVO" : " · offline") : "");
                channelStatus.Foreground = Brush("#8FE6D0");
                credentialStatus.Text = "Token aceptado por Kick mediante consulta real al canal.";
                credentialStatus.Foreground = Brush("#8FE6D0");
                FooterStatus.Text = "Kick respondió con información real del canal; esto no verifica recepción de webhooks.";
                store.Log("Kick", "Verificación real de canal completada; no se registraron tokens.");
            }
            catch (Exception exception)
            {
                channelStatus.Text = "No se pudo verificar: " + exception.Message;
                channelStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "La consulta a Kick falló; revisá autorización, scope channel:read y slug.";
            }
        }, true));

        actionPanel.Children.Add(ActionButton("Enviar mensaje de prueba", async (_, _) =>
        {
            var type = messageType.SelectedIndex == 1 ? "bot" : "user";
            string destinationSlug;
            try
            {
                destinationSlug = KickApiClient.NormalizeChannelSlug(channel.Text);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Canal Kick no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var preview = testMessage.Text.Length > 160 ? testMessage.Text[..160] + "…" : testMessage.Text;
            if (MessageBox.Show(this,
                    $"Se enviará un mensaje REAL a kick.com/{destinationSlug} como «{type}»:\n\n{preview}\n\nNo se puede retirar desde esta confirmación. ¿Continuar?",
                    "Confirmar envío al chat de Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                SaveKickSettings();
                store.SetSetting("kick.chat.messageType", type);
                var credentials = await GetValidKickCredentialsAsync();
                var result = await new KickApiClient().SendChatMessageAsync(
                    credentials.AccessToken, channel.Text, testMessage.Text, type);
                channelStatus.Text = "Kick confirmó el mensaje de prueba." +
                    (string.IsNullOrWhiteSpace(result.MessageId) ? "" : " ID: " + result.MessageId);
                channelStatus.Foreground = Brush("#8FE6D0");
                FooterStatus.Text = "Mensaje aceptado por la API de Kick; esto no confirma que un espectador lo haya visto.";
                store.Log("Kick", "La API confirmó envío del mensaje de prueba; contenido no registrado.");
            }
            catch (Exception exception)
            {
                channelStatus.Text = "El envío no se confirmó: " + exception.Message;
                channelStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "Kick no confirmó el envío. No asumas que el mensaje apareció en chat.";
                store.Log("Kick", "Falló el envío de prueba (" + exception.GetType().Name + "); contenido y tokens omitidos.");
                MessageBox.Show(this, exception.Message, "Prueba de chat Kick", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));

        actionPanel.Children.Add(ActionButton("Borrar sesión local", (_, _) =>
        {
            if (MessageBox.Show(this,
                    "Esto borra de este equipo los tokens guardados de Kick. No revoca el permiso en el servidor de Kick; para revocarlo, hacelo desde tu cuenta/app de Kick. ¿Continuar?",
                    "Borrar sesión de Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            secretStore.Delete("kick-access-token");
            secretStore.Delete("kick-refresh-token");
            store.SetSetting("kick.token.expiresAtUtc", "");
            store.SetSetting("kick.token.scopes", "");
            credentialStatus.Text = "Sesión eliminada de este equipo. La autorización remota no fue revocada.";
            credentialStatus.Foreground = Brush("#98A4BE");
            channelStatus.Text = "Canal: sesión local eliminada.";
            FooterStatus.Text = "Se borraron los tokens locales. No se modificó ninguna configuración remota.";
            store.Log("Kick", "Tokens locales eliminados a pedido del usuario; no se afirmó revocación remota.");
        }));
        root.Children.Add(actionPanel);
        root.Children.Add(Label(
            "Alcance actual: Twitch ya dispone de chat IRC autenticado y EventSub dentro de VTuberBot. Kick en este panel ofrece OAuth, consulta de canal y envío de chat por API. Los comandos/lectura automatizada del chat de Kick y alertas entrantes requieren implementar y probar el receptor de webhooks público; no están activos por el hecho de autorizar o enviar un mensaje.",
            11, "#E7B56A"));
        return root;
    }

    private void SaveKickCredentials(KickOAuthCredentials credentials)
    {
        secretStore.Save("kick-access-token", credentials.AccessToken);
        if (!string.IsNullOrWhiteSpace(credentials.RefreshToken))
            secretStore.Save("kick-refresh-token", credentials.RefreshToken);
        else
            secretStore.Delete("kick-refresh-token");
        store.SetSetting("kick.token.expiresAtUtc", credentials.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        store.SetSetting("kick.token.scopes", string.Join(' ', credentials.Scopes));
    }
}
