using System.IO;
namespace VtuberBot.Core;

public sealed class VtuberApplication
{
    public const string InternalTestEvent = "internal.test";
    public const string CommandExecutedEvent = "command.executed";
    public const string HealthUpdatedEvent = "health.updated";
    public const string Version = "0.2.3";

    private readonly string? configPath;
    private AppState state = AppState.Stopped;
    private Logger? logger;
    private ResourceManager? resources;
    private SqliteDatabase? database;
    private EventManager? events;
    private TaskScheduler? scheduler;
    private AppConfig? config;
    private CommandRouter? commandRouter;
    private PerformanceMonitor? performanceMonitor;
    private HealthSnapshot? latestHealth;
    private int processed;

    public VtuberApplication(string? configPath = null) =>
        this.configPath = configPath;

    public AppState State => state;
    public AppConfig Config => config ?? throw new InvalidOperationException();
    public SqliteDatabase Database => database ?? throw new InvalidOperationException();
    public EventManager EventManager => events ?? throw new InvalidOperationException();
    public TaskScheduler TaskScheduler => scheduler ?? throw new InvalidOperationException();
    public ResourceManager ResourceManager => resources ?? throw new InvalidOperationException();
    public CommandRouter CommandRouter =>
        commandRouter ?? throw new InvalidOperationException();
    public PerformanceMonitor PerformanceMonitor =>
        performanceMonitor ?? throw new InvalidOperationException();

    public HealthSnapshot? LatestHealth =>
        Volatile.Read(ref latestHealth);

    public PerformanceProfile PerformanceProfile =>
        performanceMonitor?.Profile ?? PerformanceProfile.Equilibrado;

    public int InternalTestEventsProcessed => processed;

    public void Initialize()
    {
        if (state != AppState.Stopped)
            throw new InvalidOperationException();

        state = AppState.Starting;

        try
        {
            config = AppConfig.Load(configPath);
            logger = new(config.LogDirectory, config.LogLevel);
            resources = new(logger);
            resources.Register("logging", logger.Dispose);

            var databaseDirectory = Path.GetDirectoryName(config.DatabasePath);
            if (!string.IsNullOrWhiteSpace(databaseDirectory))
                Directory.CreateDirectory(databaseDirectory);

            database = new(config.DatabasePath);
            database.Open();
            resources.Register("database", database.Close);
            database.InitializeSchema();

            events = new(logger);
            scheduler = new(logger);
            resources.Register("scheduler", scheduler.Shutdown);

            commandRouter = new(logger);
            RegisterCommands();

            performanceMonitor = new(
                new CurrentProcessPerformanceProbe(),
                new NullGpuPerformanceProbe(),
                new NullFpsSource(),
                new SystemHealthClock(),
                new PeriodicTimerFactory(),
                logger);
            performanceMonitor.SnapshotPublished += PublishHealthSnapshot;
            resources.Register("performance-monitor", performanceMonitor.Dispose);

            events.Subscribe(InternalTestEvent, _ =>
            {
                processed++;
                logger.Info("Internal test event processed");
            });
            events.Emit(new(InternalTestEvent));

            state = AppState.Running;
            performanceMonitor.Start();
        }
        catch (Exception exception)
        {
            state = AppState.Error;
            logger?.Error(exception, "Application initialization failed.");
            resources?.CleanupAll();
            throw new ApplicationException("Application initialization failed.", exception);
        }
    }

    public CommandResult ExecuteCommand(string input)
    {
        if (state != AppState.Running)
            return CommandResult.Failed("Application is not running.");

        var result = CommandRouter.Execute(input);

        EventManager.Emit(new AppEvent(
            CommandExecutedEvent,
            new
            {
                Input = input,
                result.Success,
                result.Message
            }));

        return result;
    }

    public void SetPerformanceProfile(PerformanceProfile newProfile)
    {
        PerformanceMonitor.SetProfile(newProfile);
    }

    public void Shutdown()
    {
        if (state == AppState.Stopped)
            return;

        state = AppState.Stopping;

        try
        {
            resources?.CleanupAll();
        }
        finally
        {
            state = AppState.Stopped;
            database = null;
            events = null;
            scheduler = null;
            resources = null;
            commandRouter = null;
            performanceMonitor = null;
            Volatile.Write(ref latestHealth, null);
            config = null;
            logger = null;
        }
    }

    private void PublishHealthSnapshot(HealthSnapshot snapshot)
    {
        Volatile.Write(ref latestHealth, snapshot);
        events?.Emit(new AppEvent(HealthUpdatedEvent, snapshot));
    }

    private void RegisterCommands()
    {
        var router = commandRouter!;

        router.Register(new(
            "help",
            "List available commands.",
            _ => CommandResult.Succeeded(router.GetHelpText())));

        router.Register(new(
            "ping",
            "Check that the command core is responding.",
            _ => CommandResult.Succeeded("pong")));

        router.Register(new(
            "status",
            "Show the current application status.",
            _ => CommandResult.Succeeded(
                $"Name: {Config.Name}{Environment.NewLine}" +
                $"Version: {Version}{Environment.NewLine}" +
                $"State: {State}")));
    }
}
