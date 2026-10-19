using System.Text;
using PhaseDrop.Platform.Windows.Native;

namespace PhaseDrop.Platform.Windows.Shell;

public static class ShellWindowDetector
{
    private static readonly HashSet<string> ShellClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CabinetWClass", // Modern File Explorer Window
        "ExploreWClass", // Classic Explorer Window
        "Progman",       // Windows Desktop Program Manager
        "WorkerW"        // Windows Desktop Worker Window
    };

    public static bool IsShellWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        var sb = new StringBuilder(256);
        int length = Win32.GetClassName(hWnd, sb, sb.Capacity);
        if (length <= 0)
            return false;

        string className = sb.ToString();
        return ShellClassNames.Contains(className);
    }

    public static bool IsForegroundShellWindow()
    {
        IntPtr fg = Win32.GetForegroundWindow();
        return IsShellWindow(fg);
    }
}
