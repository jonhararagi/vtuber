using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsLaunchArgumentsPolicyTests
{
    [Fact]
    public void DefaultIntegratedLaunchMinimizesToTrayWithoutDisablingPlugins() =>
        Assert.Equal("--minimize-to-tray", ObsLaunchArgumentsPolicy.BuildArguments(false, true));

    [Fact]
    public void LightModeCanBeCombinedWithTrayStartup() =>
        Assert.Equal("--only-bundled-plugins --minimize-to-tray",
            ObsLaunchArgumentsPolicy.BuildArguments(true, true));

    [Fact]
    public void VisibleStartupCanKeepThirdPartyPlugins() =>
        Assert.Equal(string.Empty, ObsLaunchArgumentsPolicy.BuildArguments(false, false));

    [Fact]
    public void LightModeWithoutTrayOptionDoesNotForceMinimize() =>
        Assert.Equal("--only-bundled-plugins", ObsLaunchArgumentsPolicy.BuildArguments(true, false));
}
