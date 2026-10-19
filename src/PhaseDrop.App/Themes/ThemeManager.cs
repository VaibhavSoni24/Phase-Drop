using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PhaseDrop.Infrastructure.Configuration;

namespace PhaseDrop.App.Themes;

public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static event Action? ThemeChanged;

    public static void Initialize(UserSettings settings)
    {
        ApplyTheme(settings.Theme);

        // Listen for Windows system theme changes
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (settings.Theme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTheme("System");
            }
        };
    }

    public static void ApplyTheme(string themePreference)
    {
        if (themePreference.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            IsDark = true;
        }
        else if (themePreference.Equals("Light", StringComparison.OrdinalIgnoreCase))
        {
            IsDark = false;
        }
        else
        {
            // System default: query Windows registry
            IsDark = GetWindowsSystemIsDark();
        }

        UpdateApplicationResources();
        ThemeChanged?.Invoke();
    }

    private static bool GetWindowsSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
                return intVal == 0;
        }
        catch { }

        return true; // Default to dark on Windows 11
    }

    private static void UpdateApplicationResources()
    {
        var app = Application.Current;
        if (app == null) return;

        var resources = app.Resources;

        if (IsDark)
        {
            resources["BgPrimary"] = new SolidColorBrush(Color.FromRgb(28, 32, 38));
            resources["BgSecondary"] = new SolidColorBrush(Color.FromRgb(36, 41, 48));
            resources["BgElevated"] = new SolidColorBrush(Color.FromRgb(45, 51, 60));
            resources["BorderPrimary"] = new SolidColorBrush(Color.FromRgb(60, 68, 79));
            resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(240, 244, 248));
            resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(155, 167, 180));
            resources["AccentPrimary"] = new SolidColorBrush(Color.FromRgb(0, 132, 255));
            resources["AccentHover"] = new SolidColorBrush(Color.FromRgb(30, 149, 255));
            resources["PetalBg"] = new SolidColorBrush(Color.FromRgb(38, 43, 51));
            resources["PetalBorder"] = new SolidColorBrush(Color.FromRgb(55, 62, 73));
            resources["PetalHoverBg"] = new SolidColorBrush(Color.FromRgb(0, 122, 237));
            resources["PetalHoverText"] = new SolidColorBrush(Colors.White);
        }
        else
        {
            resources["BgPrimary"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            resources["BgSecondary"] = new SolidColorBrush(Color.FromRgb(246, 248, 250));
            resources["BgElevated"] = new SolidColorBrush(Color.FromRgb(240, 243, 246));
            resources["BorderPrimary"] = new SolidColorBrush(Color.FromRgb(208, 215, 222));
            resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(31, 35, 40));
            resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(101, 109, 118));
            resources["AccentPrimary"] = new SolidColorBrush(Color.FromRgb(0, 114, 224));
            resources["AccentHover"] = new SolidColorBrush(Color.FromRgb(0, 98, 196));
            resources["PetalBg"] = new SolidColorBrush(Color.FromRgb(243, 246, 249));
            resources["PetalBorder"] = new SolidColorBrush(Color.FromRgb(216, 222, 228));
            resources["PetalHoverBg"] = new SolidColorBrush(Color.FromRgb(0, 114, 224));
            resources["PetalHoverText"] = new SolidColorBrush(Colors.White);
        }
    }
}
