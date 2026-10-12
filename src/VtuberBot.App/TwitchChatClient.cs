using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace VtuberBot.App;

/// <summary>Small official Twitch IRC-over-TLS client. It never claims to be connected before registration succeeds.</summary>
public sealed class TwitchChatClient : IAsyncDisposable
{
    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private CancellationTokenSource? cancellation;
    private Task? receiveTask;
    private readonly SemaphoreSlim sendGate = new(1,1);
    public event Action<string>? MessageReceived;
    public event Action<string>? StatusChanged;
    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(string channel, string oauthToken, CancellationToken external = default)
    {
        if (IsConnected) throw new InvalidOperationException("Ya existe una conexión Twitch.");
        channel = channel.Trim().TrimStart('#').ToLowerInvariant();
        var token = oauthToken.Trim();
        if (channel.Length == 0 || token.Length == 0) throw new ArgumentException("Se necesitan canal y token OAuth.");
        if (token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)) token = token[6..];
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(external);
        client = new TcpClient();
        StatusChanged?.Invoke("Conectando a Twitch IRC sobre TLS…");
        try
        {
            await client.ConnectAsync("irc.chat.twitch.tv", 6697, cancellation.Token).ConfigureAwait(false);
            var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsClientAsync("irc.chat.twitch.tv", cancellation.Token).ConfigureAwait(false);
            reader = new StreamReader(ssl, Encoding.UTF8);
            writer = new StreamWriter(ssl, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync($"PASS oauth:{token}".AsMemory(), cancellation.Token).ConfigureAwait(false);
            await writer.WriteLineAsync($"NICK {channel}".AsMemory(), cancellation.Token).ConfigureAwait(false);
            await writer.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands".AsMemory(), cancellation.Token).ConfigureAwait(false);
            await writer.WriteLineAsync($"JOIN #{channel}".AsMemory(), cancellation.Token).ConfigureAwait(false);
            receiveTask = ReceiveLoopAsync(channel, cancellation.Token);
            // IRC sends a server welcome after valid registration. The receive loop is authoritative.
            await Task.Delay(350, cancellation.Token).ConfigureAwait(false);
            if (client.Connected) StatusChanged?.Invoke("Socket TLS abierto; esperando confirmación de Twitch.");
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task SendMessageAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || writer is null) throw new InvalidOperationException("Twitch no está conectado.");
        var safe = message.Replace("\r"," ").Replace("\n"," ").Trim();
        if (safe.Length is 0 or > 500) throw new ArgumentException("El mensaje debe tener entre 1 y 500 caracteres.");
        await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await writer.WriteLineAsync($"PRIVMSG #{channel.Trim().TrimStart('#')} :{safe}".AsMemory(), cancellationToken).ConfigureAwait(false); }
        finally { sendGate.Release(); }
    }

    private async Task ReceiveLoopAsync(string channel, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && reader is not null)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null) break;
                if (line.StartsWith("PING ", StringComparison.Ordinal))
                {
                    if (writer is not null) await writer.WriteLineAsync(("PONG " + line[5..]).AsMemory(), ct).ConfigureAwait(false);
                    continue;
                }
                if (line.Contains(" 001 ", StringComparison.Ordinal)) { IsConnected = true; StatusChanged?.Invoke($"Conectado a #{channel}."); }
                if (line.Contains(" 366 ", StringComparison.Ordinal)) { IsConnected = true; StatusChanged?.Invoke($"Chat unido: #{channel}."); }
                if (line.Contains("Login authentication failed", StringComparison.OrdinalIgnoreCase) || line.Contains("Improperly formatted auth", StringComparison.OrdinalIgnoreCase))
                    StatusChanged?.Invoke("Autenticación rechazada por Twitch; revisá el token y sus permisos.");
                if (line.Contains(" PRIVMSG ", StringComparison.Ordinal))
                {
                    var sep = line.IndexOf(" :", StringComparison.Ordinal);
                    if (sep >= 0) MessageReceived?.Invoke(line);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusChanged?.Invoke($"Error de chat: {ex.Message}"); }
        finally { IsConnected = false; StatusChanged?.Invoke("Twitch desconectado."); }
    }

    public async Task DisconnectAsync()
    {
        cancellation?.Cancel();
        try { if (writer is not null) await writer.WriteLineAsync("QUIT :VTuberBot cerrando conexión".AsMemory()).ConfigureAwait(false); } catch { }
        client?.Close();
        if (receiveTask is not null) { try { await receiveTask.ConfigureAwait(false); } catch { } }
        reader?.Dispose(); writer?.Dispose(); client?.Dispose(); cancellation?.Dispose();
        reader=null; writer=null; client=null; cancellation=null; receiveTask=null; IsConnected=false;
        StatusChanged?.Invoke("Twitch desconectado.");
    }

    public async ValueTask DisposeAsync() { await DisconnectAsync().ConfigureAwait(false); sendGate.Dispose(); }
}
