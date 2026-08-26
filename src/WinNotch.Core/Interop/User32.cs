// WinNotch.Core/Interop/User32.cs
// WHY: User32.dll provides ALL window management APIs we need:
// - Window styles (WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE, WS_EX_LAYERED)
// - Window positioning (SetWindowPos, HWND_TOPMOST)
// - Hit testing (WM_NCHITTEST, HTTRANSPARENT)
// - Event hooks (SetWinEventHook for focus tracking)
// - Region management (SetWindowRgn for rounded-rect clipping)
//
// PERFORMANCE NOTE: These are static P/Invoke declarations.
// Zero runtime overhead until actually called. No managed heap allocation.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

/// <summary>
/// P/Invoke declarations for user32.dll.
/// Handles window styles, positioning, event hooks, and hit-testing.
/// </summary>
internal static partial class User32
{
    private const string DllName = "user32.dll";

    // ═══════════════════════════════════════════════════════════════
    // WINDOW STYLES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Extended window style: Tool window (hidden from Alt+Tab).</summary>
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>Extended window style: No activate (doesn't steal focus).</summary>
    public const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>Extended window style: Layered window (per-pixel alpha).</summary>
    public const int WS_EX_LAYERED = 0x00080000;

    /// <summary>Extended window style: Topmost window.</summary>
    public const int WS_EX_TOPMOST = 0x00000008;

    /// <summary>Window message: Non-client hit test.</summary>
    public const int WM_NCHITTEST = 0x0084;

    /// <summary>Hit test result: Transparent (click passes through).</summary>
    public const int HTTRANSPARENT = -1;

    /// <summary>Hit test result: Client area (click is handled).</summary>
    public const int HTCLIENT = 1;

    // ═══════════════════════════════════════════════════════════════
    // WINDOW POSITIONING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>HWND value representing the top of the Z-order.</summary>
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    /// <summary>SetWindowPos flags: Ignore Z-order position.</summary>
    public const uint SWP_NOMOVE = 0x0002;

    /// <summary>SetWindowPos flags: Ignore size.</summary>
    public const uint SWP_NOSIZE = 0x0001;

    /// <summary>SetWindowPos flags: Ignore Z-order.</summary>
    public const uint SWP_NOZORDER = 0x0004;

    /// <summary>SetWindowPos flags: Show window.</summary>
    public const uint SWP_SHOWWINDOW = 0x0040;

    /// <summary>SetWindowPos flags: No activate.</summary>
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>SetWindowPos flags: Force frame change (applies new styles).</summary>
    public const uint SWP_FRAMECHANGED = 0x0020;

    [LibraryImport(DllName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy,
        uint uFlags);

    [LibraryImport(DllName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy,
        SetWindowPosFlags uFlags);

    [Flags]
    public enum SetWindowPosFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOACTIVATE = 0x0010,
        SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080,
        SWP_FRAMECHANGED = 0x0020
    }

    // ═══════════════════════════════════════════════════════════════
    // WINDOW REGION (for rounded-rect clipping)
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateRoundRectRgn(
        int x1, int y1, int x2, int y2,
        int widthEllipse, int heightEllipse);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr hObject);

    // ═══════════════════════════════════════════════════════════════
    // WINDOW PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName, SetLastError = true)]
    public static partial IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    /// <summary>GetWindow: return next window in Z-order.</summary>
    public const uint GW_HWNDNEXT = 2;

    [LibraryImport(DllName, SetLastError = true)]
    public static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport(DllName, SetLastError = true)]
    public static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // WHY SetWindowLongPtr: On 64-bit Windows, SetWindowLong is limited to 32-bit.
    // SetWindowLongPtr handles both 32-bit and 64-bit correctly.
    [LibraryImport(DllName, SetLastError = true)]
    public static partial nint SetWindowLongPtr64(IntPtr hWnd, int nIndex, nint dwNewLong);

    public const int GWL_EXSTYLE = -20;

    /// <summary>
    /// Sets extended window style correctly on both 32-bit and 64-bit.
    /// </summary>
    public static void SetExtendedStyle(IntPtr hWnd, int exStyle)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GWL_EXSTYLE, exStyle);
        }
        else
        {
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        // Force window to redraw with new styles
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    // ═══════════════════════════════════════════════════════════════
    // EVENT HOOKS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>WinEventHook callback delegate.</summary>
    public delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);

    [LibraryImport(DllName)]
    public static partial IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate pfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>Event: Foreground window changed.</summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    /// <summary>Hook flag: out-of-context (no DLL injection needed).</summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    // ═══════════════════════════════════════════════════════════════
    // WINDOW TEXT / CLASS
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport(DllName, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport(DllName, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    // ═══════════════════════════════════════════════════════════════
    // WINDOW RECT
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    // ═══════════════════════════════════════════════════════════════
    // MONITOR APIs (replace System.Windows.Forms.Screen dependency)
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName)]
    public static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
