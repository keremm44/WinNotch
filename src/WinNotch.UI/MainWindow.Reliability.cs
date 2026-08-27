using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Common;
using WinNotch.Core.Interop;

using WpfApplication = System.Windows.Application;

namespace WinNotch.UI;

public partial class MainWindow
{
    private DispatcherTimer? _fullscreenFallbackTimer;
    private bool _reliabilityLayerInitialized;
    private bool _hiddenForFullscreen;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_reliabilityLayerInitialized) return;
        _reliabilityLayerInitialized = true;

        RootGrid.PreviewMouseRightButtonUp += Reliability_PreviewMouseRightButtonUp;

        _fullscreenFallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            // WinEvents are primary. This catches delayed Chromium/DWM transitions
            // and exclusive-fullscreen changes which do not emit a foreground event.
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _fullscreenFallbackTimer.Tick += FullscreenFallbackTimer_Tick;
        _fullscreenFallbackTimer.Start();

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
        if (!_initialized ||
            !string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        IntPtr foreground = User32.GetForegroundWindow();
        IntPtr foregroundMonitor = foreground == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(foreground, User32.MONITOR_DEFAULTTONEAREST);
        IntPtr notchMonitor = _hWnd == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(_hWnd, User32.MONITOR_DEFAULTTONEAREST);
        bool sameMonitor = foregroundMonitor != IntPtr.Zero && foregroundMonitor == notchMonitor;
        bool fullscreen = foreground != IntPtr.Zero && foreground != _hWnd && sameMonitor &&
                          WindowHookManager.IsWindowFullscreen(foreground);
        ApplyAutomaticFullscreenVisibility(fullscreen);
    }

    private void ApplyAutomaticFullscreenVisibility(bool fullscreen)
    {
        if (!string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        if (fullscreen)
        {
            _hiddenForFullscreen = true;

            // Hiding a hovered window does not reliably produce MouseLeave. Normalize
            // hover-only states now so fullscreen exit restores the compact persistent
            // surface rather than a stranded expanded media/hover surface.
            if (_currentState is NotchState.MediaActive or NotchState.Hover)
            {
                TransitionToState(GetPersistentState(), force: true);
            }

            // Keep WPF and the native HWND synchronized. Geometry animation used to
            // call SetWindowPos(SWP_SHOWWINDOW), making the HWND visible again while
            // WPF still reported Visibility.Hidden; subsequent checks then skipped
            // hiding it. Enforce the native state on every fullscreen verification.
            Visibility = Visibility.Hidden;
            if (_hWnd != IntPtr.Zero && User32.IsWindowVisible(_hWnd))
                User32.ShowWindow(_hWnd, User32.SW_HIDE);
            return;
        }

        // Desktop/taskbar foreground is explicitly non-fullscreen. Do not return
        // early for shell classes: that used to leave the notch hidden forever after
        // a fullscreen window closed or lost focus.
        bool wasHiddenForFullscreen = _hiddenForFullscreen;
        _hiddenForFullscreen = false;
        if (!_manuallyHidden && (wasHiddenForFullscreen || Visibility != Visibility.Visible))
        {
            Visibility = Visibility.Visible;
            if (_hWnd != IntPtr.Zero && !User32.IsWindowVisible(_hWnd))
                User32.ShowWindow(_hWnd, User32.SW_SHOWNOACTIVATE);
        }
    }

    private void Reliability_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowManagedContextMenu();
    }

    private void ShowManagedContextMenu()
    {
        var menu = new ContextMenu
        {
            PlacementTarget = RootGrid
        };

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

        menu.IsOpen = true;
    }
}
