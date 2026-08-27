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
        if (hWnd == IntPtr.Zero || !User32.IsWindowVisible(hWnd) || User32.IsIconic(hWnd))
            return false;

        try
        {
            IntPtr hMonitor = MonitorFromWindow(hWnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hMonitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref mi)) return false;

            const int tolerancePx = 8;
            bool frameCoversMonitor =
                DwmApi.DwmGetWindowAttribute(
                    hWnd,
                    DwmApi.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out DwmApi.RECT frameRect,
                    (uint)Marshal.SizeOf<DwmApi.RECT>()) == 0 &&
                CoversMonitor(frameRect.Left, frameRect.Top, frameRect.Right, frameRect.Bottom,
                    mi.rcMonitor, tolerancePx);

            // Chromium/DirectComposition can update DWM bounds one composition after
            // its HWND. Accept either source, then distinguish ordinary maximize by
            // checking whether the actual client content fills the monitor.
            bool windowCoversMonitor = User32.GetWindowRect(hWnd, out var windowRect) &&
                CoversMonitor(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom,
                    mi.rcMonitor, tolerancePx);

            bool coversMonitor = frameCoversMonitor || windowCoversMonitor;

            int style = User32.GetWindowLong(hWnd, User32.GWL_STYLE);
            const int WS_MAXIMIZE = 0x01000000;
            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            bool decoratedMaximized =
                (style & WS_MAXIMIZE) != 0 && (style & (WS_CAPTION | WS_THICKFRAME)) != 0;
            bool clientCoversMonitor = ClientCoversMonitor(hWnd, mi.rcMonitor, tolerancePx);
            bool shellFullscreen = Shell32.IsFullscreenModeActive();

            return ClassifyFullscreen(
                coversMonitor,
                clientCoversMonitor,
                decoratedMaximized,
                shellFullscreen);
        }
        catch
        {
            return false;
        }
    }

    internal static bool ClassifyFullscreen(
        bool coversMonitor,
        bool clientCoversMonitor,
        bool decoratedMaximized,
        bool shellFullscreen)
    {
        // Shell state closes the event/geometry gap for Chromium's F11/HTML-video
        // transition and exclusive Direct3D. It never classifies an ordinary maximize
        // because Windows does not enter a fullscreen notification state for maximize.
        if (shellFullscreen)
            return true;

        if (!coversMonitor)
            return false;

        // Chromium can retain WS_MAXIMIZE/WS_CAPTION during F11 and HTML-video
        // fullscreen. The decisive difference from an ordinary maximize is that its
        // client content itself reaches every monitor edge. A normal maximized client
        // remains constrained by the work area/title surface.
        if (decoratedMaximized)
            return clientCoversMonitor;

        // Borderless fullscreen must occupy both the outer frame and actual client
        // surface. This rejects oversized decorative/shadow-only windows.
        return clientCoversMonitor;
    }

    private static bool ClientCoversMonitor(IntPtr hWnd, User32.RECT monitorBounds, int tolerancePx)
    {
        if (!User32.GetClientRect(hWnd, out var client)) return false;

        var topLeft = new User32.POINT { X = client.Left, Y = client.Top };
        var bottomRight = new User32.POINT { X = client.Right, Y = client.Bottom };
        if (!User32.ClientToScreen(hWnd, ref topLeft) ||
            !User32.ClientToScreen(hWnd, ref bottomRight))
            return false;

        return CoversMonitor(
            topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y,
            monitorBounds, tolerancePx);
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
