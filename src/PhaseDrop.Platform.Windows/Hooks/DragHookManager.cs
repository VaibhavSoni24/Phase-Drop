using System.Diagnostics;
using System.Runtime.InteropServices;
using PhaseDrop.Platform.Windows.Native;
using PhaseDrop.Platform.Windows.Shell;

namespace PhaseDrop.Platform.Windows.Hooks;

public sealed class DragHookManager : IDisposable
{
    private IntPtr _hMouseHook = IntPtr.Zero;
    private IntPtr _hKbdHook = IntPtr.Zero;

    private readonly Win32.HookProc _mouseProc;
    private readonly Win32.HookProc _kbdProc;

    private bool _isCandidate;
    private bool _dragDistanceExceeded;
    private bool _isMenuTriggered;
    private int _startX;
    private int _startY;

    public int DragHorizontalThreshold { get; set; } = 4;
    public int DragVerticalThreshold { get; set; } = 4;

    public event Action<Win32.POINT, bool>? DragTriggered;
    public event Action? DragCancelled;
    public event Action? DragReleased;

    public DragHookManager()
    {
        _mouseProc = MouseHookCallback;
        _kbdProc = KeyboardHookCallback;

        int cx = Win32.GetSystemMetrics(Win32.SM_CXDRAG);
        int cy = Win32.GetSystemMetrics(Win32.SM_CYDRAG);
        DragHorizontalThreshold = Math.Max(2, cx);
        DragVerticalThreshold = Math.Max(2, cy);
    }

    public void Install()
    {
        if (_hMouseHook != IntPtr.Zero)
            return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr hMod = Win32.GetModuleHandle(curModule?.ModuleName);

        _hMouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, hMod, 0);
        _hKbdHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _kbdProc, hMod, 0);
    }

    public void ResetState()
    {
        _isCandidate = false;
        _dragDistanceExceeded = false;
        _isMenuTriggered = false;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == Win32.WM_LBUTTONDOWN)
            {
                if (ShellWindowDetector.IsForegroundShellWindow())
                {
                    var ms = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    _isCandidate = true;
                    _dragDistanceExceeded = false;
                    _isMenuTriggered = false;
                    _startX = ms.pt.X;
                    _startY = ms.pt.Y;
                }
                else
                {
                    _isCandidate = false;
                }
            }
            else if (msg == Win32.WM_LBUTTONUP)
            {
                if (_isMenuTriggered)
                {
                    DragReleased?.Invoke();
                }
                ResetState();
            }
            else if (msg == Win32.WM_MOUSEMOVE)
            {
                if (_isCandidate && !_dragDistanceExceeded)
                {
                    var ms = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    int dx = Math.Abs(ms.pt.X - _startX);
                    int dy = Math.Abs(ms.pt.Y - _startY);
                    if (dx >= DragHorizontalThreshold || dy >= DragVerticalThreshold)
                    {
                        _dragDistanceExceeded = true;

                        // Check if Shift is already pressed during drag
                        if (Win32.IsKeyDown(Win32.VK_SHIFT) && !_isMenuTriggered)
                        {
                            _isMenuTriggered = true;
                            bool toolsMode = Win32.IsKeyDown(Win32.VK_CONTROL);
                            DragTriggered?.Invoke(ms.pt, toolsMode);
                        }
                    }
                }
            }
        }

        return Win32.CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
            {
                var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                if (kb.vkCode == Win32.VK_SHIFT)
                {
                    if (_isCandidate && _dragDistanceExceeded && !_isMenuTriggered)
                    {
                        if (Win32.GetCursorPos(out var pt))
                        {
                            _isMenuTriggered = true;
                            bool toolsMode = Win32.IsKeyDown(Win32.VK_CONTROL);
                            DragTriggered?.Invoke(pt, toolsMode);
                        }
                    }
                }
                else if (kb.vkCode == Win32.VK_ESCAPE)
                {
                    if (_isMenuTriggered)
                    {
                        ResetState();
                        DragCancelled?.Invoke();
                    }
                }
            }
        }

        return Win32.CallNextHookEx(_hKbdHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hMouseHook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hMouseHook);
            _hMouseHook = IntPtr.Zero;
        }

        if (_hKbdHook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hKbdHook);
            _hKbdHook = IntPtr.Zero;
        }
    }
}
