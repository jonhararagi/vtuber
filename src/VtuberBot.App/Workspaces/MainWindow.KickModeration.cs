using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace VtuberBot.App;

public partial class MainWindow
{
    private async Task<KickOAuthCredentials> GetValidKickModerationCredentialsAsync()
    {
        var accessToken = secretStore.Read("kick-access-token");
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Primero autorizá Kick desde Bots y plataformas.");

        if (!DateTimeOffset.TryParse(store.GetSetting("kick.token.expiresAtUtc"),
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
            expiresAt = DateTimeOffset.MinValue;

        var refreshToken = secretStore.Read("kick-refresh-token");
        if (expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            var scopes = store.GetSetting("kick.token.scopes")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new KickOAuthCredentials(accessToken, refreshToken, expiresAt, scopes);
        }

        var clientId = store.GetSetting("kick.client_id").Trim();
        var clientSecret = secretStore.Read("kick-client-secret");
        if (string.IsNullOrWhiteSpace(refreshToken) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("La sesión de Kick venció o no puede renovarse. Volvé a autorizar Kick desde Bots y plataformas.");

        var refreshed = await KickOAuthClient.RefreshAsync(clientId, clientSecret, refreshToken);
        SaveKickCredentials(refreshed);
        return refreshed;
    }
    private UIElement BuildKickModerationPanel()
    {
        var panel = Panel("Acciones de Kick — manuales y con confirmación");
        panel.Margin = new Thickness(0, 12, 0, 0);
        panel.Children.Add(Label(
            "Las acciones modifican el chat real y exigen los permisos de moderación de Kick. Si faltan permisos, volvé a autorizar Kick desde Bots y plataformas. La automatización del chat de Kick no está activa: aún falta el receptor público de webhooks y su verificación de firma.",
            12, "#E7B56A"));

        var channel = TextField("Canal de Kick (slug)", store.GetSetting("kick.channel_slug"));
        var target = TextField("ID numérico del usuario objetivo");
        var reason = TextField("Motivo opcional (máximo 100 caracteres)");
        var durations = new Dictionary<string, int>
        {
            ["1 minuto"] = 1, ["5 minutos"] = 5, ["10 minutos"] = 10,
            ["30 minutos"] = 30, ["1 hora"] = 60, ["1 día"] = 1440, ["7 días"] = 10080
        };
        var duration = new ComboBox { ItemsSource = durations.Keys.ToList(), SelectedItem = "10 minutos", MinWidth = 180 };
        panel.Children.Add(Label("Canal", 12, "#98A4BE"));
        panel.Children.Add(channel);
        panel.Children.Add(Label("ID numérico del usuario (no el nombre visible)", 12, "#98A4BE"));
        panel.Children.Add(target);
        panel.Children.Add(Label("Motivo", 12, "#98A4BE"));
        panel.Children.Add(reason);
        panel.Children.Add(Label("Duración del timeout", 12, "#98A4BE"));
        panel.Children.Add(duration);
        panel.Children.Add(Label(
            "Scopes oficiales requeridos: moderation:ban y moderation:chat_message:manage. Después de ampliar los permisos solicitados, es necesario reautorizar.",
            11, "#98A4BE"));
        panel.Children.Add(Label(
            "Este panel acepta el ID numérico de usuario porque la consulta pública integrada no convierte nombres visibles en IDs. El ID del mensaje para borrado debe ser el UUID real devuelto por Kick.",
            11, "#98A4BE"));
        var autoModeration = new CheckBox
        {
            Content = "Activar las reglas automáticas también para mensajes de Kick",
            IsChecked = store.GetSetting("kick.auto_moderation.enabled", "false") == "true",
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 8, 0, 8)
        };
        autoModeration.Checked += (_, _) =>
        {
            var choice = MessageBox.Show(this,
                "Las mismas reglas que configuraste arriba podrán publicar avisos, borrar mensajes o sancionar usuarios en Kick cuando llegue un webhook con firma válida. Broadcaster y moderadores se protegen; hay un cooldown de 5 minutos por usuario/regla. ¿Activar acciones automáticas reales en Kick?",
                "Activar moderación automática de Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes)
            {
                autoModeration.IsChecked = false;
                return;
            }
            store.SetSetting("kick.auto_moderation.enabled", "true");
            store.Log("Moderación Kick", "Moderación automática activada explícitamente por el usuario.");
        };
        autoModeration.Unchecked += (_, _) =>
        {
            store.SetSetting("kick.auto_moderation.enabled", "false");
            store.Log("Moderación Kick", "Moderación automática desactivada.");
        };
        panel.Children.Add(autoModeration);

        async Task ExecuteUserAction(string action, int? durationMinutes, bool removeSanction)
        {
            if (!long.TryParse(target.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var targetId) || targetId <= 0)
            {
                MessageBox.Show(this, "Ingresá el ID numérico positivo del usuario de Kick.", "ID de usuario no válido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string slug;
            try { slug = KickApiClient.NormalizeChannelSlug(channel.Text); }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Canal Kick no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var description = removeSanction
                ? $"Se intentará retirar la sanción del usuario ID {targetId} en kick.com/{slug}."
                : durationMinutes.HasValue
                    ? $"Se aplicará un timeout de {durationMinutes.Value} minutos al usuario ID {targetId} en kick.com/{slug}."
                    : $"Se aplicará un ban permanente al usuario ID {targetId} en kick.com/{slug}.";
            if (!removeSanction && !string.IsNullOrWhiteSpace(reason.Text))
                description += "\nMotivo: " + reason.Text.Trim();
            description += "\n\nEsta acción afecta al chat real de Kick. ¿Confirmás?";
            if (MessageBox.Show(this, description, "Confirmar moderación de Kick",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                store.SetSetting("kick.channel_slug", slug);
                var credentials = await GetValidKickModerationCredentialsAsync();
                var client = new KickApiClient();
                var result = removeSanction
                    ? await client.UnbanUserAsync(credentials.AccessToken, slug, targetId)
                    : await client.BanOrTimeoutUserAsync(credentials.AccessToken, slug, targetId, durationMinutes, reason.Text.Trim());
                store.Log("Moderación Kick", action + " confirmado por API para usuario ID " + targetId + " en " + slug + ".");
                FooterStatus.Text = result;
                MessageBox.Show(this, result, "Moderación de Kick", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "Kick no confirmó la acción: " + exception.GetType().Name;
                store.Log("Moderación Kick", "Falló " + action + " (" + exception.GetType().Name + "); sin declarar éxito.");
                MessageBox.Show(this,
                    "Kick no confirmó la acción. Comprobá los permisos en el canal, los scopes y si hace falta reautorizar la cuenta.\n\n" + exception.Message,
                    "Acción de Kick fallida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        var userActions = new WrapPanel();
        userActions.Children.Add(ActionButton("Aplicar timeout", async (_, _) =>
        {
            if (duration.SelectedItem is not string selected || !durations.TryGetValue(selected, out var minutes))
            {
                MessageBox.Show(this, "Elegí una duración válida.");
                return;
            }
            await ExecuteUserAction("timeout", minutes, false);
        }, true));
        userActions.Children.Add(ActionButton("Ban permanente", async (_, _) =>
            await ExecuteUserAction("ban permanente", null, false)));
        userActions.Children.Add(ActionButton("Quitar ban / timeout", async (_, _) =>
            await ExecuteUserAction("retirar sanción", null, true)));
        panel.Children.Add(userActions);

        var messageId = TextField("ID del mensaje de Kick");
        panel.Children.Add(Label("Eliminar un mensaje específico", 12, "#98A4BE", true));
        panel.Children.Add(messageId);
        panel.Children.Add(ActionButton("Eliminar mensaje de Kick", async (_, _) =>
        {
            var id = messageId.Text.Trim();
            if (id.Length is 0 or > 128 || id.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            {
                MessageBox.Show(this, "Ingresá el ID real del mensaje de Kick (máximo 128 caracteres, sin espacios).", "ID de mensaje no válido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string slug;
            try { slug = KickApiClient.NormalizeChannelSlug(channel.Text); }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Canal Kick no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show(this,
                    $"Se eliminará el mensaje {id} del chat de Kick. Canal de referencia: kick.com/{slug}.\n\n¿Confirmás?",
                    "Confirmar borrado de Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                var credentials = await GetValidKickModerationCredentialsAsync();
                var result = await new KickApiClient().DeleteChatMessageAsync(credentials.AccessToken, id);
                store.Log("Moderación Kick", "Borrado de mensaje confirmado por API en " + slug + ".");
                FooterStatus.Text = result;
                MessageBox.Show(this, result, "Moderación de Kick", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "Kick no confirmó el borrado: " + exception.GetType().Name;
                store.Log("Moderación Kick", "Falló el borrado de mensaje (" + exception.GetType().Name + "); sin declarar éxito.");
                MessageBox.Show(this,
                    "Kick no confirmó el borrado. Revisá el ID UUID, los permisos y el scope moderation:chat_message:manage. Si faltan scopes, reautorizá Kick desde Bots y plataformas.\n\n" + exception.Message,
                    "Borrado fallido", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));

        panel.Children.Add(Label(
            "Importante: las acciones de Kick aquí son manuales. La recepción automática de mensajes y la ejecución de reglas automáticas para Kick no están conectadas todavía.",
            12, "#98A4BE"));
        return panel;
    }
}
