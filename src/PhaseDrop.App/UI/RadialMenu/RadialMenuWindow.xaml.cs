using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using PhaseDrop.App.Themes;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Registry;
using PhaseDrop.Platform.Windows.Display;
using PhaseDrop.Platform.Windows.Native;

namespace PhaseDrop.App.UI.RadialMenu;

public partial class RadialMenuWindow : Window
{
    private const double CenterX = 220.0;
    private const double CenterY = 220.0;
    private const double CenterHubRadius = 68.0;

    private readonly List<RadialPetal> _petals = [];
    private IReadOnlyList<string> _currentFiles = [];
    private bool _toolsMode;
    private RadialPetal? _hoveredPetal;
    private bool _isCenterHovered;
    private bool _isClosing;

    public event Action<FileOperation, IReadOnlyList<string>>? OperationSelected;
    public event Action? MenuDismissed;

    public RadialMenuWindow()
    {
        InitializeComponent();

        // Register drag events on root
        PreviewDragEnter += OnPreviewDragEnter;
        PreviewDragOver += OnPreviewDragOver;
        PreviewDragLeave += OnPreviewDragLeave;
        PreviewDrop += OnPreviewDrop;

        // Mouse interaction for standalone drop or clicking
        MouseMove += OnWindowMouseMove;
        MouseLeftButtonUp += OnWindowMouseLeftButtonUp;

        // Apply dark/light theme changes
        ThemeManager.ThemeChanged += () =>
        {
            Dispatcher.Invoke(RedrawPetals);
        };
    }

    public void ShowAt(int physicalX, int physicalY, bool toolsMode = false)
    {
        _isClosing = false;
        _toolsMode = toolsMode;
        UpdateModeBadge();

        // Calculate DPI-aware clamped screen position
        var visual = Application.Current.MainWindow ?? this;
        var dpi = VisualTreeHelper.GetDpi(visual);
        double dpiScale = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;

        var centerDip = new Point(physicalX / dpiScale, physicalY / dpiScale);
        var clamped = MonitorHelper.ClampWindowToScreen(centerDip, Width, Height, dpiScale);

        Left = clamped.X;
        Top = clamped.Y;

        Visibility = Visibility.Visible;
        Activate();

        if (Resources["ShowStoryboard"] is Storyboard showSb)
        {
            showSb.Begin();
        }
    }

    public void SetFiles(IReadOnlyList<string> paths)
    {
        _currentFiles = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();

        if (_currentFiles.Count == 0)
        {
            SummaryText.Text = "Drop Files Here";
            StatusText.Text = "No valid files";
            _petals.Clear();
            RedrawPetals();
            return;
        }

        if (_currentFiles.Count == 1)
        {
            SummaryText.Text = System.IO.Path.GetFileName(_currentFiles[0]);
        }
        else
        {
            SummaryText.Text = $"{_currentFiles.Count} Files";
        }

        RebuildPetals();
    }

    private void RebuildPetals()
    {
        _petals.Clear();
        _hoveredPetal = null;

        var operations = _toolsMode
            ? CapabilityRegistry.GetTools(_currentFiles)
            : CapabilityRegistry.GetConversions(_currentFiles);

        if (operations.Count == 0)
        {
            StatusText.Text = "No actions available";
            RedrawPetals();
            return;
        }

        int count = operations.Count;
        double sweep = (2.0 * Math.PI) / count;
        // Start angle: Top (-PI / 2) offset so first petal is at 12 o'clock
        double start = -Math.PI / 2.0;

        for (int i = 0; i < count; i++)
        {
            double angle = start + (i * sweep);
            if (angle < 0) angle += 2.0 * Math.PI;
            _petals.Add(new RadialPetal(operations[i], angle, sweep));
        }

        StatusText.Text = _toolsMode ? "Select tool" : "Select target format";
        RedrawPetals();
    }

    private void RedrawPetals()
    {
        PetalCanvas.Children.Clear();

        var petalBg = (Brush)FindResource("PetalBg");
        var petalBorder = (Brush)FindResource("PetalBorder");
        var petalHoverBg = (Brush)FindResource("PetalHoverBg");
        var petalHoverText = (Brush)FindResource("PetalHoverText");
        var textPrimary = (Brush)FindResource("TextPrimary");

        foreach (var petal in _petals)
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = petal.CreateGeometry(CenterX, CenterY),
                Fill = petal.IsHovered ? petalHoverBg : petalBg,
                Stroke = petal.IsHovered ? (Brush)FindResource("AccentHover") : petalBorder,
                StrokeThickness = petal.IsHovered ? 2.0 : 1.2,
                Tag = petal
            };

            if (petal.IsHovered)
            {
                path.Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    Color = Color.FromRgb(0, 132, 255),
                    Opacity = 0.5,
                    ShadowDepth = 0
                };
            }

            PetalCanvas.Children.Add(path);

            // Add Icon and Text at Petal Center
            var midPt = petal.GetCenterPoint(CenterX, CenterY);
            var contentPanel = CreatePetalContent(petal, petal.IsHovered ? petalHoverText : textPrimary);

            // Center the element at midPt
            Canvas.SetLeft(contentPanel, midPt.X - 35.0);
            Canvas.SetTop(contentPanel, midPt.Y - 26.0);
            PetalCanvas.Children.Add(contentPanel);
        }
    }

    private UIElement CreatePetalContent(RadialPetal petal, Brush textBrush)
    {
        var panel = new StackPanel
        {
            Width = 70,
            Height = 52,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        // Resolve Vector Icon
        string iconKey = ResolveIconKey(petal.Operation);
        if (TryFindResource(iconKey) is Geometry geom)
        {
            var iconPath = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = textBrush,
                Stretch = Stretch.Uniform,
                Width = petal.IsHovered ? 20 : 18,
                Height = petal.IsHovered ? 20 : 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            panel.Children.Add(iconPath);
        }

        var label = new TextBlock
        {
            Text = petal.Operation.DisplayName,
            FontSize = petal.IsHovered ? 11.5 : 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = textBrush,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(label);

        return panel;
    }

    private static string ResolveIconKey(FileOperation op)
    {
        string id = op.Id.ToLowerInvariant();
        if (id.Contains("png")) return "IconPng";
        if (id.Contains("jpg") || id.Contains("jpeg")) return "IconJpg";
        if (id.Contains("webp")) return "IconWebp";
        if (id.Contains("gif")) return "IconGif";
        if (id.Contains("mp4")) return "IconMp4";
        if (id.Contains("mp3")) return "IconMp3";
        if (id.Contains("pdf")) return "IconPdf";
        if (id.Contains("zip") || id.Contains("7z") || id.Contains("tar")) return "IconZip";
        if (id.Contains("crop")) return "IconCrop";
        if (id.Contains("resize")) return "IconResize";
        if (id.Contains("compress")) return "IconCompress";
        if (id.Contains("rotate")) return "IconRotate";
        if (id.Contains("flip")) return "IconFlip";
        if (id.Contains("exif") || id.Contains("shield") || id.Contains("clean")) return "IconShield";
        if (id.Contains("trim")) return "IconTrim";
        if (id.Contains("merge")) return "IconMerge";
        if (id.Contains("split")) return "IconSplit";
        if (id.Contains("extract")) return "IconExtract";
        if (id.Contains("volume") || id.Contains("normalize")) return "IconVolume";
        if (id.Contains("thumbnail") || id.Contains("snapshot")) return "IconCamera";
        if (id.Contains("speed")) return "IconSpeed";

        return op.Category switch
        {
            OperationCategory.Image => "IconImage",
            OperationCategory.Video => "IconVideo",
            OperationCategory.Audio => "IconAudio",
            OperationCategory.Pdf => "IconPdf",
            OperationCategory.Archive => "IconArchive",
            OperationCategory.Document => "IconDocument",
            _ => "IconSettings"
        };
    }

    public void UpdateHoverPosition(double x, double y)
    {
        double dx = x - CenterX;
        double dy = y - CenterY;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));

        if (dist <= CenterHubRadius)
        {
            if (!_isCenterHovered)
            {
                _isCenterHovered = true;
                _hoveredPetal = null;
                foreach (var p in _petals) p.IsHovered = false;
                StatusText.Text = "Drop here to Cancel";
                RedrawPetals();
            }
            return;
        }

        _isCenterHovered = false;
        RadialPetal? newHovered = null;

        foreach (var petal in _petals)
        {
            if (petal.HitTest(x, y, CenterX, CenterY))
            {
                newHovered = petal;
                break;
            }
        }

        if (newHovered != _hoveredPetal)
        {
            _hoveredPetal = newHovered;
            foreach (var p in _petals)
            {
                p.IsHovered = (p == _hoveredPetal);
            }

            if (_hoveredPetal != null)
            {
                StatusText.Text = _hoveredPetal.Operation.Description;
            }
            else
            {
                StatusText.Text = _toolsMode ? "Select tool" : "Select target format";
            }

            RedrawPetals();
        }
    }

    private void UpdateModeBadge()
    {
        ModeText.Text = _toolsMode ? "TOOLS" : "CONVERT";
        ModeBadge.Background = _toolsMode
            ? new SolidColorBrush(Color.FromRgb(154, 76, 230)) // Purple for tools
            : (Brush)FindResource("AccentPrimary");
    }

    private void CenterHub_Click(object sender, MouseButtonEventArgs e)
    {
        // Toggle Convert vs Tools mode on click
        _toolsMode = !_toolsMode;
        UpdateModeBadge();
        RebuildPetals();
    }

    private void OnPreviewDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                SetFiles(files);
            }
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var pos = e.GetPosition(this);
            UpdateHoverPosition(pos.X, pos.Y);
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        _hoveredPetal = null;
        _isCenterHovered = false;
        foreach (var p in _petals) p.IsHovered = false;
        StatusText.Text = "Release inside to cancel";
        RedrawPetals();
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                if (_hoveredPetal != null && !_isCenterHovered)
                {
                    OperationSelected?.Invoke(_hoveredPetal.Operation, files);
                }
            }
        }

        DismissMenu();
        e.Handled = true;
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        UpdateHoverPosition(pos.X, pos.Y);
    }

    private void OnWindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_hoveredPetal != null && !_isCenterHovered && _currentFiles.Count > 0)
        {
            OperationSelected?.Invoke(_hoveredPetal.Operation, _currentFiles);
            DismissMenu();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissMenu();
        }
        else if (e.Key == Key.Tab)
        {
            _toolsMode = !_toolsMode;
            UpdateModeBadge();
            RebuildPetals();
        }
    }

    public void DismissMenu()
    {
        if (_isClosing) return;
        _isClosing = true;

        if (Resources["HideStoryboard"] is Storyboard hideSb)
        {
            hideSb.Completed += (_, _) =>
            {
                Visibility = Visibility.Hidden;
                _isClosing = false;
                MenuDismissed?.Invoke();
            };
            hideSb.Begin();
        }
        else
        {
            Visibility = Visibility.Hidden;
            _isClosing = false;
            MenuDismissed?.Invoke();
        }
    }
}
