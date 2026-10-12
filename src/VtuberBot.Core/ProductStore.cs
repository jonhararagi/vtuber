using Microsoft.Data.Sqlite;

namespace VtuberBot.Core;

public sealed record ProductCommand(long Id, string Name, string Response, int CooldownSeconds, bool Enabled);
public sealed record CommunityUser(string Username, long Points, DateTimeOffset LastSeen);
public sealed record GiveawayRecord(long Id, string Name, string Status, string? Winner, int Entries);
public sealed record ProductActivity(DateTimeOffset At, string Module, string Message);
public sealed record AutomationRule(long Id, string Name, string Message, int IntervalMinutes, bool Enabled);
public sealed record AvatarLayer(long Id, string Name, string Path, int SortOrder, bool Visible);

/// <summary>Persistence for product-level features. Uses the same local SQLite file as the core.</summary>
public sealed class ProductStore
{
    private readonly string connectionString;
    private readonly object gate = new();

    public ProductStore(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(databasePath), ForeignKeys = true, DefaultTimeout = 5 };
        connectionString = builder.ConnectionString;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS product_commands(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL COLLATE NOCASE UNIQUE,
              response TEXT NOT NULL, cooldown_seconds INTEGER NOT NULL DEFAULT 0,
              enabled INTEGER NOT NULL DEFAULT 1, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS community_users(
              username TEXT PRIMARY KEY COLLATE NOCASE, points INTEGER NOT NULL DEFAULT 0,
              first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS point_ledger(
              id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL COLLATE NOCASE,
              delta INTEGER NOT NULL, reason TEXT NOT NULL, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS giveaways(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, status TEXT NOT NULL,
              winner TEXT NULL, created_utc TEXT NOT NULL, closed_utc TEXT NULL);
            CREATE TABLE IF NOT EXISTS giveaway_entries(
              giveaway_id INTEGER NOT NULL REFERENCES giveaways(id) ON DELETE CASCADE,
              username TEXT NOT NULL COLLATE NOCASE, entered_utc TEXT NOT NULL,
              PRIMARY KEY(giveaway_id, username));
            CREATE TABLE IF NOT EXISTS game_sessions(
              id INTEGER PRIMARY KEY AUTOINCREMENT, game TEXT NOT NULL, username TEXT NOT NULL,
              result TEXT NOT NULL, reward INTEGER NOT NULL, created_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS moderation_rules(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE,
              pattern TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, action TEXT NOT NULL DEFAULT 'flag');
            CREATE TABLE IF NOT EXISTS automation_rules(
              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE,
              message TEXT NOT NULL, interval_minutes INTEGER NOT NULL, enabled INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS product_activity(
              id INTEGER PRIMARY KEY AUTOINCREMENT, at_utc TEXT NOT NULL, module TEXT NOT NULL, message TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS avatar_layers(\n              id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, path TEXT NOT NULL,\n              sort_order INTEGER NOT NULL, visible INTEGER NOT NULL DEFAULT 1);\n            CREATE TABLE IF NOT EXISTS product_settings(
              key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_activity_time ON product_activity(id DESC);
            """;
        command.ExecuteNonQuery();
        SeedDefaults(connection);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("O");

    private void SeedDefaults(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO product_commands(name,response,cooldown_seconds,enabled,created_utc)
                VALUES('!hola','¡Hola, {user}! Bienvenido al stream.',5,1,$now),
                      ('!puntos','{user}, tenés {points} puntos.',3,1,$now),
                      ('!dado','{user} sacó {roll} en el dado.',5,1,$now);
                INSERT OR IGNORE INTO moderation_rules(name,pattern,enabled,action)
                VALUES('Enlaces','https?://',1,'flag');
                INSERT OR IGNORE INTO automation_rules(name,message,interval_minutes,enabled)
                VALUES('Bienvenida','¡Gracias por acompañar el stream!',30,0);
                """;
            command.Parameters.AddWithValue("$now", Now());
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IReadOnlyList<ProductCommand> GetCommands()
    {
        lock (gate)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,name,response,cooldown_seconds,enabled FROM product_commands ORDER BY name";
            using var r = cmd.ExecuteReader(); var list = new List<ProductCommand>();
            while (r.Read()) list.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4) != 0));
            return list;
        }
    }

    public void SaveCommand(long? id, string name, string response, int cooldown, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(response)) throw new ArgumentException("El nombre y la respuesta son obligatorios.");
        if (cooldown is < 0 or > 86400) throw new ArgumentOutOfRangeException(nameof(cooldown));
        lock (gate)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            if (id is null) cmd.CommandText = "INSERT INTO product_commands(name,response,cooldown_seconds,enabled,created_utc) VALUES($name,$response,$cooldown,$enabled,$now)";
            else { cmd.CommandText = "UPDATE product_commands SET name=$name,response=$response,cooldown_seconds=$cooldown,enabled=$enabled WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id.Value); }
            cmd.Parameters.AddWithValue("$name", name.Trim().TrimStart('!'));
            cmd.Parameters.AddWithValue("$response", response.Trim());
            cmd.Parameters.AddWithValue("$cooldown", cooldown);
            cmd.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$now", Now());
            cmd.ExecuteNonQuery();
            Log("Comandos", $"Comando !{name.Trim().TrimStart('!')} guardado.");
        }
    }

    public void DeleteCommand(long id)
    {
        lock (gate) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM product_commands WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery(); }
        Log("Comandos", $"Comando ID {id} eliminado.");
    }

    public CommandResult RunLocalCommand(string name, string username = "Streamer")
    {
        lock (gate)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT response FROM product_commands WHERE name=$name COLLATE NOCASE AND enabled=1";
            cmd.Parameters.AddWithValue("$name", name.Trim().TrimStart('!'));
            var response = cmd.ExecuteScalar() as string;
            if (response is null) return CommandResult.Failed("Comando no encontrado o desactivado.");
            var user = GetOrCreateUser(c, username);
            var text = response.Replace("{user}", username, StringComparison.OrdinalIgnoreCase)
                .Replace("{points}", user.Points.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{roll}", Random.Shared.Next(1, 7).ToString(), StringComparison.OrdinalIgnoreCase);
            Log("Comandos", $"Prueba local !{name.TrimStart('!')} ejecutada por {username}.");
            return CommandResult.Succeeded(text);
        }
    }

    public CommunityUser TouchUser(string username)
    {
        ValidateUsername(username);
        lock (gate) { using var c = Open(); return GetOrCreateUser(c, username.Trim()); }
    }

    private CommunityUser GetOrCreateUser(SqliteConnection c, string username)
    {
        var now = Now();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO community_users(username,points,first_seen_utc,last_seen_utc) VALUES($user,0,$now,$now) ON CONFLICT(username) DO UPDATE SET last_seen_utc=excluded.last_seen_utc";
            cmd.Parameters.AddWithValue("$user", username); cmd.Parameters.AddWithValue("$now", now); cmd.ExecuteNonQuery();
        }
        using var read = c.CreateCommand();
        read.CommandText = "SELECT username,points,last_seen_utc FROM community_users WHERE username=$user";
        read.Parameters.AddWithValue("$user", username);
        using var r = read.ExecuteReader(); r.Read();
        return new(r.GetString(0), r.GetInt64(1), DateTimeOffset.Parse(r.GetString(2)));
    }

    public CommunityUser AddPoints(string username, long delta, string reason)
    {
        ValidateUsername(username);
        if (delta == 0 || Math.Abs(delta) > 1_000_000) throw new ArgumentOutOfRangeException(nameof(delta));
        lock (gate)
        {
            using var c = Open(); using var tx = c.BeginTransaction();
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "INSERT INTO community_users(username,points,first_seen_utc,last_seen_utc) VALUES($u,0,$n,$n) ON CONFLICT(username) DO NOTHING";
                cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.Parameters.AddWithValue("$n", Now()); cmd.ExecuteNonQuery();
            }
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "UPDATE community_users SET points=MAX(0,points+$d),last_seen_utc=$n WHERE username=$u";
                cmd.Parameters.AddWithValue("$d", delta); cmd.Parameters.AddWithValue("$n", Now()); cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.ExecuteNonQuery();
            }
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx; cmd.CommandText = "INSERT INTO point_ledger(username,delta,reason,created_utc) VALUES($u,$d,$r,$n)";
                cmd.Parameters.AddWithValue("$u", username.Trim()); cmd.Parameters.AddWithValue("$d", delta); cmd.Parameters.AddWithValue("$r", reason); cmd.Parameters.AddWithValue("$n", Now()); cmd.ExecuteNonQuery();
            }
            tx.Commit();
            var result = GetOrCreateUser(c, username.Trim()); Log("Comunidad", $"{username}: {delta:+#;-#;0} puntos ({reason})."); return result;
        }
    }

    public IReadOnlyList<CommunityUser> GetLeaderboard(int limit = 50)
    {
        lock (gate) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT username,points,last_seen_utc FROM community_users ORDER BY points DESC,username LIMIT $limit"; cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit,1,200)); using var r=cmd.ExecuteReader(); var list=new List<CommunityUser>(); while(r.Read()) list.Add(new(r.GetString(0),r.GetInt64(1),DateTimeOffset.Parse(r.GetString(2)))); return list; }
    }

    public long CreateGiveaway(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ingresá un nombre para el sorteo.");
        lock (gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO giveaways(name,status,created_utc) VALUES($name,'OPEN',$now); SELECT last_insert_rowid();"; cmd.Parameters.AddWithValue("$name",name.Trim()); cmd.Parameters.AddWithValue("$now",Now()); var id=(long)cmd.ExecuteScalar()!; Log("Sorteos",$"Sorteo '{name.Trim()}' creado."); return id; }
    }

    public bool EnterGiveaway(long id, string username)
    {
        ValidateUsername(username);
        lock (gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT OR IGNORE INTO giveaway_entries(giveaway_id,username,entered_utc) SELECT $id,$user,$now WHERE EXISTS(SELECT 1 FROM giveaways WHERE id=$id AND status='OPEN')"; cmd.Parameters.AddWithValue("$id",id); cmd.Parameters.AddWithValue("$user",username.Trim()); cmd.Parameters.AddWithValue("$now",Now()); var changed=cmd.ExecuteNonQuery()>0; if(changed) Log("Sorteos",$"{username} se anotó en sorteo #{id}."); return changed; }
    }

    public GiveawayRecord DrawGiveaway(long id)
    {
        lock (gate)
        {
            using var c=Open(); using var tx=c.BeginTransaction();
            var entrants=new List<string>();
            using (var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="SELECT username FROM giveaway_entries WHERE giveaway_id=$id ORDER BY username"; cmd.Parameters.AddWithValue("$id",id); using var r=cmd.ExecuteReader(); while(r.Read()) entrants.Add(r.GetString(0)); }
            if(entrants.Count==0) throw new InvalidOperationException("No hay participantes registrados.");
            var winner=entrants[Random.Shared.Next(entrants.Count)];
            using (var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="UPDATE giveaways SET status='CLOSED',winner=$winner,closed_utc=$now WHERE id=$id AND status='OPEN'"; cmd.Parameters.AddWithValue("$winner",winner); cmd.Parameters.AddWithValue("$now",Now()); cmd.Parameters.AddWithValue("$id",id); if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("El sorteo ya está cerrado."); }
            tx.Commit(); Log("Sorteos",$"Sorteo #{id}: ganador {winner} de {entrants.Count} participantes."); return new(id,"", "CLOSED",winner,entrants.Count);
        }
    }

    public IReadOnlyList<GiveawayRecord> GetGiveaways()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT g.id,g.name,g.status,g.winner,COUNT(e.username) FROM giveaways g LEFT JOIN giveaway_entries e ON e.giveaway_id=g.id GROUP BY g.id ORDER BY g.id DESC"; using var r=cmd.ExecuteReader(); var list=new List<GiveawayRecord>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetInt32(4))); return list; }
    }

    public string PlayGame(string game, string username, int stake)
    {
        ValidateUsername(username); if(stake<0||stake>10000) throw new ArgumentOutOfRangeException(nameof(stake));
        var won=Random.Shared.Next(0,2)==1; var reward=won?Math.Max(1,stake):0; var result=won?"Victoria":"Derrota";
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO game_sessions(game,username,result,reward,created_utc) VALUES($g,$u,$r,$p,$n)"; cmd.Parameters.AddWithValue("$g",game); cmd.Parameters.AddWithValue("$u",username); cmd.Parameters.AddWithValue("$r",result); cmd.Parameters.AddWithValue("$p",reward); cmd.Parameters.AddWithValue("$n",Now()); cmd.ExecuteNonQuery(); }
        if(reward>0) AddPoints(username,reward,$"Recompensa minijuego {game}");
        Log("Minijuegos",$"{username}: {game} — {result}; recompensa {reward}."); return $"{result}. Recompensa: {reward} puntos.";
    }

    public void SaveModerationRule(string name, string pattern, bool enabled, string action)
    {
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Nombre y patrón obligatorios.");
        _=new System.Text.RegularExpressions.Regex(pattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO moderation_rules(name,pattern,enabled,action) VALUES($n,$p,$e,$a) ON CONFLICT(name) DO UPDATE SET pattern=excluded.pattern,enabled=excluded.enabled,action=excluded.action"; cmd.Parameters.AddWithValue("$n",name.Trim()); cmd.Parameters.AddWithValue("$p",pattern); cmd.Parameters.AddWithValue("$e",enabled?1:0); cmd.Parameters.AddWithValue("$a",action); cmd.ExecuteNonQuery(); }
        Log("Moderación",$"Regla '{name}' guardada ({(enabled?"activa":"pausada")}).");
    }

    public IReadOnlyList<(string Name,string Pattern,bool Enabled,string Action)> GetModerationRules()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT name,pattern,enabled,action FROM moderation_rules ORDER BY name"; using var r=cmd.ExecuteReader(); var list=new List<(string,string,bool,string)>(); while(r.Read()) list.Add((r.GetString(0),r.GetString(1),r.GetInt32(2)!=0,r.GetString(3))); return list; }
    }

    public IReadOnlyList<AvatarLayer> GetAvatarLayers()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT id,name,path,sort_order,visible FROM avatar_layers ORDER BY sort_order,id"; using var r=cmd.ExecuteReader(); var list=new List<AvatarLayer>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4)!=0)); return list; }
    }

    public long AddAvatarLayer(string name, string path)
    {
        if(string.IsNullOrWhiteSpace(name)||!File.Exists(path)) throw new ArgumentException("La capa necesita un nombre y un archivo existente.");
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO avatar_layers(name,path,sort_order,visible) VALUES($n,$p,(SELECT COALESCE(MAX(sort_order),-1)+1 FROM avatar_layers),1); SELECT last_insert_rowid();"; cmd.Parameters.AddWithValue("$n",name.Trim()); cmd.Parameters.AddWithValue("$p",Path.GetFullPath(path)); return (long)cmd.ExecuteScalar()!; }
    }

    public void SetAvatarLayerVisibility(long id, bool visible)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="UPDATE avatar_layers SET visible=$v WHERE id=$id"; cmd.Parameters.AddWithValue("$v",visible?1:0); cmd.Parameters.AddWithValue("$id",id); cmd.ExecuteNonQuery(); }
    }

    public void DeleteAvatarLayer(long id)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM avatar_layers WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id); cmd.ExecuteNonQuery(); }
        Log("Avatar",$"Capa ID {id} eliminada del perfil.");
    }

    public IReadOnlyList<AutomationRule> GetAutomationRules()
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT id,name,message,interval_minutes,enabled FROM automation_rules ORDER BY name"; using var r=cmd.ExecuteReader(); var list=new List<AutomationRule>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4)!=0)); return list; }
    }

    public void SaveAutomationRule(long? id, string name, string message, int intervalMinutes, bool enabled)
    {
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Nombre y mensaje son obligatorios.");
        if(intervalMinutes is < 5 or > 1440) throw new ArgumentOutOfRangeException(nameof(intervalMinutes),"El intervalo debe estar entre 5 y 1440 minutos.");
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); if(id is null) cmd.CommandText="INSERT INTO automation_rules(name,message,interval_minutes,enabled) VALUES($n,$m,$i,$e)"; else { cmd.CommandText="UPDATE automation_rules SET name=$n,message=$m,interval_minutes=$i,enabled=$e WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id.Value); } cmd.Parameters.AddWithValue("$n",name.Trim()); cmd.Parameters.AddWithValue("$m",message.Trim()); cmd.Parameters.AddWithValue("$i",intervalMinutes); cmd.Parameters.AddWithValue("$e",enabled?1:0); cmd.ExecuteNonQuery(); }
        Log("Automatización",$"Mensaje programado '{name}' guardado.");
    }

    public IReadOnlyList<ProductActivity> GetActivity(int limit=100)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT at_utc,module,message FROM product_activity ORDER BY id DESC LIMIT $limit"; cmd.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,500)); using var r=cmd.ExecuteReader(); var list=new List<ProductActivity>(); while(r.Read()) list.Add(new(DateTimeOffset.Parse(r.GetString(0)),r.GetString(1),r.GetString(2))); return list; }
    }

    public void Log(string module,string message)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO product_activity(at_utc,module,message) VALUES($at,$module,$message)"; cmd.Parameters.AddWithValue("$at",Now()); cmd.Parameters.AddWithValue("$module",module); cmd.Parameters.AddWithValue("$message",message); cmd.ExecuteNonQuery(); }
    }

    public string GetSetting(string key,string fallback="")
    {
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT value FROM product_settings WHERE key=$key"; cmd.Parameters.AddWithValue("$key",key); return cmd.ExecuteScalar() as string ?? fallback;
    }
    public void SetSetting(string key,string value)
    {
        lock(gate) { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="INSERT INTO product_settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value"; cmd.Parameters.AddWithValue("$k",key); cmd.Parameters.AddWithValue("$v",value); cmd.ExecuteNonQuery(); }
    }
    private static void ValidateUsername(string username) { if(string.IsNullOrWhiteSpace(username)||username.Length>25||username.Any(ch=>!(char.IsLetterOrDigit(ch)||ch=='_'||ch=='-'))) throw new ArgumentException("El usuario debe contener solo letras, números, guion o guion bajo (máx. 25 caracteres).",nameof(username)); }
}
