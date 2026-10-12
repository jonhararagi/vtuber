using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VtuberBot.App;

/// <summary>Subscribes to Twitch EventSub WebSocket events using the authorized broadcaster account.</summary>
public sealed class TwitchEventSubClient : IAsyncDisposable
{
    private ClientWebSocket? socket;
    private CancellationTokenSource? cancellation;
    private Task? receiveTask;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;
    public event Action<string,string>? EventReceived;

    public async Task ConnectAsync(string clientId, string accessToken, string channel, CancellationToken external = default)
    {
        if (IsConnected) throw new InvalidOperationException("EventSub ya está conectado.");
        if (string.IsNullOrWhiteSpace(clientId)||string.IsNullOrWhiteSpace(accessToken)||string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("EventSub necesita Client ID, token OAuth y canal.");
        cancellation=CancellationTokenSource.CreateLinkedTokenSource(external);
        cancellation.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            StatusChanged?.Invoke("Conectando a Twitch EventSub…");
            var userId=await GetBroadcasterIdAsync(clientId.Trim(),accessToken,channel.Trim().TrimStart('#'),cancellation.Token).ConfigureAwait(false);
            socket=new ClientWebSocket();
            await socket.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"),cancellation.Token).ConfigureAwait(false);
            using var welcome=await ReadJsonAsync(socket,cancellation.Token).ConfigureAwait(false);
            var type=welcome.RootElement.GetProperty("metadata").GetProperty("message_type").GetString();
            if(type!="session_welcome")throw new InvalidOperationException("Twitch EventSub no envió session_welcome.");
            var sessionId=welcome.RootElement.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
            if(string.IsNullOrWhiteSpace(sessionId))throw new InvalidOperationException("EventSub no devolvió session ID.");
            cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            IsConnected=true;
            receiveTask=ReceiveLoopAsync(socket,cancellation.Token);
            StatusChanged?.Invoke("EventSub conectado; registrando eventos oficiales…");
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("Client-Id",clientId.Trim());
            http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",accessToken.Trim());
            var subscriptions=new (string Type,string Version,object Condition)[]
            {
                ("channel.follow","2",new {broadcaster_user_id=userId,moderator_user_id=userId}),
                ("channel.subscribe","1",new {broadcaster_user_id=userId}),
                ("channel.subscription.gift","1",new {broadcaster_user_id=userId}),
                ("channel.channel_points_custom_reward_redemption.add","1",new {broadcaster_user_id=userId}),
                ("channel.raid","1",new {to_broadcaster_user_id=userId})
            };
            var registered=0;var rejected=new List<string>();
            foreach(var sub in subscriptions)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var payload=JsonSerializer.Serialize(new {type=sub.Type,version=sub.Version,condition=sub.Condition,transport=new {method="websocket",session_id=sessionId}});
                using var body=new StringContent(payload,Encoding.UTF8,"application/json");
                using var response=await http.PostAsync("https://api.twitch.tv/helix/eventsub/subscriptions",body,cancellation.Token).ConfigureAwait(false);
                if(response.IsSuccessStatusCode)registered++;
                else rejected.Add(sub.Type+" (HTTP "+(int)response.StatusCode+")");
            }
            StatusChanged?.Invoke(registered+" suscripciones EventSub registradas; "+rejected.Count+" rechazadas.");
            foreach(var failure in rejected)EventReceived?.Invoke("subscription_error",failure);
            if(registered==0)throw new InvalidOperationException("Twitch rechazó todas las suscripciones EventSub. Revisá scopes y permisos del canal.");
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> GetBroadcasterIdAsync(string clientId,string token,string channel,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.twitch.tv/helix/users?login="+Uri.EscapeDataString(channel));
        request.Headers.Add("Client-Id",clientId);
        request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(request,ct).ConfigureAwait(false);
        var body=await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("No se pudo consultar el usuario Twitch (HTTP "+(int)response.StatusCode+").");
        using var json=JsonDocument.Parse(body);
        var users=json.RootElement.GetProperty("data");
        if(users.GetArrayLength()==0)throw new InvalidOperationException("Twitch no encontró el canal indicado.");
        return users[0].GetProperty("id").GetString()!;
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws,CancellationToken ct)
    {
        try
        {
            while(!ct.IsCancellationRequested&&ws.State==WebSocketState.Open)
            {
                using var document=await ReadJsonAsync(ws,ct).ConfigureAwait(false);
                var root=document.RootElement;
                var metadata=root.GetProperty("metadata");
                var type=metadata.GetProperty("message_type").GetString()??"unknown";
                if(type=="session_keepalive")continue;
                if(type=="session_reconnect")
                {
                    StatusChanged?.Invoke("EventSub solicita reconexión; se requiere reconectar desde Twitch y chat.");
                    continue;
                }
                if(type=="notification")
                {
                    var subscription=metadata.TryGetProperty("subscription_type",out var t)?t.GetString()??"event":"event";
                    var evt=root.GetProperty("payload").GetProperty("event");
                    var summary=SummarizeEvent(subscription,evt);
                    EventReceived?.Invoke(subscription,summary);
                    StatusChanged?.Invoke("Evento Twitch recibido: "+subscription);
                }
                else if(type=="revocation")
                {
                    var subscription=metadata.TryGetProperty("subscription_type",out var t)?t.GetString()??"event":"event";
                    EventReceived?.Invoke("revocation",subscription);
                    StatusChanged?.Invoke("Twitch revocó una suscripción: "+subscription);
                }
            }
        }
        catch(OperationCanceledException){}
        catch(WebSocketException ex){StatusChanged?.Invoke("EventSub interrumpido: "+ex.Message);}
        catch(Exception ex){StatusChanged?.Invoke("Error EventSub: "+ex.Message);}
        finally{IsConnected=false;StatusChanged?.Invoke("EventSub desconectado.");}
    }

    private static string SummarizeEvent(string type,JsonElement evt)
    {
        string Value(string key)=>evt.TryGetProperty(key,out var value)?value.ToString():"";
        return type switch
        {
            "channel.follow"=>"Nuevo follow: "+Value("user_name"),
            "channel.subscribe"=>"Nueva suscripción: "+Value("user_name")+" · "+Value("tier"),
            "channel.subscription.gift"=>"Regalo de suscripción: "+Value("user_name")+" · cantidad "+Value("total"),
            "channel.channel_points_custom_reward_redemption.add"=>"Redención de puntos: "+Value("user_name")+" · "+Value("reward").Replace("\"",""),
            "channel.raid"=>"Raid de "+Value("from_broadcaster_user_name")+" · espectadores "+Value("viewers"),
            _=>evt.GetRawText()
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(ClientWebSocket ws,CancellationToken ct)
    {
        using var buffer=new MemoryStream();var chunk=new byte[8192];WebSocketReceiveResult result;
        do
        {
            result=await ws.ReceiveAsync(new ArraySegment<byte>(chunk),ct).ConfigureAwait(false);
            if(result.MessageType==WebSocketMessageType.Close)throw new WebSocketException("Twitch cerró EventSub.");
            buffer.Write(chunk,0,result.Count);
            if(buffer.Length>1_048_576)throw new InvalidOperationException("Mensaje EventSub supera 1 MiB.");
        }while(!result.EndOfMessage);
        return JsonDocument.Parse(buffer.ToArray());
    }

    public async Task DisconnectAsync()
    {
        cancellation?.Cancel();
        if(socket is {State:WebSocketState.Open} ws){try{await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"VTuberBot disconnect",CancellationToken.None).ConfigureAwait(false);}catch{}}
        if(receiveTask is not null){try{await receiveTask.ConfigureAwait(false);}catch{}}
        socket?.Dispose();cancellation?.Dispose();socket=null;cancellation=null;receiveTask=null;IsConnected=false;
        StatusChanged?.Invoke("EventSub desconectado.");
    }
    public async ValueTask DisposeAsync(){await DisconnectAsync().ConfigureAwait(false);http.Dispose();}
}
