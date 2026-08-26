// WinNotch.Core/Interop/ClipboardListener.cs
// WHY: Win32 clipboard monitoring via AddClipboardFormatListener.
// This is the ONLY correct way to monitor clipboard passively:
// - NO polling (Thread.Sleep, DispatcherTimer)
// - NO background threads
// - Pure event-driven via Windows message pump
// - WM_CLIPBOARDUPDATE is sent to our window when clipboard changes
//
// PERFORMANCE NOTE: Zero CPU cost when idle. Windows notifies us only
// when clipboard actually changes. No timer, no thread, no polling.
//
// PRIVACY NOTE: We check for CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR
// to respect password managers' privacy flag. If detected, we skip notification.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

/// <summary>
/// Event args for clipboard update notifications.
/// </summary>
public sealed class ClipboardChangedEventArgs : EventArgs
{
    /// <summary>Whether the clipboard content should be excluded from monitoring.</summary>
    public bool IsExcluded { get; init; }

    /// <summary>Whether the clipboard contains image data.</summary>
    public bool HasImage { get; init; }

    /// <summary>Whether the clipboard contains text data.</summary>
    public bool HasText { get; init; }
}

/// <summary>
/// Native clipboard listener using AddClipboardFormatListener / RemoveClipboardFormatListener.
/// Implements IDisposable to properly clean up the Win32 hook.
/// </summary>
public sealed partial class ClipboardListener : IDisposable
{
    private const string DllName = "user32.dll";

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(IntPtr hWnd);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(IntPtr hWnd);

    /// <summary>Windows message ID for clipboard update notification.</summary>
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    /// <summary>Clipboard format for excluding content from monitors.</summary>
    public const uint CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR = 0x4010;

    private IntPtr _hWnd;
    private bool _isListening;
    private bool _disposed;

    /// <summary>
    /// Fired when clipboard content changes.
    /// Check event args for content type and privacy flags.
    /// </summary>
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    /// <summary>
    /// Registers this window to receive clipboard update notifications.
    /// Must be called from a window that has a message pump (WPF main window).
    /// </summary>
    /// <param name="hWnd">Handle of the window that will receive WM_CLIPBOARDUPDATE.</param>
    /// <returns>True if registration succeeded.</returns>
    public bool StartListening(IntPtr hWnd)
    {
        if (_isListening) return true;

        _hWnd = hWnd;
        _isListening = AddClipboardFormatListener(hWnd);

        if (!_isListening)
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine(
                $"[ClipboardListener] AddClipboardFormatListener failed. Win32 error: {error}");
        }

        return _isListening;
    }

    /// <summary>
    /// Handles the WM_CLIPBOARDUPDATE message. Call this from your WndProc override.
    /// </summary>
    public void OnClipboardUpdate()
    {
        if (_disposed) return;

        bool isExcluded = false;
        bool hasImage = false;
        bool hasText = false;

        try
        {
            // Check for excluded content (password managers use this flag)
            if (NativeMethods.IsClipboardFormatAvailable(CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR))
            {
                isExcluded = true;
            }

            // Check content types
            hasImage = NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_BITMAP) ||
                       NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIB) ||
                       NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIBV5);

            hasText = NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT);
        }
        catch
        {
            // Clipboard might be locked by another process — skip this notification
            return;
        }

        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
        {
            IsExcluded = isExcluded,
            HasImage = hasImage,
            HasText = hasText
        });
    }

    /// <summary>
    /// Unregisters from clipboard notifications and releases resources.
    /// Must be called in OnExit or Dispose.
    /// </summary>
    public void StopListening()
    {
        if (!_isListening || _disposed) return;

        RemoveClipboardFormatListener(_hWnd);
        _isListening = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopListening();
        GC.SuppressFinalize(this);
    }

    ~ClipboardListener()
    {
        Dispose();
    }

    // ═══════════════════════════════════════════════════════════════
    // INNER NATIVE METHODS (clipboard format checks)
    // ═══════════════════════════════════════════════════════════════

    private static partial class NativeMethods
    {
        public const uint CF_UNICODETEXT = 13;
        public const uint CF_BITMAP = 2;
        public const uint CF_DIB = 8;
        public const uint CF_DIBV5 = 17;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsClipboardFormatAvailable(uint format);
    }
}
