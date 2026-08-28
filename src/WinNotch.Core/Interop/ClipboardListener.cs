// WinNotch.Core/Interop/ClipboardListener.cs
// Event-driven Win32 clipboard monitoring via AddClipboardFormatListener.

using System.Runtime.InteropServices;

namespace WinNotch.Core.Interop;

public sealed class ClipboardChangedEventArgs : EventArgs
{
    public bool IsExcluded { get; init; }
    public bool HasImage { get; init; }
    public bool HasText { get; init; }
}

public sealed partial class ClipboardListener : IDisposable
{
    private const string DllName = "user32.dll";

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(IntPtr hWnd);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(IntPtr hWnd);

    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const uint CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR = 0x4010;

    internal static bool IsCurrentContentExcluded()
        => NativeMethods.IsClipboardFormatAvailable(CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR);

    private IntPtr _hWnd;
    private bool _isListening;
    private bool _disposed;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public bool StartListening(IntPtr hWnd)
    {
        if (_disposed) return false;
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

    public void OnClipboardUpdate()
    {
        if (_disposed || !_isListening) return;

        try
        {
            bool isExcluded = NativeMethods.IsClipboardFormatAvailable(CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR);
            bool hasImage = NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_BITMAP) ||
                            NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIB) ||
                            NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIBV5);
            bool hasText = NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT);

            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
            {
                IsExcluded = isExcluded,
                HasImage = hasImage,
                HasText = hasText
            });
        }
        catch
        {
            // Clipboard can be temporarily locked by another process.
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;

        RemoveClipboardFormatListener(_hWnd);
        _isListening = false;
        _hWnd = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Stop first; StopListening must still be allowed to unregister the HWND.
        StopListening();
        ClipboardChanged = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

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
