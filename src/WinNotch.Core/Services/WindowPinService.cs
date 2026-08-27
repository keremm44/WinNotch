// WinNotch.Core/Services/WindowPinService.cs
// Lightweight always-on-top window management with explicit lifecycle cleanup.

using WinNotch.Core.Interop;

namespace WinNotch.Core.Services;

public sealed class WindowPinEventArgs : EventArgs
{
    public IntPtr WindowHandle { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
}

public sealed record PinnedWindowInfo(IntPtr WindowHandle, string WindowTitle);

public sealed class WindowPinService : IDisposable
{
    private readonly HashSet<IntPtr> _pinnedWindows = new();
    private bool _disposed;

    public event EventHandler<WindowPinEventArgs>? WindowPinChanged;

    public int PinnedCount
    {
        get
        {
            PruneClosedWindows();
            return _pinnedWindows.Count;
        }
    }

    public bool IsPinned(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero) return false;
        PruneClosedWindows();
        return _pinnedWindows.Contains(hWnd);
    }

    public IReadOnlyList<PinnedWindowInfo> GetPinnedWindows()
    {
        if (_disposed) return Array.Empty<PinnedWindowInfo>();

        PruneClosedWindows();
        return _pinnedWindows
            .Select(hWnd => new PinnedWindowInfo(hWnd, GetWindowTitle(hWnd)))
            .OrderBy(info => info.WindowTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public bool PinWindow(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero || !IsValidWindow(hWnd)) return false;

        try
        {
            bool success = User32.SetWindowPos(
                hWnd,
                User32.HWND_TOPMOST,
                0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE |
                User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

            if (!success) return false;

            bool newlyPinned = _pinnedWindows.Add(hWnd);
            if (newlyPinned)
            {
                WindowPinChanged?.Invoke(this, new WindowPinEventArgs
                {
                    WindowHandle = hWnd,
                    WindowTitle = GetWindowTitle(hWnd),
                    IsPinned = true
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowPinService] Error pinning window: {ex.Message}");
            return false;
        }
    }

    public bool UnpinWindow(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero) return false;

        // A closed window does not need a native NOTOPMOST call. Remove the stale
        // handle and report success so UI state can clean itself up.
        if (!IsValidWindow(hWnd))
        {
            _pinnedWindows.Remove(hWnd);
            return true;
        }

        try
        {
            bool success = User32.SetWindowPos(
                hWnd,
                new IntPtr(-2), // HWND_NOTOPMOST
                0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE |
                User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

            if (!success) return false;

            bool wasPinned = _pinnedWindows.Remove(hWnd);
            if (wasPinned)
            {
                WindowPinChanged?.Invoke(this, new WindowPinEventArgs
                {
                    WindowHandle = hWnd,
                    WindowTitle = GetWindowTitle(hWnd),
                    IsPinned = false
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WindowPinService] Error unpinning window: {ex.Message}");
            return false;
        }
    }

    public bool TogglePin(IntPtr hWnd)
        => IsPinned(hWnd) ? UnpinWindow(hWnd) : PinWindow(hWnd);

    public void UnpinAll()
    {
        if (_disposed) return;

        foreach (IntPtr hWnd in _pinnedWindows.ToArray())
            UnpinWindow(hWnd);

        // If a native call failed because the target disappeared mid-loop, do not
        // retain stale bookkeeping after an explicit clear request.
        PruneClosedWindows();
    }

    private void PruneClosedWindows()
    {
        if (_pinnedWindows.Count == 0) return;

        foreach (IntPtr hWnd in _pinnedWindows.ToArray())
        {
            if (!IsValidWindow(hWnd))
                _pinnedWindows.Remove(hWnd);
        }
    }

    private static bool IsValidWindow(IntPtr hWnd)
        => hWnd != IntPtr.Zero && User32.GetWindowRect(hWnd, out _);

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = User32.GetWindowTextLength(hWnd);
        if (length <= 0) return "Adsız pencere";

        var buffer = new char[length + 1];
        User32.GetWindowText(hWnd, buffer, buffer.Length);
        string title = new(buffer, 0, length);
        return string.IsNullOrWhiteSpace(title) ? "Adsız pencere" : title;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Unpin BEFORE marking disposed; UnpinWindow intentionally refuses work
        // after disposal. The previous order left windows stuck as TOPMOST.
        UnpinAll();
        _disposed = true;
        _pinnedWindows.Clear();
        WindowPinChanged = null;
        GC.SuppressFinalize(this);
    }
}
