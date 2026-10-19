using PhaseDrop.Platform.Windows.Native;
using WpfPoint = System.Windows.Point;

namespace PhaseDrop.Platform.Windows.Display;

public static class MonitorHelper
{
    public static WpfPoint ClampWindowToScreen(WpfPoint centerPoint, double windowWidthDip, double windowHeightDip, double dpiScale)
    {
        var pt = new Win32.POINT
        {
            X = (int)(centerPoint.X * dpiScale),
            Y = (int)(centerPoint.Y * dpiScale)
        };

        IntPtr hMonitor = Win32.MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
        var mi = new Win32.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));

        if (!Win32.GetMonitorInfo(hMonitor, ref mi))
        {
            return new WpfPoint(centerPoint.X - (windowWidthDip / 2), centerPoint.Y - (windowHeightDip / 2));
        }

        double workLeft = mi.rcWork.Left / dpiScale;
        double workTop = mi.rcWork.Top / dpiScale;
        double workRight = mi.rcWork.Right / dpiScale;
        double workBottom = mi.rcWork.Bottom / dpiScale;

        double targetLeft = centerPoint.X - (windowWidthDip / 2);
        double targetTop = centerPoint.Y - (windowHeightDip / 2);

        // Clamp
        if (targetLeft < workLeft)
            targetLeft = workLeft + 8;
        else if (targetLeft + windowWidthDip > workRight)
            targetLeft = workRight - windowWidthDip - 8;

        if (targetTop < workTop)
            targetTop = workTop + 8;
        else if (targetTop + windowHeightDip > workBottom)
            targetTop = workBottom - windowHeightDip - 8;

        return new WpfPoint(targetLeft, targetTop);
    }
}
