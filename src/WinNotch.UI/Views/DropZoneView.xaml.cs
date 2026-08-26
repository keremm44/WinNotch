// WinNotch.UI/Views/DropZoneView.xaml.cs
// WHY: Makes file drop actually useful.
// Instead of just showing a path, provides actionable buttons:
// - Copy path: copies full path to clipboard
// - Open folder: opens Explorer at the file/folder location
// - Open terminal: opens cmd/pwsh at the file's parent directory
//
// For multi-file drops, shows count + total size.
// Actions operate on the FIRST dropped file (most intuitive).
//
// PERFORMANCE: Only visible during active drag/drop.
// Zero cost when hidden (Collapsed = no layout/render passes).

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;
using WinNotch.Core.Interop;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

/// <summary>
/// Interaction logic for DropZoneView.xaml.
/// Displays dropped files with contextual actions.
/// </summary>
public partial class DropZoneView : UserControl
{
    private readonly ObservableCollection<HistoryEntry> _history = new();
    private string[] _currentPaths = Array.Empty<string>();

    public DropZoneView()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
    }

    /// <summary>
    /// Sets the dropped paths for display and shows action buttons.
    /// </summary>
    public void SetDroppedPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        _currentPaths = paths.ToArray();

        // Show file info
        if (paths.Count == 1)
        {
            string name = System.IO.Path.GetFileName(paths[0]) ?? paths[0];
            bool isDir = System.IO.Directory.Exists(paths[0]);
            FileIcon.Text = isDir ? "📁" : "📄";
            DropTargetText.Text = name;

            // Show file size for single files
            if (!isDir)
            {
                try
                {
                    var fi = new System.IO.FileInfo(paths[0]);
                    FileSummaryText.Text = FormatSize(fi.Length);
                }
                catch
                {
                    FileSummaryText.Text = paths[0];
                }
            }
            else
            {
                FileSummaryText.Text = paths[0];
            }
        }
        else
        {
            // Multiple files
            FileIcon.Text = "📦";
            DropTargetText.Text = $"{paths.Count} dosya";

            // Calculate total size
            long totalSize = 0;
            int fileCount = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        totalSize += new System.IO.FileInfo(path).Length;
                        fileCount++;
                    }
                }
                catch { }
            }

            FileSummaryText.Text = fileCount > 0
                ? $"{FormatSize(totalSize)} • {paths.Count} öğe"
                : $"{paths.Count} öğe";
        }

        // Show action buttons
        ActionButtons.Visibility = Visibility.Visible;

        // Update history
        foreach (var path in paths.Take(Constants.MaxHistoryEntries))
        {
            string name = System.IO.Path.GetFileName(path) ?? path;
            bool isDir = System.IO.Directory.Exists(path);

            _history.Insert(0, new HistoryEntry
            {
                FilePath = path,
                DisplayPath = $"  {(isDir ? "📁" : "📄")} {name}"
            });

            while (_history.Count > Constants.MaxHistoryEntries)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ACTIONS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Copy the full path(s) to clipboard.
    /// For single file: copies full path.
    /// For multiple files: copies all paths, one per line.
    /// </summary>
    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPaths.Length == 0) return;

        try
        {
            string text = _currentPaths.Length == 1
                ? _currentPaths[0]
                : string.Join(Environment.NewLine, _currentPaths);

            System.Windows.Clipboard.SetText(text);
            ShowActionFeedback("✓ Kopyalandı");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DropZoneView] Copy failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Open the folder containing the first dropped file in Explorer.
    /// For folders: opens the folder itself.
    /// </summary>
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPaths.Length == 0) return;

        try
        {
            string path = _currentPaths[0];
            if (System.IO.Directory.Exists(path))
            {
                Shell32.OpenFolder(path);
            }
            else
            {
                Shell32.OpenFileInExplorer(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DropZoneView] Open folder failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Open a terminal (cmd) at the parent directory of the first dropped file.
    /// For folders: opens terminal inside the folder.
    /// </summary>
    private void TerminalButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPaths.Length == 0) return;

        try
        {
            string path = _currentPaths[0];
            string dir = System.IO.Directory.Exists(path)
                ? path
                : System.IO.Path.GetDirectoryName(path) ?? path;

            // Open cmd.exe at the directory
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = dir,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DropZoneView] Open terminal failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles click on a history item — opens in Explorer.
    /// </summary>
    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string filePath)
        {
            try
            {
                if (System.IO.Directory.Exists(filePath))
                    Shell32.OpenFolder(filePath);
                else
                    Shell32.OpenFileInExplorer(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DropZoneView] Error opening path: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows brief feedback text on the summary line.
    /// </summary>
    private void ShowActionFeedback(string text)
    {
        string original = FileSummaryText.Text;
        FileSummaryText.Text = text;

        // Restore after 1.5 seconds
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            FileSummaryText.Text = original;
        };
        timer.Start();
    }

    /// <summary>
    /// Formats byte count to human-readable string.
    /// </summary>
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Inner model for history entries.
    /// </summary>
    private sealed class HistoryEntry
    {
        public string FilePath { get; init; } = string.Empty;
        public string DisplayPath { get; init; } = string.Empty;
    }
}
