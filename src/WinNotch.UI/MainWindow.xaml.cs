using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinNotch.Common;
using WinNotch.Core.Interop;
using WinNotch.Core.Services;

using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Application = System.Windows.Application;

namespace WinNotch.UI;

/// <summary>
/// Native-backed top-center WinNotch window.
/// Owns window integration and routes module events into the lightweight state machine.
/// </summary>
public partial class MainWindow : Window
{
    private IntPtr _hWnd;
    private HwndSource? _hwndSource;
    private WindowHookManager? _windowHookManager;
    private ClipboardService? _clipboardService;
    private DragDropService? _dragDropService;
    private MediaSessionService? _mediaSessionService;
    private WindowPinService? _windowPinService;
    private PowerMonitorService? _powerMonitorService;

    private bool _isDragging;
    private bool _initialized;
    private bool _manuallyHidden;
    private NotchState _currentState = NotchState.Idle;
    private ModuleSettings _settings = new();
    private readonly NotchStateMachine _stateMachine = new();
    private readonly AttentionPolicy _attentionPolicy = new();
    private System.Windows.Threading.DispatcherTimer? _stateReturnTimer;
    private double _currentWidth = Constants.NotchIdleWidth;
    private double _currentHeight = Constants.NotchIdleHeight;

    public ModuleSettings Settings => _settings;
    public MediaSessionService? MediaService => _mediaSessionService;

    public MainWindow()
    {
        InitializeComponent();
        DropZoneView.ShelfCleared += OnShelfCleared;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    public void SetSettings(ModuleSettings settings) => _settings = settings;

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hWnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hWnd);
        _hwndSource?.AddHook(WndProc);

        try
        {
            ApplyExtendedStyles();
            DwmApi.ExtendGlassFrame(_hWnd);
            ApplyWindowRegion();
            PositionOnTargetMonitor();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinNotch] SourceInitialized error: {ex}");
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        InitializeServices();
        DetectAndApplyTheme();
        TransitionToState(NotchState.Idle, force: true);

        Dispatcher.BeginInvoke(() =>
        {
            ApplyExtendedStyles();
            ApplyDimensions(_currentState, force: true);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyExtendedStyles()
    {
        int exStyle = User32.GetWindowLong(_hWnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
        exStyle &= ~0x00040000; // WS_EX_APPWINDOW
        User32.SetExtendedStyle(_hWnd, exStyle);
    }

    private void ApplyWindowRegion()
        => UpdateWindowRegion(Constants.NotchIdleWidth, Constants.NotchIdleHeight);

    public void UpdateWindowRegion(double width, double height)
    {
        int w = Math.Max(1, (int)Math.Round(width));
        int h = Math.Max(1, (int)Math.Round(height));
        int radius = Math.Min((int)Constants.NotchCornerRadius, h / 2);

        IntPtr hRgn = User32.CreateRoundRectRgn(0, 0, w, h, radius * 2, radius * 2);
        if (hRgn != IntPtr.Zero)
            User32.SetWindowRgn(_hWnd, hRgn, true);
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == ClipboardListener.WM_CLIPBOARDUPDATE)
        {
            _clipboardService?.OnClipboardUpdate();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == User32.WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt32() & 0xFFFF);
            int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
            var point = new User32.POINT { X = x, Y = y };
            User32.ScreenToClient(_hWnd, ref point);

            int padding = Constants.HitTestPadding;
            bool inside = point.X >= -padding && point.X <= _currentWidth + padding &&
                          point.Y >= -padding && point.Y <= _currentHeight + padding;

            handled = true;
            return new IntPtr(inside ? User32.HTCLIENT : User32.HTTRANSPARENT);
        }

        if (msg == User32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(User32.MA_NOACTIVATE);
        }

        if (msg == User32.WM_DISPLAYCHANGE)
        {
            Dispatcher.BeginInvoke(() => ApplyDimensions(_currentState, force: true),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        return IntPtr.Zero;
    }

    public void PositionOnTargetMonitor()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;

        int targetIndex = Math.Clamp(_settings.TargetMonitorIndex, 0, screens.Length - 1);
        if (_settings.TargetMonitorIndex >= screens.Length)
            _settings.TargetMonitorIndex = 0;

        var screen = screens[targetIndex];
        int x = screen.Bounds.Left + (screen.Bounds.Width - (int)_currentWidth) / 2;
        int y = screen.Bounds.Top;

        if (_hWnd != IntPtr.Zero)
        {
            User32.SetWindowPos(
                _hWnd,
                User32.HWND_TOPMOST,
                x, y, 0, 0,
                User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        }
        else
        {
            Left = x;
            Top = y;
        }
    }

    private void RecenterOnMonitor()
    {
        if (_hWnd == IntPtr.Zero) return;

        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;
        int targetIndex = Math.Clamp(_settings.TargetMonitorIndex, 0, screens.Length - 1);
        var screen = screens[targetIndex];

        User32.GetWindowRect(_hWnd, out var rect);
        int actualWidth = Math.Max(1, rect.Right - rect.Left);
        int x = screen.Bounds.Left + (screen.Bounds.Width - actualWidth) / 2;

        User32.SetWindowPos(
            _hWnd,
            User32.HWND_TOPMOST,
            x, screen.Bounds.Top, 0, 0,
            User32.SWP_NOACTIVATE | 0x0040 | User32.SWP_NOSIZE);
    }

    private void DetectAndApplyTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value && value == 1)
                ThemeBorder.Visibility = Visibility.Visible;
        }
        catch { }
    }

    private void InitializeServices()
    {
        _windowHookManager = new WindowHookManager();
        _windowHookManager.ForegroundWindowChanged += OnForegroundWindowChanged;
        _windowHookManager.StartTracking();

        _powerMonitorService = new PowerMonitorService();
        _powerMonitorService.Initialize();

        InitializeModuleServices();
    }

    private void InitializeModuleServices()
    {
        if (_settings.ModuleB_Clipboard && _clipboardService == null)
        {
            _clipboardService = new ClipboardService();
            _clipboardService.NotificationRequested += OnClipboardNotification;
            _clipboardService.ImageNotificationRequested += OnClipboardImageNotification;
            bool started = _clipboardService.Start(_hWnd);
            System.Diagnostics.Debug.WriteLine($"[WinNotch] Clipboard listener started={started}");
        }

        if (_settings.ModuleA_DragDrop && _dragDropService == null)
        {
            _dragDropService = new DragDropService();
            _dragDropService.FilesDropped += OnFilesDropped;
            _dragDropService.DragEntered += OnDragEntered;
            _dragDropService.DragLeft += OnDragLeft;
        }

        if (_settings.ModuleC_Media && _mediaSessionService == null)
        {
            _mediaSessionService = new MediaSessionService();
            _mediaSessionService.SessionChanged += OnMediaSessionChanged;
            _ = _mediaSessionService.InitializeAsync();
        }

        if (_settings.ModuleD_WindowPin && _windowPinService == null)
        {
            _windowPinService = new WindowPinService();
            _windowPinService.WindowPinChanged += OnWindowPinChanged;
        }
    }

    private void DisposeDisabledModuleServices()
    {
        if (!_settings.ModuleB_Clipboard && _clipboardService != null)
        {
            _clipboardService.NotificationRequested -= OnClipboardNotification;
            _clipboardService.ImageNotificationRequested -= OnClipboardImageNotification;
            _clipboardService.Dispose();
            _clipboardService = null;
        }

        if (!_settings.ModuleA_DragDrop && _dragDropService != null)
        {
            _dragDropService.FilesDropped -= OnFilesDropped;
            _dragDropService.DragEntered -= OnDragEntered;
            _dragDropService.DragLeft -= OnDragLeft;
            _dragDropService = null;
            DropZoneView.SetDroppedPaths(Array.Empty<string>());
            TransitionToState(NotchState.Idle, force: true);
        }

        if (!_settings.ModuleC_Media && _mediaSessionService != null)
        {
            _mediaSessionService.SessionChanged -= OnMediaSessionChanged;
            _mediaSessionService.Dispose();
            _mediaSessionService = null;
            if (_currentState is NotchState.MediaActive or NotchState.MediaAmbient)
                TransitionToState(GetPersistentState(), force: true);
        }

        if (!_settings.ModuleD_WindowPin && _windowPinService != null)
        {
            _windowPinService.WindowPinChanged -= OnWindowPinChanged;
            _windowPinService.Dispose();
            _windowPinService = null;
        }
    }

    public void OnSettingsChanged()
    {
        DisposeDisabledModuleServices();
        InitializeModuleServices();
        PositionOnTargetMonitor();
    }

    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDragging) return;

        if (_currentState == NotchState.Idle)
            TransitionToState(NotchState.Hover);
        else if (_currentState == NotchState.ShelfOccupied)
            TransitionToState(NotchState.ShelfExpanded);
        else if (_currentState == NotchState.MediaAmbient)
            TransitionToState(NotchState.MediaActive);
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging) return;

        if (_currentState == NotchState.Hover)
            TransitionToState(NotchState.Idle, force: true);
        else if (_currentState == NotchState.ShelfExpanded)
            TransitionToState(NotchState.ShelfOccupied, force: true);
        else if (_currentState == NotchState.MediaActive)
            TransitionToState(DropZoneView.HasItems ? NotchState.ShelfOccupied : NotchState.MediaAmbient, force: true);
    }

    private void RootGrid_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        _isDragging = true;
        DropZoneView.ShowDropTarget();
        _dragDropService?.NotifyDragEnter();
        TransitionToState(NotchState.DragActive, StatePriority.DropTarget, force: true);
        e.Handled = true;
    }

    private void RootGrid_DragLeave(object sender, DragEventArgs e)
    {
        _isDragging = false;
        _dragDropService?.NotifyDragLeave();
        TransitionToState(GetPersistentState(), force: true);
        e.Handled = true;
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void RootGrid_Drop(object sender, DragEventArgs e)
    {
        _isDragging = false;
        _dragDropService?.HandleFileDrop(e);
        e.Handled = true;
    }

    private void RootGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        => ShowContextMenu();

    private void TransitionToState(
        NotchState newState,
        StatePriority priority = StatePriority.None,
        TimeSpan? timeout = null,
        NotchState? returnState = null,
        bool force = false)
    {
        if (priority == StatePriority.None)
            priority = NotchStateMachine.PriorityFor(newState);

        StateTransition result = force
            ? _stateMachine.ForceTransition(newState, timeout, returnState)
            : _stateMachine.TryTransition(newState, priority, timeout, returnState);

        if (!result.ShouldApply && !force) return;

        _currentState = newState;
        UpdateContentVisibility(newState);
        ApplyDimensions(newState);
        ScheduleReturn(timeout ?? result.Timeout, returnState ?? result.ReturnState);
    }

    private void UpdateContentVisibility(NotchState state)
    {
        bool shelfVisible = state is NotchState.DragActive or NotchState.DropResult or
            NotchState.ShelfOccupied or NotchState.ShelfExpanded or NotchState.ShelfDraggingOut;

        IdleContent.Visibility = state is NotchState.Idle or NotchState.Hover or NotchState.MediaAmbient
            ? Visibility.Visible : Visibility.Collapsed;
        DropZoneView.Visibility = shelfVisible ? Visibility.Visible : Visibility.Collapsed;
        MediaWidgetView.Visibility = state == NotchState.MediaActive ? Visibility.Visible : Visibility.Collapsed;
        ClipboardToastView.Visibility = state is NotchState.ClipboardNotify or NotchState.ScreenshotNotify
            ? Visibility.Visible : Visibility.Collapsed;

        if (state == NotchState.DragActive)
            DropZoneView.ShowDropTarget();
        else
            DropZoneView.SetExpanded(state is NotchState.DropResult or NotchState.ShelfExpanded or NotchState.ShelfDraggingOut);
    }

    private void ApplyDimensions(NotchState state, bool force = false)
    {
        var (w, h) = StateDimensions.GetDimensions(state);
        if (!force && Math.Abs(w - _currentWidth) < 1 && Math.Abs(h - _currentHeight) < 1)
            return;

        _currentWidth = w;
        _currentHeight = h;
        Width = w;
        Height = h;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateWindowRegion(w, h);
            RecenterOnMonitor();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void ScheduleReturn(TimeSpan? timeout, NotchState? returnState)
    {
        _stateReturnTimer?.Stop();
        _stateReturnTimer = null;
        if (timeout == null) return;

        _stateReturnTimer = new System.Windows.Threading.DispatcherTimer { Interval = timeout.Value };
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            _stateReturnTimer?.Stop();
            if (_stateReturnTimer != null && handler != null)
                _stateReturnTimer.Tick -= handler;
            _stateReturnTimer = null;

            NotchState target = returnState ?? GetPersistentState();
            _stateMachine.ReturnTo(target);
            _currentState = target;
            UpdateContentVisibility(target);
            ApplyDimensions(target);
        };
        _stateReturnTimer.Tick += handler;
        _stateReturnTimer.Start();
    }

    private NotchState GetPersistentState()
    {
        if (DropZoneView.HasItems)
            return NotchState.ShelfOccupied;
        if (_mediaSessionService != null && _currentState is NotchState.MediaActive or NotchState.MediaAmbient)
            return NotchState.MediaAmbient;
        return NotchState.Idle;
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundChangedEventArgs e)
    {
        if (!_initialized || _settings.VisibilityMode == "AlwaysShow" || _settings.VisibilityMode == "Hidden")
            return;
        if (e.ClassName is "Shell_TrayWnd" or "WorkerW" or "Shell_SecondaryTrayWnd")
            return;

        bool isFullscreen = WindowHookManager.IsWindowFullscreen(e.WindowHandle);
        bool isMaximized = WindowHookManager.IsWindowMaximized(e.WindowHandle);

        Dispatcher.Invoke(() =>
        {
            if (isFullscreen && !isMaximized && Visibility == Visibility.Visible)
                Visibility = Visibility.Hidden;
            else if (!isFullscreen && Visibility == Visibility.Hidden && !_manuallyHidden)
                Visibility = Visibility.Visible;
        });
    }

    private void OnClipboardNotification(object? sender, ClipboardNotification e)
    {
        Dispatcher.Invoke(() =>
        {
            var contentType = ClipboardClassifier.Classify(e.PreviewText);
            var decision = _attentionPolicy.ClassifyClipboard(contentType, e.PreviewText);
            if (decision.Level == AttentionLevel.Silent || decision.Suppressed) return;

            ClipboardToastView.SetNotification(e, contentType);
            TransitionToState(
                decision.TargetState,
                decision.Priority,
                decision.Duration,
                GetPersistentState());
        });
    }

    private void OnClipboardImageNotification(object? sender, ClipboardImageNotification e)
    {
        Dispatcher.Invoke(() =>
        {
            var decision = _attentionPolicy.ClassifyScreenshot();
            if (decision.Suppressed) return;

            ClipboardToastView.SetImageNotification(e);
            TransitionToState(
                decision.TargetState,
                decision.Priority,
                decision.Duration,
                GetPersistentState());
        });
    }

    private void OnFilesDropped(object? sender, DragDropEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DropZoneView.SetDroppedPaths(e.DroppedPaths);
            TransitionToState(
                NotchState.DropResult,
                StatePriority.DropResult,
                TimeSpan.FromMilliseconds(Constants.DropResultDisplayDurationMs),
                NotchState.ShelfOccupied,
                force: true);
        });
    }

    private void OnDragEntered(object? sender, EventArgs e)
    {
        // RootGrid_DragEnter owns the visual transition so it can preserve shelf state reliably.
    }

    private void OnDragLeft(object? sender, EventArgs e)
    {
        // RootGrid_DragLeave owns the visual transition.
    }

    private void OnShelfCleared(object? sender, EventArgs e)
        => Dispatcher.Invoke(() => TransitionToState(NotchState.Idle, force: true));

    private void OnMediaSessionChanged(object? sender, MediaSessionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Session.HasSession)
            {
                MediaWidgetView.SetSessionInfo(e.Session);
                if (!DropZoneView.HasItems)
                {
                    var decision = _attentionPolicy.ClassifyMediaChange(true);
                    TransitionToState(decision.TargetState, decision.Priority, decision.Duration, NotchState.MediaAmbient);
                }
            }
            else if (!DropZoneView.HasItems && _currentState is NotchState.MediaActive or NotchState.MediaAmbient)
            {
                TransitionToState(NotchState.Idle, force: true);
            }
        });
    }

    private void OnWindowPinChanged(object? sender, WindowPinEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MainWindow] Window {(e.IsPinned ? "pinned" : "unpinned")}: {e.WindowTitle}");
    }

    private void ShowContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Ayarlar" };
        menu.Items.Add(settingsItem);

        var hideItem = new System.Windows.Controls.MenuItem { Header = "1 saat gizle" };
        hideItem.Click += (_, _) => HideNotchTemporarily();
        menu.Items.Add(hideItem);

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Çıkış" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    private void HideNotchTemporarily()
    {
        _manuallyHidden = true;
        Visibility = Visibility.Hidden;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.TemporaryHideDurationMs)
        };
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            timer.Stop();
            if (handler != null) timer.Tick -= handler;
            _manuallyHidden = false;
            Visibility = Visibility.Visible;
        };
        timer.Tick += handler;
        timer.Start();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _stateReturnTimer?.Stop();
        DropZoneView.ShelfCleared -= OnShelfCleared;

        _windowPinService?.UnpinAll();
        _windowHookManager?.Dispose();
        _clipboardService?.Dispose();
        _mediaSessionService?.Dispose();
        _windowPinService?.Dispose();
        _powerMonitorService?.Dispose();
        _hwndSource?.RemoveHook(WndProc);
    }
}
