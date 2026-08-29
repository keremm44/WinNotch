namespace WinNotch.Common;

/// <summary>
/// Pure decision policy for whether an incoming Windows drag can be accepted by
/// File Shelf. Keeping this decision outside WPF makes the edge cases testable
/// without requiring a live desktop session.
/// </summary>
public static class FileDropPolicy
{
    public static FileDropDecision Evaluate(
        bool moduleEnabled,
        bool draggingOut,
        bool hasFileDropFormat,
        bool copyAllowed)
    {
        if (!moduleEnabled)
            return new FileDropDecision(false, FileDropDecisionReason.ModuleDisabled);

        if (draggingOut)
            return new FileDropDecision(false, FileDropDecisionReason.InternalDragOut);

        if (!hasFileDropFormat)
            return new FileDropDecision(false, FileDropDecisionReason.FileDropFormatMissing);

        if (!copyAllowed)
            return new FileDropDecision(false, FileDropDecisionReason.CopyNotAllowed);

        return new FileDropDecision(true, FileDropDecisionReason.Accepted);
    }
}

public readonly record struct FileDropDecision(
    bool Accepted,
    FileDropDecisionReason Reason);

public enum FileDropDecisionReason
{
    Accepted = 0,
    ModuleDisabled = 1,
    InternalDragOut = 2,
    FileDropFormatMissing = 3,
    CopyNotAllowed = 4
}
