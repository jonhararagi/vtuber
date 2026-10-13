using System.Linq;
using System.Windows;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class App : Application
{
    private VtuberApplication? vtuberApplication;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The local segmentation worker reuses this executable in a separate, memory-limited
        // process. Do not initialize the Twitch/OBS services or create the main window in worker mode.
        if (e.Args.Length > 0 &&
            string.Equals(e.Args[0], "--avatar-segmentation-worker", StringComparison.Ordinal))
        {
            var exitCode = LocalAvatarSegmentationWorker.Run(e.Args.Skip(1).ToArray());
            Shutdown(exitCode);
            return;
        }

        try
        {
            vtuberApplication = new VtuberApplication();
            vtuberApplication.Initialize();
            var window = new MainWindow(vtuberApplication);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            try
            {
                Console.Error.WriteLine("VTuberBot startup failed:");
                Console.Error.WriteLine(exception);
            }
            catch
            {
                // A windowed release does not always have an attached console.
            }

            var cause = exception.GetBaseException();
            MessageBox.Show(cause.Message, "VTuber Bot");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        vtuberApplication?.Shutdown();
        base.OnExit(e);
    }
}
