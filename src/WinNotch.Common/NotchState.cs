// WinNotch.Common/NotchState.cs
// Central state enum for contextual surface transitions.

namespace WinNotch.Common;

public enum NotchState
{
    Idle,
    Hover,
    CommandHub,
    DragActive,
    DropResult,
    ShelfOccupied,
    ShelfExpanded,
    ShelfDraggingOut,
    MediaActive,
    MediaAmbient,
    ClipboardNotify,
    ScreenshotNotify,
    TimerNotify
}
