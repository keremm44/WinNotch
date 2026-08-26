// WinNotch.Core/Services/ClipboardService.cs
// WHY: Wraps the native ClipboardListener in a clean service interface.
// Handles privacy checks (password manager detection), content type detection,
// and provides a simple API for the UI layer to subscribe to.
//
// PERFORMANCE NOTE: Zero idle cost. The native listener is event-driven.
// This service just relays events — no polling, no timers, no threads.
//
// PRIVACY: Respects CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR.
// Password managers (1Password, Bitwarden, etc.) set this flag on clipboard
// entries containing passwords. We MUST NOT show notifications for these.

using WinNotch.Common;
using WinNotch.Core.Interop;

namespace WinNotch.Core.Services;

/// <summary>
/// High-level clipboard monitoring service.
/// Wraps the native Win32 clipboard listener with content detection and privacy filtering.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private readonly ClipboardListener _listener;
    private bool _disposed;

    /// <summary>
    /// Fired when a clipboard change is detected and should be shown in the UI.
    /// Null event args means the change was excluded (privacy filter).
    /// </summary>
    public event EventHandler<ClipboardNotification>? NotificationRequested;

    /// <summary>
    /// Fired when clipboard contains an image that should be previewed.
    /// </summary>
    public event EventHandler<ClipboardImageNotification>? ImageNotificationRequested;

    public ClipboardService()
    {
        _listener = new ClipboardListener();
        _listener.ClipboardChanged += OnClipboardChanged;
    }

    /// <summary>
    /// Starts listening for clipboard changes.
    /// Must be called with the WPF window's HWND after it's created.
    /// </summary>
    public bool Start(IntPtr hWnd) => _listener.StartListening(hWnd);

    /// <summary>
    /// Handles native clipboard change events.
    /// </summary>
    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        // PRIVACY: Skip notifications for excluded content (password managers)
        if (e.IsExcluded)
        {
            System.Diagnostics.Debug.WriteLine(
                "[ClipboardService] Clipboard change excluded by privacy flag.");
            return;
        }

        if (e.HasImage)
        {
            // Read clipboard image for thumbnail preview
            var image = ReadClipboardImage();
            if (image != null)
            {
                ImageNotificationRequested?.Invoke(this, new ClipboardImageNotification
                {
                    Image = image,
                    Timestamp = DateTime.Now
                });
            }
        }
        else if (e.HasText)
        {
            string? text = ReadClipboardText();
            NotificationRequested?.Invoke(this, new ClipboardNotification
            {
                PreviewText = text?.Length > 50 ? text[..50] + "..." : text,
                Timestamp = DateTime.Now,
                IsImage = false
            });
        }
    }

    /// <summary>
    /// Reads text from clipboard (non-allocating when possible).
    /// </summary>
    private static string? ReadClipboardText()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                return System.Windows.Clipboard.GetText();
            }
        }
        catch
        {
            // Clipboard might be locked — skip
        }
        return null;
    }

    /// <summary>
    /// Reads image from clipboard and converts to BitmapSource for thumbnail.
    /// </summary>
    private static System.Windows.Media.Imaging.BitmapSource? ReadClipboardImage()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsImage())
            {
                return System.Windows.Clipboard.GetImage();
            }
        }
        catch
        {
            // Clipboard might be locked — skip
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener.ClipboardChanged -= OnClipboardChanged;
        _listener.Dispose();
        GC.SuppressFinalize(this);
    }

    ~ClipboardService() => Dispose();
}

/// <summary>
/// Clipboard notification data for text content.
/// </summary>
public sealed class ClipboardNotification
{
    public string? PreviewText { get; init; }
    public DateTime Timestamp { get; init; }
    public bool IsImage { get; init; }
}

/// <summary>
/// Clipboard notification data for image content.
/// </summary>
public sealed class ClipboardImageNotification
{
    public System.Windows.Media.Imaging.BitmapSource? Image { get; init; }
    public DateTime Timestamp { get; init; }
}
