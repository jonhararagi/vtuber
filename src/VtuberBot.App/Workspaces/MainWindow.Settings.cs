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
    private UIElement BuildSettingsPage()
    {
        var root=new StackPanel();var panel=Panel("Preferencias locales");root.Children.Add(panel);
        var profile=new ComboBox{ItemsSource=new[]{"Ahorro","Equilibrado","Calidad"},SelectedItem=store.GetSetting("performance.profile","Equilibrado")};panel.Children.Add(Label("Perfil de rendimiento",12,"#98A4BE"));panel.Children.Add(profile);
        var reduceMotion=new CheckBox{Content="Reducir movimiento del overlay",IsChecked=store.GetSetting("ui.reduceMotion","false")=="true",Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,8,0,8)};
        var parallaxMotion=new CheckBox{Content="Parallax 2.5D sutil por movimiento del cursor",IsChecked=store.GetSetting("avatar.parallax.enabled","true")=="true",Foreground=Brush("#F8FAFC"),Margin=new Thickness(0,0,0,8)};
        panel.Children.Add(reduceMotion);
        panel.Children.Add(parallaxMotion);
        var savedParallaxStrength = double.TryParse(store.GetSetting("avatar.parallax.strength", "3"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedParallaxStrength)
            ? Math.Clamp(parsedParallaxStrength, 0, AvatarParallaxPolicy.MaximumOffsetPixels)
            : 3;
        var parallaxStrength = new Slider { Minimum = 0, Maximum = AvatarParallaxPolicy.MaximumOffsetPixels, Value = savedParallaxStrength, TickFrequency = 0.5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 4, 0, 0) };
        var parallaxStrengthLabel = Label($"Intensidad del parallax: {savedParallaxStrength:0.0} px", 12, "#98A4BE");
        parallaxStrength.ValueChanged += (_, _) => parallaxStrengthLabel.Text = $"Intensidad del parallax: {parallaxStrength.Value:0.0} px";
        panel.Children.Add(parallaxStrengthLabel);
        panel.Children.Add(parallaxStrength);
        panel.Children.Add(Label("El parallax desplaza suavemente las capas importadas; no utiliza cámara ni rastreo facial. «Reducir movimiento» desactiva el efecto. La intensidad máxima está limitada a cinco píxeles.",12,"#98A4BE"));
        panel.Children.Add(ActionButton("Guardar preferencias",(_,_)=>{store.SetSetting("performance.profile",profile.SelectedItem?.ToString()??"Equilibrado");application.SetPerformanceProfile(Enum.Parse<PerformanceProfile>(profile.SelectedItem?.ToString()??"Equilibrado"));store.SetSetting("ui.reduceMotion",reduceMotion.IsChecked==true?"true":"false");store.SetSetting("avatar.parallax.enabled",parallaxMotion.IsChecked==true?"true":"false");store.SetSetting("avatar.parallax.strength",parallaxStrength.Value.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));if(overlay is not null){overlay.ReduceMotion=reduceMotion.IsChecked==true;overlay.SetParallaxEnabled(parallaxMotion.IsChecked==true);overlay.SetParallaxStrength(parallaxStrength.Value);}FooterStatus.Text="Preferencias guardadas en SQLite y aplicadas al monitor de rendimiento/overlay.";store.Log("Ajustes","Preferencias locales actualizadas.");},true));
        var privacy=Panel("Privacidad y almacenamiento");privacy.Margin=new Thickness(0,16,0,0);privacy.Children.Add(Label($"Datos locales: {dataDirectory}",12,"#98A4BE"));privacy.Children.Add(Label("Los assets se copian al directorio local. El token de Twitch se cifra con DPAPI CurrentUser. No se incluyen datos personales en el repositorio ni en los paquetes de CI.",12,"#98A4BE"));privacy.Children.Add(ActionButton("Abrir carpeta de datos",(_,_)=>{try{Process.Start(new ProcessStartInfo(dataDirectory){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message);}}));root.Children.Add(privacy);

        var backupPanel=Panel("Copia de seguridad y restauración de SQLite");backupPanel.Margin=new Thickness(0,16,0,0);
        backupPanel.Children.Add(Label("La copia guarda comandos/alias/permisos, usuarios/puntos, sorteos, minijuegos, reglas, timers, capas del avatar y preferencias. No incluye los archivos de imagen del avatar, cookies de Telegram ni credenciales OAuth cifradas; esos elementos están fuera de SQLite.",12,"#98A4BE"));
        backupPanel.Children.Add(ActionButton("Crear copia de seguridad…",(_,_) =>
        {
            var dialog=new SaveFileDialog
            {
                Title="Crear copia de seguridad de VTuberBot",
                Filter="Copia VTuberBot (*.vtuberbot.db)|*.vtuberbot.db|Base SQLite (*.db)|*.db",
                DefaultExt=".vtuberbot.db",
                AddExtension=true,
                OverwritePrompt=true,
                FileName="VTuberBot-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".vtuberbot.db",
                InitialDirectory=dataDirectory
            };
            if(dialog.ShowDialog(this)!=true)return;
            try
            {
                var path=store.BackupTo(dialog.FileName);
                FooterStatus.Text="Copia SQLite creada y verificada.";
                store.Log("Backup","Copia de seguridad SQLite creada y verificada.");
                MessageBox.Show(this,"La copia se creó y pasó la verificación de integridad:\n"+path,"Copia de seguridad completada",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                FooterStatus.Text="No se pudo crear la copia de seguridad.";
                MessageBox.Show(this,"No se pudo crear/verificar la copia. La base activa no se modificó.\n\n"+ex.Message,"Copia de seguridad fallida",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        },true));
        backupPanel.Children.Add(ActionButton("Restaurar copia…",(_,_) =>
        {
            if(twitchChatDesired||twitchEventSubDesired||twitch.IsConnected||eventSub.IsConnected)
            {
                MessageBox.Show(this,"Antes de restaurar, desconectá Chat Twitch y EventSub para evitar que eventos del directo se escriban durante el cambio de base.","Desconectar Twitch primero",MessageBoxButton.OK,MessageBoxImage.Warning);
                return;
            }
            if(Volatile.Read(ref automationProcessing)!=0)
            {
                MessageBox.Show(this,"Hay un mensaje automático en proceso. Volvé a intentar cuando termine.","Automatización en curso",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }
            var dialog=new OpenFileDialog
            {
                Title="Seleccionar copia de VTuberBot para restaurar",
                Filter="Copia VTuberBot (*.vtuberbot.db;*.db)|*.vtuberbot.db;*.db|Todos los archivos (*.*)|*.*",
                CheckFileExists=true,
                Multiselect=false
            };
            if(dialog.ShowDialog(this)!=true)return;
            var confirm=MessageBox.Show(this,
                "Esto reemplazará los datos de SQLite activos por los de esta copia:\n"+dialog.FileName+
                "\n\nSe creará primero una copia de emergencia de la base actual. Solo se aceptan bases íntegras y compatibles. ¿Continuar?",
                "Confirmar restauración",MessageBoxButton.YesNo,MessageBoxImage.Warning);
            if(confirm!=MessageBoxResult.Yes)return;
            automationTimer.Stop();
            try
            {
                var emergency=store.RestoreFromBackup(dialog.FileName);
                automationLastRun.Clear();
                automationOfflineLog.Clear();
                automaticModerationCooldown.Clear();
                lastPointAward.Clear();
                store.Log("Backup","Base SQLite restaurada desde copia verificada; pre-restauración en "+emergency);
                Navigate(currentPage);
                FooterStatus.Text="Base restaurada. La copia anterior se conservó por seguridad.";
                MessageBox.Show(this,"Restauración completada. La base anterior se guardó automáticamente en:\n"+emergency+
                    "\n\nSi faltan imágenes del avatar, restaurá también la carpeta de assets local correspondiente.",
                    "Restauración completada",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                FooterStatus.Text="Restauración no completada.";
                MessageBox.Show(this,"No se completó la restauración. La base activa se conserva o se ha revertido automáticamente.\n\n"+ex.Message,
                    "Restauración fallida",MessageBoxButton.OK,MessageBoxImage.Error);
            }
            finally
            {
                automationTimer.Start();
            }
        }));
        backupPanel.Children.Add(Label("Restaurar solo modifica SQLite: mantené copias separadas de assets, y volvé a autorizar/abrir servicios si la copia contiene ajustes antiguos.",12,"#98A4BE"));
        root.Children.Add(backupPanel);
        return root;
    }
}
