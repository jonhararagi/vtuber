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
    private UIElement BuildCommandsPage()
    {
        var root=new StackPanel();var panel=Panel("Registro de comandos");root.Children.Add(panel);
        var list=new ListBox{Height=260};long? editingCommandId=null;
        string PermissionLabel(string scope)=>scope switch{"subscriber"=>"Suscriptores","vip"=>"VIP","moderator"=>"Moderadores","broadcaster"=>"Broadcaster",_=>"Todos"};
        void Reload(){list.ItemsSource=null;list.ItemsSource=store.GetCommands().Select(c=>$"{c.Id}  !{c.Name}  ·  {(c.Enabled?"Activo":"Pausado")}  ·  {c.CooldownSeconds}s ({(c.CooldownScope=="user"?"por usuario":"global")}) · {PermissionLabel(c.PermissionScope)}\n     Alias: {(string.IsNullOrWhiteSpace(c.Aliases)?"—":c.Aliases)}\n     {c.Response}").ToList();}
        Reload();panel.Children.Add(list);
        var name=TextField("Nombre (sin !, ej. hola)");
        var response=TextField("Respuesta. Variables: {user}, {points}, {roll}, {args}, {message}");
        var aliases=TextField("Alias separados por comas (ej. hi, saludo), sin !");
        var cooldown=TextField("Cooldown en segundos");cooldown.Text="5";
        var cooldownScope=new ComboBox
        {
            ItemsSource=new[]{"Global (limita a todo el chat)","Por usuario (cada espectador)"},
            SelectedIndex=0,
            MinWidth=250
        };
        var permissionScope=new ComboBox
        {
            ItemsSource=new[]{"Todos","Suscriptores","VIP","Moderadores","Solo broadcaster"},
            SelectedIndex=0,
            MinWidth=250
        };
        string SelectedPermissionScope()=>permissionScope.SelectedIndex switch{1=>"subscriber",2=>"vip",3=>"moderator",4=>"broadcaster",_=>"everyone"};
        var testUsername=TextField("Usuario de prueba (solo simulación)");
        testUsername.Text="preview_user";
        var testRole=new ComboBox
        {
            ItemsSource=new[]{"Sin rol","Suscriptor","VIP","Moderador","Broadcaster"},
            SelectedIndex=0,
            MinWidth=220
        };
        IReadOnlyCollection<string> SelectedTestRoles()=>testRole.SelectedIndex switch
        {
            1=>new[]{"subscriber"},
            2=>new[]{"vip"},
            3=>new[]{"moderator"},
            4=>new[]{"broadcaster"},
            _=>Array.Empty<string>()
        };
        var testArguments=TextField("Argumentos de prueba (no se envían al chat)");
        var enabled=new CheckBox{Content="Comando habilitado",IsChecked=true,Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,10)};
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split((char)32)[0]);var selected=store.GetCommands().FirstOrDefault(c=>c.Id==selectedId);if(selected is null)return;editingCommandId=selected.Id;name.Text=selected.Name;aliases.Text=selected.Aliases;response.Text=selected.Response;cooldown.Text=selected.CooldownSeconds.ToString();cooldownScope.SelectedIndex=selected.CooldownScope=="user"?1:0;permissionScope.SelectedIndex=selected.PermissionScope switch{"subscriber"=>1,"vip"=>2,"moderator"=>3,"broadcaster"=>4,_=>0};enabled.IsChecked=selected.Enabled;}catch{}};
        panel.Children.Add(Label("Crear o editar",13,"#F8FAFC",true));panel.Children.Add(name);panel.Children.Add(aliases);panel.Children.Add(response);panel.Children.Add(cooldown);panel.Children.Add(Label("Tipo de cooldown",12,"#98A4BE"));panel.Children.Add(cooldownScope);panel.Children.Add(Label("Permiso mínimo",12,"#98A4BE"));panel.Children.Add(permissionScope);
        panel.Children.Add(Label("Usuario simulado",12,"#98A4BE"));panel.Children.Add(testUsername);
        panel.Children.Add(Label("Rol de Twitch para simular permisos",12,"#98A4BE"));panel.Children.Add(testRole);
        panel.Children.Add(testArguments);panel.Children.Add(enabled);
        var row=new WrapPanel();
        row.Children.Add(ActionButton("Guardar comando",(_,_)=>{try{store.SaveCommand(editingCommandId,name.Text,response.Text,int.Parse(cooldown.Text),enabled.IsChecked==true,cooldownScope.SelectedIndex==1?"user":"global",aliases.Text,SelectedPermissionScope());Reload();list.SelectedItem=null;editingCommandId=null;name.Clear();aliases.Clear();response.Clear();testArguments.Clear();testUsername.Text="preview_user";testRole.SelectedIndex=0;cooldownScope.SelectedIndex=0;permissionScope.SelectedIndex=0;FooterStatus.Text="Comando guardado en SQLite.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        row.Children.Add(ActionButton("Simular comando (sin efectos)",(_,_) =>
        {
            var result=store.PreviewCommandResponse(
                name.Text,response.Text,testUsername.Text,testArguments.Text,
                SelectedPermissionScope(),SelectedTestRoles());
            MessageBox.Show(this,result.Message,"Simulación local · sin cooldown ni cambios",MessageBoxButton.OK,
                result.Success?MessageBoxImage.Information:MessageBoxImage.Warning);
            FooterStatus.Text=result.Success
                ?"Simulación completada; no se ejecutó el comando ni se consumió cooldown."
                :"Simulación rechazada; el comando no se ejecutó.";
        }));
        row.Children.Add(ActionButton("Ejecutar guardado localmente",(_,_) =>
        {
            if (list.SelectedItem is not string selectedText)
            {
                MessageBox.Show(this, "Seleccioná primero un comando guardado.", "Comandos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!long.TryParse(selectedText.Split((char)32)[0], out var selectedId))
                return;
            var selected = store.GetCommands().FirstOrDefault(command => command.Id == selectedId);
            if (selected is null)
            {
                Reload();
                FooterStatus.Text = "El comando seleccionado ya no existe.";
                return;
            }
            var result = store.RunLocalCommand(selected.Name, testUsername.Text, testArguments.Text, SelectedTestRoles());
            store.Log("Comandos", "Ejecución local de !" + selected.Name + (result.Success ? " completada." : " rechazada: " + result.Message));
            MessageBox.Show(this, result.Message, "Ejecución local · sin enviar a Twitch", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            FooterStatus.Text = result.Success
                ? "Comando ejecutado localmente; se aplicaron sus efectos locales y cooldown."
                : "Comando local rechazado; revisá permiso, estado y cooldown.";
        }));
        row.Children.Add(ActionButton("Eliminar seleccionado",(_,_)=>{if(list.SelectedIndex<0)return;var id=long.Parse(list.SelectedItem!.ToString()!.Split(' ')[0]);if(MessageBox.Show(this,"¿Eliminar el comando seleccionado?","Confirmar",MessageBoxButton.YesNo)==MessageBoxResult.Yes){store.DeleteCommand(id);Reload();}}));
        panel.Children.Add(row);

        var builtIns = Panel("Comandos de comunidad integrados");
        builtIns.Margin = new Thickness(0, 14, 0, 0);
        builtIns.Children.Add(Label("Estos comandos funcionan localmente y en Twitch sin crear comandos personalizados. !puntos muestra tu saldo; !top muestra los cinco primeros; !jugar moneda cara 10, !jugar ruleta rojo 10 o !jugar dado alto 10 juega una ronda persistida. Usá apuesta 0 para practicar gratis. Los nombres puntos, top y jugar quedan reservados para estas funciones.",12,"#98A4BE"));
        var builtInInput = TextField("Comando de prueba (ej.: !jugar dado alto 0)");
        builtIns.Children.Add(builtInInput);
        builtIns.Children.Add(ActionButton("Ejecutar localmente",(_,_) =>
        {
            var input = builtInInput.Text.Trim();
            var separator = input.IndexOf(' ');
            var commandName = separator < 0 ? input : input[..separator];
            var arguments = separator < 0 ? string.Empty : input[(separator + 1)..].Trim();
            var result = communityCommandRouter.TryExecute(commandName, testUsername.Text, arguments);
            if (result is null)
            {
                MessageBox.Show(this,"Usá !puntos, !top o !jugar desde esta prueba local.","Comandos de comunidad",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }
            store.Log("Comunidad","Prueba local de !" + commandName.TrimStart('!') + (result.Success ? " completada." : " rechazada: " + result.Message));
            MessageBox.Show(this,result.Message,"Prueba local · sin enviar a Twitch",MessageBoxButton.OK,
                result.Success?MessageBoxImage.Information:MessageBoxImage.Warning);
            FooterStatus.Text = result.Success ? "Comando de comunidad ejecutado localmente." : "Comando de comunidad rechazado; revisá el mensaje.";
        },true));
        root.Children.Add(builtIns);
        root.Children.Add(Label("El cooldown puede ser global o por usuario. El permiso mínimo se comprueba con etiquetas autenticadas de Twitch: Todos, Suscriptores, VIP, Moderadores o solo Broadcaster. Moderadores pueden usar los niveles de suscriptor/VIP; solo el broadcaster puede usar los comandos exclusivos del broadcaster. Un uso denegado se registra y no consume cooldown.",12,"#98A4BE"));
        return root;
    }

    private UIElement BuildCommunityPage()
    {
        var root=new StackPanel();var panel=Panel("Comunidad y puntos");root.Children.Add(panel);
        var list=new ListBox{Height=310};void Reload(){list.ItemsSource=store.GetLeaderboard().Select((u,i)=>$"{i+1,2}.  {u.Username,-24}  {u.Points,7} pts   · visto {u.LastSeen.ToLocalTime():dd/MM HH:mm}").ToList();}Reload();panel.Children.Add(list);
        var user=TextField("Usuario (letras/números/_/-)");var amount=TextField("Cantidad de puntos (+/-)");amount.Text="10";var reason=TextField("Motivo de la operación");reason.Text="Ajuste manual";
        panel.Children.Add(user);panel.Children.Add(amount);panel.Children.Add(reason);
        var row=new WrapPanel();row.Children.Add(ActionButton("Agregar / quitar puntos",(_,_)=>{try{var result=store.AddPoints(user.Text,long.Parse(amount.Text),reason.Text);Reload();FooterStatus.Text=$"{result.Username}: {result.Points} puntos.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        row.Children.Add(ActionButton("Crear perfil",(_,_)=>{try{var u=store.TouchUser(user.Text);Reload();FooterStatus.Text=$"Perfil {u.Username} disponible.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        row.Children.Add(ActionButton("Exportar ranking CSV",(_,_)=>ExportLeaderboard()));panel.Children.Add(row);
        return root;
    }

    private void ExportLeaderboard()
    {
        var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="vtuberbot-ranking.csv"};if(dialog.ShowDialog(this)!=true)return;
        var lines=new List<string>{"usuario,puntos,ultima_actividad"};
        lines.AddRange(store.GetLeaderboard(200).Select(u => Csv(u.Username) + "," + u.Points + "," + Csv(u.LastSeen.ToString("O"))));
        File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));store.Log("Comunidad","Ranking exportado a CSV.");
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private UIElement BuildGiveawaysPage()
    {
        var root = new StackPanel();
        var panel = Panel("Sorteos");
        root.Children.Add(panel);

        var list = new ListBox { Height = 250 };
        var name = TextField("Nombre del sorteo (máximo 100 caracteres)");
        var entrant = TextField("Usuario para registro manual");
        var id = TextField("ID del sorteo seleccionado");

        void Reload()
        {
            list.ItemsSource = store.GetGiveaways()
                .Select(giveaway => $"{giveaway.Id} · {giveaway.Name} · {giveaway.Status} · entradas: {giveaway.Entries} · ganador: {giveaway.Winner ?? "—"}")
                .ToList();
        }

        Reload();
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not string selected)
                return;
            if (long.TryParse(selected.Split('·')[0].Trim(), out var selectedId))
                id.Text = selectedId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        };

        panel.Children.Add(list);
        panel.Children.Add(name);
        panel.Children.Add(ActionButton("Crear sorteo", async (_, _) =>
        {
            try
            {
                id.Text = store.CreateGiveaway(name.Text).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Reload();
                FooterStatus.Text = $"Sorteo #{id.Text} abierto.";
                if (twitch.IsConnected)
                {
                    try
                    {
                        await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),
                            "🎁 Sorteo abierto: " + name.Text.Trim() + ". Escribí !entrar para participar.");
                    }
                    catch (Exception sendError)
                    {
                        FooterStatus.Text = "Sorteo abierto localmente; no se anunció en Twitch: " + sendError.Message;
                    }
                }
                else
                {
                    FooterStatus.Text = $"Sorteo #{id.Text} abierto localmente; no se anunció en Twitch porque el chat no está conectado.";
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Sorteos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));

        panel.Children.Add(Label("ID del sorteo", 12, "#98A4BE"));
        panel.Children.Add(id);
        panel.Children.Add(entrant);

        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Registrar participante", (_, _) =>
        {
            try
            {
                if (!store.EnterGiveaway(long.Parse(id.Text), entrant.Text))
                    FooterStatus.Text = "No se registró: el sorteo está cerrado o el usuario ya participaba.";
                else
                    FooterStatus.Text = "Participante registrado.";
                Reload();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Sorteos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        actions.Children.Add(ActionButton("Elegir ganador y cerrar", async (_, _) =>
        {
            try
            {
                var giveaway = store.DrawGiveaway(long.Parse(id.Text));
                Reload();
                var announcement = $"🎉 Sorteo cerrado. Ganador: {giveaway.Winner}. Participantes: {giveaway.Entries}.";
                MessageBox.Show(this,
                    $"Ganador: {giveaway.Winner}\nParticipantes: {giveaway.Entries}",
                    "Sorteo cerrado", MessageBoxButton.OK, MessageBoxImage.Information);
                if (twitch.IsConnected)
                {
                    try
                    {
                        await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), announcement);
                        FooterStatus.Text = "Ganador anunciado en Twitch.";
                    }
                    catch (Exception sendError)
                    {
                        FooterStatus.Text = "Sorteo cerrado y ganador guardado; no se pudo anunciar: " + sendError.Message;
                    }
                }
                else
                {
                    FooterStatus.Text = "Sorteo cerrado y ganador guardado localmente; no se anunció en Twitch porque el chat no está conectado.";
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Sorteos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        actions.Children.Add(ActionButton("Cancelar sorteo sin ganador", async (_, _) =>
        {
            try
            {
                var giveawayId = long.Parse(id.Text);
                store.CancelGiveaway(giveawayId);
                Reload();
                FooterStatus.Text = $"Sorteo #{giveawayId} cancelado sin ganador.";
                if (twitch.IsConnected)
                {
                    try
                    {
                        await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),
                            $"El sorteo #{giveawayId} fue cancelado; no se eligió ganador.");
                    }
                    catch (Exception sendError)
                    {
                        FooterStatus.Text = "Sorteo cancelado localmente; no se pudo anunciar: " + sendError.Message;
                    }
                }
                else
                {
                    FooterStatus.Text = $"Sorteo #{giveawayId} cancelado localmente; no se anunció en Twitch porque el chat no está conectado.";
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Sorteos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        panel.Children.Add(actions);
        root.Children.Add(Label(
            "Solo puede haber un sorteo abierto para que !entrar siempre apunte al sorteo correcto. Podés cancelar uno sin participantes y crear otro; las entradas de un sorteo cancelado se conservan en el historial, pero no se pueden modificar ni sortear.",
            12, "#98A4BE"));
        return root;
    }

    private UIElement BuildMinigamesPage()
    {
        var root = new StackPanel();
        var intro = Panel("Minijuegos locales");
        intro.Children.Add(Label("Tres juegos reales, elecciones claras y saldo persistido en SQLite. Practicá sin arriesgar puntos o apostá desde tu saldo.", 13, "#98A4BE"));
        root.Children.Add(intro);

        var gamePanel = Panel("Preparar una ronda");
        gamePanel.Margin = new Thickness(0, 0, 0, 14);
        var user = TextField("Usuario (letras, números, guion bajo)");
        user.Text = "Streamer";
        var game = new ComboBox
        {
            ItemsSource = new[] { "Duelo de moneda", "Ruleta de suerte", "Dado" },
            SelectedIndex = 0,
            MinWidth = 260
        };
        var choice = new ComboBox { MinWidth = 260 };
        var stake = TextField("Apuesta en puntos (0 para práctica)");
        stake.Text = "0";
        var balance = Label("Saldo actual: 0 puntos", 15, "#69D8F2", true);
        var ruleText = Label("", 12, "#98A4BE");
        var resultCard = new Border
        {
            Background = Brush("#101525"),
            BorderBrush = Brush("#34415E"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 10, 0, 10)
        };
        var resultTitle = Label("Elegí una opción y jugá cuando estés listo.", 19, "#F8FAFC", true);
        var resultDetail = Label("La ronda y el saldo se guardan de forma atómica.", 12, "#98A4BE");
        var resultContent = new StackPanel();
        resultContent.Children.Add(resultTitle);
        resultContent.Children.Add(resultDetail);
        resultCard.Child = resultContent;

        var history = new ListBox
        {
            ItemsSource = store.GetActivity(100)
                .Where(activity => activity.Module == "Minijuegos")
                .Select(activity => $"{activity.At.ToLocalTime():HH:mm:ss} · {activity.Message}")
                .ToList(),
            Height = 230
        };

        void RefreshBalance()
        {
            if (string.IsNullOrWhiteSpace(user.Text) ||
                !System.Text.RegularExpressions.Regex.IsMatch(user.Text.Trim(), "^[a-zA-Z0-9_-]{1,25}$"))
            {
                balance.Text = "Ingresá un usuario válido para consultar su saldo.";
                return;
            }

            var current = store.TouchUser(user.Text.Trim());
            balance.Text = $"Saldo actual de {current.Username}: {current.Points:N0} puntos";
        }

        void RefreshChoices()
        {
            if (game.SelectedItem is not string selectedGame)
                return;

            try
            {
                choice.ItemsSource = MiniGameEngine.GetChoices(selectedGame);
                choice.SelectedIndex = 0;
                ruleText.Text = selectedGame switch
                {
                    "Duelo de moneda" => "Elegí Cara o Cruz. Acierto: recibís 2× la apuesta total; fallo: perdés la apuesta.",
                    "Ruleta de suerte" => "Ruleta europea 0–36. Rojo/negro paga 2×; verde (0) paga 36× la apuesta total. Si falla, perdés la apuesta.",
                    "Dado" => "Elegí Bajo (1–3) o Alto (4–6). Acierto: 2× la apuesta total; fallo: perdés la apuesta.",
                    _ => ""
                };
                resultTitle.Text = selectedGame;
                resultTitle.Foreground = Brush("#F8FAFC");
                resultDetail.Text = "La ronda siguiente aparecerá aquí.";
            }
            catch (Exception exception)
            {
                ruleText.Text = exception.Message;
            }
        }

        gamePanel.Children.Add(Label("Usuario", 12, "#98A4BE"));
        gamePanel.Children.Add(user);
        gamePanel.Children.Add(Label("Minijuego", 12, "#98A4BE"));
        gamePanel.Children.Add(game);
        gamePanel.Children.Add(Label("Tu elección", 12, "#98A4BE"));
        gamePanel.Children.Add(choice);
        gamePanel.Children.Add(Label("Apuesta en puntos · 0 = modo práctica", 12, "#98A4BE"));
        gamePanel.Children.Add(stake);
        gamePanel.Children.Add(balance);
        gamePanel.Children.Add(ruleText);

        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        actions.Children.Add(ActionButton("Actualizar saldo", (_, _) =>
        {
            try { RefreshBalance(); }
            catch (Exception exception) { resultTitle.Text = "No se pudo consultar el saldo"; resultDetail.Text = exception.Message; }
        }));
        actions.Children.Add(ActionButton("Jugar ronda", async (_, _) =>
        {
            if (game.SelectedItem is not string gameName || choice.SelectedItem is not string selectedChoice)
            {
                resultTitle.Text = "Elegí un juego y una opción";
                resultDetail.Text = "No se descontaron puntos.";
                return;
            }

            if (!int.TryParse(stake.Text.Trim(), out var wager) || wager is < 0 or > 10000)
            {
                resultTitle.Text = "Apuesta inválida";
                resultDetail.Text = "Ingresá un entero entre 0 y 10.000. Usá 0 para practicar gratis.";
                return;
            }

            try
            {
                var roundResult = store.PlayGame(gameName, user.Text, wager, selectedChoice);
                var isWin = roundResult.StartsWith("Victoria.", StringComparison.Ordinal);
                resultTitle.Text = isWin ? "✦  ¡Victoria!" : "Ronda terminada";
                resultTitle.Foreground = Brush(isWin ? "#75E0BF" : "#F8FAFC");
                resultDetail.Text = roundResult;
                RefreshBalance();
                history.ItemsSource = store.GetActivity(100)
                    .Where(activity => activity.Module == "Minijuegos")
                    .Select(activity => $"{activity.At.ToLocalTime():HH:mm:ss} · {activity.Message}")
                    .ToList();

                if (twitch.IsConnected)
                {
                    try
                    {
                        await twitch.SendMessageAsync(
                            store.GetSetting("twitch.channel"),
                            user.Text.Trim() + ": " + gameName + " — " + roundResult);
                        FooterStatus.Text = "Resultado del minijuego publicado en Twitch.";
                    }
                    catch (Exception sendError)
                    {
                        FooterStatus.Text = "Ronda guardada; no se publicó en Twitch: " + sendError.Message;
                    }
                }
            }
            catch (Exception exception)
            {
                resultTitle.Text = "Ronda no ejecutada";
                resultTitle.Foreground = Brush("#FCA5A5");
                resultDetail.Text = exception.Message + " No se descontaron puntos.";
            }
        }, true));
        gamePanel.Children.Add(actions);
        gamePanel.Children.Add(resultCard);
        root.Children.Add(gamePanel);

        var historyPanel = Panel("Historial reciente");
        historyPanel.Margin = new Thickness(0, 0, 0, 12);
        historyPanel.Children.Add(history);
        root.Children.Add(historyPanel);

        game.SelectionChanged += (_, _) => RefreshChoices();
        user.LostFocus += (_, _) => { try { RefreshBalance(); } catch { } };
        RefreshChoices();
        RefreshBalance();
        return root;
    }
}
