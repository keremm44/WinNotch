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
    private uint _shellHookMessage;
    private bool _shellHookRegistered;
    private IntPtr _shellFullscreenWindow;
    private ContextMenu? _managedContextMenu;
    private bool _suppressPrimaryClickAfterMenuDismiss;
    private bool _reliabilityLayerInitialized;
    private bool _hiddenForFullscreen;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_reliabilityLayerInitialized) return;
        _reliabilityLayerInitialized = true;

        RootGrid.PreviewMouseRightButtonUp += Reliability_PreviewMouseRightButtonUp;
        RootGrid.PreviewMouseLeftButtonDown += Reliability_PreviewMouseLeftButtonDown;
        UpdateFullscreenFallbackChecks();
        VerifyAutomaticFullscreenVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        StopFullscreenFallbackChecks();
        StopShellFullscreenTracking();
        CloseManagedContextMenu();

        RootGrid.PreviewMouseRightButtonUp -= Reliability_PreviewMouseRightButtonUp;
        RootGrid.PreviewMouseLeftButtonDown -= Reliability_PreviewMouseLeftButtonDown;
        base.OnClosed(e);
    }

    private void UpdateFullscreenFallbackChecks()
    {
        bool shouldRun = _reliabilityLayerInitialized && !_manuallyHidden &&
            string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase);
        if (!shouldRun)
        {
            StopFullscreenFallbackChecks();
            StopShellFullscreenTracking();
            return;
        }

        StartShellFullscreenTracking();
        if (_fullscreenFallbackTimer != null) return;
        _fullscreenFallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            // WinEvents are primary. This catches delayed Chromium/DWM transitions
            // and exclusive-fullscreen changes which do not emit a foreground event.
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _fullscreenFallbackTimer.Tick += FullscreenFallbackTimer_Tick;
        _fullscreenFallbackTimer.Start();
    }

    private void StopFullscreenFallbackChecks()
    {
        if (_fullscreenFallbackTimer == null) return;
        _fullscreenFallbackTimer.Stop();
        _fullscreenFallbackTimer.Tick -= FullscreenFallbackTimer_Tick;
        _fullscreenFallbackTimer = null;
    }

    private void StartShellFullscreenTracking()
    {
        if (_shellHookRegistered || _hWnd == IntPtr.Zero)
            return;

        _shellHookMessage = User32.RegisterWindowMessage("SHELLHOOK");
        if (_shellHookMessage == 0)
            return;

        _shellHookRegistered = User32.RegisterShellHookWindow(_hWnd);
        if (!_shellHookRegistered)
            _shellHookMessage = 0;
    }

    private void StopShellFullscreenTracking()
    {
        if (_shellHookRegistered && _hWnd != IntPtr.Zero)
            User32.DeregisterShellHookWindow(_hWnd);

        _shellHookRegistered = false;
        _shellHookMessage = 0;
        _shellFullscreenWindow = IntPtr.Zero;
    }

    private bool TryHandleShellFullscreenMessage(int message, IntPtr codeValue, IntPtr windowValue)
    {
        if (!_shellHookRegistered || _shellHookMessage == 0 ||
            unchecked((uint)message) != _shellHookMessage)
            return false;

        int code = unchecked((int)codeValue.ToInt64()) & 0x7FFF;
        IntPtr root = NormalizeRootWindow(windowValue);
        if (root == IntPtr.Zero)
            root = NormalizeRootWindow(User32.GetForegroundWindow());

        if (code == User32.HSHELL_WINDOWFULLSCREEN)
        {
            _shellFullscreenWindow = root;
            VerifyAutomaticFullscreenVisibility();
        }
        else if (code == User32.HSHELL_WINDOWNORMAL)
        {
            if (_shellFullscreenWindow == IntPtr.Zero || root == IntPtr.Zero ||
                root == _shellFullscreenWindow)
                _shellFullscreenWindow = IntPtr.Zero;
            VerifyAutomaticFullscreenVisibility();
        }

        return true;
    }

    private void FullscreenFallbackTimer_Tick(object? sender, EventArgs e)
        => VerifyAutomaticFullscreenVisibility();

    private void VerifyAutomaticFullscreenVisibility()
    {
        if (!_initialized ||
            !string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        IntPtr foreground = NormalizeRootWindow(User32.GetForegroundWindow());
        IntPtr foregroundMonitor = foreground == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(foreground, User32.MONITOR_DEFAULTTONEAREST);
        IntPtr notchMonitor = _hWnd == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(_hWnd, User32.MONITOR_DEFAULTTONEAREST);
        bool sameMonitor = foregroundMonitor != IntPtr.Zero && foregroundMonitor == notchMonitor;
        bool shellReportedFullscreen = foreground != IntPtr.Zero &&
            foreground == _shellFullscreenWindow &&
            User32.IsWindowVisible(foreground) && !User32.IsIconic(foreground);
        bool geometryFullscreen = foreground != IntPtr.Zero &&
            WindowHookManager.IsWindowFullscreen(foreground);
        bool fullscreen = foreground != IntPtr.Zero && foreground != _hWnd && sameMonitor &&
            (shellReportedFullscreen || geometryFullscreen);
        ApplyAutomaticFullscreenVisibility(fullscreen);
    }

    private static IntPtr NormalizeRootWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr root = User32.GetAncestor(window, User32.GA_ROOT);
        return root == IntPtr.Zero ? window : root;
    }

    private void ApplyAutomaticFullscreenVisibility(bool fullscreen)
    {
        if (!string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        if (fullscreen)
        {
            _hiddenForFullscreen = true;
            CloseManagedContextMenu();

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

    private void Reliability_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_managedContextMenu?.IsOpen != true)
            return;

        // A dismiss click must not also execute the surface underneath (for example,
        // opening Command Hub). Consume its matching button-up in the primary handler.
        _suppressPrimaryClickAfterMenuDismiss = true;
        CloseManagedContextMenu();
        e.Handled = true;
    }

    private bool ConsumeManagedContextMenuDismissal()
    {
        if (!_suppressPrimaryClickAfterMenuDismiss)
            return false;

        _suppressPrimaryClickAfterMenuDismiss = false;
        return true;
    }

    private void Reliability_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowManagedContextMenu();
    }

    private void ShowManagedContextMenu()
    {
        CloseManagedContextMenu();

        var menu = new ContextMenu
        {
            PlacementTarget = RootGrid,
            StaysOpen = false
        };
        menu.Closed += ManagedContextMenu_Closed;

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

        _managedContextMenu = menu;
        menu.IsOpen = true;
    }

    private void CloseManagedContextMenu()
    {
        if (_managedContextMenu?.IsOpen == true)
            _managedContextMenu.IsOpen = false;
    }

    private void ManagedContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        // WPF may close the popup from its mouse capture before the click is
        // re-routed to RootGrid. Remember that same click so MouseUp cannot open Hub.
        if (Mouse.LeftButton == MouseButtonState.Pressed && RootGrid.IsMouseOver)
            _suppressPrimaryClickAfterMenuDismiss = true;

        menu.Closed -= ManagedContextMenu_Closed;
        menu.Items.Clear();
        menu.PlacementTarget = null;
        if (ReferenceEquals(_managedContextMenu, menu))
            _managedContextMenu = null;
    }
}
