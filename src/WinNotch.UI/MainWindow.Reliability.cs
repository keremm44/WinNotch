using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinNotch.Common;
using WinNotch.Core.Interop;

using WpfApplication = System.Windows.Application;

namespace WinNotch.UI;

public partial class MainWindow
{
    private enum ShellPresentationState
    {
        Unknown,
        Normal,
        Fullscreen
    }

    private DispatcherTimer? _fullscreenFallbackTimer;
    private uint _shellHookMessage;
    private bool _shellHookRegistered;
    private IntPtr _shellPresentationWindow;
    private IntPtr _shellPresentationMessageWindow;
    private ShellPresentationState _shellPresentationState;
    private ContextMenu? _managedContextMenu;
    private bool _suppressPrimaryClickAfterMenuDismiss;
    private bool _reliabilityLayerInitialized;
    private bool _hiddenForFullscreen;
    private int _fullscreenAnimationGeneration;
    private bool _fullscreenAnimationInProgress;

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
        CancelFullscreenTransitionAnimation();
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
            CancelFullscreenTransitionAnimation();
            _hiddenForFullscreen = false;
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

    private void RearmShellFullscreenTracking()
    {
        if (!_shellHookRegistered || _hWnd == IntPtr.Zero)
            return;

        // WPF implements Window.Visibility by hiding/showing the native HWND. Some
        // Explorer versions stop delivering later SHELLHOOK presentation messages to
        // a recipient after that cycle. Re-register whenever the notch is shown so
        // every subsequent F11 entry/exit pair is observed; this is event re-arming,
        // not a delayed or time-based recovery.
        User32.DeregisterShellHookWindow(_hWnd);
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
        ClearShellPresentationState();
    }

    private bool TryHandleShellFullscreenMessage(int message, IntPtr codeValue, IntPtr windowValue)
    {
        if (!_shellHookRegistered || _shellHookMessage == 0 ||
            unchecked((uint)message) != _shellHookMessage)
            return false;

        int code = unchecked((int)codeValue.ToInt64()) & 0x7FFF;

        // The shell message can carry a transient Chromium-owned HWND while styles
        // are being restored. The foreground root is the stable identity used by the
        // detector and is therefore preferred for both entering and leaving F11.
        IntPtr foregroundRoot = NormalizeRootWindow(User32.GetForegroundWindow());
        IntPtr messageRoot = NormalizeRootWindow(windowValue);
        IntPtr presentationRoot = foregroundRoot != IntPtr.Zero ? foregroundRoot : messageRoot;

        if (code == User32.HSHELL_WINDOWFULLSCREEN)
        {
            SetShellPresentationState(
                presentationRoot,
                messageRoot,
                ShellPresentationState.Fullscreen);
            VerifyAutomaticFullscreenVisibility();
        }
        else if (code == User32.HSHELL_WINDOWNORMAL)
        {
            // This is authoritative for F11 exit. Do not immediately ask geometry to
            // overrule it: maximized Chromium can remain borderless and monitor-sized.
            IntPtr normalRoot = presentationRoot != IntPtr.Zero
                ? presentationRoot
                : _shellPresentationWindow;
            SetShellPresentationState(
                normalRoot,
                messageRoot,
                ShellPresentationState.Normal);
            ApplyAutomaticFullscreenVisibility(false);
        }

        return true;
    }

    private void SetShellPresentationState(
        IntPtr foregroundWindow,
        IntPtr messageWindow,
        ShellPresentationState state)
    {
        _shellPresentationWindow = foregroundWindow;
        _shellPresentationMessageWindow = messageWindow;
        _shellPresentationState = foregroundWindow == IntPtr.Zero && messageWindow == IntPtr.Zero
            ? ShellPresentationState.Unknown
            : state;
    }

    private void ClearShellPresentationState()
    {
        _shellPresentationWindow = IntPtr.Zero;
        _shellPresentationMessageWindow = IntPtr.Zero;
        _shellPresentationState = ShellPresentationState.Unknown;
    }

    private void FullscreenFallbackTimer_Tick(object? sender, EventArgs e)
        => VerifyAutomaticFullscreenVisibility();

    private void VerifyAutomaticFullscreenVisibility()
    {
        if (!_initialized ||
            !string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        IntPtr foreground = NormalizeRootWindow(User32.GetForegroundWindow());
        bool matchesShellWindow = foreground != IntPtr.Zero &&
            (foreground == _shellPresentationWindow ||
             foreground == _shellPresentationMessageWindow);
        if ((_shellPresentationWindow != IntPtr.Zero ||
             _shellPresentationMessageWindow != IntPtr.Zero) &&
            foreground != IntPtr.Zero &&
            foreground != _hWnd &&
            !matchesShellWindow)
        {
            ClearShellPresentationState();
        }

        IntPtr foregroundMonitor = foreground == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(foreground, User32.MONITOR_DEFAULTTONEAREST);
        IntPtr notchMonitor = _hWnd == IntPtr.Zero
            ? IntPtr.Zero
            : User32.MonitorFromWindow(_hWnd, User32.MONITOR_DEFAULTTONEAREST);
        bool sameMonitor = foregroundMonitor != IntPtr.Zero && foregroundMonitor == notchMonitor;
        bool shellStateApplies = matchesShellWindow;

        bool fullscreenEvidence = shellStateApplies
            ? _shellPresentationState == ShellPresentationState.Fullscreen
            : WindowHookManager.IsWindowFullscreen(foreground);
        bool fullscreen = foreground != IntPtr.Zero && foreground != _hWnd && sameMonitor &&
            User32.IsWindowVisible(foreground) && !User32.IsIconic(foreground) &&
            fullscreenEvidence;
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
            // The fallback check runs every 650ms. Never restart an active/completed
            // transition for the same fullscreen window.
            if (_hiddenForFullscreen)
            {
                if (!_fullscreenAnimationInProgress && Visibility != Visibility.Hidden)
                    HideFullscreenImmediately();
                return;
            }

            _hiddenForFullscreen = true;
            CloseManagedContextMenu();

            // Hiding a hovered window does not reliably produce MouseLeave. Normalize
            // the state and stop its size animation before moving the HWND upward.
            if (_currentState is NotchState.MediaActive or NotchState.Hover)
            {
                NotchState persistent = GetPersistentState();
                TransitionToState(persistent, force: true);
                ApplyDimensions(persistent, force: true);
            }

            // A size transition recenters the native HWND every frame. Complete it
            // before the vertical slide so the two motion systems cannot fight.
            _motionController.Apply(_currentWidth, _currentHeight, immediate: true);

            if (ShouldAnimateFullscreenTransition())
                BeginFullscreenHideAnimation();
            else
                HideFullscreenImmediately();
            return;
        }

        // Desktop/taskbar foreground is explicitly non-fullscreen. Do not return
        // early for shell classes: that used to leave the notch hidden forever after
        // a fullscreen window closed or lost focus.
        bool wasHiddenForFullscreen = _hiddenForFullscreen;
        _hiddenForFullscreen = false;
        if (_manuallyHidden || (!wasHiddenForFullscreen && Visibility == Visibility.Visible))
            return;

        if (ShouldAnimateFullscreenTransition())
            BeginFullscreenShowAnimation();
        else
            ShowFullscreenImmediately();
    }

    private bool ShouldAnimateFullscreenTransition()
    {
        if (!SystemParameters.ClientAreaAnimation)
            return false;

        MotionProfile motion = AppearanceResolver.ResolveMotion(_settings.Appearance);
        return motion.ContentOffsetY > 0.01;
    }

    private void BeginFullscreenHideAnimation()
    {
        int generation = ++_fullscreenAnimationGeneration;
        CancelFullscreenWindowPropertyAnimations();
        _fullscreenAnimationInProgress = true;

        double startTop = ResolveWindowTop();
        double distance = ResolveFullscreenSlideDistance();
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(Constants.FullscreenHideAnimationMs);
        var slide = new DoubleAnimation(startTop, startTop - distance, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var fade = new DoubleAnimation(1.0, 0.35, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        slide.Completed += (_, _) =>
        {
            if (generation != _fullscreenAnimationGeneration || !_hiddenForFullscreen)
                return;

            _fullscreenAnimationInProgress = false;
            HideFullscreenImmediately();
        };

        BeginAnimation(TopProperty, slide, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void BeginFullscreenShowAnimation()
    {
        int generation = ++_fullscreenAnimationGeneration;
        double animatedTop = ResolveWindowTop();
        double animatedOpacity = Opacity;
        bool wasActuallyHidden = Visibility != Visibility.Visible;
        CancelFullscreenWindowPropertyAnimations();
        _fullscreenAnimationInProgress = true;

        double targetTop = ResolveWindowTop();
        double distance = ResolveFullscreenSlideDistance();
        double startTop = wasActuallyHidden
            ? targetTop - distance
            : Math.Min(animatedTop, targetTop);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(Constants.FullscreenShowAnimationMs);
        var slide = new DoubleAnimation(startTop, targetTop, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var fade = new DoubleAnimation(wasActuallyHidden ? 0.35 : animatedOpacity, 1.0, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        slide.Completed += (_, _) =>
        {
            if (generation != _fullscreenAnimationGeneration || _hiddenForFullscreen)
                return;

            _fullscreenAnimationInProgress = false;
            CancelFullscreenWindowPropertyAnimations();
        };

        BeginAnimation(TopProperty, slide, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        Visibility = Visibility.Visible;
        if (_hWnd != IntPtr.Zero && !User32.IsWindowVisible(_hWnd))
            User32.ShowWindow(_hWnd, User32.SW_SHOWNOACTIVATE);
        RearmShellFullscreenTracking();
    }

    private void HideFullscreenImmediately()
    {
        CancelFullscreenWindowPropertyAnimations();
        _fullscreenAnimationInProgress = false;
        Visibility = Visibility.Hidden;
        if (_hWnd != IntPtr.Zero && User32.IsWindowVisible(_hWnd))
            User32.ShowWindow(_hWnd, User32.SW_HIDE);
    }

    private void ShowFullscreenImmediately()
    {
        CancelFullscreenWindowPropertyAnimations();
        _fullscreenAnimationInProgress = false;
        Visibility = Visibility.Visible;
        if (_hWnd != IntPtr.Zero && !User32.IsWindowVisible(_hWnd))
            User32.ShowWindow(_hWnd, User32.SW_SHOWNOACTIVATE);
        RearmShellFullscreenTracking();
    }

    private void CancelFullscreenTransitionAnimation()
    {
        _fullscreenAnimationGeneration++;
        _fullscreenAnimationInProgress = false;
        CancelFullscreenWindowPropertyAnimations();
    }

    private void CancelFullscreenWindowPropertyAnimations()
    {
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
    }

    private double ResolveWindowTop()
        => double.IsNaN(Top) || double.IsInfinity(Top) ? 0.0 : Top;

    private double ResolveFullscreenSlideDistance()
    {
        double height = ActualHeight > 1 ? ActualHeight : Height;
        return Math.Max(
            42.0,
            height + Constants.FullscreenSlideExtraDistance);
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
