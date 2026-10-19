using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PhaseDrop.Platform.Windows.Shell;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    public event Action? OpenRadialRequested;
    public event Action? SettingsRequested;
    public event Action? OpenOutputRequested;
    public event Action? ViewLogsRequested;
    public event Action? ExitRequested;

    public TrayIconService(string? customIconPath = null)
    {
        _contextMenu = new ContextMenuStrip();

        // 1. Title Header
        var titleItem = new ToolStripMenuItem("Phase Drop v1.0.0")
        {
            Enabled = false,
            Font = new Font(_contextMenu.Font, FontStyle.Bold)
        };
        _contextMenu.Items.Add(titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 2. Open Radial / Drop Zone
        var openItem = new ToolStripMenuItem("Open Radial Menu / Drop Zone", null, (_, _) => OpenRadialRequested?.Invoke());
        _contextMenu.Items.Add(openItem);

        // 3. Settings
        var settingsItem = new ToolStripMenuItem("Settings...", null, (_, _) => SettingsRequested?.Invoke());
        _contextMenu.Items.Add(settingsItem);

        // 4. Output Folder
        var outputItem = new ToolStripMenuItem("Open Output Folder", null, (_, _) => OpenOutputRequested?.Invoke());
        _contextMenu.Items.Add(outputItem);

        // 5. Logs
        var logsItem = new ToolStripMenuItem("View Activity Logs", null, (_, _) => ViewLogsRequested?.Invoke());
        _contextMenu.Items.Add(logsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 6. Exit
        var exitItem = new ToolStripMenuItem("Exit Phase Drop", null, (_, _) => ExitRequested?.Invoke());
        _contextMenu.Items.Add(exitItem);

        // Configure NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Text = "Phase Drop — Drop. Transform. Done.",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Load Icon
        Icon? icon = null;
        if (!string.IsNullOrEmpty(customIconPath) && File.Exists(customIconPath))
        {
            try
            {
                icon = new Icon(customIconPath);
            }
            catch { }
        }

        if (icon == null)
        {
            // Fallback to application icon
            icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly()?.Location ?? Application.ExecutablePath) 
                   ?? SystemIcons.Application;
        }

        _notifyIcon.Icon = icon;
        _notifyIcon.DoubleClick += (_, _) => OpenRadialRequested?.Invoke();
    }

    public void ShowNotification(string title, string text, ToolTipIcon iconType = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, iconType);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
