using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using PhaseDrop.App.Themes;
using PhaseDrop.App.UI.RadialMenu;
using PhaseDrop.App.UI.Settings;
using PhaseDrop.App.UI.Tasks;
using PhaseDrop.App.UI.Tools;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;
using PhaseDrop.Engines.Archives;
using PhaseDrop.Engines.Documents;
using PhaseDrop.Engines.Images;
using PhaseDrop.Engines.Media;
using PhaseDrop.Engines.Pdf;
using PhaseDrop.Infrastructure.Configuration;
using PhaseDrop.Infrastructure.Logging;
using PhaseDrop.Platform.Windows.Hooks;
using PhaseDrop.Platform.Windows.Lifecycle;
using PhaseDrop.Platform.Windows.Native;
using PhaseDrop.Platform.Windows.Shell;

namespace PhaseDrop.App;

public partial class App : Application
{
    private SingleInstanceManager? _singleInstanceManager;
    private UserSettings _settings = new();
    private DragHookManager? _dragHookManager;
    private TrayIconService? _trayIconService;
    private JobQueue? _jobQueue;

    // Engines
    private readonly ImageConversionEngine _imageEngine = new();
    private MediaConversionEngine _mediaEngine = new();
    private readonly PdfConversionEngine _pdfEngine = new();
    private readonly ArchiveConversionEngine _archiveEngine = new();
    private readonly DocumentConversionEngine _documentEngine = new();

    // Windows
    private RadialMenuWindow? _radialWindow;
    private TaskIndicatorWindow? _taskWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Single Instance Check & IPC
        _singleInstanceManager = new SingleInstanceManager();
        if (!_singleInstanceManager.Initialize(e.Args))
        {
            // Secondary instance; forwarded args to primary instance, now exit
            Shutdown();
            return;
        }

        // 2. Settings & Logging
        _settings = UserSettings.Load();
        AppLogger.Info("Phase Drop application starting up.");

        // 3. Theme Manager
        ThemeManager.Initialize(_settings);

        // 4. Engine Configuration (FFmpeg path discovery)
        InitializeEngines();

        // 5. Job Queue & Task Indicator UI
        var engines = new IConversionEngine[]
        {
            _imageEngine,
            _mediaEngine,
            _pdfEngine,
            _archiveEngine,
            _documentEngine
        };

        _jobQueue = new JobQueue(engines, _settings.MaxConcurrency);
        _taskWindow = new TaskIndicatorWindow();

        _jobQueue.JobStarted += job =>
        {
            AppLogger.Info($"Job started: {job.Id} ({job.Operation.DisplayName}) on {job.SourcePaths.Count} files");
            if (_settings.ShowNotifications)
            {
                _taskWindow.ShowJobStarted(job);
            }
        };

        _jobQueue.JobProgressChanged += (job, progress) =>
        {
            if (_settings.ShowNotifications)
            {
                _taskWindow.UpdateProgress(progress);
            }
        };

        _jobQueue.JobCompleted += (job, result) =>
        {
            if (result.Success)
            {
                AppLogger.Info($"Job succeeded: {job.Id} -> {result.OutputFiles.Count} output files ({result.Elapsed.TotalMilliseconds:0} ms)");
            }
            else
            {
                AppLogger.Error($"Job failed: {job.Id}: {result.ErrorMessage}");
            }

            if (_settings.ShowNotifications)
            {
                _taskWindow.ShowJobCompleted(result);
            }
        };

        // 6. Radial Menu Window
        _radialWindow = new RadialMenuWindow();
        _radialWindow.OperationSelected += OnOperationSelected;

        // 7. System Tray Service
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
        _trayIconService = new TrayIconService(iconPath);
        _trayIconService.OpenRadialRequested += ShowRadialAtCursor;
        _trayIconService.SettingsRequested += OpenSettings;
        _trayIconService.OpenOutputRequested += OpenOutputFolder;
        _trayIconService.ViewLogsRequested += OpenLogsFolder;
        _trayIconService.ExitRequested += ExitApplication;

        // 8. Low-level Drag Hooks
        _dragHookManager = new DragHookManager
        {
            DragHorizontalThreshold = _settings.DragHorizontalThreshold,
            DragVerticalThreshold = _settings.DragVerticalThreshold
        };

        _dragHookManager.DragTriggered += (pt, toolsMode) =>
        {
            Dispatcher.Invoke(() =>
            {
                _radialWindow?.ShowAt(pt.X, pt.Y, toolsMode);
            });
        };

        _dragHookManager.DragCancelled += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _radialWindow?.DismissMenu();
            });
        };

        _dragHookManager.DragReleased += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _radialWindow?.DismissMenu();
            });
        };

        _dragHookManager.Install();

        // 9. Handle Secondary Instance Signals
        _singleInstanceManager.ShowRequested += () =>
        {
            Dispatcher.Invoke(ShowRadialAtCursor);
        };

        _singleInstanceManager.FilesReceived += paths =>
        {
            Dispatcher.Invoke(() =>
            {
                if (paths.Count > 0 && Win32.GetCursorPos(out var pt))
                {
                    _radialWindow?.SetFiles(paths);
                    _radialWindow?.ShowAt(pt.X, pt.Y, false);
                }
            });
        };

        // If files were passed on command line to first instance
        if (e.Args.Length > 0 && Win32.GetCursorPos(out var initPt))
        {
            _radialWindow.SetFiles(e.Args);
            _radialWindow.ShowAt(initPt.X, initPt.Y, false);
        }

        AppLogger.Info("Phase Drop initialized and running quietly in system tray.");
    }

    private void InitializeEngines()
    {
        // Check for bundled FFmpeg in engines/media/ relative to App domain
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string bundledFfmpeg = Path.Combine(appDir, "engines", "media", "ffmpeg.exe");
        string bundledFfprobe = Path.Combine(appDir, "engines", "media", "ffprobe.exe");

        if (File.Exists(bundledFfmpeg) && File.Exists(bundledFfprobe))
        {
            _mediaEngine = new MediaConversionEngine(bundledFfmpeg, bundledFfprobe);
            AppLogger.Info($"Using bundled media engine: {bundledFfmpeg}");
        }
        else
        {
            _mediaEngine = new MediaConversionEngine();
            AppLogger.Info("Using system PATH media engine discovery.");
        }
    }

    private void ShowRadialAtCursor()
    {
        if (Win32.GetCursorPos(out var pt))
        {
            _radialWindow?.ShowAt(pt.X, pt.Y, false);
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.SettingsSaved += savedSettings =>
            {
                _settings = savedSettings;
                if (_dragHookManager != null)
                {
                    _dragHookManager.DragHorizontalThreshold = _settings.DragHorizontalThreshold;
                    _dragHookManager.DragVerticalThreshold = _settings.DragVerticalThreshold;
                }
            };
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenOutputFolder()
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

    private void OpenLogsFolder()
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhaseDrop", "logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }

    private void OnOperationSelected(FileOperation op, IReadOnlyList<string> files)
    {
        if (files.Count == 0) return;

        // Specialized Interactive Tools
        if (op.Id.Equals("tool:crop", StringComparison.OrdinalIgnoreCase))
        {
            var cropWin = new CropWindow(files[0]);
            cropWin.CropConfirmed += (srcPath, x, y, w, h) =>
            {
                var parameters = new JsonObject
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["width"] = w,
                    ["height"] = h
                };
                var job = new ConversionJob([srcPath], op, _settings.CustomOutputDirectory, parameters);
                _jobQueue?.Enqueue(job);
            };
            cropWin.Show();
            return;
        }

        if (op.Id.Equals("tool:resize", StringComparison.OrdinalIgnoreCase))
        {
            var resizeWin = new ResizeWindow(files[0]);
            resizeWin.ResizeConfirmed += (srcPath, w, h, targetExt) =>
            {
                var parameters = new JsonObject
                {
                    ["width"] = w,
                    ["height"] = h
                };
                var resizeOp = op with { TargetExtension = targetExt };
                var job = new ConversionJob([srcPath], resizeOp, _settings.CustomOutputDirectory, parameters);
                _jobQueue?.Enqueue(job);
            };
            resizeWin.Show();
            return;
        }

        if (op.Id.Equals("tool:trim", StringComparison.OrdinalIgnoreCase))
        {
            var trimWin = new TrimWindow(files[0], _mediaEngine);
            trimWin.TrimConfirmed += (srcPath, start, dur) =>
            {
                var parameters = new JsonObject
                {
                    ["start"] = $"{(int)start.TotalHours:00}:{start.Minutes:00}:{start.Seconds:00}",
                    ["duration"] = $"{(int)dur.TotalHours:00}:{dur.Minutes:00}:{dur.Seconds:00}"
                };
                var job = new ConversionJob([srcPath], op, _settings.CustomOutputDirectory, parameters);
                _jobQueue?.Enqueue(job);
            };
            trimWin.Show();
            return;
        }

        // Standard batch or multi-file operation
        var standardJob = new ConversionJob(files, op, _settings.CustomOutputDirectory);
        _jobQueue?.Enqueue(standardJob);
    }

    private void ExitApplication()
    {
        AppLogger.Info("Exiting Phase Drop on user request.");
        _dragHookManager?.Dispose();
        _trayIconService?.Dispose();
        _singleInstanceManager?.Dispose();
        _jobQueue?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dragHookManager?.Dispose();
        _trayIconService?.Dispose();
        _singleInstanceManager?.Dispose();
        _jobQueue?.Dispose();
        base.OnExit(e);
    }
}
