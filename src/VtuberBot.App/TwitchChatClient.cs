using VtuberBot.Core;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace VtuberBot.App;

/// <summary>
/// Twitch IRC-over-TLS client with supervised reconnects. A connection is only considered
/// active after Twitch sends its registration/join acknowledgement.
/// </summary>
public sealed class TwitchChatClient : IAsyncDisposable
{
    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private CancellationTokenSource? cancellation;
    private Task? supervisorTask;
    private string? sessionChannel;
    private readonly SemaphoreSlim sendGate = new(1, 1);

    public event Action<string>? MessageReceived;
    public event Action<string>? StatusChanged;
    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(string channel, string oauthToken, CancellationToken external = default)
    {
        if (cancellation is not null)
        {
            // A fatal post-connect failure (for example, revoked OAuth) ends the supervisor.
            // Reclaim that completed session here so the user can reconnect without first
            // pressing Disconnect; an active supervisor still prevents duplicate sessions.
            if (supervisorTask?.IsCompleted == true)
                await DisconnectAsync().ConfigureAwait(false);
            else
                throw new InvalidOperationException("Twitch ya está conectado o intentando reconectarse.");
        }

        channel = TwitchIrcValidation.NormalizeChannelLogin(channel);
        var token = TwitchIrcValidation.NormalizeOAuthToken(oauthToken);

        sessionChannel = channel;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(external);
        var firstConnection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenSource = cancellation;
        supervisorTask = RunConnectionSupervisorAsync(channel, token, firstConnection, tokenSource.Token);

        try
        {
            await firstConnection.Task.WaitAsync(TimeSpan.FromSeconds(25), external).ConfigureAwait(false);
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task SendMessageAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        var safe = message.Replace("\r", " ").Replace("\n", " ").Trim();
        if (safe.Length is 0 or > 500)
            throw new ArgumentException("El mensaje debe tener entre 1 y 500 caracteres.");

        await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected || writer is null)
                throw new InvalidOperationException("Twitch no está conectado; se reintentará automáticamente.");
            var expectedChannel = sessionChannel
                ?? throw new InvalidOperationException("No hay un canal vinculado a la sesión IRC actual.");
            var targetChannel = TwitchIrcValidation.NormalizeTargetChannel(expectedChannel, channel);
            await writer.WriteLineAsync($"PRIVMSG #{targetChannel} :{safe}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private async Task RunConnectionSupervisorAsync(
        string channel,
        string token,
        TaskCompletionSource<bool> firstConnection,
        CancellationToken cancellationToken)
    {
        var retryAttempt = 0;
        var initialSignalled = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient? nextClient = null;
                SslStream? ssl = null;
                StreamReader? nextReader = null;
                StreamWriter? nextWriter = null;
                var fatalAuthenticationFailure = false;
                var sessionConnectedAt = (DateTimeOffset?)null;
                try
                {
                    SafeEventDispatch.Invoke(StatusChanged, retryAttempt == 0
                        ? "Conectando a Twitch IRC sobre TLS…"
                        : $"Reconectando a Twitch IRC (intento {retryAttempt})…");

                    nextClient = new TcpClient();
                    await nextClient.ConnectAsync("irc.chat.twitch.tv", 6697, cancellationToken).ConfigureAwait(false);
                    ssl = new SslStream(nextClient.GetStream(), false);
                    await ssl.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions { TargetHost = "irc.chat.twitch.tv" },
                        cancellationToken).ConfigureAwait(false);

                    nextReader = new StreamReader(ssl, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                    nextWriter = new StreamWriter(ssl, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
                    {
                        NewLine = "\r\n",
                        AutoFlush = true
                    };

                    await nextWriter.WriteLineAsync(("PASS oauth:" + token).AsMemory(), cancellationToken).ConfigureAwait(false);
                    await nextWriter.WriteLineAsync(("NICK " + channel).AsMemory(), cancellationToken).ConfigureAwait(false);
                    await nextWriter.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands".AsMemory(), cancellationToken).ConfigureAwait(false);
                    await nextWriter.WriteLineAsync(("JOIN #" + channel).AsMemory(), cancellationToken).ConfigureAwait(false);

                    await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        client = nextClient;
                        reader = nextReader;
                        writer = nextWriter;
                    }
                    finally { sendGate.Release(); }

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await nextReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line is null)
                            throw new IOException("Twitch cerró el flujo IRC.");

                        if (line.StartsWith("PING ", StringComparison.Ordinal))
                        {
                            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                            try
                            {
                                if (ReferenceEquals(writer, nextWriter))
                                    await nextWriter.WriteLineAsync(("PONG " + line[5..]).AsMemory(), cancellationToken).ConfigureAwait(false);
                            }
                            finally { sendGate.Release(); }
                            continue;
                        }

                        if (line.Contains("Login authentication failed", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Improperly formatted auth", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains(" 464 ", StringComparison.Ordinal))
                        {
                            fatalAuthenticationFailure = true;
                            throw new TwitchChatAuthenticationException(
                                "Twitch rechazó la autenticación IRC. Volvé a autorizar la cuenta y comprobá los permisos chat:read/chat:edit.");
                        }

                        if (line.Contains(" 001 ", StringComparison.Ordinal))
                        {
                            // RPL_WELCOME means IRC registration succeeded, not that JOIN did.
                            // Keep the connection unavailable until Twitch confirms channel entry.
                            SafeEventDispatch.Invoke(StatusChanged, $"Twitch IRC registrado; esperando confirmación de ingreso a #{channel}.");
                        }
                        else if (line.Contains(" 366 ", StringComparison.Ordinal))
                        {
                            IsConnected = true;
                            sessionConnectedAt ??= DateTimeOffset.UtcNow;
                            firstConnection.TrySetResult(true);
                            initialSignalled = true;
                            SafeEventDispatch.Invoke(StatusChanged, $"Chat unido: #{channel}.");
                        }

                        if (line.Contains(" PRIVMSG ", StringComparison.Ordinal))
                        {
                            var separator = line.IndexOf(" :", StringComparison.Ordinal);
                            if (separator >= 0)
                                MessageReceived?.Invoke(line);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (TwitchChatAuthenticationException exception)
                {
                    SafeEventDispatch.Invoke(StatusChanged, exception.Message);
                    firstConnection.TrySetException(exception);
                    fatalAuthenticationFailure = true;
                }
                catch (Exception exception)
                {
                    IsConnected = false;
                    if (sessionConnectedAt.HasValue && DateTimeOffset.UtcNow - sessionConnectedAt.Value > TimeSpan.FromSeconds(30))
                        retryAttempt = 0;
                    retryAttempt++;
                    var delaySeconds = Math.Min(30, 1 << Math.Min(retryAttempt - 1, 5));
                    SafeEventDispatch.Invoke(StatusChanged, "Chat Twitch perdido (" + exception.GetType().Name +
                        "); reintento automático en " + delaySeconds + " s.");
                }
                finally
                {
                    IsConnected = false;
                    try
                    {
                        await sendGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        try
                        {
                            if (ReferenceEquals(writer, nextWriter)) writer = null;
                            if (ReferenceEquals(reader, nextReader)) reader = null;
                            if (ReferenceEquals(client, nextClient)) client = null;
                        }
                        finally { sendGate.Release(); }
                    }
                    catch (ObjectDisposedException) { }

                    try { nextClient?.Close(); } catch { }
                    nextReader?.Dispose();
                    nextWriter?.Dispose();
                    ssl?.Dispose();
                    nextClient?.Dispose();
                }

                if (cancellationToken.IsCancellationRequested || fatalAuthenticationFailure)
                    break;

                var delaySecondsAfterFailure = Math.Min(30, 1 << Math.Min(retryAttempt - 1, 5));
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySecondsAfterFailure), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            IsConnected = false;
            firstConnection.TrySetException(new IOException(
                initialSignalled
                    ? "La conexión inicial se perdió antes de completarse."
                    : "No se pudo establecer la primera conexión IRC."));
            SafeEventDispatch.Invoke(StatusChanged, "Twitch chat desconectado.");
        }
    }

    public async Task DisconnectAsync()
    {
        var tokenSource = cancellation;
        if (tokenSource is null) return;
        tokenSource.Cancel();

        try
        {
            await sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (writer is not null)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                    try { await writer.WriteLineAsync("QUIT :VTuberBot cerrando conexión".AsMemory(), timeout.Token).ConfigureAwait(false); }
                    catch { }
                }
                client?.Close();
            }
            finally { sendGate.Release(); }
        }
        catch (ObjectDisposedException) { }

        if (supervisorTask is not null)
        {
            try { await supervisorTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (TwitchChatAuthenticationException) { }
        }

        reader?.Dispose();
        writer?.Dispose();
        client?.Dispose();
        tokenSource.Dispose();
        cancellation = null;
        supervisorTask = null;
        sessionChannel = null;
        reader = null;
        writer = null;
        client = null;
        IsConnected = false;
        SafeEventDispatch.Invoke(StatusChanged, "Twitch desconectado.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        sendGate.Dispose();
    }

    private sealed class TwitchChatAuthenticationException(string message) : Exception(message) { }
}
