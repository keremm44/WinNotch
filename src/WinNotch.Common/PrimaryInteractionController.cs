namespace WinNotch.Common;

/// <summary>
/// Pure decision layer for the notch's primary (left-click) interaction.
/// Hover owns media expansion, drag owns File Shelf, and an unclaimed primary click
/// opens the compact Command Hub.
/// </summary>
public static class PrimaryInteractionController
{
    public static PrimaryInteractionDecision Resolve(NotchState state) => state switch
    {
        NotchState.Idle or NotchState.Hover or
        NotchState.MediaAmbient or NotchState.MediaActive or
        NotchState.ShelfOccupied or NotchState.ShelfExpanded or NotchState.DropResult or
        NotchState.TimerNotify
            => new(PrimaryInteractionKind.OpenCommandHub, NotchState.CommandHub),

        // Notifications remain contextual: their background click reveals the
        // already-resolved action rather than replacing an active notification.
        NotchState.ClipboardNotify or NotchState.ScreenshotNotify
            => new(PrimaryInteractionKind.ExpandContextAction, state),

        NotchState.CommandHub
            => new(PrimaryInteractionKind.CollapseToPersistent, null),

        // Drag gestures own these states exclusively.
        NotchState.DragActive or NotchState.ShelfDraggingOut
            => new(PrimaryInteractionKind.None, null),

        _ => new(PrimaryInteractionKind.None, null)
    };
}

public enum PrimaryInteractionKind
{
    None,
    OpenCommandHub,
    ExpandContextAction,
    CollapseToPersistent
}

public readonly record struct PrimaryInteractionDecision(
    PrimaryInteractionKind Kind,
    NotchState? TargetState);

/// <summary>
/// Process-lifetime cache of one actionable clipboard context. This is intentionally
/// not a clipboard-history feature and is never persisted to disk.
/// </summary>
public sealed class LastMeaningfulClipboardContextCache
{
    public LastMeaningfulClipboardContext? Current { get; private set; }

    public bool TryRemember(
        ClipboardContentType contentType,
        string? rawText,
        string? previewText,
        DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        ContextAction? action = ContextActionResolver.ResolveClipboard(contentType, rawText);
        if (action == null)
            return false;

        Current = new LastMeaningfulClipboardContext(
            contentType,
            rawText,
            previewText ?? rawText,
            timestamp,
            action);
        return true;
    }

    public void Clear() => Current = null;
}

public sealed record LastMeaningfulClipboardContext(
    ClipboardContentType ContentType,
    string RawText,
    string PreviewText,
    DateTime Timestamp,
    ContextAction Action);
