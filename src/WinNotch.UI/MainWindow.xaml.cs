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

namespace WinNotch.UI;

public partial class MainWindow : Window
{
    private IntPtr _hWnd;
    private HwndSource? _hwndSource;
    private WindowHookManager? _windowHookManager;
    private ClipboardService? _clipboardService;
    private DragDropService? _dragDropService;
    private MediaSessionService? _mediaSessionService;
    private Views.DropZoneView? _dropZoneView;
    private Views.MediaWidgetView? _mediaWidgetView;
    private Views.ClipboardToastView? _clipboardToastView;
    private Views.CommandHubView? _commandHubView;
    private readonly NotchMotionController _motionController;

    private bool _isDragging;
    private bool _isDraggingOut;
    private bool _hasActiveMediaSession;
    private bool _initialized;
    private bool _manuallyHidden;
    private NotchState _currentState = NotchState.Idle;
    private ModuleSettings _settings = new();
    private readonly NotchStateMachine _stateMachine = new();
    private readonly AttentionPolicy _attentionPolicy = new();
    private System.Windows.Threading.DispatcherTimer? _stateReturnTimer;
    private System.Windows.Threading.DispatcherTimer? _temporaryHideTimer;
    private NotchState? _scheduledReturnState;
    private double _currentWidth = Constants.NotchIdleWidth;
    private double _currentHeight = Constants.NotchIdleHeight;
    private int _hitWidthPx = (int)Constants.NotchIdleWidth;
    private int _hitHeightPx = (int)Constants.NotchIdleHeight;

    public ModuleSettings Settings => _settings;
    public MediaSessionService? MediaService => _mediaSessionService;
    public event EventHandler? SettingsRequested;

    public MainWindow()
    {
        InitializeComponent();
        _motionController = new NotchMotionController(this, SyncNativeGeometry);
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private Views.DropZoneView EnsureDropZoneView()
    {
        if (_dropZoneView != null) return _dropZoneView;

        _dropZoneView = new Views.DropZoneView { Visibility = Visibility.Visible };
        _dropZoneView.ShelfCleared += OnShelfCleared;
        _dropZoneView.DragOutStarted += OnShelfDragOutStarted;
        _dropZoneView.DragOutCompleted += OnShelfDragOutCompleted;
        _dropZoneView.ApplyAppearance(_settings.Appearance);
        DropZoneHost.Content = _dropZoneView;
        return _dropZoneView;
    }

    private Views.MediaWidgetView EnsureMediaWidgetView()
    {
        if (_mediaWidgetView != null) return _mediaWidgetView;

        _mediaWidgetView = new Views.MediaWidgetView { Visibility = Visibility.Visible };
        _mediaWidgetView.ApplyAppearance(_settings.Appearance);
        MediaWidgetHost.Content = _mediaWidgetView;
        return _mediaWidgetView;
    }

    private Views.CommandHubView EnsureCommandHubView()
    {
        if (_commandHubView != null) return _commandHubView;

        _commandHubView = new Views.CommandHubView { Visibility = Visibility.Visible };
        _commandHubView.ClipboardRequested += OnCommandHubClipboardRequested;
        _commandHubView.ShelfRequested += OnCommandHubShelfRequested;
        _commandHubView.SettingsRequested += OnCommandHubSettingsRequested;
        _commandHubView.ApplyAppearance(_settings.Appearance);
        _commandHubView.SetClipboardContext(_lastMeaningfulClipboard.Current);
        _commandHubView.SetShelfItemCount(_dropZoneView?.Items.Count ?? 0);
        CommandHubHost.Content = _commandHubView;
        return _commandHubView;
    }

    private void ReleaseCommandHubView()
    {
        if (_commandHubView == null) return;
        _commandHubView.ClipboardRequested -= OnCommandHubClipboardRequested;
        _commandHubView.ShelfRequested -= OnCommandHubShelfRequested;
        _commandHubView.SettingsRequested -= OnCommandHubSettingsRequested;
        CommandHubHost.Content = null;
        _commandHubView = null;
    }

    private Views.ClipboardToastView EnsureClipboardToastView()
    {
        if (_clipboardToastView != null) return _clipboardToastView;

        _clipboardToastView = new Views.ClipboardToastView { Visibility = Visibility.Visible };
        _clipboardToastView.MeaningfulContextAvailable += ClipboardToastView_MeaningfulContextAvailable;
        _clipboardToastView.ApplyAppearance(_settings.Appearance);
        ClipboardToastHost.Content = _clipboardToastView;
        return _clipboardToastView;
    }

    private void ReleaseClipboardToastView()
    {
        if (_clipboardToastView == null) return;
        _clipboardToastView.MeaningfulContextAvailable -= ClipboardToastView_MeaningfulContextAvailable;
        _clipboardToastView.ClearRetainedContent();
        ClipboardToastHost.Content = null;
        _clipboardToastView = null;
    }

    private void ReleaseDropZoneView()
    {
        if (_dropZoneView == null) return;
        _dropZoneView.ShelfCleared -= OnShelfCleared;
        _dropZoneView.DragOutStarted -= OnShelfDragOutStarted;
        _dropZoneView.DragOutCompleted -= OnShelfDragOutCompleted;
        _dropZoneView.ResetShelf(notify: false);
        DropZoneHost.Content = null;
        _dropZoneView = null;
    }

    private void ReleaseMediaWidgetView()
    {
        if (_mediaWidgetView == null) return;
        _mediaWidgetView.ClearSessionInfo();
        MediaWidgetHost.Content = null;
        _mediaWidgetView = null;
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
            ApplyVisibilityMode();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyExtendedStyles()
    {
        int exStyle = User32.GetWindowLong(_hWnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
        exStyle &= ~0x00040000;
        User32.SetExtendedStyle(_hWnd, exStyle);
    }

    private void ApplyWindowRegion()
        => UpdateWindowRegion(Constants.NotchIdleWidth, Constants.NotchIdleHeight);

    public void UpdateWindowRegion(double width, double height)
    {
        if (_hWnd == IntPtr.Zero) return;

        int w = Math.Max(1, (int)Math.Round(width));
        int h = Math.Max(1, (int)Math.Round(height));
        int radius = Math.Min((int)Constants.NotchCornerRadius, h / 2);

        IntPtr rounded = User32.CreateRoundRectRgn(0, 0, w + 1, h + 1, radius * 2, radius * 2);
        if (rounded == IntPtr.Zero) return;

        IntPtr topRect = User32.CreateRectRgn(0, 0, w + 1, Math.Min(h + 1, radius + 2));
        if (topRect != IntPtr.Zero)
        {
            User32.CombineRgn(rounded, rounded, topRect, User32.RGN_OR);
            User32.DeleteObject(topRect);
        }

        if (!User32.SetWindowRgn(_hWnd, rounded, true))
            User32.DeleteObject(rounded);
    }

    private void SyncNativeGeometry()
    {
        if (_hWnd == IntPtr.Zero) return;

        if (User32.GetClientRect(_hWnd, out var clientRect))
        {
            int width = Math.Max(1, clientRect.Right - clientRect.Left);
            int height = Math.Max(1, clientRect.Bottom - clientRect.Top);
            _hitWidthPx = width;
            _hitHeightPx = height;
            UpdateWindowRegion(width, height);
        }

        RecenterOnMonitor();
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (TryHandleShellFullscreenMessage(msg, wParam, lParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == ClipboardListener.WM_CLIPBOARDUPDATE)
        {
            _clipboardService?.OnClipboardUpdate();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == User32.WM_NCHITTEST)
        {
            // LPARAM carries signed 16-bit screen coordinates in its low 32 bits.
            // IntPtr.ToInt32 can overflow on 64-bit Windows for negative-monitor Y.
            long packedPoint = lParam.ToInt64();
            int x = (short)(packedPoint & 0xFFFF);
            int y = (short)((packedPoint >> 16) & 0xFFFF);
            var point = new User32.POINT { X = x, Y = y };
            User32.ScreenToClient(_hWnd, ref point);

            int padding = Constants.HitTestPadding;
            bool inside = point.X >= -padding && point.X <= _hitWidthPx + padding &&
                          point.Y >= -padding && point.Y <= _hitHeightPx + padding;

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
                User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
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
            User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
    }

    private void DetectAndApplyTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value && value == 1)
                ThemeBorder.Visibility = Visibility.Visible;
            else
                ThemeBorder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ThemeBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void InitializeServices()
    {
        UpdateWindowTracking();
        InitializeModuleServices();
    }

    private bool ShouldRunModuleServices()
        => !_manuallyHidden &&
           !string.Equals(_settings.VisibilityMode, "Hidden", StringComparison.OrdinalIgnoreCase);

    private void UpdateWindowTracking()
    {
        bool needsTracking = !_manuallyHidden && string.Equals(
            _settings.VisibilityMode,
            "Auto",
            StringComparison.OrdinalIgnoreCase);

        if (needsTracking && _windowHookManager == null)
        {
            _windowHookManager = new WindowHookManager();
            _windowHookManager.ForegroundWindowChanged += OnForegroundWindowChanged;
            _windowHookManager.StartTracking();
        }
        else if (!needsTracking && _windowHookManager != null)
        {
            _windowHookManager.ForegroundWindowChanged -= OnForegroundWindowChanged;
            _windowHookManager.Dispose();
            _windowHookManager = null;
        }
    }

    private void InitializeModuleServices()
    {
        if (!ShouldRunModuleServices()) return;

        bool needsClipboardListener = _settings.ModuleB_Clipboard || _settings.ModuleE_Screenshot;
        if (needsClipboardListener && _clipboardService == null)
        {
            _clipboardService = new ClipboardService();
            _clipboardService.NotificationRequested += OnClipboardNotification;
            _clipboardService.ImageNotificationRequested += OnClipboardImageNotification;
            bool started = _clipboardService.Start(_hWnd);
            System.Diagnostics.Debug.WriteLine($"[WinNotch] Clipboard listener started={started}");
        }
        _clipboardService?.SetContentPreferences(
            _settings.ModuleB_Clipboard,
            _settings.ModuleE_Screenshot);

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
    }

    private void DisposeDisabledModuleServices()
    {
        bool shouldRun = ShouldRunModuleServices();
        bool needsClipboardListener = shouldRun &&
            (_settings.ModuleB_Clipboard || _settings.ModuleE_Screenshot);
        if (!needsClipboardListener && _clipboardService != null)
        {
            _clipboardService.NotificationRequested -= OnClipboardNotification;
            _clipboardService.ImageNotificationRequested -= OnClipboardImageNotification;
            _clipboardService.Dispose();
            _clipboardService = null;
            ReleaseClipboardToastView();
            if (!_settings.ModuleB_Clipboard && !_settings.ModuleE_Screenshot)
            {
                _lastMeaningfulClipboard.Clear();
                _commandHubView?.SetClipboardContext(null);
            }
            if (_currentState is NotchState.ClipboardNotify or NotchState.ScreenshotNotify)
                TransitionToState(GetPersistentState(), force: true);
        }

        if ((!shouldRun || !_settings.ModuleA_DragDrop) && _dragDropService != null)
        {
            _dragDropService.FilesDropped -= OnFilesDropped;
            _dragDropService.DragEntered -= OnDragEntered;
            _dragDropService.DragLeft -= OnDragLeft;
            _dragDropService = null;
        }
        if (!_settings.ModuleA_DragDrop && _dropZoneView != null)
        {
            ReleaseDropZoneView();
            TransitionToState(GetPersistentState(), force: true);
        }

        if ((!shouldRun || !_settings.ModuleC_Media) && _mediaSessionService != null)
        {
            _mediaSessionService.SessionChanged -= OnMediaSessionChanged;
            _mediaSessionService.Dispose();
            _mediaSessionService = null;
            _hasActiveMediaSession = false;
            ReleaseMediaWidgetView();
            if (_currentState is NotchState.MediaActive or NotchState.MediaAmbient)
                TransitionToState(GetPersistentState(), force: true);
        }
    }

    public void OnSettingsChanged()
    {
        UpdateWindowTracking();
        DisposeDisabledModuleServices();
        InitializeModuleServices();
        UpdateRuntimeReliabilityChecks();
        UpdateFullscreenFallbackChecks();
        PositionOnTargetMonitor();
        ApplyVisibilityMode();
    }

    private void ApplyVisibilityMode()
    {
        if (!_initialized) return;

        if (string.Equals(_settings.VisibilityMode, "Hidden", StringComparison.OrdinalIgnoreCase))
        {
            _hiddenForFullscreen = false;
            Visibility = Visibility.Hidden;
            return;
        }

        if (string.Equals(_settings.VisibilityMode, "AlwaysShow", StringComparison.OrdinalIgnoreCase))
        {
            _hiddenForFullscreen = false;
            if (!_manuallyHidden)
                Visibility = Visibility.Visible;
            return;
        }

        VerifyAutomaticFullscreenVisibility();
    }

    private bool ShouldShowMediaAmbient()
        => _settings.ModuleC_Media && _hasActiveMediaSession;

    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isDraggingOut) return;

        if (_currentState == NotchState.Idle)
        {
            if (_hasActiveMediaSession && _settings.ModuleC_Media)
                TransitionToState(NotchState.MediaActive);
            else
                TransitionToState(NotchState.Hover);
        }
        else if (_currentState == NotchState.ShelfOccupied)
            TransitionToState(NotchState.ShelfExpanded);
        else if (_currentState == NotchState.MediaAmbient)
            TransitionToState(NotchState.MediaActive);
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isDraggingOut) return;

        if (_currentState == NotchState.Hover)
            TransitionToState(NotchState.Idle, force: true);
        else if (_currentState == NotchState.ShelfExpanded)
            TransitionToState(NotchState.ShelfOccupied, force: true);
        else if (_currentState == NotchState.MediaActive)
            TransitionToState(GetPersistentState(), force: true);
    }

    private void RootGrid_DragEnter(object sender, DragEventArgs e)
    {
        if (_isDraggingOut || !_settings.ModuleA_DragDrop)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        _isDragging = true;
        EnsureDropZoneView().ShowDropTarget();
        _dragDropService?.NotifyDragEnter();
        TransitionToState(NotchState.DragActive, StatePriority.DropTarget, force: true);
        e.Handled = true;
    }

    private void RootGrid_DragLeave(object sender, DragEventArgs e)
    {
        if (_isDraggingOut)
        {
            e.Handled = true;
            return;
        }

        _isDragging = false;
        _dragDropService?.NotifyDragLeave();
        TransitionToState(GetPersistentState(), force: true);
        e.Handled = true;
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (_isDraggingOut || !_settings.ModuleA_DragDrop)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (_isDraggingOut || !_settings.ModuleA_DragDrop)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        _isDragging = false;
        _dragDropService?.HandleFileDrop(e);
        e.Handled = true;
    }

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
        ApplyDimensions(newState, force: force && !_initialized);
        ScheduleReturn(timeout ?? result.Timeout, returnState ?? result.ReturnState);
    }

    private void UpdateContentVisibility(NotchState state)
    {
        bool shelfVisible = state is NotchState.DragActive or NotchState.DropResult or
            NotchState.ShelfOccupied or NotchState.ShelfExpanded or NotchState.ShelfDraggingOut;

        IdleContent.Visibility = state is NotchState.Idle or NotchState.Hover
            ? Visibility.Visible : Visibility.Collapsed;
        MediaAmbientContent.Visibility = state == NotchState.MediaAmbient
            ? Visibility.Visible : Visibility.Collapsed;
        DropZoneHost.Visibility = shelfVisible ? Visibility.Visible : Visibility.Collapsed;
        MediaWidgetHost.Visibility = state == NotchState.MediaActive ? Visibility.Visible : Visibility.Collapsed;
        bool commandHubVisible = state == NotchState.CommandHub;
        CommandHubHost.Visibility = commandHubVisible ? Visibility.Visible : Visibility.Collapsed;
        if (commandHubVisible)
            EnsureCommandHubView();
        else
            ReleaseCommandHubView();

        bool contextVisible = state is NotchState.ClipboardNotify or NotchState.ScreenshotNotify;
        ClipboardToastHost.Visibility = contextVisible ? Visibility.Visible : Visibility.Collapsed;
        if (!contextVisible)
            ReleaseClipboardToastView();

        if (shelfVisible)
        {
            Views.DropZoneView shelf = EnsureDropZoneView();
            if (state == NotchState.DragActive)
                shelf.ShowDropTarget();
            else
                shelf.SetExpanded(state is NotchState.DropResult or NotchState.ShelfExpanded or NotchState.ShelfDraggingOut);
        }
        else
        {
            _dropZoneView?.SetExpanded(false);
        }
    }

    private void ApplyDimensions(NotchState state, bool force = false)
    {
        var (w, h) = ResolveAppearanceDimensions(state);
        if (!force && Math.Abs(w - _currentWidth) < 1 && Math.Abs(h - _currentHeight) < 1)
            return;

        _currentWidth = w;
        _currentHeight = h;
        _motionController.Apply(w, h, immediate: force || !_initialized);
    }

    private void ScheduleReturn(TimeSpan? timeout, NotchState? returnState)
    {
        _stateReturnTimer?.Stop();
        _scheduledReturnState = null;
        if (timeout == null) return;

        if (_stateReturnTimer == null)
        {
            _stateReturnTimer = new System.Windows.Threading.DispatcherTimer();
            _stateReturnTimer.Tick += StateReturnTimer_Tick;
        }

        _scheduledReturnState = returnState;
        _stateReturnTimer.Interval = timeout.Value;
        _stateReturnTimer.Start();
    }

    private void StateReturnTimer_Tick(object? sender, EventArgs e)
    {
        _stateReturnTimer?.Stop();
        NotchState target = _scheduledReturnState ?? GetPersistentState();
        _scheduledReturnState = null;
        _stateMachine.ReturnTo(target);
        _currentState = target;
        UpdateContentVisibility(target);
        ApplyDimensions(target);
    }

    private NotchState GetPersistentState()
    {
        if (_dropZoneView?.HasItems == true)
            return NotchState.ShelfOccupied;
        if (ShouldShowMediaAmbient())
            return NotchState.MediaAmbient;
        return NotchState.Idle;
    }

    private void OnForegroundWindowChanged(object? sender, ForegroundChangedEventArgs e)
    {
        if (!_initialized) return;

        // WinEvent callbacks run outside WPF's dispatcher. Resolve the current
        // foreground window on the UI turn instead of trusting an event HWND which
        // may already be stale during Chromium's two-step fullscreen transition.
        Dispatcher.BeginInvoke(
            () => VerifyAutomaticFullscreenVisibility(),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnClipboardNotification(object? sender, ClipboardNotification e)
    {
        if (!_settings.ModuleB_Clipboard) return;

        Dispatcher.Invoke(() =>
        {
            string rawText = e.RawText ?? e.PreviewText ?? string.Empty;
            var contentType = ClipboardClassifier.Classify(rawText);
            var decision = _attentionPolicy.ClassifyClipboard(
                contentType,
                rawText,
                _settings.ReactionLevel);
            if (decision.Level == AttentionLevel.Silent || decision.Suppressed) return;

            EnsureClipboardToastView().SetNotification(e, contentType);
            TransitionToState(
                decision.TargetState,
                decision.Priority,
                decision.Duration,
                GetPersistentState());
        });
    }

    private void OnClipboardImageNotification(object? sender, ClipboardImageNotification e)
    {
        if (!_settings.ModuleE_Screenshot) return;

        Dispatcher.Invoke(() =>
        {
            var decision = _attentionPolicy.ClassifyScreenshot();
            if (decision.Suppressed) return;

            EnsureClipboardToastView().SetImageNotification(e);
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
            EnsureDropZoneView().SetDroppedPaths(e.DroppedPaths);
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
    }

    private void OnDragLeft(object? sender, EventArgs e)
    {
    }

    private void OnShelfCleared(object? sender, EventArgs e)
        => Dispatcher.Invoke(() =>
        {
            TransitionToState(GetPersistentState(), force: true);
            _dragDropService?.ClearHistory();
            ReleaseDropZoneView();
        });

    private void OnShelfDragOutStarted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isDraggingOut = true;
            _isDragging = false;
            TransitionToState(NotchState.ShelfDraggingOut, force: true);
        });
    }

    private void OnShelfDragOutCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isDraggingOut = false;
            TransitionToState(GetPersistentState(), force: true);
        });
    }

    private void OnMediaSessionChanged(object? sender, MediaSessionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            bool hadSession = _hasActiveMediaSession;
            _hasActiveMediaSession = e.Session.HasSession;

            if (_hasActiveMediaSession)
            {
                EnsureMediaWidgetView().SetSessionInfo(e.Session);
                MediaAmbientTitle.Text = string.IsNullOrWhiteSpace(e.Session.Title)
                    ? "Medya"
                    : e.Session.Title;

                if (_dropZoneView?.HasItems == true || _isDragging || _isDraggingOut ||
                    !ShouldShowMediaAmbient())
                    return;

                // Timeline/playback/property updates are data updates, not UI state
                // transitions. In particular they must never collapse MediaActive.
                // Establish the media surface only when media first appears or an
                // idle-like state needs reconciliation; hover decides its size.
                if (_currentState is NotchState.Idle or NotchState.Hover)
                {
                    TransitionToState(
                        RootGrid.IsMouseOver ? NotchState.MediaActive : NotchState.MediaAmbient,
                        force: true);
                }

                return;
            }

            MediaAmbientTitle.Text = "Medya";
            ReleaseMediaWidgetView();
            if (hadSession &&
                _currentState is (NotchState.MediaActive or NotchState.MediaAmbient))
                TransitionToState(GetPersistentState(), force: true);
        });
    }

    private void HideNotchTemporarily()
    {
        _manuallyHidden = true;
        Visibility = Visibility.Hidden;
        UpdateWindowTracking();
        DisposeDisabledModuleServices();
        UpdateRuntimeReliabilityChecks();
        UpdateFullscreenFallbackChecks();

        _temporaryHideTimer ??= CreateTemporaryHideTimer();
        _temporaryHideTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreateTemporaryHideTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.TemporaryHideDurationMs)
        };
        timer.Tick += TemporaryHideTimer_Tick;
        return timer;
    }

    private void TemporaryHideTimer_Tick(object? sender, EventArgs e)
    {
        ReleaseTemporaryHideTimer();
        _manuallyHidden = false;
        UpdateWindowTracking();
        InitializeModuleServices();
        UpdateRuntimeReliabilityChecks();
        UpdateFullscreenFallbackChecks();
        ApplyVisibilityMode();
    }

    private void ReleaseTemporaryHideTimer()
    {
        if (_temporaryHideTimer == null) return;
        _temporaryHideTimer.Stop();
        _temporaryHideTimer.Tick -= TemporaryHideTimer_Tick;
        _temporaryHideTimer = null;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_stateReturnTimer != null)
        {
            _stateReturnTimer.Stop();
            _stateReturnTimer.Tick -= StateReturnTimer_Tick;
            _stateReturnTimer = null;
        }
        ReleaseCommandHubLeaveTimer();
        ReleaseCommandHubView();
        ReleaseTemporaryHideTimer();
        _motionController.Dispose();
        ReleaseDropZoneView();

        _windowHookManager?.Dispose();
        _clipboardService?.Dispose();
        _mediaSessionService?.Dispose();
        ReleaseMediaWidgetView();
        ReleaseClipboardToastView();
        _hwndSource?.RemoveHook(WndProc);
    }
}
