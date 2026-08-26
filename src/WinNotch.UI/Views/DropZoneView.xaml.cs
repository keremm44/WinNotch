using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;
using WinNotch.Core.Interop;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

/// <summary>
/// Persistent lightweight file shelf.
/// Holds metadata only; file contents are never loaded into WinNotch memory.
/// </summary>
public partial class DropZoneView : UserControl
{
    private HeldItem[] _items = Array.Empty<HeldItem>();
    private Point _dragStart;

    public event EventHandler? ShelfCleared;

    public bool HasItems => _items.Length > 0;
    public IReadOnlyList<HeldItem> Items => _items;

    public DropZoneView()
    {
        InitializeComponent();
    }

    public void SetDroppedPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        _items = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Take(Constants.MaxShelfItems)
            .Select(HeldItem.FromPath)
            .ToArray();

        RenderShelf();
    }

    public void SetExpanded(bool expanded)
    {
        ActionButtons.Visibility = expanded && HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;

        RemoveButton.Visibility = HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void ShowDropTarget()
    {
        FileIcon.Text = "+";
        DropTargetText.Text = "Dosyayı buraya bırak";
        FileSummaryText.Text = "WinNotch burada tutacak";
        ActionButtons.Visibility = Visibility.Collapsed;
        RemoveButton.Visibility = Visibility.Collapsed;
    }

    private void RenderShelf()
    {
        if (_items.Length == 0)
        {
            ShowDropTarget();
            return;
        }

        RemoveButton.Visibility = Visibility.Visible;

        if (_items.Length == 1)
        {
            HeldItem item = _items[0];
            FileIcon.Text = item.IsDirectory ? "D" : GetExtensionLabel(item.SourcePath);
            DropTargetText.Text = item.DisplayName;
            FileSummaryText.Text = item.Exists
                ? FormatSummary(item)
                : "Source unavailable";
            return;
        }

        FileIcon.Text = _items.Length.ToString();
        DropTargetText.Text = $"{_items.Length} items";

        long knownBytes = _items.Where(i => i.SizeBytes.HasValue).Sum(i => i.SizeBytes!.Value);
        FileSummaryText.Text = knownBytes > 0
            ? $"{FormatSize(knownBytes)} · drag out or copy"
            : "drag out or copy";
    }

    private static string FormatSummary(HeldItem item)
    {
        if (item.IsDirectory) return "folder · drag out or copy";
        if (item.SizeBytes is long size) return $"{FormatSize(size)} · drag out or copy";
        return "drag out or copy";
    }

    private static string GetExtensionLabel(string path)
    {
        string ext = System.IO.Path.GetExtension(path).TrimStart('.');
        if (string.IsNullOrWhiteSpace(ext)) return "F";
        return ext.Length <= 3 ? ext.ToUpperInvariant() : ext[..3].ToUpperInvariant();
    }

    // Put the actual files on the Windows clipboard, not their path text.
    // Explorer and other shell targets can then use Ctrl+V normally.
    private void CopyFilesButton_Click(object sender, RoutedEventArgs e)
    {
        string[] validPaths = GetValidPaths();
        if (validPaths.Length == 0)
        {
            ShowActionFeedback("Source unavailable");
            return;
        }

        try
        {
            var files = new StringCollection();
            files.AddRange(validPaths);
            Clipboard.SetFileDropList(files);
            ShowActionFeedback("Copied · Ctrl+V to paste");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] File clipboard copy failed: {ex.Message}");
            ShowActionFeedback("Copy failed");
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        HeldItem? item = _items.FirstOrDefault();
        if (item == null) return;

        try
        {
            if (Directory.Exists(item.SourcePath))
                Shell32.OpenFolder(item.SourcePath);
            else if (File.Exists(item.SourcePath))
                Shell32.OpenFileInExplorer(item.SourcePath);
            else
                ShowActionFeedback("Source unavailable");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Open failed: {ex.Message}");
            ShowActionFeedback("Open failed");
        }
    }

    // Secondary developer actions live behind ••• so transfer remains the primary workflow.
    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Length == 0) return;

        var menu = new ContextMenu();

        var copyPath = new MenuItem { Header = "Copy path" };
        copyPath.Click += (_, _) => CopyPathsAsText();
        menu.Items.Add(copyPath);

        var terminal = new MenuItem { Header = "Open terminal here" };
        terminal.Click += (_, _) => OpenTerminalAtFirstItem();
        menu.Items.Add(terminal);

        var clear = new MenuItem { Header = "Remove from shelf" };
        clear.Click += (_, _) => ClearShelf();
        menu.Items.Add(clear);

        menu.PlacementTarget = MoreButton;
        menu.IsOpen = true;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e) => ClearShelf();

    private void ClearShelf()
    {
        _items = Array.Empty<HeldItem>();
        RenderShelf();
        ActionButtons.Visibility = Visibility.Collapsed;
        ShelfCleared?.Invoke(this, EventArgs.Empty);
    }

    private void CopyPathsAsText()
    {
        if (_items.Length == 0) return;
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, _items.Select(i => i.SourcePath)));
            ShowActionFeedback("Path copied");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Path copy failed: {ex.Message}");
        }
    }

    private void OpenTerminalAtFirstItem()
    {
        HeldItem? item = _items.FirstOrDefault();
        if (item == null) return;

        string dir = item.IsDirectory
            ? item.SourcePath
            : System.IO.Path.GetDirectoryName(item.SourcePath) ?? item.SourcePath;

        if (!Directory.Exists(dir))
        {
            ShowActionFeedback("Source unavailable");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = dir,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Terminal failed: {ex.Message}");
        }
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _items.Length == 0)
            return;

        Point now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        string[] validPaths = GetValidPaths();
        if (validPaths.Length == 0)
        {
            ShowActionFeedback("Source unavailable");
            return;
        }

        try
        {
            var data = new DataObject(DataFormats.FileDrop, validPaths);
            DragDrop.DoDragDrop(DragHandle, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Drag-out failed: {ex.Message}");
        }
    }

    private string[] GetValidPaths() => _items
        .Where(i => i.Exists)
        .Select(i => i.SourcePath)
        .ToArray();

    private void ShowActionFeedback(string text)
    {
        string original = FileSummaryText.Text;
        FileSummaryText.Text = text;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };

        EventHandler? handler = null;
        handler = (_, _) =>
        {
            timer.Stop();
            if (handler != null) timer.Tick -= handler;
            if (HasItems) FileSummaryText.Text = original;
        };

        timer.Tick += handler;
        timer.Start();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
