using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace VtuberBot.App;

/// <summary>
/// A compact, separate window that hosts the official ChatGPT website.
/// It deliberately does not access private page content or automate the user's account.
/// </summary>
internal sealed class ChatGptMiniWindow : Window
{
    private readonly WebView2 browser = new();
    private readonly TextBlock statusLabel = new()
    {
        Text = "Preparando ChatGPT…",
        Foreground = new SolidColorBrush(Color.FromRgb(152, 164, 190)),
        Margin = new Thickness(10, 5, 10, 8),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly string userDataFolder;
    private bool initializationStarted;

    public ChatGptMiniWindow(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        userDataFolder = Path.Combine(Path.GetFullPath(dataDirectory), "chatgpt-webview");

        Title = "ChatGPT · VTuberBot";
        Width = 820;
        Height = 900;
        MinWidth = 520;
        MinHeight = 500;
        Background = new SolidColorBrush(Color.FromRgb(16, 20, 35));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(6, 6, 6, 2)
        };

        var back = MakeButton("← Atrás");
        back.Click += (_, _) =>
        {
            if (browser.CoreWebView2?.CanGoBack == true)
                browser.GoBack();
        };
        var forward = MakeButton("Adelante →");
        forward.Click += (_, _) =>
        {
            if (browser.CoreWebView2?.CanGoForward == true)
                browser.GoForward();
        };
        var reload = MakeButton("Recargar");
        reload.Click += (_, _) => browser.CoreWebView2?.Reload();
        var external = MakeButton("Abrir navegador normal");
        external.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://chatgpt.com/") { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                statusLabel.Text = "No se pudo abrir el navegador externo: " + exception.Message;
            }
        };

        toolbar.Children.Add(back);
        toolbar.Children.Add(forward);
        toolbar.Children.Add(reload);
        toolbar.Children.Add(external);

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(statusLabel, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(statusLabel);
        root.Children.Add(browser);
        Content = root;

        Loaded += async (_, _) => await InitializeBrowserAsync();
    }

    private static Button MakeButton(string label) => new()
    {
        Content = label,
        Padding = new Thickness(10, 6, 10, 6),
        Margin = new Thickness(3),
        Background = new SolidColorBrush(Color.FromRgb(31, 42, 62)),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(58, 75, 103)),
        BorderThickness = new Thickness(1),
        Cursor = Cursors.Hand
    };

    private async Task InitializeBrowserAsync()
    {
        if (initializationStarted)
            return;

        initializationStarted = true;
        statusLabel.Text = "Cargando ChatGPT web…";
        try
        {
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.NavigationStarting += (_, _) =>
                statusLabel.Text = "Conectando con chatgpt.com…";
            browser.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                statusLabel.Text = args.IsSuccess
                    ? "ChatGPT web listo. Iniciá sesión si te lo solicita. La conversación queda en el sitio; VTuberBot no la lee."
                    : "No se pudo cargar la página (" + args.WebErrorStatus + "). Probá «Abrir navegador normal».";
            };
            browser.CoreWebView2.Navigate("https://chatgpt.com/");
        }
        catch (Exception exception)
        {
            statusLabel.Text = "WebView2 no pudo iniciar ChatGPT: " + exception.Message +
                " Usá «Abrir navegador normal» como alternativa.";
        }
    }
}
