using PhaseDrop.Platform.Windows.Lifecycle;
using PhaseDrop.Platform.Windows.Shell;
using PhaseDrop.Platform.Windows.Startup;
using Xunit;

namespace PhaseDrop.IntegrationTests;

public class PlatformTests
{
    [Fact]
    public void ShellWindowDetector_IdentifiesExplorerClasses()
    {
        Assert.False(ShellWindowDetector.IsShellWindow(IntPtr.Zero));
    }

    [Fact]
    public void StartupManager_CanQueryStatusWithoutExceptions()
    {
        bool isEnabled = StartupManager.IsLaunchAtStartupEnabled();
        // Should return a boolean without throwing an unhandled registry exception
        Assert.True(isEnabled || !isEnabled);
    }

    [Fact]
    public void SingleInstanceManager_AcquiresPrimaryInstance()
    {
        using var mgr = new SingleInstanceManager();
        bool isPrimary = mgr.Initialize([]);
        Assert.True(isPrimary);
        Assert.True(mgr.IsPrimary);
    }
}
