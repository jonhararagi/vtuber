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
}
