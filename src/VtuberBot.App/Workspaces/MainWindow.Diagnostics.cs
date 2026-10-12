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
    private UIElement BuildActivityPage()
    {
        var root=new StackPanel();var panel=Panel("Actividad reciente");root.Children.Add(panel);
        var list=new ListBox{Height=470,ItemsSource=store.GetActivity(250).Select(a=>$"{a.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}  [{a.Module}]  {a.Message}").ToList()};panel.Children.Add(list);
        var row=new WrapPanel();row.Children.Add(ActionButton("Actualizar registro",(_,_)=>Navigate("Actividad y diagnóstico")));
        row.Children.Add(ActionButton("Exportar diagnóstico",(_,_)=>ExportDiagnostics(),true));panel.Children.Add(row);
        panel.Children.Add(Label("El diagnóstico exportado incluye eventos de la app y configuración no secreta; excluye tokens OAuth, cookies de WebView2 y datos binarios de usuario.",12,"#98A4BE"));return root;
    }

    private void ExportDiagnostics()
    {
        var dialog=new SaveFileDialog{Filter="Texto|*.txt",FileName="vtuberbot-diagnostic.txt"};if(dialog.ShowDialog(this)!=true)return;
        var sb=new StringBuilder();sb.AppendLine("VTuberBot — diagnóstico local");sb.AppendLine($"Fecha UTC: {DateTimeOffset.UtcNow:O}");sb.AppendLine($"Versión core: {VtuberApplication.Version}");sb.AppendLine($"Estado core: {application.State}");sb.AppendLine($"SQLite: {application.Database.Path}");sb.AppendLine($"Avatar configurado: {!string.IsNullOrWhiteSpace(avatarPath)&&File.Exists(avatarPath)}");sb.AppendLine($"Twitch conectado: {twitch.IsConnected}");sb.AppendLine($"Dispositivos de entrada: {WaveIn.DeviceCount}");sb.AppendLine("No se exportan tokens, cookies ni contraseñas.");sb.AppendLine();foreach(var a in store.GetActivity(250))sb.AppendLine($"{a.At:O}\t{a.Module}\t{a.Message}");
        File.WriteAllText(dialog.FileName,sb.ToString(),new UTF8Encoding(true));store.Log("Diagnóstico","Informe de diagnóstico exportado.");
    }
}
