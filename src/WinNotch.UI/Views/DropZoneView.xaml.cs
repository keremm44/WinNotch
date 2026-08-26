// WinNotch.UI/Views/DropZoneView.xaml.cs
// WHY: Displays dropped file paths and history.
// Click on a path → Open in Explorer (Shell32.ShellExecute).
// Right-click → Copy path to clipboard.
//
// PERFORMANCE: Only visible during active drag operations.
// Zero cost when hidden (Collapsed = no layout/render passes).

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Common;
using WinNotch.Core.Interop;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

/// <summary>
/// Interaction logic for DropZoneView.xaml.
/// Displays file paths after drag-drop operations.
/// </summary>
public partial class DropZoneView : UserControl
{
    private readonly ObservableCollection<HistoryEntry> _history = new();

    public DropZoneView()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
    }

    /// <summary>
    /// Sets the dropped paths for display.
    /// </summary>
    public void SetDroppedPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        // Show first file in the main display
        var firstPath = paths[0];
        string displayName = System.IO.Path.GetFileName(firstPath) ?? firstPath;
        DropTargetText.Text = displayName;

        // Add to history
        foreach (var path in paths.Take(Constants.MaxHistoryEntries))
        {
            string name = System.IO.Path.GetFileName(path) ?? path;
            bool isDir = System.IO.Directory.Exists(path);

            _history.Insert(0, new HistoryEntry
            {
                FilePath = path,
                DisplayPath = $"  {(isDir ? "📁" : "📄")} {name}"
            });

            // Enforce max history
            while (_history.Count > Constants.MaxHistoryEntries)
            {
                _history.RemoveAt(_history.Count - 1);
            }
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
                {
                    Shell32.OpenFolder(filePath);
                }
                else
                {
                    Shell32.OpenFileInExplorer(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DropZoneView] Error opening path: {ex.Message}");
            }
        }
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
