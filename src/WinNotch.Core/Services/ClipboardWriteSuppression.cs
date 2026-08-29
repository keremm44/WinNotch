namespace WinNotch.Core.Services;

/// <summary>
/// One-shot guard for clipboard writes initiated by WinNotch itself.
/// The guard is armed before the Windows clipboard mutation so a synchronous or
/// immediately queued WM_CLIPBOARDUPDATE cannot race ahead of suppression state.
/// </summary>
internal sealed class ClipboardWriteSuppression
{
    private readonly object _lock = new();
    private string? _pendingText;
    private bool _pendingImage;

    internal bool HasPendingText
    {
        get
        {
            lock (_lock)
                return _pendingText != null;
        }
    }

    internal void ArmText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_lock)
            _pendingText = text;
    }

    internal void CancelText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_lock)
        {
            if (string.Equals(_pendingText, text, StringComparison.Ordinal))
                _pendingText = null;
        }
    }

    internal bool ConsumeText(string? observedText)
    {
        lock (_lock)
        {
            string? expected = _pendingText;
            _pendingText = null;
            return expected != null &&
                   string.Equals(expected, observedText, StringComparison.Ordinal);
        }
    }

    internal void ClearText()
    {
        lock (_lock)
            _pendingText = null;
    }

    internal void ArmImage()
    {
        lock (_lock)
            _pendingImage = true;
    }

    internal void CancelImage()
    {
        lock (_lock)
            _pendingImage = false;
    }

    internal bool ConsumeImage()
    {
        lock (_lock)
        {
            bool pending = _pendingImage;
            _pendingImage = false;
            return pending;
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _pendingText = null;
            _pendingImage = false;
        }
    }
}
