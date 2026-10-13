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
    private TextBlock? obsStreamOutputStatusLabel;
    private TextBlock? obsRecordOutputStatusLabel;
    private Action<string, JsonElement>? obsEventHandler;
    private Action<string>? obsStatusHandler;

    private UIElement BuildObsPage()
    {
        var root=new StackPanel();var panel=Panel("OBS Studio · WebSocket v5");root.Children.Add(panel);
        panel.Children.Add(Label("Conexión autenticada y control de escenas",19,"#F8FAFC",true));
        panel.Children.Add(Label("Activá WebSocket Server en OBS → Herramientas. La app no registra una conexión hasta completar Hello/Identify. La contraseña se cifra con DPAPI CurrentUser.",13,"#98A4BE"));
        var host=TextField("Host",store.GetSetting("obs.host","127.0.0.1"));
        var port=TextField("Puerto",store.GetSetting("obs.port","4455"));
        var password=new PasswordBox{Password=LoadObsPassword(),Background=Brush("#101525"),Foreground=Brush("#F8FAFC"),BorderBrush=Brush("#34415E"),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)};
        obsStreamOutputStatusLabel = Label("Transmisión: estado desconocido", 12, "#98A4BE");
        obsRecordOutputStatusLabel = Label("Grabación: estado desconocido", 12, "#98A4BE");

        if (obsEventHandler is not null)
            obs.EventReceived -= obsEventHandler;
        if (obsStatusHandler is not null)
            obs.StatusChanged -= obsStatusHandler;

        obsEventHandler = (eventType, eventData) =>
        {
            if (eventType is not ("StreamStateChanged" or "RecordStateChanged"))
                return;

            bool? active = eventData.ValueKind == JsonValueKind.Object &&
                eventData.TryGetProperty("outputActive", out var activeValue) &&
                activeValue.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? activeValue.GetBoolean()
                    : null;

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var label = eventType == "StreamStateChanged"
                        ? obsStreamOutputStatusLabel
                        : obsRecordOutputStatusLabel;
                    if (label is null)
                        return;

                    var outputName = eventType == "StreamStateChanged" ? "Transmisión" : "Grabación";
                    label.Text = active.HasValue
                        ? outputName + ": " + (active.Value ? "ACTIVA" : "Inactiva")
                        : outputName + ": estado no disponible";
                }));
            }
            catch (InvalidOperationException)
            {
                // Dispatcher shutdown is not a WebSocket transport error.
            }
        };
        obs.EventReceived += obsEventHandler;

        async Task RefreshOutputStatusAsync()
        {
            if (!obs.IsConnected)
            {
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: OBS desconectado";
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: OBS desconectado";
                return;
            }

            try
            {
                var active = await obs.IsStreamingAsync();
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                if (obsStreamOutputStatusLabel is not null)
                    obsStreamOutputStatusLabel.Text = "Transmisión: no disponible";
                store.Log("OBS", "No se pudo leer el estado de transmisión (" + exception.GetType().Name + ").");
            }

            try
            {
                var active = await obs.IsRecordingAsync();
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: " + (active ? "ACTIVA" : "Inactiva");
            }
            catch (Exception exception)
            {
                if (obsRecordOutputStatusLabel is not null)
                    obsRecordOutputStatusLabel.Text = "Grabación: no disponible";
                store.Log("OBS", "No se pudo leer el estado de grabación (" + exception.GetType().Name + ").");
            }
        }

        // Keep the output indicators aligned with the actual WebSocket session. The
        // client can disconnect or reconnect without the user pressing a page button.
        obsStatusHandler = status =>
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => _ = RefreshOutputStatusAsync()));
            }
            catch (InvalidOperationException)
            {
                // The dispatcher may already be shutting down.
            }
        };
        obs.StatusChanged += obsStatusHandler;
        var pageStatusHandler = obsStatusHandler;
        var pageEventHandler = obsEventHandler;
        root.Unloaded += (_, _) =>
        {
            if (pageStatusHandler is not null)
                obs.StatusChanged -= pageStatusHandler;
            if (pageEventHandler is not null)
                obs.EventReceived -= pageEventHandler;

            if (ReferenceEquals(obsStatusHandler, pageStatusHandler))
                obsStatusHandler = null;
            if (ReferenceEquals(obsEventHandler, pageEventHandler))
                obsEventHandler = null;
        };

        // Refresh on entry as well as on subsequent connection/reconnection events.
        _ = RefreshOutputStatusAsync();

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
        panel.Children.Add(Label("Contraseña de WebSocket (obligatoria; habilitala en OBS)",12,"#98A4BE"));panel.Children.Add(password);
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
        outputPanel.Children.Add(obsStreamOutputStatusLabel);
        outputPanel.Children.Add(obsRecordOutputStatusLabel);
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

        var destinationsPanel = Panel("Transmitir a Twitch o YouTube");
        destinationsPanel.Margin = new Thickness(0, 14, 0, 0);
        destinationsPanel.Children.Add(Label("Elegí la plataforma, guardá su clave una sola vez y usa «Configurar destino». La clave se cifra con Windows y nunca se muestra en pantalla ni se registra en logs.", 12, "#98A4BE"));

        var destinationChoice = new ComboBox
        {
            ItemsSource = new[] { "Twitch", "YouTube" },
            SelectedItem = store.GetSetting("obs.stream.platform", "Twitch") == "YouTube" ? "YouTube" : "Twitch",
            MinWidth = 240,
            Margin = new Thickness(0, 4, 8, 8)
        };
        var destinationServer = new TextBox
        {
            IsReadOnly = true,
            Text = destinationChoice.SelectedItem?.ToString() == "YouTube"
                ? "rtmps://a.rtmp.youtube.com:443/live2"
                : "rtmps://live.twitch.tv:443/app",
            ToolTip = "Servidor seguro RTMPS",
            MinWidth = 320,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var destinationKey = new PasswordBox
        {
            MaxLength = 8192,
            MinWidth = 320,
            Background = Brush("#101525"),
            Foreground = Brush("#F8FAFC"),
            BorderBrush = Brush("#34415E"),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var destinationKeyStatus = Label("", 12, "#98A4BE");

        string DestinationSecretName() =>
            destinationChoice.SelectedItem?.ToString() == "YouTube"
                ? "obs-youtube-stream-key"
                : "obs-twitch-stream-key";

        void RefreshDestinationChoice()
        {
            var isYouTube = destinationChoice.SelectedItem?.ToString() == "YouTube";
            destinationServer.Text = isYouTube
                ? "rtmps://a.rtmp.youtube.com:443/live2"
                : "rtmps://live.twitch.tv:443/app";
            destinationKey.Clear();
            destinationKeyStatus.Text = secretStore.HasSecret(DestinationSecretName())
                ? "Clave guardada de forma cifrada para " + (isYouTube ? "YouTube." : "Twitch.") + " Dejá la caja vacía para reutilizarla."
                : "Todavía no hay una clave guardada para esta plataforma.";
            store.SetSetting("obs.stream.platform", isYouTube ? "YouTube" : "Twitch");
        }

        destinationChoice.SelectionChanged += (_, _) => RefreshDestinationChoice();
        destinationsPanel.Children.Add(Label("Plataforma", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationChoice);
        destinationsPanel.Children.Add(Label("Servidor RTMPS", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationServer);
        destinationsPanel.Children.Add(Label("Clave de transmisión (no la compartas con nadie)", 12, "#98A4BE"));
        destinationsPanel.Children.Add(destinationKey);
        destinationsPanel.Children.Add(destinationKeyStatus);

        var destinationButtons = new WrapPanel();
        destinationButtons.Children.Add(ActionButton("Abrir panel de la plataforma", (_, _) =>
        {
            var url = destinationChoice.SelectedItem?.ToString() == "YouTube"
                ? "https://studio.youtube.com/"
                : "https://dashboard.twitch.tv/settings/stream";
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo abrir el panel web: " + exception.Message, "Transmisión", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }, true));
        destinationButtons.Children.Add(ActionButton("Guardar clave cifrada", (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationKey.Password))
                {
                    destinationKeyStatus.Text = secretStore.HasSecret(DestinationSecretName())
                        ? "Ya existe una clave cifrada. Dejá el campo vacío para conservarla."
                        : "Pegá primero la clave de la plataforma.";
                    return;
                }
                secretStore.Save(DestinationSecretName(), destinationKey.Password);
                destinationKey.Clear();
                destinationKeyStatus.Text = "Clave guardada de forma cifrada. No se guardó en SQLite.";
                store.Log("OBS", "Se guardó una clave de transmisión cifrada para " + destinationChoice.SelectedItem + ".");
            }
            catch (Exception exception)
            {
                destinationKeyStatus.Text = "No se pudo guardar la clave: " + exception.GetType().Name;
            }
        }));
        destinationButtons.Children.Add(ActionButton("Configurar destino en OBS", async (_, _) =>
        {
            try
            {
                if (!obs.IsConnected)
                    throw new InvalidOperationException("Primero conectá y autenticá OBS WebSocket en el panel de arriba.");
                var platform = destinationChoice.SelectedItem?.ToString() == "YouTube" ? "YouTube" : "Twitch";
                if (!string.IsNullOrWhiteSpace(destinationKey.Password))
                {
                    secretStore.Save(DestinationSecretName(), destinationKey.Password);
                    destinationKey.Clear();
                }
                var key = secretStore.Read(DestinationSecretName());
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("Todavía no hay una clave guardada. Pegá la clave y pulsa «Guardar clave cifrada».");
                await obs.ConfigureStreamDestinationAsync(platform, key);
                store.SetSetting("obs.stream.platform", platform);
                destinationKeyStatus.Text = "OBS quedó configurado para " + platform + ". La transmisión todavía no se inició.";
                FooterStatus.Text = "Destino de OBS configurado para " + platform + "; falta iniciar la transmisión.";
                store.Log("OBS", "Destino de streaming configurado en OBS para " + platform + "; no se registró la clave.");
            }
            catch (Exception exception)
            {
                FooterStatus.Text = "No se pudo configurar el destino: " + exception.Message;
                MessageBox.Show(this, "No se pudo configurar la plataforma. La clave no se mostrará en el mensaje.\n\n" + exception.Message,
                    "Configurar transmisión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, true));
        destinationsPanel.Children.Add(destinationButtons);
        destinationsPanel.Children.Add(Label("Para iniciar: conectá OBS, configurá una plataforma aquí y pulsa «Iniciar transmisión» en el panel de arriba. Cambiar de destino exige detener la emisión actual.", 12, "#98A4BE"));

        var multistreamPanel = Panel("Emitir en Twitch y YouTube al mismo tiempo");
        multistreamPanel.Margin = new Thickness(0, 14, 0, 0);
        multistreamPanel.Children.Add(Label("OBS normal envía la salida principal a un destino. Para salir en los dos al mismo tiempo necesitás un complemento multi-RTMP de OBS. Es opcional, gratuito y externo; VTuberBot no lo instala ni lo incluye.", 12, "#98A4BE"));
        multistreamPanel.Children.Add(ActionButton("Abrir página del complemento multi-RTMP", (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://obsproject.com/forum/resources/multiple-rtmp-outputs-plugin.964/") { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo abrir la página del complemento: " + exception.Message, "Multistream", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }, true));
        multistreamPanel.Children.Add(Label("Instalá solo una versión compatible con tu versión de OBS; agrega YouTube y Twitch en el complemento y configura allí el destino secundario. Luego inicia cada salida según sus controles. Si compartís encoder, el consumo puede ser menor; si cada destino codifica por separado, aumentan el uso de CPU/GPU y el ancho de subida. La compatibilidad del complemento debe comprobarse en tu instalación.", 12, "#98A4BE"));
        RefreshDestinationChoice();
        root.Children.Add(destinationsPanel);
        root.Children.Add(multistreamPanel);

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
        var root = new DockPanel();
        var toolbar = new WrapPanel();
        DockPanel.SetDock(toolbar, Dock.Top);

        // This page is rebuilt during navigation, so subscribe to the WebView2 control
        // itself, not MainWindow.Loaded (which has already fired by the time the user
        // navigates here). Share one initialization task to avoid racing auto-load and
        // the toolbar action when both run as soon as the control enters the visual tree.
        var view = new WebView2();
        telegramView = view;
        Task? initializationTask = null;

        async Task EnsureTelegramWebAsync(bool openBrowserOnFailure)
        {
            try
            {
                if (view.CoreWebView2 is null)
                {
                    initializationTask ??= view.EnsureCoreWebView2Async();
                    await initializationTask;
                }

                if (view.Source is null)
                    view.Source = new Uri("https://web.telegram.org/");

                FooterStatus.Text = "Telegram Web cargando en WebView2.";
            }
            catch (Exception exception)
            {
                initializationTask = null;
                FooterStatus.Text = "WebView2 no disponible: " + exception.Message;
                store.Log("Telegram", "No se pudo inicializar WebView2 (" + exception.GetType().Name + ").");
                if (openBrowserOnFailure)
                    OpenTelegramExternal();
            }
        }

        toolbar.Children.Add(ActionButton(
            "Abrir Telegram Web",
            async (_, _) => await EnsureTelegramWebAsync(openBrowserOnFailure: true),
            true));
        toolbar.Children.Add(ActionButton("Abrir en navegador", (_, _) => OpenTelegramExternal()));

        root.Children.Add(toolbar);
        root.Children.Add(view);
        view.Loaded += async (_, _) => await EnsureTelegramWebAsync(openBrowserOnFailure: false);
        return root;
    }

    private void OpenTelegramExternal(){try{Process.Start(new ProcessStartInfo("https://web.telegram.org/"){UseShellExecute=true});store.Log("Telegram","Telegram Web abierto en navegador externo.");}catch(Exception ex){MessageBox.Show(this,ex.Message);}}
}
