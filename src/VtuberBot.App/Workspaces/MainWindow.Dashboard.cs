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
    private UIElement BuildDashboard()
    {
        var root = new StackPanel();
        var hero = new Border { Background = Brush("#171D30"), BorderBrush = Brush("#293650"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(24), Margin = new Thickness(0,0,0,18) };
        var heroGrid = new Grid(); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) }); heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label("Tu stream, bajo control.", 28, "#F8FAFC", true));
        text.Children.Add(Label("Configurá el avatar y probá la lógica local antes de conectar servicios externos.", 14, "#A8B3CC"));
        var buttons = new WrapPanel { Margin = new Thickness(0,18,0,0) };
        buttons.Children.Add(ActionButton("Abrir Avatar Studio", (_,_)=>Navigate("Avatar Studio"), true));
        buttons.Children.Add(ActionButton("Probar comandos", (_,_)=>Navigate("Comandos")));
        text.Children.Add(buttons); Grid.SetColumn(text,0); heroGrid.Children.Add(text);
        var preview = new Border { Background = Brush("#101525"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Margin = new Thickness(18,0,0,0), MinHeight = 220 };
        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            try { var image = new Image { Source = LoadImage(avatarPath), Stretch = Stretch.Uniform, MaxHeight = 240 }; preview.Child = image; }
            catch { preview.Child = Label("No se pudo cargar el avatar. Volvé a importarlo.",13,"#FCA5A5"); }
        }
        else
        {
            var empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(Label("✧",48,"#69D8F2",true)); empty.Children.Add(Label("Tu avatar va acá",17,"#F8FAFC",true));
            empty.Children.Add(Label("Importá un PNG en Avatar Studio",12,"#98A4BE")); preview.Child = empty;
        }
        Grid.SetColumn(preview,1); heroGrid.Children.Add(preview); hero.Child=heroGrid; root.Children.Add(hero);
        var cards = new UniformGrid { Columns = 4, Margin = new Thickness(0,0,0,18) };
        cards.Children.Add(MetricCard("AVATAR", string.IsNullOrWhiteSpace(avatarPath) ? "Sin importar" : "Configurado", "PNG local"));
        cards.Children.Add(MetricCard("OBS", obs.IsConnected ? "Conectado" : "Desconectado", "WebSocket v5"));
        cards.Children.Add(MetricCard("COMANDOS LOCALES", store.GetCommands().Count.ToString(), "Acciones configuradas"));
        var selectedVoiceId = store.GetSetting("audio.tts.voiceId");
        cards.Children.Add(MetricCard("VOZ LOCAL", VoicevoxSpeechSession.IsVoiceId(selectedVoiceId) ? "VOICEVOX elegido" : "Sin VOICEVOX elegido", "Motor debe estar disponible"));
        root.Children.Add(cards);

        var performancePanel = Panel("Rendimiento en vivo");
        var performanceGrid = new UniformGrid { Columns = 2 };
        var cpuCard = new Border
        {
            Background = Brush("#101525"), BorderBrush = Brush("#293650"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16), Margin = new Thickness(0, 0, 10, 0)
        };
        var cpuContent = new StackPanel();
        cpuContent.Children.Add(Label("CPU DEL PROCESO", 10, "#98A4BE", true));
        dashboardCpuValue = Label("Tomando muestra…", 24, "#F8FAFC", true);
        dashboardCpuBar = new ProgressBar
        {
            Minimum = 0, Maximum = 100, Height = 8, Value = 0,
            Background = Brush("#252E46"), Foreground = Brush("#69D8F2"),
            BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 8)
        };
        cpuContent.Children.Add(dashboardCpuValue);
        cpuContent.Children.Add(dashboardCpuBar);
        cpuContent.Children.Add(Label("Uso de CPU de VTuberBot, normalizado sobre los núcleos disponibles.", 11, "#98A4BE"));
        cpuCard.Child = cpuContent;

        var ramCard = new Border
        {
            Background = Brush("#101525"), BorderBrush = Brush("#293650"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };
        var ramContent = new StackPanel();
        ramContent.Children.Add(Label("MEMORIA DEL PROCESO", 10, "#98A4BE", true));
        dashboardRamValue = Label("Tomando muestra…", 24, "#F8FAFC", true);
        dashboardRamBar = new ProgressBar
        {
            Minimum = 0, Maximum = 100, Height = 8, Value = 0,
            Background = Brush("#252E46"), Foreground = Brush("#B9A2FF"),
            BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 8)
        };
        ramContent.Children.Add(dashboardRamValue);
        ramContent.Children.Add(dashboardRamBar);
        dashboardRamWarningLabel = Label("Objetivo: aviso anticipado desde 800 MiB y alerta desde 1 GiB.", 11, "#98A4BE");
        ramContent.Children.Add(dashboardRamWarningLabel);
        ramContent.Children.Add(Label("RAM residente del proceso; el porcentaje usa el límite disponible reportado por .NET. OBS y VOICEVOX se ejecutan como procesos separados.", 11, "#98A4BE"));
        ramCard.Child = ramContent;

        performanceGrid.Children.Add(cpuCard);
        performanceGrid.Children.Add(ramCard);
        performancePanel.Children.Add(performanceGrid);
        root.Children.Add(performancePanel);

        var lower = new Grid(); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) }); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        var quick = Panel("Accesos rápidos"); var quickWrap = new WrapPanel();
        foreach(var item in new[] {("Avatar Studio","Avatar Studio"),("Voz y audio","Voz y audio"),("OBS","OBS"),("Diagnósticos","Actividad y diagnóstico")})
            quickWrap.Children.Add(ActionButton(item.Item1,(_,_)=>Navigate(item.Item2)));
        quick.Children.Add(quickWrap); Grid.SetColumn(quick,0); lower.Children.Add(quick);
        var health = Panel("Estado real de módulos");
        health.Children.Add(StatusRow("Núcleo y SQLite","Disponible",true));
        health.Children.Add(StatusRow("Avatar","",!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath)));
        health.Children.Add(StatusRow("Micrófono",microphone is null?"No iniciado":"Capturando",microphone is not null));
        health.Children.Add(StatusRow("OBS",obs.IsConnected?"Autenticado":"Requiere configuración",obs.IsConnected));
        Grid.SetColumn(health,1); lower.Children.Add(health); root.Children.Add(lower);
        root.Children.Add(Label("Los módulos locales funcionan sin cuentas externas. Una conexión externa nunca se marca como activa solo por guardar sus datos.",12,"#7F8AA5"));
        return root;
    }
    private void StartDashboardMetrics()
    {
        dashboardMetricsTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        dashboardMetricsTimer.Tick -= DashboardMetricsTimer_Tick;
        dashboardMetricsTimer.Tick += DashboardMetricsTimer_Tick;
        UpdateDashboardMetrics();
        dashboardMetricsTimer.Start();
    }

    private void DashboardMetricsTimer_Tick(object? sender, EventArgs e) => UpdateDashboardMetrics();

    private void UpdateDashboardMetrics()
    {
        if (currentPage != "Inicio" || dashboardCpuValue is null || dashboardCpuBar is null ||
            dashboardRamValue is null || dashboardRamBar is null || dashboardRamWarningLabel is null)
            return;

        var health = application.LatestHealth;
        if (health?.CpuPercent is double cpu)
        {
            dashboardCpuValue.Text = $"{cpu:0.0}%";
            dashboardCpuBar.Value = Math.Clamp(cpu, 0, 100);
        }
        else
        {
            dashboardCpuValue.Text = "Midiendo…";
            dashboardCpuBar.Value = 0;
        }

        if (health?.WorkingSetBytes is long residentBytes)
        {
            var residentMegabytes = residentBytes / (1024d * 1024d);
            var memoryWarning = ProcessMemoryWarningPolicy.Evaluate(residentBytes);
            (dashboardRamWarningLabel.Text, dashboardRamWarningLabel.Foreground) = memoryWarning switch
            {
                ProcessMemoryWarningLevel.ApproachingLimit => ("Aviso: VTuberBot se acerca a 1 GiB de RAM (desde 800 MiB).", Brush("#E7B56A")),
                ProcessMemoryWarningLevel.LimitReached => ("ALERTA: VTuberBot alcanzó o superó 1 GiB. Es un umbral de aviso, no un límite duro.", Brush("#F2A6A6")),
                _ => ("Sin advertencia de memoria para el proceso VTuberBot. OBS y VOICEVOX son procesos separados.", Brush("#98A4BE"))
            };
            var availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (availableBytes > 0)
            {
                var percentage = Math.Clamp(residentBytes / (double)availableBytes * 100, 0, 100);
                dashboardRamValue.Text = $"{residentMegabytes:N0} MiB · {percentage:0.0}%";
                dashboardRamBar.Value = percentage;
            }
            else
            {
                dashboardRamValue.Text = $"{residentMegabytes:N0} MiB";
                dashboardRamBar.Value = 0;
            }
        }
        else
        {
            dashboardRamValue.Text = "No disponible";
            dashboardRamBar.Value = 0;
            dashboardRamWarningLabel.Text = "No se pudo medir la memoria del proceso; no se infiere que esté dentro del umbral.";
            dashboardRamWarningLabel.Foreground = Brush("#E7B56A");
        }
    }
}
