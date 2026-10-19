using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PhaseDrop.Core.Models;

namespace PhaseDrop.App.UI.Tasks;

public partial class TaskIndicatorWindow : Window
{
    private string? _lastOutputFile;
    private DispatcherTimer? _autoDismissTimer;

    public TaskIndicatorWindow()
    {
        InitializeComponent();
    }

    public void ShowJobStarted(ConversionJob job)
    {
        _autoDismissTimer?.Stop();
        _lastOutputFile = null;

        Dispatcher.Invoke(() =>
        {
            string firstFile = job.SourcePaths.Count > 0 ? Path.GetFileName(job.SourcePaths[0]) : "selection";
            string title = job.SourcePaths.Count > 1
                ? $"{job.Operation.DisplayName}: {job.SourcePaths.Count} files"
                : $"{job.Operation.DisplayName}: {firstFile}";

            JobTitleText.Text = title;
            JobDetailText.Text = "Starting conversion...";
            JobProgressBar.Value = 0;
            JobProgressBar.IsIndeterminate = false;
            ProgressPercentText.Text = "0%";
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            JobDetailText.Visibility = Visibility.Visible;

            if (TryFindResource("IconSettings") is Geometry iconGeom)
            {
                StatusIcon.Data = iconGeom;
                StatusIcon.Fill = (Brush)FindResource("AccentPrimary");
            }

            PositionBottomRight();
            Visibility = Visibility.Visible;

            if (Resources["FadeInStoryboard"] is Storyboard sb)
            {
                sb.Begin();
            }
        });
    }

    public void UpdateProgress(JobProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            if (progress.ProgressRatio >= 0)
            {
                JobProgressBar.IsIndeterminate = false;
                double percent = Math.Clamp(progress.ProgressRatio * 100.0, 0.0, 100.0);
                JobProgressBar.Value = percent;
                ProgressPercentText.Text = $"{percent:0}%";
            }
            else
            {
                JobProgressBar.IsIndeterminate = true;
                ProgressPercentText.Text = "...";
            }

            if (!string.IsNullOrWhiteSpace(progress.StatusDescription))
            {
                JobDetailText.Text = progress.StatusDescription;
            }
        });
    }

    public void ShowJobCompleted(JobResult result)
    {
        _lastOutputFile = result.OutputFiles.Count > 0 ? result.OutputFiles[0] : null;

        Dispatcher.Invoke(() =>
        {
            JobProgressBar.IsIndeterminate = false;
            JobProgressBar.Value = 100;
            ProgressPercentText.Text = "100%";

            if (result.Success)
            {
                string label = result.OutputFiles.Count > 1
                    ? $"{result.OutputFiles.Count} files converted"
                    : Path.GetFileName(result.OutputFiles[0]);

                JobTitleText.Text = $"Completed: {label}";
                JobDetailText.Text = $"Finished in {(int)result.Elapsed.TotalMilliseconds} ms";
                ActionButtonsPanel.Visibility = Visibility.Visible;

                if (TryFindResource("IconCheck") is Geometry checkGeom)
                {
                    StatusIcon.Data = checkGeom;
                    StatusIcon.Fill = new SolidColorBrush(Color.FromRgb(35, 175, 80)); // Green
                }

                StartAutoDismiss(5);
            }
            else
            {
                JobTitleText.Text = "Operation Failed";
                JobDetailText.Text = result.ErrorMessage ?? "Unknown error occurred";
                ActionButtonsPanel.Visibility = Visibility.Collapsed;

                if (TryFindResource("IconClose") is Geometry closeGeom)
                {
                    StatusIcon.Data = closeGeom;
                    StatusIcon.Fill = new SolidColorBrush(Color.FromRgb(230, 55, 60)); // Red
                }

                StartAutoDismiss(8);
            }
        });
    }

    private void PositionBottomRight()
    {
        var primaryMonitor = SystemParameters.WorkArea;
        Left = primaryMonitor.Right - Width - 16;
        Top = primaryMonitor.Bottom - Height - 16;
    }

    private void StartAutoDismiss(int seconds)
    {
        _autoDismissTimer?.Stop();
        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _autoDismissTimer.Tick += (_, _) =>
        {
            _autoDismissTimer.Stop();
            Dismiss();
        };
        _autoDismissTimer.Start();
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        _autoDismissTimer?.Stop();
        Dismiss();
    }

    private void Dismiss()
    {
        Dispatcher.Invoke(() =>
        {
            if (Resources["FadeOutStoryboard"] is Storyboard sb)
            {
                sb.Completed += (_, _) =>
                {
                    Visibility = Visibility.Hidden;
                };
                sb.Begin();
            }
            else
            {
                Visibility = Visibility.Hidden;
            }
        });
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastOutputFile) && (File.Exists(_lastOutputFile) || Directory.Exists(_lastOutputFile)))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastOutputFile,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastOutputFile))
        {
            try
            {
                string? folder = File.Exists(_lastOutputFile)
                    ? Path.GetDirectoryName(_lastOutputFile)
                    : _lastOutputFile;

                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = File.Exists(_lastOutputFile) ? $"/select,\"{_lastOutputFile}\"" : $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }
    }
}
