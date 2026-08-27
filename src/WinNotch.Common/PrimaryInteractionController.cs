namespace WinNotch.Common;

/// <summary>
/// Pure decision layer for the notch's primary (left-click) interaction.
/// A primary click reveals or expands context; it never executes a destructive/external action directly.
/// </summary>
public static class PrimaryInteractionController
{
    public static PrimaryInteractionDecision Resolve(NotchState state) => state switch
    {
        NotchState.Idle or NotchState.Hover
            => new(PrimaryInteractionKind.OpenQuickPeek, NotchState.QuickPeek),

        NotchState.ShelfOccupied or NotchState.DropResult
            => new(PrimaryInteractionKind.ExpandShelf, NotchState.ShelfExpanded),

        NotchState.MediaAmbient
            => new(PrimaryInteractionKind.ExpandMedia, NotchState.MediaActive),

        NotchState.ClipboardNotify or NotchState.ScreenshotNotify
            => new(PrimaryInteractionKind.ExpandContextAction, state),

        NotchState.QuickPeek or NotchState.ShelfExpanded or NotchState.MediaActive
            => new(PrimaryInteractionKind.CollapseToPersistent, null),

        _ => new(PrimaryInteractionKind.None, null)
    };
}

public enum PrimaryInteractionKind
{
    None,
    OpenQuickPeek,
    ExpandShelf,
    ExpandMedia,
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
