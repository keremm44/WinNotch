// WinNotch.Common/NotchState.cs
// WHY: Central state enum drives the entire UI transition system.
// Each state maps to a specific visual configuration and behavior set.

namespace WinNotch.Common;

public enum NotchState
{
    Idle,
    Hover,
    DragActive,
    DropResult,
    ShelfOccupied,
    ShelfExpanded,
    ShelfDraggingOut,
    MediaActive,
    MediaAmbient,
    ClipboardNotify,
    ScreenshotNotify,
    WindowPinned
}
