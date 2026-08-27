// WinNotch.Core/Interop/WindowHookManager.cs
// Event-first foreground/fullscreen tracking. MainWindow adds a low-frequency
// foreground verification only while automatic fullscreen hiding is enabled.

using System.Runtime.InteropServices;
using static WinNotch.Core.Interop.User32;

namespace WinNotch.Core.Interop;

public sealed class ForegroundChangedEventArgs : EventArgs
{
    public IntPtr WindowHandle { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
}

public sealed class WindowHookManager : IDisposable
{
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const int OBJID_WINDOW = 0;

    private IntPtr _foregroundHook;
    private IntPtr _locationHook;
    private User32.WinEventDelegate? _foregroundCallback;
    private User32.WinEventDelegate? _locationCallback;
    private IntPtr _lastForegroundWindow;
    private bool _disposed;

    public event EventHandler<ForegroundChangedEventArgs>? ForegroundWindowChanged;
    public IntPtr CurrentForegroundWindow => _lastForegroundWindow;

    public bool StartTracking()
    {
        if (_foregroundHook != IntPtr.Zero) return true;

        _foregroundCallback = OnForegroundChanged;
        _locationCallback = OnLocationChanged;
        _lastForegroundWindow = User32.GetForegroundWindow();

        _foregroundHook = User32.SetWinEventHook(
            User32.EVENT_SYSTEM_FOREGROUND,
            User32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _foregroundCallback,
            0, 0,
            User32.WINEVENT_OUTOFCONTEXT);

        if (_foregroundHook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine(
                $"[WindowHookManager] Foreground hook failed. Win32 error: {error}");
            return false;
        }

        _locationHook = User32.SetWinEventHook(
            EVENT_OBJECT_LOCATIONCHANGE,
            EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _locationCallback,
            0, 0,
            User32.WINEVENT_OUTOFCONTEXT);

        if (_locationHook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine(
                $"[WindowHookManager] Location hook failed. Win32 error: {error}");
        }

        RefreshForegroundWindow();
        return true;
    }

    public void RefreshForegroundWindow()
    {
        if (_disposed) return;

        IntPtr hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        _lastForegroundWindow = hwnd;
        RaiseWindowChanged(hwnd);
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero) return;

        _lastForegroundWindow = hwnd;
        RaiseWindowChanged(hwnd);
    }

    private void OnLocationChanged(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero) return;
        if (idObject != OBJID_WINDOW || idChild != 0) return;

        IntPtr foreground = User32.GetForegroundWindow();
        if (foreground != IntPtr.Zero)
            _lastForegroundWindow = foreground;

        if (hwnd != _lastForegroundWindow) return;
        RaiseWindowChanged(hwnd);
    }

    private void RaiseWindowChanged(IntPtr hwnd)
    {
        try
        {
            ForegroundWindowChanged?.Invoke(this, new ForegroundChangedEventArgs
            {
                WindowHandle = hwnd,
                WindowTitle = GetWindowTitle(hwnd),
                ClassName = GetWindowClassName(hwnd)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowHookManager] Error raising window change: {ex.Message}");
        }
    }

    public static bool IsWindowFullscreen(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        try
        {
            IntPtr hMonitor = MonitorFromWindow(hWnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hMonitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref mi)) return false;

            const int tolerancePx = 8;

            if (DwmApi.DwmGetWindowAttribute(
                    hWnd,
                    DwmApi.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out DwmApi.RECT frameRect,
                    (uint)Marshal.SizeOf<DwmApi.RECT>()) &&
                CoversMonitor(frameRect.Left, frameRect.Top, frameRect.Right, frameRect.Bottom,
                    mi.rcMonitor, tolerancePx))
            {
                return true;
            }

            // Chromium/DirectComposition transitions can briefly report stale DWM
            // frame bounds. The top-level window rect is a reliable second source.
            return User32.GetWindowRect(hWnd, out var windowRect) &&
                   CoversMonitor(
                       windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom,
                       mi.rcMonitor, tolerancePx);
        }
        catch
        {
            return false;
        }
    }

    private static bool CoversMonitor(
        int left, int top, int right, int bottom,
        User32.RECT monitorBounds,
        int tolerancePx)
    {
        return left <= monitorBounds.Left + tolerancePx &&
               top <= monitorBounds.Top + tolerancePx &&
               right >= monitorBounds.Right - tolerancePx &&
               bottom >= monitorBounds.Bottom - tolerancePx;
    }

    public static bool IsWindowMaximized(IntPtr hWnd)
    {
        try
        {
            if (IsWindowFullscreen(hWnd))
                return false;

            int style = User32.GetWindowLong(hWnd, User32.GWL_STYLE);
            return (style & 0x01000000) != 0; // WS_MAXIMIZE
        }
        catch
        {
            return false;
        }
    }

    public static string GetWindowClassName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return string.Empty;
        var buffer = new char[256];
        User32.GetClassName(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = User32.GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var buffer = new char[length + 1];
        User32.GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    public void StopTracking()
    {
        if (_locationHook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_locationHook);
            _locationHook = IntPtr.Zero;
        }

        if (_foregroundHook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        _lastForegroundWindow = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopTracking();
        _disposed = true;
        ForegroundWindowChanged = null;
        GC.SuppressFinalize(this);
    }
}
