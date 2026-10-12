using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Wpf;
using NAudio.Wave;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildTwitchPage()
    {
        var root=new StackPanel();var panel=Panel("Conexión de Twitch");root.Children.Add(panel);
        var channel=TextField("Nombre del canal",store.GetSetting("twitch.channel"));
        var clientId=TextField("Twitch Application Client ID",store.GetSetting("twitch.client_id"));
        var token=new PasswordBox{Password=LoadTwitchToken(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        twitchTokenBox = token;
        panel.Children.Add(Label("Canal",12,"#98A4BE"));panel.Children.Add(channel);
        panel.Children.Add(Label("Client ID de tu aplicación Twitch",12,"#98A4BE"));panel.Children.Add(clientId);
        panel.Children.Add(Label("Token OAuth de chat (no se registra en logs)",12,"#98A4BE"));panel.Children.Add(token);
        panel.Children.Add(Label("Usá OAuth de Twitch con permisos chat:read, chat:edit, moderator:read:followers, moderator:manage:banned_users, moderator:manage:chat_messages, channel:read:subscriptions y channel:read:redemptions. Registrá la URL de redirección local indicada abajo en la consola de desarrolladores. El token se cifra con DPAPI para el usuario actual y nunca se escribe en GitHub ni en el registro de actividad. Redirección: http://localhost:17563/callback/",12,"#98A4BE"));
        var speakAlerts = new CheckBox
        {
            Content = "Leer alertas de Twitch con la voz local",
            IsChecked = store.GetSetting("twitch.speak_alerts", "false") == "true",
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        speakAlerts.Checked += (_, _) => store.SetSetting("twitch.speak_alerts", "true");
        speakAlerts.Unchecked += (_, _) => store.SetSetting("twitch.speak_alerts", "false");
        panel.Children.Add(speakAlerts);
        panel.Children.Add(Label("Si la opción está activa, las alertas EventSub se agregan a la cola TTS local con prioridad alta; el audio actual no se interrumpe.", 12, "#98A4BE"));
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Autorizar con Twitch",async (_,_)=>{try{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());FooterStatus.Text="Esperando autorización de Twitch en el navegador…";var credentials=await TwitchOAuth.AuthorizeCredentialsAsync(clientId.Text.Trim());SaveTwitchCredentials(credentials);token.Password=credentials.AccessToken;FooterStatus.Text="OAuth completado. Access y refresh token cifrados localmente; renovación automática habilitada cuando Twitch entrega refresh token.";store.Log("Twitch","Flujo OAuth completado; los tokens no se registran.");}catch(Exception ex){FooterStatus.Text=$"OAuth no completado: {ex.Message}";MessageBox.Show(this,$"No se pudo completar OAuth. Confirmá que el Client ID sea válido y que la URL de redirección registrada sea {TwitchOAuth.RedirectUri}\n\nDetalle: {ex.Message}");}}));
        actions.Children.Add(ActionButton("Guardar configuración segura",(_,_)=>{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());var saved=LoadTwitchCredentials();if(string.IsNullOrWhiteSpace(token.Password))SaveTwitchToken("");else if(saved is null||!string.Equals(saved.AccessToken,token.Password.Trim(),StringComparison.Ordinal))SaveTwitchToken(token.Password);FooterStatus.Text="Canal y credenciales guardados localmente; aún no conectado.";store.Log("Twitch","Configuración local guardada sin registrar tokens.");},true));
        actions.Children.Add(ActionButton("Conectar",async (_,_)=>{try{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());var credentials=await EnsureValidTwitchCredentialsAsync(clientId.Text,token.Password);token.Password=credentials.AccessToken;twitchChatDesired=true;await twitch.ConnectAsync(channel.Text,credentials.AccessToken);}catch(Exception ex){twitchChatDesired=false;FooterStatus.Text=$"Twitch: {ex.Message}";store.Log("Twitch",$"Falló conexión: {ex.GetType().Name}; revisá configuración y permisos.");MessageBox.Show(this,$"No se pudo conectar a Twitch. Revisá canal, Client ID, token OAuth, permisos y conexión a Internet.\n\nDetalle: {ex.Message}");}}));
        actions.Children.Add(ActionButton("Conectar EventSub (alertas)",async (_,_)=>{try{store.SetSetting("twitch.client_id",clientId.Text.Trim());store.SetSetting("twitch.channel",channel.Text.Trim());var credentials=await EnsureValidTwitchCredentialsAsync(clientId.Text,token.Password);token.Password=credentials.AccessToken;await eventSub.ConnectAsync(clientId.Text.Trim(),credentials.AccessToken,channel.Text.Trim());twitchEventSubDesired=true;}catch(Exception ex){twitchEventSubDesired=false;FooterStatus.Text="EventSub no conectado: "+ex.Message;MessageBox.Show(this,"No se pudieron registrar los eventos oficiales. Si el token ya existía, volvé a autorizar para aceptar los scopes adicionales.\n\n"+ex.Message);}}));
        actions.Children.Add(ActionButton("Desconectar EventSub",async (_,_)=>{twitchEventSubDesired=false;await eventSub.DisconnectAsync();}));
        actions.Children.Add(ActionButton("Desconectar",async (_,_)=>{twitchChatDesired=false;await twitch.DisconnectAsync();}));
        panel.Children.Add(actions);
        var chatPanel=Panel("Chat en directo");chatPanel.Margin=new Thickness(0,16,0,0);
        var list=new ListBox{ItemsSource=chatLines,Height=260};chatPanel.Children.Add(list);
        var send=TextField("Escribí un mensaje para el chat…");var sendRow=new DockPanel();DockPanel.SetDock(sendRow, Dock.Left);sendRow.Children.Add(send);
        sendRow.Children.Add(ActionButton("Enviar",async (_,_)=>{try{await twitch.SendMessageAsync(channel.Text,send.Text);store.Log("Twitch","Mensaje enviado al chat.");send.Clear();}catch(Exception ex){FooterStatus.Text=$"No se pudo enviar: {ex.Message}";}}));chatPanel.Children.Add(sendRow);
        root.Children.Add(chatPanel);
        root.Children.Add(Label("La lectura de chat, los comandos y las alertas EventSub solo funcionan después de autenticación real. EventSub incluye follows, suscripciones, regalos, redenciones de puntos y raids hacia el canal, reintenta tras perder la sesión y puede leer alertas en voz local si activaste esa opción. La conexión IRC del chat reintenta automáticamente con espera creciente limitada; si Twitch rechaza definitivamente el token, volvé a autorizar la cuenta antes de reconectar.",12,"#98A4BE"));
        return root;
    }

    private void HandleTwitchMessage(string raw)
    {
        // Reserve capacity before posting to the UI Dispatcher. This bounds both the
        // dispatcher backlog and the messages waiting for asynchronous command processing.
        if (!twitchMessageSlots.Wait(0))
        {
            if (Interlocked.Exchange(ref twitchQueueOverflowNotificationPending, 1) == 0)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        store.Log("Twitch", "La cola de chat alcanzó 1000 mensajes; se descartan entradas nuevas hasta que baje la carga.")));
                }
                catch
                {
                    Interlocked.Exchange(ref twitchQueueOverflowNotificationPending, 0);
                }
            }
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                pendingTwitchMessages.Enqueue(raw);
                if (!isProcessingTwitchMessages)
                    _ = ProcessPendingTwitchMessagesAsync();
            }));
        }
        catch
        {
            // The window may be closing. Return the reserved slot without crashing the
            // IRC receiver or treating UI shutdown as a network failure.
            twitchMessageSlots.Release();
        }
    }

    private async Task ProcessPendingTwitchMessagesAsync()
    {
        if (isProcessingTwitchMessages)
            return;

        isProcessingTwitchMessages = true;
        try
        {
            while (pendingTwitchMessages.Count > 0)
            {
                var nextMessage = pendingTwitchMessages.Dequeue();
                try
                {
                    await ProcessTwitchMessageAsync(nextMessage);
                }
                catch (Exception exception)
                {
                    store.Log("Twitch", "Falló el procesamiento de un mensaje (" + exception.GetType().Name + ").");
                    FooterStatus.Text = "No se pudo procesar un mensaje de Twitch; revisá Actividad y diagnóstico.";
                }

                finally
                {
                    twitchMessageSlots.Release();
                    if (twitchMessageSlots.CurrentCount > MaxPendingTwitchMessages / 2)
                        Interlocked.Exchange(ref twitchQueueOverflowNotificationPending, 0);
                }
            }
        }
        finally
        {
            isProcessingTwitchMessages = false;
        }
    }

    private async Task ProcessTwitchMessageAsync(string raw)
    {
        if (!TwitchIrcMessage.TryParse(raw, out var parsed) || parsed is null ||
            !string.Equals(parsed.Command, "PRIVMSG", StringComparison.OrdinalIgnoreCase) ||
            parsed.Trailing is null)
        {
            store.Log("Twitch", "Se ignoró una línea IRC que no era un PRIVMSG válido.");
            return;
        }

        var prefix = parsed.Prefix ?? string.Empty;
        var bang = prefix.IndexOf('!');
        var username = (bang > 0 ? prefix[..bang] : prefix).ToLowerInvariant();
        if (!Regex.IsMatch(username, "^[a-zA-Z0-9_]{1,25}$"))
            username = "viewer";
        var text = parsed.Trailing;

        chatLines.Insert(0, username + ": " + text);
        while (chatLines.Count > 200) chatLines.RemoveAt(chatLines.Count - 1);
        store.Log("Twitch", "Mensaje recibido de " + username + ".");

        var autoModerationEnabled = store.GetSetting("twitch.auto_moderation.enabled", "false") == "true";
        var messageId = parsed.Tags.TryGetValue("id", out var taggedMessageId) ? taggedMessageId : string.Empty;
        var channelLogin = store.GetSetting("twitch.channel");
        var moderationRoles = CommandPermissionPolicy.GetTwitchRoles(parsed, channelLogin);
        var skipMessageSideEffects = false;
        foreach (var rule in store.GetModerationRules().Where(rule => rule.Enabled))
        {
            try
            {
                if (!Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                    continue;

                store.Log("Moderación", "Coincidencia de regla " + rule.Name + " en mensaje de " + username +
                    " (acción " + rule.Action + "; global " + (autoModerationEnabled ? "activa" : "inactiva") + ").");

                var cooldownKey = rule.Name + ":" + username;
                var moderationNow = DateTimeOffset.UtcNow;
                automaticModerationCooldown.TryGetValue(cooldownKey, out var previousAction);
                var decision = AutomaticModerationPolicy.Evaluate(
                    rule.Action,
                    autoModerationEnabled,
                    moderationRoles,
                    messageId,
                    automaticModerationCooldown.ContainsKey(cooldownKey) ? previousAction : null,
                    moderationNow,
                    TimeSpan.FromMinutes(5));

                switch (decision)
                {
                    case AutomaticModerationDecision.LogOnly:
                    case AutomaticModerationDecision.Disabled:
                        continue;
                    case AutomaticModerationDecision.UnsupportedAction:
                        store.Log("Moderación", "Acción no reconocida; no se ejecutó: " + rule.Action);
                        continue;
                    case AutomaticModerationDecision.ProtectedUser:
                        store.Log("Moderación", "Acción automática omitida: se protegen broadcaster y moderadores de sanciones automáticas.");
                        continue;
                    case AutomaticModerationDecision.MissingMessageId:
                        store.Log("Moderación", "No se borró el mensaje porque Twitch no proporcionó un ID IRC válido.");
                        break;
                    case AutomaticModerationDecision.Cooldown:
                        store.Log("Moderación", "Acción automática omitida por límite de 5 minutos para " + username + " / " + rule.Name + ".");
                        break;
                    case AutomaticModerationDecision.Execute:
                        // A rule-violating message must not also earn points or execute a custom command,
                        // even if Twitch rejects the remote moderation request.
                        skipMessageSideEffects = true;
                        var succeeded = await ApplyAutomaticModerationAsync(rule.Name, rule.Action, username, messageId);
                        if (succeeded)
                            automaticModerationCooldown[cooldownKey] = DateTimeOffset.UtcNow;
                        // Apply at most one non-flag action per incoming message.
                        break;
                }

                if (decision is AutomaticModerationDecision.MissingMessageId or
                    AutomaticModerationDecision.Cooldown or AutomaticModerationDecision.Execute)
                    break;
            }
            catch (RegexMatchTimeoutException)
            {
                store.Log("Moderación", "La regla " + rule.Name + " agotó el tiempo de análisis.");
            }
        }

        if (skipMessageSideEffects)
            return;

        var now = DateTimeOffset.UtcNow;
        if (!lastPointAward.TryGetValue(username, out var last) || now - last >= TimeSpan.FromMinutes(5))
        {
            lastPointAward[username] = now;
            try { store.AddPoints(username, 1, "Actividad de chat"); }
            catch (Exception exception) { store.Log("Comunidad", "No se pudieron asignar puntos de actividad: " + exception.GetType().Name); }
        }

        if (!text.StartsWith('!'))
            return;

        var separatorIndex = text.IndexOf(' ');
        var command = separatorIndex < 0 ? text : text[..separatorIndex];
        var arguments = separatorIndex < 0 ? string.Empty : text[(separatorIndex + 1)..].Trim();
        if (string.Equals(command, "!entrar", StringComparison.OrdinalIgnoreCase))
        {
            var activeGiveaways = store.GetGiveaways()
                .Where(giveaway => string.Equals(giveaway.Status, "OPEN", StringComparison.Ordinal))
                .ToList();

            string reply;
            if (activeGiveaways.Count == 0)
            {
                reply = "No hay ningún sorteo abierto en este momento.";
            }
            else if (activeGiveaways.Count > 1)
            {
                reply = "Hay varios sorteos abiertos y no se puede determinar en cuál anotarte. Avisá al streamer para resolverlo desde el panel.";
                store.Log("Sorteos", "Se rechazó !entrar porque hay varios sorteos abiertos en la base de datos.");
            }
            else
            {
                var active = activeGiveaways[0];
                reply = store.EnterGiveaway(active.Id, username)
                    ? username + ", quedaste anotado en el sorteo " + active.Name + "."
                    : username + ", ya estabas anotado en el sorteo " + active.Name + ".";
            }

            try
            {
                if (twitch.IsConnected)
                    await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), reply);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo responder al chat: " + exception.Message;
            }
            return;
        }

        var twitchRoles = CommandPermissionPolicy.GetTwitchRoles(parsed, store.GetSetting("twitch.channel"));
        var result = store.RunLocalCommand(command, username, arguments, twitchRoles);
        if (result.Success)
        {
            try { await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), result.Message); }
            catch (Exception exception) { FooterStatus.Text = "Comando ejecutado localmente, respuesta no enviada: " + exception.Message; }
        }
    }

    private async Task<bool> ApplyAutomaticModerationAsync(string ruleName, string action, string username, string messageId)
    {
        var clientId = store.GetSetting("twitch.client_id").Trim();
        var accessToken = LoadTwitchToken();
        var channel = store.GetSetting("twitch.channel").Trim();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(channel))
        {
            store.Log("Moderación", "No se ejecutó '" + action + "' para " + username + ": Twitch no está autenticado/configurado.");
            return false;
        }

        try
        {
            string result;
            using var moderator = new TwitchModerationClient();
            switch (action)
            {
                case "warn":
                    if (!twitch.IsConnected)
                        throw new InvalidOperationException("El chat IRC no está conectado para enviar el aviso.");
                    await twitch.SendMessageAsync(channel,
                        username + ", tu mensaje coincidió con la regla de moderación '" + ruleName + "'. Revisá las normas del canal.");
                    result = "Aviso enviado a " + username + ".";
                    break;
                case "delete":
                    result = await moderator.DeleteChatMessageAsync(clientId, accessToken, channel, messageId);
                    break;
                case "timeout":
                    result = await moderator.BanOrTimeoutAsync(
                        clientId, accessToken, channel, username, 600,
                        "Regla automática: " + ruleName);
                    break;
                case "ban":
                    result = await moderator.BanOrTimeoutAsync(
                        clientId, accessToken, channel, username, null,
                        "Regla automática: " + ruleName);
                    break;
                default:
                    store.Log("Moderación", "Acción no ejecutada: '" + action + "' no está soportada.");
                    return false;
            }

            store.Log("Moderación", "Acción automática procesada: " +
                action + " · regla " + ruleName + " · usuario " + username + " · " + result);
            return true;
        }
        catch (Exception exception)
        {
            store.Log("Moderación", "Falló la acción automática '" + action + "' de la regla " +
                ruleName + " para " + username + " (" + exception.GetType().Name + "): " + exception.Message);
            FooterStatus.Text = "Moderación automática falló: " + action + " / " + ruleName;
            return false;
        }
    }
}
