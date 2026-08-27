using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Core.Interop;
using WinNotch.Core.Services;

namespace WinNotch.UI;

public partial class MainWindow
{
    private DispatcherTimer? _fullscreenFallbackTimer;
    private bool _reliabilityLayerInitialized;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_reliabilityLayerInitialized) return;
        _reliabilityLayerInitialized = true;

        // Preview handles the event before the legacy MouseRightButtonUp handler,
        // so the themed/managed menu below becomes the single context menu path.
        RootGrid.PreviewMouseRightButtonUp += Reliability_PreviewMouseRightButtonUp;

        _fullscreenFallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _fullscreenFallbackTimer.Tick += FullscreenFallbackTimer_Tick;
        _fullscreenFallbackTimer.Start();

        // Do not wait for the first interval after startup.
        VerifyAutomaticFullscreenVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_fullscreenFallbackTimer != null)
        {
            _fullscreenFallbackTimer.Stop();
            _fullscreenFallbackTimer.Tick -= FullscreenFallbackTimer_Tick;
            _fullscreenFallbackTimer = null;
        }

        RootGrid.PreviewMouseRightButtonUp -= Reliability_PreviewMouseRightButtonUp;
        base.OnClosed(e);
    }

    private void FullscreenFallbackTimer_Tick(object? sender, EventArgs e)
        => VerifyAutomaticFullscreenVisibility();

    private void VerifyAutomaticFullscreenVisibility()
    {
        if (!_initialized || _manuallyHidden) return;
        if (!string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        IntPtr foreground = User32.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == _hWnd) return;

        string className = WindowHookManager.GetWindowClassName(foreground);
        if (className is "Shell_TrayWnd" or "WorkerW" or "Shell_SecondaryTrayWnd")
            return;

        bool fullscreen = WindowHookManager.IsWindowFullscreen(foreground);

        if (fullscreen)
        {
            if (Visibility == Visibility.Visible)
                Visibility = Visibility.Hidden;
        }
        else if (Visibility == Visibility.Hidden)
        {
            Visibility = Visibility.Visible;
        }
    }

    public IReadOnlyList<PinnedWindowInfo> GetPinnedWindows()
        => _windowPinService?.GetPinnedWindows() ?? Array.Empty<PinnedWindowInfo>();

    public bool UnpinWindow(IntPtr hWnd)
        => _windowPinService?.UnpinWindow(hWnd) == true;

    public void UnpinAllPinnedWindows()
        => _windowPinService?.UnpinAll();

    private void Reliability_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowManagedContextMenu();
    }

    private void ShowManagedContextMenu()
    {
        var menu = new ContextMenu();
        IntPtr foreground = User32.GetForegroundWindow();

        if (_settings.ModuleD_WindowPin && _windowPinService != null)
        {
            if (foreground != IntPtr.Zero && foreground != _hWnd)
            {
                bool pinned = _windowPinService.IsPinned(foreground);
                var pinItem = new MenuItem
                {
                    Header = pinned ? "Aktif pencerenin sabitlemesini kaldır" : "Aktif pencereyi sabitle"
                };
                pinItem.Click += (_, _) => _windowPinService.TogglePin(foreground);
                menu.Items.Add(pinItem);
            }

            IReadOnlyList<PinnedWindowInfo> pinnedWindows = _windowPinService.GetPinnedWindows();
            if (pinnedWindows.Count > 0)
            {
                var pinnedMenu = new MenuItem
                {
                    Header = $"Sabitlenen pencereler  ·  {pinnedWindows.Count}"
                };

                foreach (PinnedWindowInfo info in pinnedWindows)
                {
                    var windowItem = new MenuItem
                    {
                        Header = $"Kaldır  ·  {TrimWindowTitle(info.WindowTitle)}",
                        ToolTip = info.WindowTitle
                    };
                    IntPtr handle = info.WindowHandle;
                    windowItem.Click += (_, _) => _windowPinService.UnpinWindow(handle);
                    pinnedMenu.Items.Add(windowItem);
                }

                pinnedMenu.Items.Add(new Separator());
                var clearAll = new MenuItem { Header = "Tüm sabitlemeleri kaldır" };
                clearAll.Click += (_, _) => _windowPinService.UnpinAll();
                pinnedMenu.Items.Add(clearAll);
                menu.Items.Add(pinnedMenu);
            }

            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());
        }

        var settingsItem = new MenuItem { Header = "Ayarlar" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var hideItem = new MenuItem { Header = "1 saat gizle" };
        hideItem.Click += (_, _) => HideNotchTemporarily();
        menu.Items.Add(hideItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Çıkış" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        menu.PlacementTarget = RootGrid;
        menu.IsOpen = true;
    }

    private static string TrimWindowTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Adsız pencere";
        const int max = 42;
        return title.Length <= max ? title : title[..(max - 1)] + "…";
    }
}
