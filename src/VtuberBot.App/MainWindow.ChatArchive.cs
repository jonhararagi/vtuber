using System.Globalization;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private ChatArchiveService? chatArchiveService;
    private ChatArchiveWindow? chatArchiveWindow;
    private DateTimeOffset lastChatArchiveQueueWarning = DateTimeOffset.MinValue;

    private ChatArchiveService GetChatArchiveService()
    {
        chatArchiveService ??= new ChatArchiveService(Path.Combine(dataDirectory, "chat-history.sqlite3"));
        return chatArchiveService;
    }

    private void OpenChatArchiveWindow()
    {
        try
        {
            if (chatArchiveWindow is null)
            {
                var created = new ChatArchiveWindow(store, GetChatArchiveService()) { Owner = this };
                chatArchiveWindow = created;
                created.Closed += (_, _) =>
                {
                    if (ReferenceEquals(chatArchiveWindow, created))
                        chatArchiveWindow = null;
                };
            }

            if (!chatArchiveWindow.IsVisible)
                chatArchiveWindow.Show();
            if (chatArchiveWindow.WindowState == WindowState.Minimized)
                chatArchiveWindow.WindowState = WindowState.Normal;
            chatArchiveWindow.Activate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo abrir el historial de chat (" + exception.GetType().Name + "). No se guardó ningún mensaje por abrir esta ventana.",
                "Historial de chat", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void QueueChatArchiveMessage(string platform, string channel, string username, string message, string? providerMessageId)
    {
        if (!string.Equals(store.GetSetting("chat.archive.enabled", "false"), "true", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(username))
            return;

        try
        {
            var retentionDays = int.TryParse(store.GetSetting("chat.archive.retentionDays", "30"),
                NumberStyles.None, CultureInfo.InvariantCulture, out var configuredDays)
                ? Math.Clamp(configuredDays, ChatArchiveStore.MinimumRetentionDays, ChatArchiveStore.MaximumRetentionDays)
                : 30;
            var queued = GetChatArchiveService().TryEnqueue(
                new ChatArchiveMessage(platform, channel, DateTimeOffset.UtcNow, username, message, providerMessageId),
                retentionDays);
            if (!queued)
                WarnChatArchiveQueuePressure("La cola del historial de chat está llena; se omitieron mensajes nuevos del archivo para no frenar el bot.");
        }
        catch (Exception exception)
        {
            WarnChatArchiveQueuePressure("El historial local no pudo aceptar un mensaje (" + exception.GetType().Name + "). El procesamiento normal del chat continúa.");
        }
    }

    private void WarnChatArchiveQueuePressure(string message)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastChatArchiveQueueWarning < TimeSpan.FromMinutes(1))
            return;
        lastChatArchiveQueueWarning = now;
        try { store.Log("Archivo de chat", message); }
        catch { }
    }
}
