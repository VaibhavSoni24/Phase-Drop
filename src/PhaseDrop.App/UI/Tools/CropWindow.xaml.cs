using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PhaseDrop.App.UI.Tools;

public partial class CropWindow : Window
{
    private readonly string _filePath;
    private BitmapImage? _bitmap;
    private Rect _imageDisplayRect;
    private Rect _cropRectDip;

    private bool _isDragging;
    private string? _activeHandle;
    private Point _dragStartPoint;
    private Rect _dragStartCropRect;
    private double? _targetAspectRatio; // null for free

    public event Action<string, int, int, int, int>? CropConfirmed;

    public CropWindow(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            FileTitleText.Text = System.IO.Path.GetFileName(_filePath);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(_filePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            _bitmap = bmp;
            PreviewImage.Source = bmp;
            DimensionInfoText.Text = $"Original Size: {bmp.PixelWidth} × {bmp.PixelHeight} px";

            UpdateImagePlacement();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load image: {ex.Message}", "Phase Drop", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void CropCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateImagePlacement();
    }

    private void UpdateImagePlacement()
    {
        if (_bitmap == null || CropCanvas.ActualWidth <= 0 || CropCanvas.ActualHeight <= 0)
            return;

        double canvasW = CropCanvas.ActualWidth;
        double canvasH = CropCanvas.ActualHeight;

        double imgW = _bitmap.PixelWidth;
        double imgH = _bitmap.PixelHeight;

        double scale = Math.Min((canvasW - 40) / imgW, (canvasH - 40) / imgH);
        double displayW = imgW * scale;
        double displayH = imgH * scale;

        double left = (canvasW - displayW) / 2.0;
        double top = (canvasH - displayH) / 2.0;

        _imageDisplayRect = new Rect(left, top, displayW, displayH);

        Canvas.SetLeft(PreviewImage, left);
        Canvas.SetTop(PreviewImage, top);
        PreviewImage.Width = displayW;
        PreviewImage.Height = displayH;

        // Default crop to 80% centered
        double cropW = displayW * 0.8;
        double cropH = displayH * 0.8;

        if (_targetAspectRatio.HasValue)
        {
            cropH = cropW / _targetAspectRatio.Value;
            if (cropH > displayH)
            {
                cropH = displayH * 0.8;
                cropW = cropH * _targetAspectRatio.Value;
            }
        }

        _cropRectDip = new Rect(
            left + ((displayW - cropW) / 2.0),
            top + ((displayH - cropH) / 2.0),
            cropW,
            cropH);

        UpdateCropVisuals();
    }

    private void UpdateCropVisuals()
    {
        // Clamp crop rect within image rect
        double x = Math.Max(_imageDisplayRect.Left, Math.Min(_cropRectDip.Left, _imageDisplayRect.Right - 20));
        double y = Math.Max(_imageDisplayRect.Top, Math.Min(_cropRectDip.Top, _imageDisplayRect.Bottom - 20));
        double w = Math.Max(20, Math.Min(_cropRectDip.Width, _imageDisplayRect.Right - x));
        double h = Math.Max(20, Math.Min(_cropRectDip.Height, _imageDisplayRect.Bottom - y));

        _cropRectDip = new Rect(x, y, w, h);

        Canvas.SetLeft(CropRect, _cropRectDip.Left);
        Canvas.SetTop(CropRect, _cropRectDip.Top);
        CropRect.Width = _cropRectDip.Width;
        CropRect.Height = _cropRectDip.Height;

        // Place Handles
        Canvas.SetLeft(HandleNW, _cropRectDip.Left - 7);
        Canvas.SetTop(HandleNW, _cropRectDip.Top - 7);

        Canvas.SetLeft(HandleNE, _cropRectDip.Right - 7);
        Canvas.SetTop(HandleNE, _cropRectDip.Top - 7);

        Canvas.SetLeft(HandleSW, _cropRectDip.Left - 7);
        Canvas.SetTop(HandleSW, _cropRectDip.Bottom - 7);

        Canvas.SetLeft(HandleSE, _cropRectDip.Right - 7);
        Canvas.SetTop(HandleSE, _cropRectDip.Bottom - 7);

        // Update Shading overlay
        var fullRect = new RectangleGeometry(new Rect(0, 0, CropCanvas.ActualWidth, CropCanvas.ActualHeight));
        var innerRect = new RectangleGeometry(_cropRectDip);
        var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, innerRect);
        ShadeOverlay.Data = combined;

        // Calculate Pixel Output
        if (_bitmap != null && _imageDisplayRect.Width > 0)
        {
            double scaleX = _bitmap.PixelWidth / _imageDisplayRect.Width;
            double scaleY = _bitmap.PixelHeight / _imageDisplayRect.Height;

            int pxW = (int)Math.Round(_cropRectDip.Width * scaleX);
            int pxH = (int)Math.Round(_cropRectDip.Height * scaleY);
            CropSizeStatusText.Text = $"Crop Output: {pxW} × {pxH} px";
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pt = e.GetPosition(CropCanvas);

        // Check if handle was clicked
        if (e.OriginalSource is Ellipse ell && ell.Tag is string tag)
        {
            _activeHandle = tag;
        }
        else if (_cropRectDip.Contains(pt))
        {
            _activeHandle = "MOVE";
        }
        else
        {
            return;
        }

        _isDragging = true;
        _dragStartPoint = pt;
        _dragStartCropRect = _cropRectDip;
        CropCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || string.IsNullOrEmpty(_activeHandle))
            return;

        var cur = e.GetPosition(CropCanvas);
        double dx = cur.X - _dragStartPoint.X;
        double dy = cur.Y - _dragStartPoint.Y;

        double newLeft = _dragStartCropRect.Left;
        double newTop = _dragStartCropRect.Top;
        double newWidth = _dragStartCropRect.Width;
        double newHeight = _dragStartCropRect.Height;

        if (_activeHandle == "MOVE")
        {
            newLeft += dx;
            newTop += dy;
        }
        else if (_activeHandle == "SE")
        {
            newWidth = Math.Max(20, _dragStartCropRect.Width + dx);
            newHeight = _targetAspectRatio.HasValue
                ? newWidth / _targetAspectRatio.Value
                : Math.Max(20, _dragStartCropRect.Height + dy);
        }
        else if (_activeHandle == "SW")
        {
            newWidth = Math.Max(20, _dragStartCropRect.Width - dx);
            newLeft = _dragStartCropRect.Right - newWidth;
            newHeight = _targetAspectRatio.HasValue
                ? newWidth / _targetAspectRatio.Value
                : Math.Max(20, _dragStartCropRect.Height + dy);
        }
        else if (_activeHandle == "NE")
        {
            newWidth = Math.Max(20, _dragStartCropRect.Width + dx);
            newHeight = _targetAspectRatio.HasValue
                ? newWidth / _targetAspectRatio.Value
                : Math.Max(20, _dragStartCropRect.Height - dy);
            newTop = _dragStartCropRect.Bottom - newHeight;
        }
        else if (_activeHandle == "NW")
        {
            newWidth = Math.Max(20, _dragStartCropRect.Width - dx);
            newLeft = _dragStartCropRect.Right - newWidth;
            newHeight = _targetAspectRatio.HasValue
                ? newWidth / _targetAspectRatio.Value
                : Math.Max(20, _dragStartCropRect.Height - dy);
            newTop = _dragStartCropRect.Bottom - newHeight;
        }

        _cropRectDip = new Rect(newLeft, newTop, newWidth, newHeight);
        UpdateCropVisuals();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _activeHandle = null;
            CropCanvas.ReleaseMouseCapture();
        }
    }

    private void AspectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AspectCombo.SelectedIndex == 1) _targetAspectRatio = 1.0;
        else if (AspectCombo.SelectedIndex == 2) _targetAspectRatio = 4.0 / 3.0;
        else if (AspectCombo.SelectedIndex == 3) _targetAspectRatio = 16.0 / 9.0;
        else if (AspectCombo.SelectedIndex == 4) _targetAspectRatio = 3.0 / 2.0;
        else if (AspectCombo.SelectedIndex == 5) _targetAspectRatio = 9.0 / 16.0;
        else _targetAspectRatio = null;

        UpdateImagePlacement();
    }

    private void ResetCrop_Click(object sender, RoutedEventArgs e)
    {
        UpdateImagePlacement();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyCrop_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap == null || _imageDisplayRect.Width <= 0)
        {
            Close();
            return;
        }

        double scaleX = _bitmap.PixelWidth / _imageDisplayRect.Width;
        double scaleY = _bitmap.PixelHeight / _imageDisplayRect.Height;

        int cropX = (int)Math.Max(0, Math.Round((_cropRectDip.Left - _imageDisplayRect.Left) * scaleX));
        int cropY = (int)Math.Max(0, Math.Round((_cropRectDip.Top - _imageDisplayRect.Top) * scaleY));
        int cropW = (int)Math.Min(_bitmap.PixelWidth - cropX, Math.Round(_cropRectDip.Width * scaleX));
        int cropH = (int)Math.Min(_bitmap.PixelHeight - cropY, Math.Round(_cropRectDip.Height * scaleY));

        CropConfirmed?.Invoke(_filePath, cropX, cropY, cropW, cropH);
        Close();
    }
}
