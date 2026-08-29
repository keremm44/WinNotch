// WinNotch.Core/Interop/Shell32.cs
// WHY: Shell32.dll provides system-level utilities:
// - SHQueryUserNotificationState: Detect fullscreen/game mode
// - ShellExecute: Open file locations in Explorer
// - IShellWindows / IShellDispatch: Advanced shell operations
//
// PERFORMANCE NOTE: These are only called on user interaction.
// Zero idle cost.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

/// <summary>
/// P/Invoke declarations for shell32.dll.
/// Provides shell operations and fullscreen detection.
/// </summary>
internal static partial class Shell32
{
    private const string DllName = "shell32.dll";

    // ═══════════════════════════════════════════════════════════════
    // NOTIFICATION STATE (Fullscreen detection)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>User notification state values.</summary>
    public enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,       // Normal desktop mode
        QUNS_BUSY = 2,              // User is busy (fullscreen app)
        QUNS_RUNNING_D3D_FULL_SCREEN = 3, // Direct3D fullscreen (game)
        QUNS_PRESENTATION_MODE = 4, // Presentation mode
        QUNS_ACCEPTS_NOTIFICATIONS = 5, // Can show notifications
        QUNS_QUIET_TIME = 6,        // Quiet hours active
        QUNS_APP = 7                // App is in fullscreen
    }

    /// <summary>
    /// Queries whether the user is in a state where notifications should be shown.
    /// Use this to hide the notch during fullscreen games/movies.
    /// </summary>
    [LibraryImport(DllName)]
    public static partial int SHQueryUserNotificationState(
        out QUERY_USER_NOTIFICATION_STATE queryUserNotificationState);

    public static bool IsFullscreenModeActive()
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) != 0)
                return false;

            return state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY or
                QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN or
                QUERY_USER_NOTIFICATION_STATE.QUNS_APP;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SHELL OPERATIONS
    // ═══════════════════════════════════════════════════════════════

    [LibraryImport(DllName, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr ShellExecuteW(
        IntPtr hWnd,
        string? lpOperation,
        string lpFile,
        string? lpParameters,
        string? lpDirectory,
        int nShowCmd);

    public const int SW_SHOW = 5;

    /// <summary>
    /// Opens Explorer and selects a file (shows it in the file list).
    /// </summary>
    public static void OpenFileInExplorer(string filePath)
    {
        ShellExecuteW(
            IntPtr.Zero,
            "open",
            "explorer.exe",
            $"/select,\"{filePath}\"",
            null,
            SW_SHOW);
    }

    /// <summary>
    /// Opens a folder path in Explorer.
    /// </summary>
    public static void OpenFolder(string folderPath)
    {
        ShellExecuteW(IntPtr.Zero, "open", folderPath, null, null, SW_SHOW);
    }

    public const uint CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR = 0x4010;

    // Kept for documentation; taskbar class checks use User32.GetClassName.
    [LibraryImport(DllName, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_TrayWnd();
}
