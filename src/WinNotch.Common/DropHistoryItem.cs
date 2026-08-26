// WinNotch.Common/DropHistoryItem.cs
// WHY: In-memory history for drag-drop operations. No disk I/O.
// Limited to MaxHistoryEntries to prevent memory growth.

namespace WinNotch.Common;

/// <summary>
/// Represents a single drag-drop history entry.
/// Stored in memory only — never persisted to disk.
/// </summary>
public sealed record DropHistoryItem
{
    /// <summary>Full path of the dropped file/folder.</summary>
    public required string FilePath { get; init; }

    /// <summary>Display name (filename or folder name).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether this entry is a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Timestamp when the drop occurred.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
