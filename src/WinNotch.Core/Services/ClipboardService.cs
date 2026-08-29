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
    private readonly ClipboardWriteSuppression _writeSuppression = new();
    private volatile bool _monitorText = true;
    private volatile bool _monitorImages = true;
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
    /// Prevents expensive clipboard payload materialization for disabled modules.
    /// Format detection stays native and allocation-free; only requested payloads
    /// cross into managed/WPF memory.
    /// </summary>
    public void SetContentPreferences(bool monitorText, bool monitorImages)
    {
        _monitorText = monitorText;
        _monitorImages = monitorImages;
    }

    /// <summary>
    /// Called by MainWindow WndProc when WM_CLIPBOARDUPDATE arrives.
    /// WHY: The native ClipboardListener registers our HWND but Windows sends
    /// the message to WndProc, not to the listener directly. We must forward it.
    /// </summary>
    public void OnClipboardUpdate() => _listener.OnClipboardUpdate();

    /// <summary>
    /// Arms suppression for a WinNotch-originated text write. Call this BEFORE
    /// mutating the Windows clipboard so the clipboard update cannot win the race.
    /// </summary>
    public void SuppressNextTextNotification(string text)
        => _writeSuppression.ArmText(text);

    /// <summary>
    /// Rolls back an armed text suppression when the clipboard write itself fails.
    /// It only clears the matching pending write and cannot cancel a newer write.
    /// </summary>
    public void CancelTextNotificationSuppression(string text)
        => _writeSuppression.CancelText(text);

    /// <summary>Arms suppression for a WinNotch-originated image write.</summary>
    public void SuppressNextImageNotification()
        => _writeSuppression.ArmImage();

    /// <summary>Rolls back image suppression when the clipboard write fails.</summary>
    public void CancelImageNotificationSuppression()
        => _writeSuppression.CancelImage();

    public static bool TryReadSafeText(out string? text)
    {
        text = null;
        try
        {
            if (ClipboardListener.IsCurrentContentExcluded() ||
                !System.Windows.Clipboard.ContainsText())
                return false;
            text = System.Windows.Clipboard.GetText();
            return !string.IsNullOrEmpty(text);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles native clipboard change events.
    /// </summary>
    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        // PRIVACY: Skip notifications for excluded content (password managers).
        // Also invalidate any one-shot self-write guard because this clipboard event
        // belongs to protected external content, not the pending WinNotch payload.
        if (e.IsExcluded)
        {
            _writeSuppression.Clear();
            System.Diagnostics.Debug.WriteLine(
                "[ClipboardService] Clipboard change excluded by privacy flag.");
            return;
        }

        if (e.HasImage)
        {
            _writeSuppression.ClearText();
            if (_writeSuppression.ConsumeImage())
                return;

            if (_monitorImages)
            {
                // Clipboard images can be tens of megabytes. Never materialize one
                // when the screenshot module is disabled.
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
            return;
        }

        if (e.HasText)
        {
            _writeSuppression.CancelImage();

            // A pending self-write must be consumed even when text monitoring is
            // currently disabled; otherwise stale suppression could survive until a
            // later user copy. Only materialize text in that disabled case when the
            // one-shot guard actually needs comparison.
            bool needsText = _monitorText || _writeSuppression.HasPendingText;
            string? text = needsText ? ReadClipboardText() : null;
            if (_writeSuppression.ConsumeText(text))
                return;
            if (!_monitorText)
                return;

            NotificationRequested?.Invoke(this, new ClipboardNotification
            {
                RawText = text,
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
        _writeSuppression.Clear();
        _listener.ClipboardChanged -= OnClipboardChanged;
        _listener.Dispose();
        NotificationRequested = null;
        ImageNotificationRequested = null;
    }
}

/// <summary>
/// Clipboard notification data for text content.
/// RawText is kept separately from PreviewText so actions never operate on a truncated value.
/// </summary>
public sealed class ClipboardNotification
{
    public string? RawText { get; init; }
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
