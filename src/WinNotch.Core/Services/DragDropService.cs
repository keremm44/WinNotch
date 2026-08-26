// WinNotch.Core/Services/DragDropService.cs
// WHY: Wraps WPF drag-drop events with file path extraction.
// Keeps an in-memory history of the last 5 drops (no disk I/O).
// The service itself is lightweight — it only processes data when
// a drag operation actually occurs.
//
// PERFORMANCE NOTE: Zero idle cost. Events are only processed
// during active drag operations (typically <1 second).

using System.Windows;
using WinNotch.Common;

namespace WinNotch.Core.Services;

/// <summary>
/// Event args for drag-drop operations.
/// </summary>
public sealed class DragDropEventArgs : EventArgs
{
    /// <summary>List of file/folder paths that were dropped.</summary>
    public required IReadOnlyList<string> DroppedPaths { get; init; }

    /// <summary>Whether this was a title-bar drag (for Module D window pinning).</summary>
    public bool IsTitleBarDrag { get; init; }

    /// <summary>Handle of the window being dragged (if title bar drag).</summary>
    public IntPtr DraggedWindowHandle { get; init; }
}

/// <summary>
/// High-level drag-drop service.
/// Manages file history and provides clean event interface for the UI.
/// </summary>
public sealed class DragDropService
{
    private readonly List<DropHistoryItem> _history = new();
    private readonly object _lock = new();

    /// <summary>
    /// Fired when files/folders are successfully dropped onto the notch.
    /// </summary>
    public event EventHandler<DragDropEventArgs>? FilesDropped;

    /// <summary>
    /// Fired when drag enters the notch area (for expansion animation).
    /// </summary>
    public event EventHandler? DragEntered;

    /// <summary>
    /// Fired when drag leaves the notch area (for contraction animation).
    /// </summary>
    public event EventHandler? DragLeft;

    /// <summary>
    /// Gets a read-only copy of the drop history.
    /// </summary>
    public IReadOnlyList<DropHistoryItem> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList(); // Snapshot for thread safety
            }
        }
    }

    /// <summary>
    /// Handles a file drop event from WPF DragDrop.
    /// Extracts paths, updates history, and fires events.
    /// </summary>
    public void HandleFileDrop(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            // Add to history (respecting max entries)
            lock (_lock)
            {
                foreach (var path in paths)
                {
                    var item = new DropHistoryItem
                    {
                        FilePath = path,
                        DisplayName = System.IO.Path.GetFileName(path) ?? path,
                        IsDirectory = System.IO.Directory.Exists(path),
                        Timestamp = DateTime.Now
                    };

                    _history.Insert(0, item);

                    // Enforce max history limit
                    while (_history.Count > Constants.MaxHistoryEntries)
                    {
                        _history.RemoveAt(_history.Count - 1);
                    }
                }
            }

            FilesDropped?.Invoke(this, new DragDropEventArgs
            {
                DroppedPaths = paths
            });
        }
    }

    /// <summary>
    /// Notifies that drag has entered the notch area.
    /// </summary>
    public void NotifyDragEnter() => DragEntered?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Notifies that drag has left the notch area.
    /// </summary>
    public void NotifyDragLeave() => DragLeft?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Clears the drop history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }
}
