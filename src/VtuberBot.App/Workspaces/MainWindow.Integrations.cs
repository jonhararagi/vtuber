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
    private UIElement BuildObsPage()
    {
        var root=new StackPanel();var panel=Panel("OBS Studio · WebSocket v5");root.Children.Add(panel);
        panel.Children.Add(Label("Conexión autenticada y control de escenas",19,"#F8FAFC",true));
        panel.Children.Add(Label("Activá WebSocket Server en OBS → Herramientas. La app no registra una conexión hasta completar Hello/Identify. La contraseña se cifra con DPAPI CurrentUser.",13,"#98A4BE"));
        var host=TextField("Host",store.GetSetting("obs.host","127.0.0.1"));
        var port=TextField("Puerto",store.GetSetting("obs.port","4455"));
        var password=new PasswordBox{Password=LoadObsPassword(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        var streamOutputStatus = Label("Transmisión: estado desconocido", 12, "#98A4BE");
        var recordOutputStatus = Label("Grabación: estado desconocido", 12, "#98A4BE");

        async Task RefreshOutputStatusAsync()
        {
            if (!obs.IsConnected)
            {
                streamOutputStatus.Text = "Transmisión: OBS desconectado";
                recordOutputStatus.Text = "Grabación: OBS desconectado";
                return;
            }

            try
            {
                var active = await obs.IsStreamingAsync();
                streamOutputStatus.Text = "Transmisión: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                streamOutputStatus.Text = "Transmisión: no disponible";
                store.Log("OBS", "No se pudo leer el estado de transmisión (" + exception.GetType().Name + ").");
            }

            try
            {
                var active = await obs.IsRecordingAsync();
                recordOutputStatus.Text = "Grabación: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                recordOutputStatus.Text = "Grabación: no disponible";
                store.Log("OBS", "No se pudo leer el estado de grabación (" + exception.GetType().Name + ").");
            }
        }

        async Task RunOutputActionAsync(string action, Func<Task> operation)
        {
            try
            {
                await operation();
                store.Log("OBS", action + " ejecutada correctamente.");
                FooterStatus.Text = action + " ejecutada en OBS.";
            }
            catch (Exception exception)
            {
                store.Log("OBS", "Falló " + action + " (" + exception.GetType().Name + ").");
                FooterStatus.Text = action + " falló: " + exception.Message;
                MessageBox.Show(this, action + " no se pudo completar. Revisá el estado real de OBS.\n\n" + exception.Message,
                    "OBS Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            await RefreshOutputStatusAsync();
        }

        panel.Children.Add(Label("Host",12,"#98A4BE"));panel.Children.Add(host);
        panel.Children.Add(Label("Puerto",12,"#98A4BE"));panel.Children.Add(port);
        panel.Children.Add(Label("Contraseña de WebSocket (si OBS la exige)",12,"#98A4BE"));panel.Children.Add(password);
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Conectar y autenticar",async (_,_)=>{try{store.SetSetting("obs.host",host.Text.Trim());store.SetSetting("obs.port",port.Text.Trim());SaveObsPassword(password.Password);await obs.ConnectAsync(host.Text.Trim(),int.Parse(port.Text),password.Password);var version=await obs.GetVersionAsync();FooterStatus.Text="OBS autenticado · versión "+version;store.Log("OBS","Conexión WebSocket v5 autenticada; versión "+version);await RefreshOutputStatusAsync();}catch(Exception ex){FooterStatus.Text="OBS no conectado: "+ex.Message;store.Log("OBS","Conexión WebSocket fallida: "+ex.GetType().Name);MessageBox.Show(this,"No se pudo autenticar con OBS. Confirmá que WebSocket esté habilitado, el puerto y la contraseña.\n\n"+ex.Message);}} ,true));
        actions.Children.Add(ActionButton("Desconectar",async (_,_)=>{await obs.DisconnectAsync();await RefreshOutputStatusAsync();}));
        panel.Children.Add(actions);
        var scenes=new ComboBox{MinWidth=260};
        var sceneRow=new WrapPanel();
        sceneRow.Children.Add(ActionButton("Actualizar escenas",async (_,_)=>{try{scenes.ItemsSource=await obs.GetScenesAsync();if(scenes.Items.Count>0)scenes.SelectedIndex=0;FooterStatus.Text="Escenas recibidas de OBS.";}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));
        sceneRow.Children.Add(scenes);
        sceneRow.Children.Add(ActionButton("Cambiar escena",async (_,_)=>{try{if(scenes.SelectedItem is not string selected)throw new InvalidOperationException("Primero cargá y elegí una escena.");await obs.SetCurrentSceneAsync(selected);store.Log("OBS","Escena cambiada a "+selected);FooterStatus.Text="Escena activa: "+selected;}catch(Exception ex){MessageBox.Show(this,ex.Message);}} ,true));
        panel.Children.Add(Label("Control de escenas",14,"#F8FAFC",true));panel.Children.Add(sceneRow);

        var outputPanel = Panel("Transmisión y grabación");
        outputPanel.Margin = new Thickness(0, 14, 0, 0);
        outputPanel.Children.Add(streamOutputStatus);
        outputPanel.Children.Add(recordOutputStatus);
        var outputActions = new WrapPanel();
        outputActions.Children.Add(ActionButton("Actualizar estado", async (_, _) => await RefreshOutputStatusAsync()));
        outputActions.Children.Add(ActionButton("Iniciar transmisión", async (_, _) =>
            await RunOutputActionAsync("Iniciar transmisión", () => obs.StartStreamingAsync()), true));
        outputActions.Children.Add(ActionButton("Detener transmisión", async (_, _) =>
        {
            if (MessageBox.Show(this, "¿Detener la transmisión en vivo de OBS?", "Confirmar detención",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Detener transmisión", () => obs.StopStreamingAsync());
        }));
        outputActions.Children.Add(ActionButton("Iniciar grabación", async (_, _) =>
            await RunOutputActionAsync("Iniciar grabación", () => obs.StartRecordingAsync())));
        outputActions.Children.Add(ActionButton("Detener grabación", async (_, _) =>
        {
            if (MessageBox.Show(this, "¿Detener la grabación de OBS?", "Confirmar detención",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            await RunOutputActionAsync("Detener grabación", () => obs.StopRecordingAsync());
        }));
        outputPanel.Children.Add(outputActions);
        outputPanel.Children.Add(Label("Las acciones solo se envían tras la autenticación WebSocket. El estado se lee desde OBS; guardar la configuración no simula una salida activa.", 12, "#98A4BE"));
        root.Children.Add(outputPanel);

        var overlayPanel=Panel("Avatar overlay para OBS");overlayPanel.Children.Add(Label("El overlay se abre como ventana sin bordes y fondo transparente. En OBS agregá una fuente Captura de ventana y seleccioná “VTuberBot Avatar Overlay”. Doble clic en el overlay alterna Topmost; arrastrá para ubicarlo.",13,"#98A4BE"));
        overlayPanel.Children.Add(ActionButton("Mostrar overlay",(_,_)=>ShowOverlay(),true));root.Children.Add(overlayPanel);
        return root;
    }

    private string LoadObsPassword()
    {
        var path=Path.Combine(secretDirectory,"obs-password.bin");if(!File.Exists(path))return "";
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}
        catch{FooterStatus.Text="No se pudo descifrar la contraseña guardada de OBS.";return "";}
    }
    private void SaveObsPassword(string password)
    {
        var path=Path.Combine(secretDirectory,"obs-password.bin");
        if(string.IsNullOrEmpty(password)){if(File.Exists(path))File.Delete(path);return;}
        File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(password),null,DataProtectionScope.CurrentUser));
    }

    private UIElement BuildTelegramPage()
    {
        var root=new DockPanel();var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);
        toolbar.Children.Add(ActionButton("Abrir Telegram Web",async (_,_)=>{if(telegramView is null){FooterStatus.Text="Inicializando WebView2…";return;}try{await telegramView.EnsureCoreWebView2Async();telegramView.Source=new Uri("https://web.telegram.org/");FooterStatus.Text="Telegram Web cargando en WebView2.";}catch(Exception ex){FooterStatus.Text=$"WebView2 no disponible: {ex.Message}";OpenTelegramExternal();}},true));
        toolbar.Children.Add(ActionButton("Abrir en navegador",(_,_)=>OpenTelegramExternal()));
        root.Children.Add(toolbar);telegramView=new WebView2();root.Children.Add(telegramView);
        Loaded+=async (_,_)=>{try{await telegramView.EnsureCoreWebView2Async();telegramView.Source=new Uri("https://web.telegram.org/");}catch{FooterStatus.Text="WebView2 no está disponible. Usá el botón para abrir Telegram en el navegador.";}};return root;
    }
    private void OpenTelegramExternal(){try{Process.Start(new ProcessStartInfo("https://web.telegram.org/"){UseShellExecute=true});store.Log("Telegram","Telegram Web abierto en navegador externo.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}
}
