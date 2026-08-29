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
        _lastForegroundWindow = NormalizeRootWindow(User32.GetForegroundWindow());

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

        IntPtr hwnd = NormalizeRootWindow(User32.GetForegroundWindow());
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

        IntPtr root = NormalizeRootWindow(hwnd);
        _lastForegroundWindow = root;
        RaiseWindowChanged(root);
    }

    private void OnLocationChanged(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero) return;
        if (idObject != OBJID_WINDOW || idChild != 0) return;

        IntPtr foreground = NormalizeRootWindow(User32.GetForegroundWindow());
        if (foreground != IntPtr.Zero)
            _lastForegroundWindow = foreground;

        IntPtr changedRoot = NormalizeRootWindow(hwnd);
        if (changedRoot != _lastForegroundWindow) return;
        RaiseWindowChanged(changedRoot);
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

    private static IntPtr NormalizeRootWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr root = User32.GetAncestor(window, User32.GA_ROOT);
        return root == IntPtr.Zero ? window : root;
    }

    public static bool IsWindowFullscreen(IntPtr hWnd)
    {
        hWnd = NormalizeRootWindow(hWnd);
        if (hWnd == IntPtr.Zero) return false;

        if (!User32.IsWindowVisible(hWnd) || User32.IsIconic(hWnd))
            return false;

        try
        {
            IntPtr hMonitor = MonitorFromWindow(hWnd, User32.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref mi)) return false;

            // Resize-frame metrics scale with the target window's DPI. A fixed 8px
            // tolerance rejects valid fullscreen Chromium windows at 150–200% scaling.
            uint dpi = User32.GetDpiForWindow(hWnd);
            int tolerancePx = GeometryToleranceForDpi(dpi);
            bool frameCoversMonitor =
                DwmApi.DwmGetWindowAttribute(
                    hWnd,
                    DwmApi.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out DwmApi.RECT frameRect,
                    (uint)Marshal.SizeOf<DwmApi.RECT>()) == 0 &&
                MatchesMonitor(frameRect.Left, frameRect.Top, frameRect.Right, frameRect.Bottom,
                    mi.rcMonitor, tolerancePx);

            // Chromium/DirectComposition can update DWM bounds one composition after
            // its HWND. Accept either source, then distinguish ordinary maximize by
            // checking whether the actual client content fills the monitor.
            bool windowCoversMonitor = User32.GetWindowRect(hWnd, out var windowRect) &&
                MatchesMonitor(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom,
                    mi.rcMonitor, tolerancePx);

            bool coversMonitor = frameCoversMonitor || windowCoversMonitor;

            int style = User32.GetWindowLong(hWnd, User32.GWL_STYLE);
            const int WS_MAXIMIZE = 0x01000000;
            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            bool decoratedMaximized =
                (style & WS_MAXIMIZE) != 0 && (style & (WS_CAPTION | WS_THICKFRAME)) != 0;
            bool clientCoversMonitor = ClientCoversMonitor(hWnd, mi.rcMonitor, tolerancePx);

            // Explorer shell-hook messages bridge the short Chromium transition in
            // MainWindow. Do not treat global notification/busy state as permanent
            // evidence here; it can remain stale after F11 exits.
            return ClassifyFullscreen(
                coversMonitor,
                clientCoversMonitor,
                decoratedMaximized);
        }
        catch
        {
            return false;
        }
    }

    internal static bool ClassifyFullscreen(
        bool coversMonitor,
        bool clientCoversMonitor,
        bool decoratedMaximized)
    {
        // A normal maximized window remains decorated. Its outer/client bounds can
        // still match rcMonitor with an auto-hidden taskbar, so geometry alone must
        // never override this style signal. Chromium's Windows fullscreen handler
        // removes WS_CAPTION and WS_THICKFRAME before sizing to rcMonitor.
        if (decoratedMaximized)
            return false;

        // Client geometry can lead DWM by one composition during Chromium's two-step
        // transition. Outer geometry remains a fallback for exclusive/borderless apps
        // whose client coordinates cannot be queried.
        return clientCoversMonitor || coversMonitor;
    }

    internal static int GeometryToleranceForDpi(uint dpi)
    {
        uint effectiveDpi = dpi == 0 ? 96u : dpi;
        return Math.Clamp((int)Math.Ceiling(8.0 * effectiveDpi / 96.0), 8, 32);
    }

    private static bool ClientCoversMonitor(IntPtr hWnd, User32.RECT monitorBounds, int tolerancePx)
    {
        if (!User32.GetClientRect(hWnd, out var client)) return false;

        var topLeft = new User32.POINT { X = client.Left, Y = client.Top };
        var bottomRight = new User32.POINT { X = client.Right, Y = client.Bottom };
        if (!User32.ClientToScreen(hWnd, ref topLeft) ||
            !User32.ClientToScreen(hWnd, ref bottomRight))
            return false;

        return MatchesMonitor(
            topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y,
            monitorBounds, tolerancePx);
    }

    private static bool MatchesMonitor(
        int left, int top, int right, int bottom,
        User32.RECT monitorBounds,
        int tolerancePx)
    {
        // Match all four edges, rather than merely accepting a rectangle which
        // contains the monitor. This prevents a borderless window spanning multiple
        // displays from being mistaken for fullscreen on its nearest monitor.
        return Math.Abs(left - monitorBounds.Left) <= tolerancePx &&
               Math.Abs(top - monitorBounds.Top) <= tolerancePx &&
               Math.Abs(right - monitorBounds.Right) <= tolerancePx &&
               Math.Abs(bottom - monitorBounds.Bottom) <= tolerancePx;
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
