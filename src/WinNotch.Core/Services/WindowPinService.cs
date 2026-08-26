// WinNotch.Core/Services/WindowPinService.cs
// WHY: Module D - Window Pinner.
// When a title bar is dragged to the notch, pin the window as HWND_TOPMOST.
// Uses SetWindowPos for reliable topmost behavior.
//
// PERFORMANCE NOTE: Only active during drag operations.
// No idle cost. Pinned windows are tracked in a HashSet for O(1) lookup.
//
// LIMITATION: Windows 10+ limits the number of topmost windows.
// We track our pinned windows and can unpin them all on exit.

using WinNotch.Core.Interop;

namespace WinNotch.Core.Services;

/// <summary>
/// Event args for window pin/unpin operations.
/// </summary>
public sealed class WindowPinEventArgs : EventArgs
{
    /// <summary>Handle of the pinned/unpinned window.</summary>
    public IntPtr WindowHandle { get; init; }

    /// <summary>Title of the window.</summary>
    public string WindowTitle { get; init; } = string.Empty;

    /// <summary>True if window was pinned, false if unpinned.</summary>
    public bool IsPinned { get; init; }
}

/// <summary>
/// Manages always-on-top pinning for windows dragged to the notch.
/// </summary>
public sealed class WindowPinService : IDisposable
{
    private readonly HashSet<IntPtr> _pinnedWindows = new();
    private bool _disposed;

    /// <summary>
    /// Fired when a window is pinned or unpinned.
    /// </summary>
    public event EventHandler<WindowPinEventArgs>? WindowPinChanged;

    /// <summary>
    /// Gets the number of currently pinned windows.
    /// </summary>
    public int PinnedCount => _pinnedWindows.Count;

    /// <summary>
    /// Checks if a window handle is currently pinned by us.
    /// </summary>
    public bool IsPinned(IntPtr hWnd) => _pinnedWindows.Contains(hWnd);

    /// <summary>
    /// Pins a window to always-on-top.
    /// </summary>
    /// <param name="hWnd">Handle of the window to pin.</param>
    /// <returns>True if successfully pinned.</returns>
    public bool PinWindow(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero) return false;

        try
        {
            bool success = User32.SetWindowPos(
                hWnd,
                User32.HWND_TOPMOST,
                0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

            if (success)
            {
                _pinnedWindows.Add(hWnd);
                string title = GetWindowTitle(hWnd);

                WindowPinChanged?.Invoke(this, new WindowPinEventArgs
                {
                    WindowHandle = hWnd,
                    WindowTitle = title,
                    IsPinned = true
                });
            }

            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowPinService] Error pinning window: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unpins a previously pinned window (removes HWND_TOPMOST).
    /// </summary>
    public bool UnpinWindow(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero) return false;

        try
        {
            // Set to HWND_NOTOPMOST to remove topmost flag
            bool success = User32.SetWindowPos(
                hWnd,
                new IntPtr(-2), // HWND_NOTOPMOST
                0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

            if (success)
            {
                _pinnedWindows.Remove(hWnd);
                string title = GetWindowTitle(hWnd);

                WindowPinChanged?.Invoke(this, new WindowPinEventArgs
                {
                    WindowHandle = hWnd,
                    WindowTitle = title,
                    IsPinned = false
                });
            }

            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowPinService] Error unpinning window: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Toggles pin state for a window.
    /// </summary>
    public bool TogglePin(IntPtr hWnd)
    {
        return IsPinned(hWnd) ? UnpinWindow(hWnd) : PinWindow(hWnd);
    }

    /// <summary>
    /// Unpins all windows. Called on application exit to clean up.
    /// </summary>
    public void UnpinAll()
    {
        var handles = _pinnedWindows.ToList();
        foreach (var hWnd in handles)
        {
            UnpinWindow(hWnd);
        }
    }

    /// <summary>
    /// Gets the title of a window.
    /// </summary>
    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = User32.GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var buffer = new char[length + 1];
        User32.GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnpinAll();
        GC.SuppressFinalize(this);
    }

    ~WindowPinService() => Dispose();
}
