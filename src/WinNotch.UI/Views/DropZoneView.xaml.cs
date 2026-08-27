using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;
using WinNotch.Core.Interop;

using UserControl = System.Windows.Controls.UserControl;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDrop = System.Windows.DragDrop;
using WpfDragDropEffects = System.Windows.DragDropEffects;

namespace WinNotch.UI.Views;

public partial class DropZoneView : UserControl
{
    private HeldItem[] _items = Array.Empty<HeldItem>();
    private Point _dragStart;

    public event EventHandler? ShelfCleared;
    public event EventHandler? DragOutStarted;
    public event EventHandler? DragOutCompleted;

    public bool HasItems => _items.Length > 0;
    public IReadOnlyList<HeldItem> Items => _items;

    public DropZoneView()
    {
        InitializeComponent();
    }

    public void SetDroppedPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        var seen = new HashSet<string>(
            _items.Select(i => i.SourcePath),
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<HeldItem>(_items.Length + paths.Count);
        merged.AddRange(_items);

        foreach (string path in paths)
        {
            if (merged.Count >= Constants.MaxShelfItems)
                break;
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                continue;

            merged.Add(HeldItem.FromPath(path));
        }

        _items = merged.ToArray();
        RenderShelf();
    }

    public void ResetShelf(bool notify = false)
    {
        _items = Array.Empty<HeldItem>();
        RenderShelf();
        ActionButtons.Visibility = Visibility.Collapsed;

        if (notify)
            ShelfCleared?.Invoke(this, EventArgs.Empty);
    }

    public void SetExpanded(bool expanded)
    {
        if (HasItems)
            RenderShelf();

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
        FileSummaryText.Text = HasItems
            ? $"Mevcut {_items.Length} öğeye eklenecek"
            : "Geçici rafta tutulacak";
        ActionButtons.Visibility = Visibility.Collapsed;
        RemoveButton.Visibility = Visibility.Collapsed;
    }

    private void RenderShelf()
    {
        if (_items.Length == 0)
        {
            FileIcon.Text = "+";
            DropTargetText.Text = "Dosyayı buraya bırak";
            FileSummaryText.Text = "Geçici rafta tutulacak";
            RemoveButton.Visibility = Visibility.Collapsed;
            return;
        }

        RemoveButton.Visibility = Visibility.Visible;

        if (_items.Length == 1)
        {
            HeldItem item = _items[0];
            FileIcon.Text = item.IsDirectory ? "KL" : GetExtensionLabel(item.SourcePath);
            DropTargetText.Text = item.DisplayName;
            FileSummaryText.Text = item.Exists
                ? FormatSummary(item)
                : "Kaynak artık bulunamıyor";
            return;
        }

        FileIcon.Text = _items.Length.ToString();
        DropTargetText.Text = $"{_items.Length} öğe";

        long knownBytes = _items.Where(i => i.SizeBytes.HasValue).Sum(i => i.SizeBytes!.Value);
        FileSummaryText.Text = knownBytes > 0
            ? $"{FormatSize(knownBytes)} · sürükle veya kopyala"
            : "Sürükle veya kopyala";
    }

    private static string FormatSummary(HeldItem item)
    {
        if (item.IsDirectory) return "Klasör · sürükle veya kopyala";
        if (item.SizeBytes is long size) return $"{FormatSize(size)} · sürükle veya kopyala";
        return "Sürükle veya kopyala";
    }

    private static string GetExtensionLabel(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.');
        if (string.IsNullOrWhiteSpace(ext)) return "DOS";
        return ext.Length <= 3 ? ext.ToUpperInvariant() : ext[..3].ToUpperInvariant();
    }

    private void CopyFilesButton_Click(object sender, RoutedEventArgs e)
    {
        string[] validPaths = GetValidPaths();
        if (validPaths.Length == 0)
        {
            ShowActionFeedback("Kaynak bulunamıyor");
            return;
        }

        try
        {
            var files = new StringCollection();
            files.AddRange(validPaths);
            WpfClipboard.SetFileDropList(files);
            ShowActionFeedback("Kopyalandı · Ctrl+V ile yapıştır");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] File clipboard copy failed: {ex.Message}");
            ShowActionFeedback("Kopyalama başarısız");
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
                ShowActionFeedback("Kaynak bulunamıyor");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Open failed: {ex.Message}");
            ShowActionFeedback("Açılamadı");
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Length == 0) return;

        var menu = new ContextMenu();

        var copyPath = new MenuItem { Header = "Yolları metin olarak kopyala" };
        copyPath.Click += (_, _) => CopyPathsAsText();
        menu.Items.Add(copyPath);

        var terminal = new MenuItem { Header = "Terminali burada aç" };
        terminal.Click += (_, _) => OpenTerminalAtFirstItem();
        menu.Items.Add(terminal);

        var clear = new MenuItem { Header = "Raftan kaldır" };
        clear.Click += (_, _) => ResetShelf(notify: true);
        menu.Items.Add(clear);

        menu.PlacementTarget = MoreButton;
        menu.IsOpen = true;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
        => ResetShelf(notify: true);

    private void CopyPathsAsText()
    {
        if (_items.Length == 0) return;
        try
        {
            WpfClipboard.SetText(string.Join(Environment.NewLine, _items.Select(i => i.SourcePath)));
            ShowActionFeedback("Yol kopyalandı");
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
            : Path.GetDirectoryName(item.SourcePath) ?? item.SourcePath;

        if (!Directory.Exists(dir))
        {
            ShowActionFeedback("Kaynak bulunamıyor");
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
            ShowActionFeedback("Kaynak bulunamıyor");
            return;
        }

        DragOutStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            var data = new WpfDataObject(WpfDataFormats.FileDrop, validPaths);
            WpfDragDrop.DoDragDrop(DragHandle, data, WpfDragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Drag-out failed: {ex.Message}");
        }
        finally
        {
            DragOutCompleted?.Invoke(this, EventArgs.Empty);
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
