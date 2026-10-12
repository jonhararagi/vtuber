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
    private UIElement BuildModerationPage()
    {
        var root=new StackPanel();var panel=Panel("Reglas de moderación");root.Children.Add(panel);
        var list=new ListBox{Height=180};void Reload(){list.ItemsSource=store.GetModerationRules().Select(r=>$"{r.Name} | /{r.Pattern}/ | {(r.Enabled?"activa":"pausada")} | {r.Action}").ToList();}Reload();panel.Children.Add(list);
        var autoModeration = new CheckBox
        {
            Content = "Activar acciones automáticas en Twitch",
            IsChecked = store.GetSetting("twitch.auto_moderation.enabled", "false") == "true",
            Foreground = Brush("#F8FAFC"),
            Margin = new Thickness(0, 6, 0, 6)
        };
        autoModeration.Checked += (_, _) =>
        {
            var choice = MessageBox.Show(this,
                "Las reglas configuradas como warn/delete/timeout/ban podrán actuar en el chat real. Regex mal diseñada puede afectar mensajes legítimos. ¿Querés activar la moderación automática?",
                "Activar moderación automática", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes)
            {
                autoModeration.IsChecked = false;
                return;
            }
            store.SetSetting("twitch.auto_moderation.enabled", "true");
            store.Log("Moderación", "Moderación automática activada explícitamente por el usuario.");
        };
        autoModeration.Unchecked += (_, _) =>
        {
            store.SetSetting("twitch.auto_moderation.enabled", "false");
            store.Log("Moderación", "Moderación automática desactivada.");
        };
        panel.Children.Add(autoModeration);
        var name=TextField("Nombre de la regla");var pattern=TextField("Patrón regex (ej. https?://)");
        var action=new ComboBox{ItemsSource=new[]{"flag","warn","delete","timeout","ban"},SelectedIndex=0};
        var enabled=new CheckBox{Content="Habilitada",IsChecked=true,Foreground=Brush("#F8FAFC")};panel.Children.Add(name);panel.Children.Add(pattern);panel.Children.Add(action);panel.Children.Add(enabled);
        panel.Children.Add(ActionButton("Guardar regla",(_,_)=>{try{store.SaveModerationRule(name.Text,pattern.Text,enabled.IsChecked==true,action.SelectedItem?.ToString()??"flag");Reload();}catch(Exception ex){MessageBox.Show(this,$"Regla inválida: {ex.Message}");}},true));
        var test=TextField("Texto para analizar");panel.Children.Add(Label("Probar un mensaje (solo observación, no borra ni sanciona)",13,"#F8FAFC",true));panel.Children.Add(test);
        panel.Children.Add(ActionButton("Analizar mensaje",(_,_)=>{var matches=new List<string>();foreach(var r in store.GetModerationRules().Where(x=>x.Enabled)){try{if(Regex.IsMatch(test.Text,r.Pattern,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))matches.Add($"{r.Name} ({r.Action})");}catch(RegexMatchTimeoutException){matches.Add($"{r.Name} — tiempo agotado");}}var message=matches.Count==0?"No se detectaron coincidencias.": "Coincidencias: "+string.Join(", ",matches);store.Log("Moderación",message);MessageBox.Show(this,message,"Diagnóstico de moderación");}));
        root.Children.Add(Label("Por defecto, las reglas solo registran coincidencias. La activación global exige confirmación: flag solo registra, warn publica un aviso, delete borra el mensaje coincidente, timeout aplica 10 minutos y ban es permanente. El límite es una acción por usuario/regla cada 5 minutos; si una acción falla, queda registrada como fallida, nunca como exitosa.",12,"#98A4BE"));
        var remotePanel = Panel("Acciones de Twitch — manuales y con confirmación");
        remotePanel.Margin = new Thickness(0, 12, 0, 0);
        remotePanel.Children.Add(Label("Estas acciones solo se ejecutan al pulsar un botón y confirmar. Las reglas automáticas siguen en modo observación.", 12, "#98A4BE"));
        var moderationChannel = TextField("Canal Twitch", store.GetSetting("twitch.channel"));
        var moderationTarget = TextField("Usuario de Twitch (login, sin @)");
        var moderationReason = TextField("Motivo (opcional, máximo 500 caracteres)");
        var durationValues = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["30 segundos"] = 30, ["1 minuto"] = 60, ["5 minutos"] = 300,
            ["10 minutos"] = 600, ["1 hora"] = 3600, ["1 día"] = 86400
        };
        var durationBox = new ComboBox { ItemsSource = durationValues.Keys.ToList(), SelectedItem = "10 minutos", MinWidth = 180, Margin = new Thickness(0, 0, 0, 8) };
        remotePanel.Children.Add(Label("Canal", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationChannel);
        remotePanel.Children.Add(Label("Usuario objetivo", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationTarget);
        remotePanel.Children.Add(Label("Motivo", 12, "#98A4BE"));
        remotePanel.Children.Add(moderationReason);
        remotePanel.Children.Add(Label("Duración del timeout", 12, "#98A4BE"));
        remotePanel.Children.Add(durationBox);

        async Task ExecuteManualModerationAction(string actionName, int? durationSeconds, bool unban)
        {
            var channelName = moderationChannel.Text.Trim().TrimStart('#');
            var targetLogin = moderationTarget.Text.Trim().TrimStart('@');
            var reason = moderationReason.Text.Trim();
            var clientId = store.GetSetting("twitch.client_id").Trim();
            var accessToken = LoadTwitchToken();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
            {
                MessageBox.Show(this, "Primero configurá Twitch y autorizá la cuenta. El token se lee del almacenamiento cifrado local.", "Twitch no configurado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var details = unban
                ? $"Vas a retirar un ban/timeout a {targetLogin} en {channelName}."
                : durationSeconds.HasValue
                    ? $"Vas a aplicar un timeout de {durationSeconds.Value} segundos a {targetLogin} en {channelName}."
                    : $"Vas a BANEAR PERMANENTEMENTE a {targetLogin} en {channelName}.";
            if (!string.IsNullOrWhiteSpace(reason)) details += "\nMotivo: " + reason;
            details += "\n\nEsta acción afecta al chat real de Twitch. ¿Confirmás?";
            if (MessageBox.Show(this, details, "Confirmar acción de moderación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using var moderator = new TwitchModerationClient();
                var result = unban
                    ? await moderator.UnbanAsync(clientId, accessToken, channelName, targetLogin)
                    : await moderator.BanOrTimeoutAsync(clientId, accessToken, channelName, targetLogin, durationSeconds, reason);
                store.Log("Moderación", "Acción manual '" + actionName + "' ejecutada para " + targetLogin + " en " + channelName + ".");
                FooterStatus.Text = result;
                MessageBox.Show(this, result, "Moderación de Twitch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var safeMessage = "No se pudo ejecutar la acción. Verificá conexión Twitch, cuenta moderadora y el scope moderator:manage:banned_users. Si acabás de añadir permisos, volvé a autorizar la cuenta.\n\n" + ex.Message;
                FooterStatus.Text = "Moderación no ejecutada: " + ex.GetType().Name;
                store.Log("Moderación", "Falló una acción manual (" + ex.GetType().Name + ").");
                MessageBox.Show(this, safeMessage, "Acción de moderación fallida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        var remoteActions = new WrapPanel();
        remoteActions.Children.Add(ActionButton("Aplicar timeout", async (_, _) =>
        {
            var label = durationBox.SelectedItem as string;
            if (label is null || !durationValues.TryGetValue(label, out var duration))
            {
                MessageBox.Show(this, "Elegí una duración de timeout válida.");
                return;
            }
            await ExecuteManualModerationAction("timeout", duration, false);
        }, true));
        remoteActions.Children.Add(ActionButton("Ban permanente", async (_, _) => await ExecuteManualModerationAction("ban permanente", null, false)));
        remoteActions.Children.Add(ActionButton("Quitar ban / timeout", async (_, _) => await ExecuteManualModerationAction("desbanear", null, true)));
        remotePanel.Children.Add(remoteActions);
        remotePanel.Children.Add(Label("Si Twitch rechaza la solicitud, la aplicación registra el tipo de error pero nunca presenta la sanción como exitosa. No se ejecutan sanciones por coincidencias regex.", 12, "#98A4BE"));
        root.Children.Add(remotePanel);
        var auto=Panel("Mensajes automáticos programados");auto.Margin=new Thickness(0,12,0,0);
        var autoList=new ListBox{Height=130};long? editingAutomationId=null;
        void ReloadAuto(){autoList.ItemsSource=store.GetAutomationRules().Select(a=>string.Format("{0} · {1} · cada {2} min · {3} · último envío: {4}",a.Id,a.Name,a.IntervalMinutes,a.Enabled?"activo":"pausado",a.LastRunUtc.HasValue?a.LastRunUtc.Value.ToLocalTime().ToString("dd/MM HH:mm:ss"):"nunca")).ToList();}
        ReloadAuto();auto.Children.Add(autoList);
        var autoName=TextField("Nombre del timer");var autoMessage=TextField("Mensaje programado");var autoInterval=TextField("Intervalo en minutos (mín. 5)");autoInterval.Text="30";
        var autoEnabled=new CheckBox{Content="Activar al guardar",IsChecked=false,Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,8)};
        autoList.SelectionChanged+=(_,_)=>{if(autoList.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split('·')[0].Trim());var selected=store.GetAutomationRules().FirstOrDefault(a=>a.Id==selectedId);if(selected is null)return;editingAutomationId=selected.Id;autoName.Text=selected.Name;autoMessage.Text=selected.Message;autoInterval.Text=selected.IntervalMinutes.ToString();autoEnabled.IsChecked=selected.Enabled;}catch{}};
        auto.Children.Add(autoName);auto.Children.Add(autoMessage);auto.Children.Add(autoInterval);auto.Children.Add(autoEnabled);
        auto.Children.Add(ActionButton("Guardar mensaje automático",(_,_)=>{try{if(editingAutomationId.HasValue)automationFirstObserved.Remove(editingAutomationId.Value);store.SaveAutomationRule(editingAutomationId,autoName.Text,autoMessage.Text,int.Parse(autoInterval.Text),autoEnabled.IsChecked==true);ReloadAuto();autoList.SelectedItem=null;editingAutomationId=null;autoName.Clear();autoMessage.Clear();autoInterval.Text="30";autoEnabled.IsChecked=false;}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true));
        auto.Children.Add(ActionButton("Pausar / reactivar seleccionado",(_,_)=>{if(autoList.SelectedItem is not string selectedText)return;try{var selectedId=long.Parse(selectedText.Split('·')[0].Trim());var selected=store.GetAutomationRules().FirstOrDefault(a=>a.Id==selectedId);if(selected is null)return;store.SaveAutomationRule(selected.Id,selected.Name,selected.Message,selected.IntervalMinutes,!selected.Enabled);automationFirstObserved.Remove(selected.Id);ReloadAuto();autoList.SelectedItem=null;}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        auto.Children.Add(ActionButton("Eliminar seleccionado",(_,_)=>
        {
            if(autoList.SelectedItem is not string selectedText)return;
            if(MessageBox.Show(this,"¿Eliminar definitivamente este mensaje programado?","Confirmar eliminación",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            try
            {
                var selectedId=long.Parse(selectedText.Split('·')[0].Trim());
                store.DeleteAutomationRule(selectedId);
                automationLastRun.Remove(selectedId);
                automationOfflineLog.Remove(selectedId);
                automationFirstObserved.Remove(selectedId);
                ReloadAuto();
                autoList.SelectedItem=null;
                editingAutomationId=null;
                autoName.Clear();
                autoMessage.Clear();
                autoInterval.Text="30";
                autoEnabled.IsChecked=false;
                FooterStatus.Text="Mensaje automático eliminado.";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message);}
        }));
        auto.Children.Add(ActionButton("Nuevo / limpiar formulario",(_,_)=>{autoList.SelectedItem=null;editingAutomationId=null;autoName.Clear();autoMessage.Clear();autoInterval.Text="30";autoEnabled.IsChecked=false;}));
        auto.Children.Add(Label("Los mensajes programados solo se envían cuando Twitch está conectado; sin conexión, el intervalo no se consume. Podés seleccionar para editar, pausar/reactivar o eliminar. Solo se envía como máximo un mensaje automático por minuto.",11,"#98A4BE"));
        root.Children.Add(auto);return root;
    }

    private async Task ProcessAutomationsAsync()
    {
        if (Interlocked.Exchange(ref automationProcessing, 1) != 0)
            return;

        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var rule in store.GetAutomationRules().Where(rule => rule.Enabled))
            {
                var lastRun = rule.LastRunUtc;
                if (!lastRun.HasValue && automationLastRun.TryGetValue(rule.Id, out var inMemoryLastRun))
                    lastRun = inMemoryLastRun;
                var firstObserved = now;
                if (!lastRun.HasValue && !automationFirstObserved.TryGetValue(rule.Id, out firstObserved))
                {
                    // A newly enabled timer must wait its configured interval before its first
                    // broadcast; startup must not send every never-used timer immediately.
                    automationFirstObserved[rule.Id] = now;
                    continue;
                }
                if (!AutomationSchedulePolicy.IsDue(now, lastRun, firstObserved, rule.IntervalMinutes))
                    continue;

                if (!twitch.IsConnected)
                {
                    if (!automationOfflineLog.TryGetValue(rule.Id, out var lastOfflineLog) ||
                        now - lastOfflineLog >= TimeSpan.FromMinutes(10))
                    {
                        automationOfflineLog[rule.Id] = now;
                        store.Log("Automatización", $"'{rule.Name}' espera: Twitch está desconectado; no se consumió su intervalo.");
                    }
                    continue;
                }

                var safeMessage = rule.Message.Replace("\\r", " ", StringComparison.Ordinal)
                    .Replace("\\n", " ", StringComparison.Ordinal).Trim();
                if (safeMessage.Length is 0 or > 500)
                {
                    store.Log("Automatización", $"'{rule.Name}' no se envió: el mensaje está vacío o supera 500 caracteres.");
                    break;
                }

                try
                {
                    await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), safeMessage);
                    var sentAt = DateTimeOffset.UtcNow;
                    store.MarkAutomationRun(rule.Id, sentAt);
                    automationLastRun[rule.Id] = sentAt;
                    automationFirstObserved.Remove(rule.Id);
                    automationOfflineLog.Remove(rule.Id);
                    store.Log("Automatización", $"Mensaje '{rule.Name}' enviado.");
                    // At most one scheduled message per minute, even if several rules are overdue.
                    break;
                }
                catch (Exception exception)
                {
                    store.Log("Automatización", $"'{rule.Name}' falló ({exception.GetType().Name}); se reintentará más tarde.");
                    break;
                }
            }
        }
        finally
        {
            Volatile.Write(ref automationProcessing, 0);
        }
    }
}
