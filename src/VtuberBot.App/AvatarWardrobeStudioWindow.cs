using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace VtuberBot.App;

/// <summary>
/// Hosts the open-source Character Studio editor inside VTuberBot instead of launching a separate browser.
/// The editor itself is currently served by its upstream project and therefore requires an internet connection.
/// </summary>
public sealed class AvatarWardrobeStudioWindow : Window
{
    private const string StudioUrl = "https://studio.m3org.com/";
    private readonly WebView2 browser = new();
    private readonly TextBlock status = new();

    public AvatarWardrobeStudioWindow()
    {
        Title = "VTuberBot · Mini estudio 3D";
        Width = 1420;
        Height = 920;
        MinWidth = 900;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(11, 14, 23));
        Foreground = Brushes.White;

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(18, 12, 18, 12)
        };
        var retry = new Button
        {
            Content = "Recargar estudio",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        retry.Click += (_, _) => NavigateToStudio();
        DockPanel.SetDock(retry, Dock.Right);
        header.Children.Add(retry);

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Mini estudio 3D de personaje",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        status.Text = "Conectando con Character Studio…";
        status.Margin = new Thickness(0, 3, 0, 0);
        status.Foreground = new SolidColorBrush(Color.FromRgb(152, 164, 190));
        status.TextWrapping = TextWrapping.Wrap;
        heading.Children.Add(status);
        header.Children.Add(heading);

        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid.SetRow(browser, 1);
        layout.Children.Add(browser);

        Content = layout;
        Loaded += async (_, _) =>
        {
            try
            {
                await browser.EnsureCoreWebView2Async();
                browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                browser.NavigationCompleted += (_, args) =>
                {
                    status.Text = args.IsSuccess
                        ? "Editor integrado cargado. Importá tu VRM y probá los recursos disponibles. Requiere conexión a Internet."
                        : "No se pudo cargar el editor. Comprobá la conexión y pulsá «Recargar estudio».";
                };
                NavigateToStudio();
            }
            catch (Exception exception)
            {
                status.Text = "WebView2 no está disponible: " + exception.GetBaseException().Message
                    + " Instala o repara Microsoft Edge WebView2 Runtime e inténtalo de nuevo.";
            }
        };
    }

    private void NavigateToStudio()
    {
        if (browser.CoreWebView2 is null)
        {
            status.Text = "El navegador integrado todavía se está inicializando.";
            return;
        }

        status.Text = "Cargando Character Studio…";
        browser.CoreWebView2.Navigate(StudioUrl);
    }
}
