using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PhaseDrop.App.Themes;
using PhaseDrop.Infrastructure.Configuration;
using PhaseDrop.Platform.Windows.Startup;

namespace PhaseDrop.App.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private bool _isLoaded;

    public event Action<UserSettings>? SettingsSaved;

    public SettingsWindow(UserSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Theme
        if (_settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            ThemeDarkRadio.IsChecked = true;
        else if (_settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            ThemeLightRadio.IsChecked = true;
        else
            ThemeSystemRadio.IsChecked = true;

        // Output Directory
        if (string.IsNullOrWhiteSpace(_settings.CustomOutputDirectory))
        {
            OutputSourceFolderRadio.IsChecked = true;
            CustomFolderBox.Text = "(Source folder)";
            BrowseFolderBtn.IsEnabled = false;
        }
        else
        {
            OutputCustomFolderRadio.IsChecked = true;
            CustomFolderBox.Text = _settings.CustomOutputDirectory;
            BrowseFolderBtn.IsEnabled = true;
        }

        // Concurrency
        ConcurrencySlider.Value = Math.Clamp(_settings.MaxConcurrency, 1, 8);
        ConcurrencyValueText.Text = ConcurrencySlider.Value.ToString();

        // Drag Threshold
        DragThresholdSlider.Value = Math.Clamp(_settings.DragHorizontalThreshold, 2, 16);
        DragThresholdText.Text = $"{DragThresholdSlider.Value} px";

        // Startup
        StartupCheck.IsChecked = StartupManager.IsLaunchAtStartupEnabled();

        // Notifications
        NotifyCheck.IsChecked = _settings.ShowNotifications;

        _isLoaded = true;
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;

        string theme = "System";
        if (ThemeDarkRadio.IsChecked == true) theme = "Dark";
        else if (ThemeLightRadio.IsChecked == true) theme = "Light";

        _settings.Theme = theme;
        ThemeManager.ApplyTheme(theme);
    }

    private void OutputLocation_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;

        if (OutputSourceFolderRadio.IsChecked == true)
        {
            _settings.CustomOutputDirectory = null;
            CustomFolderBox.Text = "(Source folder)";
            BrowseFolderBtn.IsEnabled = false;
        }
        else
        {
            BrowseFolderBtn.IsEnabled = true;
            if (string.IsNullOrWhiteSpace(_settings.CustomOutputDirectory))
            {
                BrowseFolder_Click(sender, e);
            }
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder for Phase Drop"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _settings.CustomOutputDirectory = dialog.FolderName;
            CustomFolderBox.Text = dialog.FolderName;
            OutputCustomFolderRadio.IsChecked = true;
        }
    }

    private void ConcurrencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int val = (int)e.NewValue;
        if (ConcurrencyValueText != null)
        {
            ConcurrencyValueText.Text = val.ToString();
        }
        _settings.MaxConcurrency = val;
    }

    private void DragThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int val = (int)e.NewValue;
        if (DragThresholdText != null)
        {
            DragThresholdText.Text = $"{val} px";
        }
        _settings.DragHorizontalThreshold = val;
        _settings.DragVerticalThreshold = val;
    }

    private void StartupCheck_Click(object sender, RoutedEventArgs e)
    {
        bool enable = StartupCheck.IsChecked == true;
        StartupManager.SetLaunchAtStartup(enable);
        _settings.LaunchAtStartup = enable;
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhaseDrop", "logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        string dir = string.IsNullOrWhiteSpace(_settings.CustomOutputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            : _settings.CustomOutputDirectory;

        if (Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all Phase Drop settings to default values?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _settings.Theme = "System";
            _settings.CustomOutputDirectory = null;
            _settings.MaxConcurrency = 4;
            _settings.DragHorizontalThreshold = 4;
            _settings.DragVerticalThreshold = 4;
            _settings.ShowNotifications = true;
            _settings.LaunchAtStartup = false;

            StartupManager.SetLaunchAtStartup(false);
            ThemeManager.ApplyTheme("System");

            OnWindowLoaded(sender, e);
        }
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowNotifications = (NotifyCheck.IsChecked == true);
        _settings.Save();
        SettingsSaved?.Invoke(_settings);
        Close();
    }
}
