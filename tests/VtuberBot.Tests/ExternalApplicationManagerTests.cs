using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ExternalApplicationManagerTests
{
    [Fact]
    public void EmptyPathIsNotConfigured()
    {
        var manager = new ExternalApplicationManager(new FakeRuntime());
        var status = manager.GetStatus("  ");
        Assert.Equal(ExternalApplicationState.NotConfigured, status.State);
    }

    [Fact]
    public void MissingExecutableIsNotFoundAndIsNotStarted()
    {
        var runtime = new FakeRuntime();
        var manager = new ExternalApplicationManager(runtime);
        var status = manager.Launch(@"C:\Apps\Streamer.bot.exe");
        Assert.Equal(ExternalApplicationState.NotFound, status.State);
        Assert.Equal(0, runtime.StartCount);
    }

    [Fact]
    public void ExistingIdleExecutableIsConfigured()
    {
        var runtime = new FakeRuntime { ExistingPath = @"C:\Apps\Streamer.bot.exe" };
        var status = new ExternalApplicationManager(runtime).GetStatus(runtime.ExistingPath);
        Assert.Equal(ExternalApplicationState.Configured, status.State);
        Assert.False(status.IsRunning);
    }

    [Fact]
    public void RunningProcessIsReportedWithoutClaimingConnection()
    {
        var runtime = new FakeRuntime
        {
            ExistingPath = @"C:\Apps\PNGTuberPlus.exe",
            RunningPath = @"C:\Apps\PNGTuberPlus.exe"
        };
        var status = new ExternalApplicationManager(runtime).GetStatus(runtime.ExistingPath);
        Assert.Equal(ExternalApplicationState.Running, status.State);
        Assert.True(status.IsRunning);
        Assert.Contains("no está verificada", status.Message);
    }

    [Fact]
    public void StartFailureIsReturnedAsRealError()
    {
        var runtime = new FakeRuntime
        {
            ExistingPath = @"C:\Apps\Streamer.bot.exe",
            StartException = new InvalidOperationException("access denied by test")
        };
        var status = new ExternalApplicationManager(runtime).Launch(runtime.ExistingPath);
        Assert.Equal(ExternalApplicationState.Error, status.State);
        Assert.Contains("access denied by test", status.Message);
    }

    [Fact]
    public void SuccessfulLaunchReportsStartedButNotConnected()
    {
        var runtime = new FakeRuntime { ExistingPath = @"C:\Apps\Streamer.bot.exe" };
        var status = new ExternalApplicationManager(runtime).Launch(runtime.ExistingPath);
        Assert.Equal(ExternalApplicationState.Started, status.State);
        Assert.Equal(1, runtime.StartCount);
        Assert.Contains("no confirma una conexión", status.Message);
    }

    private sealed class FakeRuntime : IExternalApplicationRuntime
    {
        public string ExistingPath { get; init; } = string.Empty;
        public string RunningPath { get; init; } = string.Empty;
        public Exception? StartException { get; init; }
        public int StartCount { get; private set; }

        public bool FileExists(string executablePath) =>
            string.Equals(ExistingPath, executablePath, StringComparison.OrdinalIgnoreCase);

        public bool IsRunning(string executablePath) =>
            string.Equals(RunningPath, executablePath, StringComparison.OrdinalIgnoreCase);

        public void Start(string executablePath)
        {
            StartCount++;
            if (StartException is not null)
                throw StartException;
        }
    }
}
