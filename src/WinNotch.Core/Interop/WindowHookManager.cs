// WinNotch.Core/Interop/WindowHookManager.cs
// WHY: SetWinEventHook provides efficient foreground window tracking.
// Instead of polling with timers, Windows calls our callback when
// the foreground window changes. This is the event-driven approach.
//
// PERFORMANCE NOTE: Zero CPU cost when no window changes occur.
// The hook is OUTOFCONTEXT — no DLL injection, no cross-process overhead.
//
// USAGE:
// - Detect fullscreen apps → auto-hide notch (Module: Fullscreen detection)
// - Track active window → optional focus-aware display
// - Detect pin-eligible windows → Module D (Window Pinner)

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

/// <summary>
/// Event args for foreground window change notifications.
/// </summary>
public sealed class ForegroundChangedEventArgs : EventArgs
{
    /// <summary>Handle of the new foreground window.</summary>
    public IntPtr WindowHandle { get; init; }

    /// <summary>Title of the new foreground window.</summary>
    public string WindowTitle { get; init; } = string.Empty;

    /// <summary>Class name of the new foreground window.</summary>
    public string ClassName { get; init; } = string.Empty;
}

/// <summary>
/// Manages Win32 event hooks for window focus tracking.
/// Implements IDisposable to properly clean up hooks.
/// </summary>
public sealed class WindowHookManager : IDisposable
{
    private IntPtr _foregroundHook;
    private User32.WinEventDelegate? _foregroundCallback;
    private bool _disposed;

    /// <summary>
    /// Fired when the foreground window changes.
    /// Used for fullscreen detection and focus-aware features.
    /// </summary>
    public event EventHandler<ForegroundChangedEventArgs>? ForegroundWindowChanged;

    /// <summary>
    /// Starts monitoring foreground window changes.
    /// </summary>
    public bool StartTracking()
    {
        if (_foregroundHook != IntPtr.Zero) return true;

        // Keep delegate alive to prevent GC from collecting it
        _foregroundCallback = OnForegroundChanged;

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
                $"[WindowHookManager] SetWinEventHook failed. Win32 error: {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Callback invoked by Windows when foreground window changes.
    /// </summary>
    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero) return;

        try
        {
            string title = GetWindowTitle(hwnd);
            string className = GetWindowClassName(hwnd);

            ForegroundWindowChanged?.Invoke(this, new ForegroundChangedEventArgs
            {
                WindowHandle = hwnd,
                WindowTitle = title,
                ClassName = className
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowHookManager] Error in foreground callback: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a window is in fullscreen mode by comparing its bounds to screen bounds.
    /// </summary>
    public static bool IsWindowFullscreen(IntPtr hWnd)
    {
        try
        {
            // Get extended frame bounds (DWM-aware, respects fullscreen)
            if (DwmApi.DwmGetWindowAttribute(hWnd, DwmApi.DWMWA_EXTENDED_FRAME_BOUNDS,
                out DwmApi.RECT frameRect, (uint)Marshal.SizeOf<DwmApi.RECT>()))
            {
                // Get the screen bounds
                var screen = System.Windows.Forms.Screen.FromHandle(hWnd);
                if (screen != null)
                {
                    var bounds = screen.Bounds;
                    // Window covers the entire screen
                    return frameRect.Left <= bounds.Left &&
                           frameRect.Top <= bounds.Top &&
                           frameRect.Right >= bounds.Right &&
                           frameRect.Bottom >= bounds.Bottom;
                }
            }
        }
        catch
        {
            // Ignore errors — assume not fullscreen
        }

        return false;
    }

    /// <summary>
    /// Gets the title text of a window.
    /// </summary>
    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = User32.GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var buffer = new char[length + 1];
        User32.GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    /// <summary>
    /// Gets the window class name.
    /// </summary>
    private static string GetWindowClassName(IntPtr hWnd)
    {
        var buffer = new char[256];
        User32.GetClassName(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    /// <summary>
    /// Stops tracking and cleans up hooks.
    /// </summary>
    public void StopTracking()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            User32.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTracking();
        GC.SuppressFinalize(this);
    }

    ~WindowHookManager()
    {
        Dispose();
    }
}
