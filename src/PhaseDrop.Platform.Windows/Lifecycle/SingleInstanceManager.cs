using System.IO;

namespace PhaseDrop.Platform.Windows.Lifecycle;

public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\PhaseDrop_SingleInstance_Mutex";
    private const string ShowEventName = @"Local\PhaseDrop_Show_Event";
    private const string OpenEventName = @"Local\PhaseDrop_Open_Event";

    private static readonly string InboxDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhaseDrop",
        "inbox");

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private EventWaitHandle? _openEvent;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;
    private bool _isPrimary;

    public bool IsPrimary => _isPrimary;

    public event Action? ShowRequested;
    public event Action<IReadOnlyList<string>>? FilesReceived;

    public bool Initialize(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out _isPrimary);

        if (!_isPrimary)
        {
            // Another instance is already running
            if (args.Length > 0)
            {
                SendFilesToRunningInstance(args);
            }
            else
            {
                SignalShowRunningInstance();
            }
            return false;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _openEvent = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);

        // Start listening thread
        _thread = new Thread(ListenForSecondaryInstances)
        {
            IsBackground = true,
            Name = "PhaseDrop_IPC_Listener"
        };
        _thread.Start();

        return true;
    }

    private static void SignalShowRunningInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch
        {
            // Ignore
        }
    }

    private static void SendFilesToRunningInstance(string[] paths)
    {
        try
        {
            Directory.CreateDirectory(InboxDir);
            string batchFile = Path.Combine(InboxDir, $"batch-{Guid.NewGuid():N}.txt");
            File.WriteAllLines(batchFile, paths);

            using var openEvent = EventWaitHandle.OpenExisting(OpenEventName);
            openEvent.Set();
        }
        catch
        {
            // Ignore
        }
    }

    private void ListenForSecondaryInstances()
    {
        try
        {
            var waitHandles = new WaitHandle[] { _cts.Token.WaitHandle, _showEvent!, _openEvent! };

            while (!_cts.IsCancellationRequested)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                if (index == 0) // Cancellation
                    break;

                if (index == 1) // Show event
                {
                    ShowRequested?.Invoke();
                }
                else if (index == 2) // Open files event
                {
                    ProcessInbox();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Clean exit on disposal
        }
        catch (OperationCanceledException)
        {
            // Clean exit on cancellation
        }
    }

    private void ProcessInbox()
    {
        try
        {
            if (!Directory.Exists(InboxDir))
                return;

            var allPaths = new List<string>();
            foreach (var file in Directory.EnumerateFiles(InboxDir, "batch-*.txt"))
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    allPaths.AddRange(lines.Where(p => File.Exists(p) || Directory.Exists(p)));
                    File.Delete(file);
                }
                catch
                {
                    // Ignore transient lock
                }
            }

            if (allPaths.Count > 0)
            {
                FilesReceived?.Invoke(allPaths);
            }
        }
        catch
        {
            // Ignore
        }
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Ignore
        }

        _thread?.Join(500);

        _cts.Dispose();
        _showEvent?.Dispose();
        _openEvent?.Dispose();

        if (_mutex != null)
        {
            if (_isPrimary)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore
                }
            }
            _mutex.Dispose();
        }
    }
}
