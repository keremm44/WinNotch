// WinNotch.Common/NotchState.cs
// WHY: Central state enum drives the entire UI transition system.
// Each state maps to a specific visual configuration and behavior set.

namespace WinNotch.Common;

/// <summary>
/// Represents the current visual/functional state of the notch widget.
/// Drives animation triggers and which content is displayed.
/// </summary>
public enum NotchState
{
    /// <summary>
    /// Default idle state. Small 130x28 black pill at screen top-center.
    /// No modules active, minimal resource usage.
    /// </summary>
    Idle,

    /// <summary>
    /// Mouse is hovering over the notch area. Slight visual feedback.
    /// </summary>
    Hover,

    /// <summary>
    /// A file/folder is being dragged over the notch. Expanded to show drop zone.
    /// Module A active.
    /// </summary>
    DragActive,

    /// <summary>
    /// Active media session detected via SMTC. Shows album art + controls.
    /// Module C active.
    /// </summary>
    MediaActive,

    /// <summary>
    /// Clipboard change detected. Brief flash notification (1.5s).
    /// Module B active.
    /// </summary>
    ClipboardNotify,

    /// <summary>
    /// Screenshot detected in clipboard (Win+Shift+S).
    /// Module E active.
    /// </summary>
    ScreenshotNotify,

    /// <summary>
    /// Window pin operation in progress or pin badge showing.
    /// Module D active.
    /// </summary>
    WindowPinned
}
