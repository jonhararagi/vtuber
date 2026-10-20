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
        return panel;
    }
}
