using System.IO;
using System.Windows;
using PhaseDrop.Engines.Media;

namespace PhaseDrop.App.UI.Tools;

public partial class TrimWindow : Window
{
    private readonly string _filePath;
    private readonly MediaConversionEngine _mediaEngine;
    private double _totalSeconds = 60.0;
    private bool _isUpdating;

    public event Action<string, TimeSpan, TimeSpan>? TrimConfirmed;

    public TrimWindow(string filePath, MediaConversionEngine mediaEngine)
    {
        _filePath = filePath;
        _mediaEngine = mediaEngine;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        FileTitleText.Text = Path.GetFileName(_filePath);

        try
        {
            var dur = await _mediaEngine.ProbeDurationAsync(_filePath);
            if (dur.HasValue && dur.Value.TotalSeconds > 0)
            {
                _totalSeconds = dur.Value.TotalSeconds;
            }
        }
        catch
        {
            _totalSeconds = 60.0;
        }

        DurationInfoText.Text = $"Total Duration: {FormatTimeSpan(TimeSpan.FromSeconds(_totalSeconds))}";

        _isUpdating = true;
        StartSlider.Maximum = _totalSeconds;
        StartSlider.Value = 0;
        EndSlider.Maximum = _totalSeconds;
        EndSlider.Value = _totalSeconds;

        StartTimeBox.Text = "00:00:00";
        EndTimeBox.Text = FormatTimeSpan(TimeSpan.FromSeconds(_totalSeconds));
        _isUpdating = false;

        UpdateSummary();
    }

    private void StartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        if (StartSlider.Value >= EndSlider.Value)
        {
            StartSlider.Value = Math.Max(0, EndSlider.Value - 1.0);
        }

        _isUpdating = true;
        StartTimeBox.Text = FormatTimeSpan(TimeSpan.FromSeconds(StartSlider.Value));
        _isUpdating = false;

        UpdateSummary();
    }

    private void EndSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        if (EndSlider.Value <= StartSlider.Value)
        {
            EndSlider.Value = Math.Min(_totalSeconds, StartSlider.Value + 1.0);
        }

        _isUpdating = true;
        EndTimeBox.Text = FormatTimeSpan(TimeSpan.FromSeconds(EndSlider.Value));
        _isUpdating = false;

        UpdateSummary();
    }

    private void StartTimeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (TimeSpan.TryParse(StartTimeBox.Text, out var ts))
        {
            double sec = ts.TotalSeconds;
            if (sec >= 0 && sec < EndSlider.Value)
            {
                _isUpdating = true;
                StartSlider.Value = sec;
                _isUpdating = false;
                UpdateSummary();
            }
        }
    }

    private void EndTimeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (TimeSpan.TryParse(EndTimeBox.Text, out var ts))
        {
            double sec = ts.TotalSeconds;
            if (sec > StartSlider.Value && sec <= _totalSeconds)
            {
                _isUpdating = true;
                EndSlider.Value = sec;
                _isUpdating = false;
                UpdateSummary();
            }
        }
    }

    private void UpdateSummary()
    {
        double dur = Math.Max(0, EndSlider.Value - StartSlider.Value);
        TrimSummaryText.Text = $"Output Duration: {FormatTimeSpan(TimeSpan.FromSeconds(dur))}";
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var start = TimeSpan.FromSeconds(StartSlider.Value);
        var dur = TimeSpan.FromSeconds(EndSlider.Value - StartSlider.Value);

        TrimConfirmed?.Invoke(_filePath, start, dur);
        Close();
    }
}
