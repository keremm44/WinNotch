using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Core.Interop;
using WinNotch.Core.Services;

using WpfApplication = System.Windows.Application;

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

        // This is the single right-click path. MainWindow.xaml no longer wires the
        // legacy bubbling MouseRightButtonUp handler.
        RootGrid.PreviewMouseRightButtonUp += Reliability_PreviewMouseRightButtonUp;

        _fullscreenFallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _fullscreenFallbackTimer.Tick += FullscreenFallbackTimer_Tick;
        _fullscreenFallbackTimer.Start();

        EnforcePinnedWindowSafety();
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
    {
        // Pin safety is independent of VisibilityMode. A window pinned while small
        // can later be maximized/fullscreen and must then lose TOPMOST immediately.
        EnforcePinnedWindowSafety();
        VerifyAutomaticFullscreenVisibility();
    }

    private void EnforcePinnedWindowSafety()
    {
        if (_windowPinService?.UnpinUnsafeWindows() > 0)
            ReassertNotchTopmost();
    }

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
            ReassertNotchTopmost();
        }
    }

    private void ReassertNotchTopmost()
    {
        if (_hWnd == IntPtr.Zero) return;

        User32.SetWindowPos(
            _hWnd,
            User32.HWND_TOPMOST,
            0, 0, 0, 0,
            User32.SWP_NOMOVE |
            User32.SWP_NOSIZE |
            User32.SWP_NOACTIVATE |
            User32.SWP_SHOWWINDOW);
    }

    private void ReassertNotchTopmostDeferred()
        => Dispatcher.BeginInvoke(
            ReassertNotchTopmost,
            DispatcherPriority.Background);

    public IReadOnlyList<PinnedWindowInfo> GetPinnedWindows()
        => _windowPinService?.GetPinnedWindows() ?? Array.Empty<PinnedWindowInfo>();

    public bool UnpinWindow(IntPtr hWnd)
    {
        bool result = _windowPinService?.UnpinWindow(hWnd) == true;
        ReassertNotchTopmostDeferred();
        return result;
    }

    public void UnpinAllPinnedWindows()
    {
        _windowPinService?.UnpinAll();
        ReassertNotchTopmostDeferred();
    }

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
                bool tracked = _windowPinService.IsPinned(foreground);
                bool nativeTopmost = _windowPinService.IsNativeTopmost(foreground);
                bool fullscreen = WindowHookManager.IsWindowFullscreen(foreground);
                bool maximized = WindowHookManager.IsWindowMaximized(foreground);

                MenuItem pinItem;
                if (tracked || nativeTopmost)
                {
                    pinItem = new MenuItem
                    {
                        Header = tracked
                            ? "Aktif pencerenin sabitlemesini kaldır"
                            : "Aktif pencerenin üstte kalmasını kaldır"
                    };
                    pinItem.Click += (_, _) =>
                    {
                        _windowPinService.UnpinWindow(foreground);
                        ReassertNotchTopmostDeferred();
                    };
                }
                else if (fullscreen || maximized)
                {
                    pinItem = new MenuItem
                    {
                        Header = "Maksimize / tam ekran pencere sabitlenemez",
                        IsEnabled = false,
                        ToolTip = "Önce pencereyi normal boyuta getir. Tam ekran TOPMOST pencereler diğer uygulamaları örtebilir."
                    };
                }
                else
                {
                    pinItem = new MenuItem { Header = "Aktif pencereyi sabitle" };
                    pinItem.Click += (_, _) =>
                    {
                        _windowPinService.PinWindow(foreground);
                        // SetWindowPos(HWND_TOPMOST) places the target at the front
                        // of the TOPMOST band. Put WinNotch back above it after the
                        // context-menu click completes so the notch remains usable.
                        ReassertNotchTopmostDeferred();
                    };
                }

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
                    windowItem.Click += (_, _) =>
                    {
                        _windowPinService.UnpinWindow(handle);
                        ReassertNotchTopmostDeferred();
                    };
                    pinnedMenu.Items.Add(windowItem);
                }

                pinnedMenu.Items.Add(new Separator());
                var clearAll = new MenuItem { Header = "Tüm sabitlemeleri kaldır" };
                clearAll.Click += (_, _) =>
                {
                    _windowPinService.UnpinAll();
                    ReassertNotchTopmostDeferred();
                };
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
        exitItem.Click += (_, _) => WpfApplication.Current.Shutdown();
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
