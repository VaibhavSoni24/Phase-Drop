using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PhaseDrop.App.UI.Tools;

public partial class ResizeWindow : Window
{
    private readonly string _filePath;
    private int _origWidth;
    private int _origHeight;
    private bool _isUpdating;

    public event Action<string, int, int, string>? ResizeConfirmed;

    public ResizeWindow(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            FileNameText.Text = System.IO.Path.GetFileName(_filePath);

            using var stream = File.OpenRead(_filePath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];

            _origWidth = frame.PixelWidth;
            _origHeight = frame.PixelHeight;

            OriginalSizeText.Text = $"Original: {_origWidth} × {_origHeight} px";

            _isUpdating = true;
            WidthBox.Text = _origWidth.ToString();
            HeightBox.Text = _origHeight.ToString();
            _isUpdating = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to inspect image: {ex.Message}", "Phase Drop", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ScalePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int percent))
        {
            double scale = percent / 100.0;
            int newW = Math.Max(1, (int)Math.Round(_origWidth * scale));
            int newH = Math.Max(1, (int)Math.Round(_origHeight * scale));

            _isUpdating = true;
            WidthBox.Text = newW.ToString();
            HeightBox.Text = newH.ToString();
            _isUpdating = false;
        }
    }

    private void WidthBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || KeepAspectCheck.IsChecked != true || _origWidth <= 0)
            return;

        if (int.TryParse(WidthBox.Text, out int w) && w > 0)
        {
            _isUpdating = true;
            int h = Math.Max(1, (int)Math.Round((double)w * _origHeight / _origWidth));
            HeightBox.Text = h.ToString();
            _isUpdating = false;
        }
    }

    private void HeightBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || KeepAspectCheck.IsChecked != true || _origHeight <= 0)
            return;

        if (int.TryParse(HeightBox.Text, out int h) && h > 0)
        {
            _isUpdating = true;
            int w = Math.Max(1, (int)Math.Round((double)h * _origWidth / _origHeight));
            WidthBox.Text = w.ToString();
            _isUpdating = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out int w) || w <= 0 ||
            !int.TryParse(HeightBox.Text, out int h) || h <= 0)
        {
            MessageBox.Show("Please enter valid width and height values.", "Phase Drop", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string ext = System.IO.Path.GetExtension(_filePath);
        if (FormatCombo.SelectedIndex == 1) ext = ".png";
        else if (FormatCombo.SelectedIndex == 2) ext = ".jpg";
        else if (FormatCombo.SelectedIndex == 3) ext = ".webp";

        ResizeConfirmed?.Invoke(_filePath, w, h, ext);
        Close();
    }
}
