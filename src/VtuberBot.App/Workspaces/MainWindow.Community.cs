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
        var testArguments=TextField("Argumentos de prueba (no se envían al chat)");
        var enabled=new CheckBox{Content="Comando habilitado",IsChecked=true,Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,10)};
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split((char)32)[0]);var selected=store.GetCommands().FirstOrDefault(c=>c.Id==selectedId);if(selected is null)return;editingCommandId=selected.Id;name.Text=selected.Name;aliases.Text=selected.Aliases;response.Text=selected.Response;cooldown.Text=selected.CooldownSeconds.ToString();cooldownScope.SelectedIndex=selected.CooldownScope=="user"?1:0;permissionScope.SelectedIndex=selected.PermissionScope switch{"subscriber"=>1,"vip"=>2,"moderator"=>3,"broadcaster"=>4,_=>0};enabled.IsChecked=selected.Enabled;}catch{}};
        panel.Children.Add(Label("Crear o editar",13,"#F8FAFC",true));panel.Children.Add(name);panel.Children.Add(aliases);panel.Children.Add(response);panel.Children.Add(cooldown);panel.Children.Add(Label("Tipo de cooldown",12,"#98A4BE"));panel.Children.Add(cooldownScope);panel.Children.Add(Label("Permiso mínimo",12,"#98A4BE"));panel.Children.Add(permissionScope);panel.Children.Add(testArguments);panel.Children.Add(enabled);
        var row=new WrapPanel();
        row.Children.Add(ActionButton("Guardar comando",(_,_)=>{try{store.SaveCommand(editingCommandId,name.Text,response.Text,int.Parse(cooldown.Text),enabled.IsChecked==true,cooldownScope.SelectedIndex==1?"user":"global",aliases.Text,SelectedPermissionScope());Reload();list.SelectedItem=null;editingCommandId=null;name.Clear();aliases.Clear();response.Clear();testArguments.Clear();cooldownScope.SelectedIndex=0;permissionScope.SelectedIndex=0;FooterStatus.Text="Comando guardado en SQLite.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        row.Children.Add(ActionButton("Previsualizar respuesta",(_,_)=>{var result=store.PreviewCommandResponse(name.Text,response.Text,"preview_user",testArguments.Text);MessageBox.Show(this,result.Message,"Previsualización local");}));
        row.Children.Add(ActionButton("Eliminar seleccionado",(_,_)=>{if(list.SelectedIndex<0)return;var id=long.Parse(list.SelectedItem!.ToString()!.Split(' ')[0]);if(MessageBox.Show(this,"¿Eliminar el comando seleccionado?","Confirmar",MessageBoxButton.YesNo)==MessageBoxResult.Yes){store.DeleteCommand(id);Reload();}}));
        panel.Children.Add(row);root.Children.Add(Label("El cooldown puede ser global o por usuario. El permiso mínimo se comprueba con etiquetas autenticadas de Twitch: Todos, Suscriptores, VIP, Moderadores o solo Broadcaster. Moderadores pueden usar los niveles de suscriptor/VIP; solo el broadcaster puede usar los comandos exclusivos del broadcaster. Un uso denegado se registra y no consume cooldown.",12,"#98A4BE"));return root;
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
        var root=new StackPanel();var panel=Panel("Sorteos");root.Children.Add(panel);
        var list=new ListBox{Height=250};void Reload(){list.ItemsSource=store.GetGiveaways().Select(g=>$"{g.Id} · {g.Name} · {g.Status} · entradas: {g.Entries} · ganador: {g.Winner??"—"}").ToList();}Reload();panel.Children.Add(list);
        var name=TextField("Nombre del sorteo");var entrant=TextField("Usuario para registrar como participante");var id=TextField("ID del sorteo");panel.Children.Add(name);panel.Children.Add(ActionButton("Crear sorteo",async (_,_)=>{try{id.Text=store.CreateGiveaway(name.Text).ToString();Reload();FooterStatus.Text=$"Sorteo #{id.Text} abierto.";if(twitch.IsConnected){try{await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),"🎁 Sorteo abierto: "+name.Text.Trim()+". Escribí !entrar para participar.");}catch(Exception sendError){FooterStatus.Text="Sorteo abierto localmente; no se anunció en Twitch: "+sendError.Message;}}}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));panel.Children.Add(Label("ID de sorteo",12,"#98A4BE"));panel.Children.Add(id);panel.Children.Add(entrant);
        var row=new WrapPanel();row.Children.Add(ActionButton("Registrar participante",(_,_)=>{try{if(!store.EnterGiveaway(long.Parse(id.Text),entrant.Text))FooterStatus.Text="No se registró: el sorteo puede estar cerrado o el usuario ya participaba.";else FooterStatus.Text="Participante registrado.";Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        row.Children.Add(ActionButton("Elegir ganador y cerrar",async (_,_)=>{try{var g=store.DrawGiveaway(long.Parse(id.Text));Reload();var announcement=$"🎉 Sorteo cerrado. Ganador: {g.Winner}. Participantes: {g.Entries}.";MessageBox.Show(this,$"Ganador: {g.Winner}\nParticipantes: {g.Entries}","Sorteo cerrado");if(twitch.IsConnected){try{await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),announcement);FooterStatus.Text="Ganador anunciado en Twitch.";}catch(Exception sendError){FooterStatus.Text="Sorteo cerrado y ganador guardado; no se pudo anunciar: "+sendError.Message;}}}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));panel.Children.Add(row);
        root.Children.Add(Label("La selección usa el conjunto de entradas únicas almacenadas; el ganador y el cierre quedan persistidos.",12,"#98A4BE"));return root;
    }

    private UIElement BuildMinigamesPage()
    {
        var root=new StackPanel();var panel=Panel("Minijuegos locales");root.Children.Add(panel);
        panel.Children.Add(Label("Duelo de moneda",22,"#F8FAFC",true));panel.Children.Add(Label("Elegí un nombre de usuario y jugá una ronda. Las victorias otorgan 10 puntos; cada sesión queda registrada.",13,"#98A4BE"));
        var user=TextField("Usuario");user.Text="Streamer";var game=new ComboBox{ItemsSource=new[]{"Duelo de moneda","Ruleta de suerte","Dado"},SelectedIndex=0};panel.Children.Add(user);panel.Children.Add(game);
        panel.Children.Add(ActionButton("Jugar ronda",async (_,_)=>{try{var gameName=game.SelectedItem?.ToString()??"Duelo de moneda";var result=store.PlayGame(gameName,user.Text,10);MessageBox.Show(this,result,"Resultado del minijuego");if(twitch.IsConnected){try{await twitch.SendMessageAsync(store.GetSetting("twitch.channel"),user.Text.Trim()+": "+gameName+" — "+result);}catch(Exception sendError){FooterStatus.Text="Ronda guardada; no se publicó en Twitch: "+sendError.Message;}}}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        var history=Panel("Historial reciente");history.Margin=new Thickness(0,16,0,0);history.Children.Add(new ListBox{ItemsSource=store.GetActivity(40).Where(a=>a.Module=="Minijuegos").Select(a=>$"{a.At.ToLocalTime():HH:mm:ss} · {a.Message}").ToList(),Height=220});root.Children.Add(history);return root;
    }
}
