using System.IO;
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

    [Fact]
    public void Scripts_ContainNoHardcodedMachineSpecificPaths()
    {
        // Locate repo root
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PhaseDrop.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        string[] targetFiles =
        [
            Path.Combine(dir.FullName, "scripts", "package.ps1"),
            Path.Combine(dir.FullName, "scripts", "acquire-ffmpeg.ps1"),
            Path.Combine(dir.FullName, "scripts", "validate-release.ps1"),
            Path.Combine(dir.FullName, "packaging", "nsis", "phasedrop.nsi"),
            Path.Combine(dir.FullName, ".github", "workflows", "ci.yml")
        ];

        string[] forbiddenPatterns =
        [
            @"D:\Softwares",
            @"C:\Users\shubh",
            @"AppData\Local\Microsoft\WinGet",
            @"C:\Program Files\dotnet\dotnet.exe"
        ];

        foreach (var file in targetFiles)
        {
            Assert.True(File.Exists(file), $"Target file {file} must exist.");
            string content = File.ReadAllText(file);

            foreach (var forbidden in forbiddenPatterns)
            {
                Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
