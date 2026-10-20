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
            "Twitch dispone de chat, EventSub y reglas de moderación. Kick dispone de API para moderación manual y recepción de eventos por webhook cuando configurás una URL pública HTTPS, el túnel y las suscripciones. OBS sigue siendo el motor de transmisión.",
            12, "#98A4BE"));

        var summary = Panel("Estado real de conexiones");
        var twitchState = Label(
            twitch.IsConnected ? "Twitch chat: conectado" : "Twitch chat: desconectado",
            12, twitch.IsConnected ? "#8FE6D0" : "#E7B56A", true);
        var eventSubState = Label(
            eventSub.IsConnected ? "Twitch EventSub: conectado" : "Twitch EventSub: desconectado",
            12, eventSub.IsConnected ? "#8FE6D0" : "#98A4BE");
        string KickSummaryText()
        {
            if (!secretStore.HasSecret("kick-access-token"))
                return "Kick: sin autorización guardada.";
            var verifiedText = store.GetSetting("kick.token.lastVerifiedAtUtc");
            if (DateTimeOffset.TryParse(verifiedText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var verifiedAt))
                return $"Kick: token local; última verificación API {verifiedAt.ToLocalTime():dd/MM/yyyy HH:mm} (no es conexión de chat entrante).";
            return "Kick: token cifrado local, aún sin verificación de API.";
        }
        var kickState = Label(KickSummaryText(), 12, "#98A4BE");
        summary.Children.Add(twitchState);
        summary.Children.Add(eventSubState);
        summary.Children.Add(kickState);
        summary.Children.Add(ActionButton("Actualizar estados", (_, _) =>
        {
            twitchState.Text = twitch.IsConnected ? "Twitch chat: conectado" : "Twitch chat: desconectado";
            twitchState.Foreground = Brush(twitch.IsConnected ? "#8FE6D0" : "#E7B56A");
            eventSubState.Text = eventSub.IsConnected ? "Twitch EventSub: conectado" : "Twitch EventSub: desconectado";
            eventSubState.Foreground = Brush(eventSub.IsConnected ? "#8FE6D0" : "#98A4BE");
            kickState.Text = KickSummaryText();
            kickState.Foreground = Brush("#98A4BE");
            FooterStatus.Text = "Estados locales actualizados; Kick solo se marca como verificado tras una consulta real a su API.";
        }));

        var tabs = new TabControl { MinHeight = 640, Margin = new Thickness(0, 0, 0, 10) };
        tabs.Items.Add(new TabItem { Header = "Twitch", Content = BuildTwitchPage() });
        tabs.Items.Add(new TabItem { Header = "Kick", Content = BuildKickBotPage() });
        tabs.Items.Add(new TabItem { Header = "Reglas y voz (Twitch actual)", Content = BuildSocialBotPage() });
        tabs.Items.Add(new TabItem { Header = "Streamer.bot (opcional)", Content = BuildStreamerBotWorkspace() });
        tabs.Items.Add(new TabItem { Header = "¿Cómo funciona?", Content = BuildBotsQuickGuide() });

        root.Children.Add(summary);
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildKickBotPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Bot de Kick · API oficial", 20, "#F8FAFC", true));
        root.Children.Add(Label(
            "Esta versión autoriza tu propia aplicación Kick, protege los tokens con DPAPI y permite consultar el canal, enviar mensajes y moderar. También incluye receptor local con verificación de firma y suscripción de eventos; para activarlo debés configurar una URL pública HTTPS/túnel en Kick Developer e iniciar la escucha manualmente. No se afirma que haya entrega entrante hasta recibir un evento real.",
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
            " · Scopes solicitados: user:read, channel:read, chat:write, moderation:ban y moderation:chat_message:manage. Volvé a autorizar Kick para conceder los nuevos permisos. No pegues el secreto en GitHub ni en mensajes de soporte.",
            11, "#98A4BE"));

        var credentialStatus = Label(
            secretStore.HasSecret("kick-access-token")
                ? "Hay una sesión guardada cifrada. Pulsa Verificar canal para consultar de nuevo la API."
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
            // An empty optional password field means "keep the saved secret".
            // Deletion is exposed separately and asks for explicit confirmation.
            secretStore.SaveIfProvided("kick-client-secret", clientSecret.Password);
        }

        var credentialActions = new WrapPanel();
        credentialActions.Children.Add(ActionButton("Guardar configuración segura", (_, _) =>
        {
            try
            {
                var secretWasProvided = !string.IsNullOrWhiteSpace(clientSecret.Password);
                SaveKickSettings();
                var secretExists = secretStore.HasSecret("kick-client-secret");
                var secretStatus = secretWasProvided
                    ? "Client Secret guardado cifrado con DPAPI."
                    : secretExists
                        ? "Se conservó el Client Secret cifrado que ya estaba guardado."
                        : "No hay Client Secret guardado; ingresalo antes de autorizar Kick.";
                credentialStatus.Text = (secretStore.HasSecret("kick-access-token")
                    ? "Configuración guardada. El token existe, pero el canal debe verificarse al usar «Verificar canal»."
                    : "Configuración guardada localmente; todavía no hay sesión Kick.") + " " + secretStatus;
                credentialStatus.Foreground = Brush("#E7B56A");
                FooterStatus.Text = "Configuración Kick guardada. " + secretStatus + " No se escribieron secretos en logs.";
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
                store.SetSetting("kick.token.lastVerifiedAtUtc", "");
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
        credentialActions.Children.Add(ActionButton("Eliminar Client Secret guardado…", (_, _) =>
        {
            if (MessageBox.Show(this,
                "Se eliminará únicamente el Client Secret guardado en este equipo. No se eliminarán los tokens OAuth ni se revocará el acceso en Kick. Para autorizar de nuevo, tendrás que ingresar el secreto otra vez. ¿Continuar?",
                "Eliminar Client Secret local", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                secretStore.Delete("kick-client-secret");
                clientSecret.Clear();
                credentialStatus.Text = secretStore.HasSecret("kick-access-token")
                    ? "Client Secret local eliminado. La sesión existente se conserva; renovar o autorizar puede requerir ingresar el secreto otra vez."
                    : "Client Secret local eliminado. No se modificó ninguna autorización remota.";
                credentialStatus.Foreground = Brush("#E7B56A");
                FooterStatus.Text = "Client Secret local eliminado tras confirmación; no se borraron tokens ni configuración del canal.";
                store.Log("Kick", "El usuario eliminó el Client Secret local tras confirmación; no se registraron secretos.");
            }
            catch (Exception exception)
            {
                credentialStatus.Text = "No se pudo eliminar el Client Secret: " + exception.GetType().Name;
                FooterStatus.Text = "No se eliminó el Client Secret local.";
            }
        }));
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
            kickWebhookReceiver?.IsRunning == true
                ? "Receptor local activo. La llegada real se confirma al ver mensajes verificados aquí."
                : "Receptor local detenido. Configurá URL pública HTTPS y túnel, iniciá el receptor y suscribí eventos; la firma se verifica antes del procesamiento.",
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
                store.SetSetting("kick.token.lastVerifiedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
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
            secretStore.Delete("kick-app-access-token");
            store.SetSetting("kick.appToken.expiresAtUtc", "");
            store.SetSetting("kick.token.expiresAtUtc", "");
            store.SetSetting("kick.token.scopes", "");
            store.SetSetting("kick.token.lastVerifiedAtUtc", "");
            credentialStatus.Text = "Sesión eliminada de este equipo. La autorización remota no fue revocada.";
            credentialStatus.Foreground = Brush("#98A4BE");
            channelStatus.Text = "Canal: sesión local eliminada.";
            FooterStatus.Text = "Se borraron los tokens locales. No se modificó ninguna configuración remota.";
            store.Log("Kick", "Tokens locales eliminados a pedido del usuario; no se afirmó revocación remota.");
        }));
        root.Children.Add(actionPanel);

        var eventPanel = Panel("3. Chat y eventos entrantes de Kick");
        var webhookUrl = TextField("URL pública HTTPS terminada en /kick-webhook", store.GetSetting("kick.webhook.public_url"));
        var webhookStatus = Label(
            store.GetSetting("kick.webhook.lastStatus", "Aún no se configuró ni inició el receptor de eventos."),
            12, "#E7B56A", true);
        eventPanel.Children.Add(Label(
            "La app escucha solamente en " + EnsureKickWebhookReceiver().LocalEndpoint +
            ". Kick no puede llamar a localhost directamente: tenés que crear vos un túnel HTTPS público (por ejemplo Cloudflare Tunnel o ngrok) que reenvíe esta ruta a esa dirección local, y registrar la URL HTTPS exacta en Kick Developer con Enable Webhooks activado. VTuberBot no abre puertos públicos ni crea túneles automáticamente.",
            12, "#98A4BE"));
        eventPanel.Children.Add(Label("URL pública registrada en Kick Developer", 12, "#98A4BE"));
        eventPanel.Children.Add(webhookUrl);
        eventPanel.Children.Add(webhookStatus);

        eventPanel.Children.Add(ActionButton("Guardar URL pública", (_, _) =>
        {
            if (!IsConfiguredPublicKickWebhookUrl(webhookUrl.Text, out var configuredUri))
            {
                MessageBox.Show(this,
                    "Ingresá una URL pública HTTPS con la ruta exacta /kick-webhook, sin query ni fragmento. No sirven localhost ni direcciones loopback.",
                    "URL de webhook no válida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            store.SetSetting("kick.webhook.public_url", configuredUri!.AbsoluteUri.TrimEnd('/'));
            var status = "URL guardada. Registrá la misma URL exacta en Kick Developer y configurá el túnel para reenviar a " +
                EnsureKickWebhookReceiver().LocalEndpoint + ". Guardar la URL no prueba que Kick pueda alcanzarla.";
            store.SetSetting("kick.webhook.lastStatus", status);
            webhookStatus.Text = status;
            webhookStatus.Foreground = Brush("#E7B56A");
            FooterStatus.Text = "URL pública guardada; no se realizó ninguna conexión externa.";
        }, true));

        eventPanel.Children.Add(ActionButton("Iniciar receptor local", (_, _) =>
        {
            if (!IsConfiguredPublicKickWebhookUrl(webhookUrl.Text, out var configuredUri))
            {
                MessageBox.Show(this, "Guardá primero una URL pública HTTPS válida terminada en /kick-webhook.", "Falta URL pública",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            store.SetSetting("kick.webhook.public_url", configuredUri!.AbsoluteUri.TrimEnd('/'));
            try
            {
                var receiver = EnsureKickWebhookReceiver();
                receiver.Start();
                var status = "Receptor local activo en " + receiver.LocalEndpoint +
                    ". Esto confirma solo la escucha local, no una entrega real desde Kick. Comprobá que tu túnel esté activo.";
                store.SetSetting("kick.webhook.lastStatus", status);
                webhookStatus.Text = status;
                webhookStatus.Foreground = Brush("#E7B56A");
                FooterStatus.Text = "Receptor local de Kick iniciado; esperando eventos firmados.";
                eventStatus.Text = "Receptor local en marcha; la entrega pública todavía requiere verificación.";
            }
            catch (Exception exception)
            {
                webhookStatus.Text = "No se pudo iniciar el receptor local: " + exception.Message;
                webhookStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "No se inició el receptor webhook de Kick.";
            }
        }));

        eventPanel.Children.Add(ActionButton("Suscribir chat y eventos", async (_, _) =>
        {
            if (!IsConfiguredPublicKickWebhookUrl(webhookUrl.Text, out var configuredUri))
            {
                MessageBox.Show(this, "Guardá primero una URL pública HTTPS válida y registrala en Kick Developer.", "Falta URL pública",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (kickWebhookReceiver is null || !kickWebhookReceiver.IsRunning)
            {
                MessageBox.Show(this, "Iniciá primero el receptor local y asegurate de que el túnel HTTPS público ya esté funcionando.",
                    "Receptor detenido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show(this,
                    "VTuberBot solicitará suscripciones reales de eventos para el canal de Kick configurado. La creación se hará con un App Access Token y se omitirán los tipos ya suscritos según la consulta de la API. Una suscripción aceptada no garantiza que Kick entregue eventos. ¿Continuar?",
                    "Confirmar suscripciones de Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                SaveKickSettings();
                store.SetSetting("kick.webhook.public_url", configuredUri!.AbsoluteUri.TrimEnd('/'));
                var userCredentials = await GetValidKickCredentialsAsync();
                var api = new KickApiClient();
                var channelInfo = await api.GetChannelAsync(userCredentials.AccessToken, channel.Text);
                var appToken = await GetValidKickAppAccessTokenAsync();
                var results = await api.SubscribeToDefaultEventsAsync(appToken, channelInfo.BroadcasterUserId);
                var storedIds = store.GetSetting("kick.webhook.subscription_ids")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var item in results.Where(item => !string.IsNullOrWhiteSpace(item.SubscriptionId) && string.IsNullOrWhiteSpace(item.Error)))
                    storedIds.Add(item.SubscriptionId!);
                store.SetSetting("kick.webhook.subscription_ids", string.Join('\n', storedIds));

                var failures = results.Where(item => !string.IsNullOrWhiteSpace(item.Error)).ToArray();
                var accepted = results.Where(item => !string.IsNullOrWhiteSpace(item.SubscriptionId) && string.IsNullOrWhiteSpace(item.Error)).ToArray();
                var summary = "Kick devolvió " + accepted.Length + " suscripciones aceptadas para " + channelInfo.Slug +
                    " y " + failures.Length + " errores. Esto confirma el resultado API, no la entrega al webhook.";
                if (failures.Length > 0)
                    summary += "\n" + string.Join("\n", failures.Select(item => item.Name + ": " + item.Error));
                store.SetSetting("kick.webhook.lastStatus", summary);
                webhookStatus.Text = summary;
                webhookStatus.Foreground = Brush(failures.Length == 0 ? "#8FE6D0" : "#F2A6A6");
                eventStatus.Text = summary;
                eventStatus.Foreground = Brush(failures.Length == 0 ? "#8FE6D0" : "#F2A6A6");
                FooterStatus.Text = failures.Length == 0
                    ? "Kick aceptó las suscripciones; falta verificar recepción real."
                    : "Kick aceptó algunas suscripciones y rechazó otras; revisá el detalle.";
                store.Log("Kick Events", "Resultado de suscripción: " + accepted.Length + " aceptadas, " + failures.Length + " fallidas; no se afirmó entrega real.");
            }
            catch (Exception exception)
            {
                var status = "No se completó la suscripción de eventos de Kick: " + exception.Message;
                store.SetSetting("kick.webhook.lastStatus", status);
                webhookStatus.Text = status;
                webhookStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "Suscripciones de Kick no confirmadas.";
                store.Log("Kick Events", "La suscripción falló (" + exception.GetType().Name + "); no se declaró conectada.");
            }
        }, true));

        eventPanel.Children.Add(ActionButton("Cancelar suscripciones registradas", async (_, _) =>
        {
            var subscriptionIds = store.GetSetting("kick.webhook.subscription_ids")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (subscriptionIds.Length == 0)
            {
                MessageBox.Show(this, "No hay IDs de suscripción guardados por VTuberBot. Si creaste suscripciones por fuera de la app, gestionálas desde la API o las herramientas de Kick Developer.",
                    "No hay suscripciones registradas", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(this,
                    "Se cancelarán " + subscriptionIds.Length + " suscripciones de eventos cuyos IDs quedaron guardados por VTuberBot. No se borrarán los tokens OAuth. ¿Continuar?",
                    "Confirmar cancelación de webhooks", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                var appToken = await GetValidKickAppAccessTokenAsync();
                await new KickApiClient().DeleteEventSubscriptionsAsync(appToken, subscriptionIds);
                store.SetSetting("kick.webhook.subscription_ids", "");
                var status = "Kick confirmó la cancelación de las suscripciones que VTuberBot tenía registradas.";
                store.SetSetting("kick.webhook.lastStatus", status);
                webhookStatus.Text = status;
                webhookStatus.Foreground = Brush("#8FE6D0");
                FooterStatus.Text = status;
                store.Log("Kick Events", "Kick confirmó cancelar las suscripciones guardadas por VTuberBot.");
            }
            catch (Exception exception)
            {
                var status = "No se confirmó la cancelación de suscripciones: " + exception.Message;
                webhookStatus.Text = status;
                webhookStatus.Foreground = Brush("#F2A6A6");
                FooterStatus.Text = "Cancelación de suscripciones no confirmada.";
                store.Log("Kick Events", "Falló cancelación de suscripciones (" + exception.GetType().Name + ").");
            }
        }));

        eventPanel.Children.Add(ActionButton("Detener receptor local", async (_, _) =>
        {
            try
            {
                await StopKickWebhookReceiverAsync();
                var status = "Receptor local detenido. Las suscripciones remotas pueden seguir activas; cancelalas arriba si ya no querés recibirlas.";
                store.SetSetting("kick.webhook.lastStatus", status);
                webhookStatus.Text = status;
                webhookStatus.Foreground = Brush("#E7B56A");
                FooterStatus.Text = "Receptor local de Kick detenido; revisá las suscripciones remotas.";
            }
            catch (Exception exception)
            {
                webhookStatus.Text = "No se pudo detener el receptor: " + exception.Message;
                webhookStatus.Foreground = Brush("#F2A6A6");
            }
        }));

        root.Children.Add(eventPanel);
        root.Children.Add(Label(
            "Moderación automática de Kick tiene un interruptor independiente en Moderación y permanece desactivada por defecto. Los eventos solo afectan reglas después de verificar firma, antigüedad e ID duplicado. La primera prueba real requiere una cuenta autorizada y la URL pública/túnel configurados por vos.",
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
