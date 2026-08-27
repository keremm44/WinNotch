using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;
using WinNotch.Core.Interop;

using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
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
    private int _selectedIndex = -1;
    private bool _isExpanded;

    public event EventHandler? ShelfCleared;
    public event EventHandler? DragOutStarted;
    public event EventHandler? DragOutCompleted;

    public bool HasItems => _items.Length > 0;
    public IReadOnlyList<HeldItem> Items => _items;

    private HeldItem? SelectedItem
        => _selectedIndex >= 0 && _selectedIndex < _items.Length
            ? _items[_selectedIndex]
            : _items.FirstOrDefault();

    public DropZoneView()
    {
        InitializeComponent();
        IsVisibleChanged += DropZoneView_IsVisibleChanged;
    }

    private void DropZoneView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this);
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
        _selectedIndex = _items.Length > 0 ? _items.Length - 1 : -1;
        RenderShelf();
    }

    public void ResetShelf(bool notify = false)
    {
        _items = Array.Empty<HeldItem>();
        _selectedIndex = -1;
        _isExpanded = false;
        ShelfChipsPanel.Children.Clear();
        ShelfChipsPanel.Visibility = Visibility.Collapsed;
        ActionButtons.Visibility = Visibility.Collapsed;
        RenderShelf();

        if (notify)
            ShelfCleared?.Invoke(this, EventArgs.Empty);
    }

    public void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;

        if (HasItems)
            RenderShelf();

        ActionButtons.Visibility = expanded && HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        ShelfChipsPanel.Visibility = expanded && HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        RemoveButton.Visibility = HasItems
            ? Visibility.Visible
            : Visibility.Collapsed;

        RenderChips();

        if (expanded && HasItems)
        {
            SurfaceMotion.Reveal(ShelfChipsPanel, 1.5, 95);
            SurfaceMotion.Reveal(ActionButtons, 1.5, 105);
        }
    }

    public void ShowDropTarget()
    {
        _isExpanded = false;
        FileIcon.Text = "+";
        DropTargetText.Text = "Dosyayı buraya bırak";
        FileSummaryText.Text = HasItems
            ? $"Mevcut {_items.Length} öğeye eklenecek"
            : "Geçici rafta tutulacak";
        ShelfChipsPanel.Visibility = Visibility.Collapsed;
        ActionButtons.Visibility = Visibility.Collapsed;
        RemoveButton.Visibility = Visibility.Collapsed;
    }

    private void RenderShelf()
    {
        EnsureSelection();

        if (_items.Length == 0)
        {
            FileIcon.Text = "+";
            DropTargetText.Text = "Dosyayı buraya bırak";
            FileSummaryText.Text = "Geçici rafta tutulacak";
            RemoveButton.Visibility = Visibility.Collapsed;
            ShelfChipsPanel.Visibility = Visibility.Collapsed;
            ActionButtons.Visibility = Visibility.Collapsed;
            return;
        }

        RemoveButton.Visibility = Visibility.Visible;
        HeldItem item = SelectedItem ?? _items[0];
        FileIcon.Text = item.IsDirectory ? "KL" : GetExtensionLabel(item.SourcePath);
        DropTargetText.Text = item.DisplayName;

        if (!item.Exists)
        {
            FileSummaryText.Text = _items.Length > 1
                ? $"{_items.Length} öğe · seçili kaynak bulunamıyor"
                : "Kaynak artık bulunamıyor";
        }
        else if (_items.Length == 1)
        {
            FileSummaryText.Text = FormatSummary(item);
        }
        else
        {
            long knownBytes = _items.Where(i => i.SizeBytes.HasValue).Sum(i => i.SizeBytes!.Value);
            string sizeText = knownBytes > 0 ? $" · {FormatSize(knownBytes)}" : string.Empty;
            FileSummaryText.Text = $"{_items.Length} öğe{sizeText} · seçili";
        }

        if (_isExpanded)
            RenderChips();
    }

    private void RenderChips()
    {
        ShelfChipsPanel.Children.Clear();

        if (!_isExpanded || _items.Length == 0)
        {
            ShelfChipsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ShelfChipsPanel.Visibility = Visibility.Visible;

        int firstVisible = Math.Max(0, _items.Length - 2);
        for (int index = firstVisible; index < _items.Length; index++)
            ShelfChipsPanel.Children.Add(CreateChip(index));

        int hiddenCount = firstVisible;
        if (hiddenCount > 0)
        {
            var overflow = new Border
            {
                Height = 24,
                CornerRadius = new CornerRadius(7),
                Background = FindBrush("Brush.Surface.Soft"),
                BorderBrush = FindBrush("Brush.Border.OnDark"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = $"+{hiddenCount}",
                    FontSize = 8.5,
                    Foreground = FindBrush("Brush.Text.OnDarkSecondary"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ShelfChipsPanel.Children.Add(overflow);
        }
    }

    private FrameworkElement CreateChip(int index)
    {
        HeldItem item = _items[index];
        var container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 5, 0)
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = item.IsDirectory ? "KL" : GetExtensionLabel(item.SourcePath),
            FontSize = 7.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("Brush.Text.OnDarkSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 8.5,
            MaxWidth = 88,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = FindBrush("Brush.Text.OnDarkPrimary"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var selectButton = new Button
        {
            Tag = index,
            Content = content,
            Style = (Style)FindResource("ShelfChipButton")
        };
        if (index == _selectedIndex)
        {
            selectButton.Background = FindBrush("Brush.Accent.Subtle");
            selectButton.BorderBrush = FindBrush("Brush.Accent.Border");
        }
        selectButton.Click += ShelfChip_Select;

        var removeButton = new Button
        {
            Tag = index,
            Content = "×",
            ToolTip = "Bu öğeyi raftan çıkar",
            Style = (Style)FindResource("ShelfChipRemoveButton")
        };
        removeButton.Click += ShelfChip_Remove;

        container.Children.Add(selectButton);
        container.Children.Add(removeButton);
        return container;
    }

    private void ShelfChip_Select(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index < 0 || index >= _items.Length)
            return;

        _selectedIndex = index;
        RenderShelf();
        RenderChips();
    }

    private void ShelfChip_Remove(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index < 0 || index >= _items.Length)
            return;

        var list = _items.ToList();
        list.RemoveAt(index);
        _items = list.ToArray();
        _selectedIndex = _items.Length == 0 ? -1 : Math.Min(index, _items.Length - 1);

        if (_items.Length == 0)
        {
            ResetShelf(notify: true);
            return;
        }

        RenderShelf();
        RenderChips();
    }

    private Brush FindBrush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private void EnsureSelection()
    {
        if (_items.Length == 0)
        {
            _selectedIndex = -1;
            return;
        }

        if (_selectedIndex < 0 || _selectedIndex >= _items.Length)
            _selectedIndex = _items.Length - 1;
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
        HeldItem? item = SelectedItem;
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

        var terminal = new MenuItem { Header = "Terminali seçili öğede aç" };
        terminal.Click += (_, _) => OpenTerminalAtSelectedItem();
        menu.Items.Add(terminal);

        var clear = new MenuItem { Header = "Rafı temizle" };
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
            ShowActionFeedback("Yollar kopyalandı");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileShelf] Path copy failed: {ex.Message}");
        }
    }

    private void OpenTerminalAtSelectedItem()
    {
        HeldItem? item = SelectedItem;
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
            if (HasItems) RenderShelf();
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
