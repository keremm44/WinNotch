// WinNotch.UI/MainWindow.xaml.cs
// WHY: This is the heart of WinNotch's Win32 integration.
// Every critical behavior is implemented here:
// - WndProc override for WM_NCHITTEST (click-through behavior)
// - DwmExtendFrameIntoClientArea for native transparency
// - SetWindowRgn for rounded-rect clipping
// - Window positioning on correct monitor
// - Service initialization and lifecycle management
//
// PERFORMANCE: The WndProc override is lightweight — it only checks
// mouse position and returns HTTRANSPARENT or HTCLIENT. No allocations,
// no heavy computation. This runs on every mouse move but is negligible.
//
// MEMORY: All services are created once and held as fields.
// No per-frame allocations. No DispatcherTimers running in idle state.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinNotch.Common;
using WinNotch.Core.Interop;
using WinNotch.Core.Services;

// Disambiguate WPF vs WinForms types
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Application = System.Windows.Application;

namespace WinNotch.UI;

/// <summary>
/// Main notch window. Borderless, topmost, always visible.
/// Handles Win32 interop for transparency, hit-testing, and lifecycle.
/// </summary>
public partial class MainWindow : Window
{
    // ═══════════════════════════════════════════════════════════════
    // FIELDS
    // ═══════════════════════════════════════════════════════════════

    private IntPtr _hWnd;
    private HwndSource? _hwndSource;
    private WindowHookManager? _windowHookManager;
    private ClipboardService? _clipboardService;
    private DragDropService? _dragDropService;
    private MediaSessionService? _mediaSessionService;
    private WindowPinService? _windowPinService;
    private PowerMonitorService? _powerMonitorService;

    private bool _isDragging;
    private NotchState _currentState = NotchState.Idle;
    private ModuleSettings _settings = new();
    private DateTime _lastInteraction = DateTime.MinValue;

    /// <summary>
    /// Exposes settings to the tray app for module toggling.
    /// </summary>
    public ModuleSettings Settings => _settings;

    /// <summary>
    /// Injects settings loaded from disk. Must be called before Show().
    /// WHY: Settings are loaded in App.OnStartup from SettingsStore.Load().
    /// MainWindow needs them before InitializeServices() to decide which
    /// modules to create.
    /// </summary>
    public void SetSettings(ModuleSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Exposes services for diagnostics panel.
    /// </summary>
    public MediaSessionService? MediaService => _mediaSessionService;

    // ═══════════════════════════════════════════════════════════════
    // CONSTRUCTION & INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        // WHY SourceInitialized: This fires BEFORE the window is shown,
        // giving us the HWND early. We set up Win32 here so WPF
        // doesn't overwrite our styles during Loaded/render.
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// SourceInitialized fires first — HWND is available, window not yet shown.
    /// This is the BEST place for Win32 interop setup.
    /// </summary>
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hWnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hWnd);

        // Register WndProc hook
        _hwndSource?.AddHook(WndProc);

        // ═══════════════════════════════════════════════════════════
        // Apply ALL Win32 setup here — BEFORE window is shown.
        // WHY SourceInitialized: HWND is valid but WPF hasn't rendered yet.
        // Applying styles here gives them the best chance of sticking.
        // ═══════════════════════════════════════════════════════════
        try
        {
            // 1. Extended styles FIRST (before DWM, which may affect style visibility)
            ApplyExtendedStyles();

            // 2. DWM transparency
            DwmApi.ExtendGlassFrame(_hWnd);

            // 3. Window region (rounded rect)
            ApplyWindowRegion();

            // 4. Position on monitor
            PositionOnTargetMonitor();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinNotch] SourceInitialized error: {ex}");
        }
    }

    /// <summary>
    /// Called after window is loaded.
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        // Initialize services
        InitializeServices();

        // Theme detection
        DetectAndApplyTheme();

        // ═══════════════════════════════════════════════════════════
        // WHY: WPF may have overwritten our styles during the first render.
        // Re-apply styles ONCE after render completes.
        // No DispatcherTimer — single re-application is sufficient.
        // ═══════════════════════════════════════════════════════════
        Dispatcher.BeginInvoke(() =>
        {
            ApplyExtendedStyles();
            PositionOnTargetMonitor();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ═══════════════════════════════════════════════════════════════
    // WIN32 WINDOW STYLES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies extended window styles via Win32 SetWindowLong.
    /// WHY: WPF doesn't expose WS_EX_TOOLWINDOW or WS_EX_NOACTIVATE
    /// in XAML. We must set them via P/Invoke after HWND creation.
    /// </summary>
    private void ApplyExtendedStyles()
    {
        int exStyle = User32.GetWindowLong(_hWnd, User32.GWL_EXSTYLE);

        // Add: Tool window (hidden from Alt+Tab), No activate (no focus steal)
        // WHY NOT WS_EX_LAYERED: DwmExtendFrameIntoClientArea handles transparency.
        // Adding WS_EX_LAYERED without SetLayeredWindowAttributes makes window INVISIBLE!
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;

        // Remove: App window (prevents taskbar appearance)
        exStyle &= ~0x00040000; // WS_EX_APPWINDOW

        // Use SetExtendedStyle which handles 32/64-bit and forces frame update
        User32.SetExtendedStyle(_hWnd, exStyle);
    }

    // ═══════════════════════════════════════════════════════════════
    // WINDOW REGION (Rounded Rectangle Clipping)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates and applies a rounded-rect window region.
    /// WHY: SetWindowRgn clips the window to a region. The DWM compositor
    /// renders the anti-aliased edges. This is how we get the pill shape
    /// without AllowsTransparency="True".
    /// </summary>
    private void ApplyWindowRegion()
    {
        int width = (int)Constants.NotchIdleWidth;
        int height = (int)Constants.NotchIdleHeight;
        int radius = (int)Constants.NotchCornerRadius;

        // CreateRoundRectRgn(x1, y1, x2, y2, ellipseWidth, ellipseHeight)
        IntPtr hRgn = User32.CreateRoundRectRgn(0, 0, width, height, radius, radius);
        if (hRgn != IntPtr.Zero)
        {
            User32.SetWindowRgn(_hWnd, hRgn, true);
            // Note: After SetWindowRgn, the system owns the region handle.
            // We must NOT call DeleteObject on it.
        }
    }

    /// <summary>
    /// Updates the window region for expanded dimensions.
    /// Called during animations when the notch changes size.
    /// </summary>
    public void UpdateWindowRegion(double width, double height)
    {
        int w = (int)width;
        int h = (int)height;
        int radius = (int)Constants.NotchCornerRadius;

        IntPtr hRgn = User32.CreateRoundRectRgn(0, 0, w, h, radius, radius);
        if (hRgn != IntPtr.Zero)
        {
            User32.SetWindowRgn(_hWnd, hRgn, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // WNDPROC OVERRIDE (Hit-Testing)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Window procedure override for hit-testing.
    /// WHY: This is the KEY mechanism that makes the notch work:
    /// - Mouse OUTSIDE notch area → return HTTRANSPARENT (click passes through)
    /// - Mouse INSIDE notch area → return HTCLIENT (click is handled by WPF)
    ///
    /// Without this, the notch window would block ALL clicks on the desktop
    /// beneath it, making it unusable.
    ///
    /// PERFORMANCE: This runs on every mouse move over the window area.
    /// It only does a simple point-in-rect check. Zero allocations.
    /// </summary>
    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WHY: Forward WM_CLIPBOARDUPDATE to ClipboardListener.
        // Without this, AddClipboardFormatListener registers our HWND but the
        // clipboard events are silently lost. This was the root cause of Module B failure.
        if (msg == ClipboardListener.WM_CLIPBOARDUPDATE)
        {
            _clipboardService?.OnClipboardUpdate();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == User32.WM_NCHITTEST)
        {
            // Get mouse position in screen coordinates
            int x = (short)(lParam.ToInt32() & 0xFFFF);
            int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

            // Convert to client coordinates
            var point = new User32.POINT { X = x, Y = y };
            User32.ScreenToClient(_hWnd, ref point);

            // WHY: Use Constants instead of ActualWidth for hit-testing.
            // ActualWidth may be stale during animations. Use the known idle
            // dimensions plus a generous padding for better UX.
            double hitWidth = Math.Max(NotchBorder.ActualWidth, Constants.NotchIdleWidth);
            double hitHeight = Math.Max(NotchBorder.ActualHeight, Constants.NotchIdleHeight);

            // Add padding for better UX
            const int padding = 5;

            if (point.X >= -padding && point.X <= hitWidth + padding &&
                point.Y >= -padding && point.Y <= hitHeight + padding)
            {
                handled = true;
                return new IntPtr(User32.HTCLIENT);
            }
            else
            {
                handled = true;
                return new IntPtr(User32.HTTRANSPARENT);
            }
        }

        return IntPtr.Zero;
    }

    // ═══════════════════════════════════════════════════════════════
    // MONITOR POSITIONING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Positions the notch at the top-center of the target monitor.
    /// WHY: Uses Screen.AllScreens to support multi-monitor setups.
    /// The user can configure which monitor to use via tray menu.
    /// </summary>
    public void PositionOnTargetMonitor()
    {
        // WHY use Win32 directly: WPF Left/Top uses DIPs, screen.Bounds uses physical pixels.
        // On mixed-DPI setups this causes offset errors (observed at 51,51 instead of 0,0).
        // Using SetWindowPos with physical pixel coordinates avoids DPI confusion.

        var screens = System.Windows.Forms.Screen.AllScreens;
        int targetIndex = Math.Clamp(_settings.TargetMonitorIndex, 0, screens.Length - 1);
        var screen = screens[targetIndex];

        int screenWidth = screen.Bounds.Width;
        int notchWidth = (int)Constants.NotchIdleWidth;

        // Position at top-center of the target monitor (physical pixels)
        int x = screen.Bounds.Left + (screenWidth - notchWidth) / 2;
        int y = screen.Bounds.Top;

        if (_hWnd != IntPtr.Zero)
        {
            // Use Win32 SetWindowPos for pixel-accurate positioning
            User32.SetWindowPos(
                _hWnd,
                User32.HWND_TOPMOST,
                x, y, notchWidth, (int)Constants.NotchIdleHeight,
                User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE);
        }
        else
        {
            // Fallback to WPF positioning if HWND not yet available
            Left = x;
            Top = y;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // THEME DETECTION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads the AppsUseLightTheme registry value to detect Windows theme.
    /// WHY: On light theme, a black notch looks like a "cut" in the taskbar.
    /// Adding a subtle border makes it look intentional and polished.
    /// </summary>
    private void DetectAndApplyTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value && value == 1)
            {
                // Light theme — show subtle border
                ThemeBorder.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Registry read failed — assume dark theme (no border needed)
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SERVICE INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes only the enabled modules' services.
    /// WHY: Disabled modules consume ZERO resources — no event subscriptions,
    /// no Win32 hooks, no memory allocations.
    /// </summary>
    private void InitializeServices()
    {
        // Window Hook Manager (always active — for fullscreen detection)
        _windowHookManager = new WindowHookManager();
        _windowHookManager.ForegroundWindowChanged += OnForegroundWindowChanged;
        _windowHookManager.StartTracking();

        // Power Monitor (always active — for adaptive power mode)
        _powerMonitorService = new PowerMonitorService();
        _powerMonitorService.Initialize();

        // Module B: Clipboard Service
        if (_settings.ModuleB_Clipboard)
        {
            _clipboardService = new ClipboardService();
            _clipboardService.NotificationRequested += OnClipboardNotification;
            _clipboardService.ImageNotificationRequested += OnClipboardImageNotification;
            _clipboardService.Start(_hWnd);
        }

        // Module A: Drag & Drop Service
        if (_settings.ModuleA_DragDrop)
        {
            _dragDropService = new DragDropService();
            _dragDropService.FilesDropped += OnFilesDropped;
            _dragDropService.DragEntered += OnDragEntered;
            _dragDropService.DragLeft += OnDragLeft;
        }

        // Module C: Media Session Service
        if (_settings.ModuleC_Media)
        {
            _mediaSessionService = new MediaSessionService();
            _mediaSessionService.SessionChanged += OnMediaSessionChanged;
            _ = _mediaSessionService.InitializeAsync(); // Fire and forget — async init
        }

        // Module D: Window Pin Service
        if (_settings.ModuleD_WindowPin)
        {
            _windowPinService = new WindowPinService();
            _windowPinService.WindowPinChanged += OnWindowPinChanged;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mouse enters the notch — expand slightly for visual feedback.
    /// </summary>
    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_currentState == NotchState.Idle && !_isDragging)
        {
            TransitionToState(NotchState.Hover);
        }
    }

    /// <summary>
    /// Mouse leaves the notch — contract back to idle.
    /// </summary>
    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_currentState == NotchState.Hover)
        {
            TransitionToState(NotchState.Idle);
        }
    }

    /// <summary>
    /// Drag enters the notch — expand to show drop zone.
    /// </summary>
    private void RootGrid_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            _isDragging = true;
            _dragDropService?.NotifyDragEnter();
            TransitionToState(NotchState.DragActive);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Drag leaves the notch — contract.
    /// </summary>
    private void RootGrid_DragLeave(object sender, DragEventArgs e)
    {
        _isDragging = false;
        _dragDropService?.NotifyDragLeave();
        TransitionToState(NotchState.Idle);
        e.Handled = true;
    }

    /// <summary>
    /// Drag over — set drop effect.
    /// </summary>
    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// File drop — process through DragDropService.
    /// </summary>
    private void RootGrid_Drop(object sender, DragEventArgs e)
    {
        _isDragging = false;
        _dragDropService?.HandleFileDrop(e);
        TransitionToState(NotchState.Idle);
        e.Handled = true;
    }

    /// <summary>
    /// Right-click on notch — show context menu.
    /// </summary>
    private void RootGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Context menu will be implemented in Phase 9 (TrayApp)
        // For now, just show a basic context menu
        ShowContextMenu();
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Transitions the notch to a new state with appropriate animations.
    /// WHY: Central state machine prevents conflicting animations.
    /// Each state has a clear visual configuration and behavior set.
    /// </summary>
    private void TransitionToState(NotchState newState)
    {
        if (newState == _currentState) return;

        var oldState = _currentState;
        _currentState = newState;

        // Update content visibility
        UpdateContentVisibility(newState);

        // Trigger animation (implemented in Phase 7 - NotchAnimator)
        AnimateTransition(oldState, newState);
    }

    /// <summary>
    /// Updates which content is visible based on the current state.
    /// </summary>
    private void UpdateContentVisibility(NotchState state)
    {
        IdleContent.Visibility = (state == NotchState.Idle || state == NotchState.Hover)
            ? Visibility.Visible : Visibility.Collapsed;

        DropZoneView.Visibility = state == NotchState.DragActive
            ? Visibility.Visible : Visibility.Collapsed;

        MediaWidgetView.Visibility = state == NotchState.MediaActive
            ? Visibility.Visible : Visibility.Collapsed;

        ClipboardToastView.Visibility = (state == NotchState.ClipboardNotify || state == NotchState.ScreenshotNotify)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Triggers the appropriate animation for a state transition.
    /// Stub — will be fully implemented in Phase 7.
    /// </summary>
    private void AnimateTransition(NotchState from, NotchState to)
    {
        // Animation logic will be implemented in NotchAnimator (Phase 7)
        // For now, just update dimensions directly
        switch (to)
        {
            case NotchState.Idle:
                Width = Constants.NotchIdleWidth;
                Height = Constants.NotchIdleHeight;
                break;
            case NotchState.Hover:
                Width = Constants.NotchIdleWidth + 20;
                Height = Constants.NotchIdleHeight + 8;
                break;
            case NotchState.DragActive:
                Width = Constants.NotchExpandedWidth;
                Height = Constants.NotchExpandedHeight;
                break;
            case NotchState.MediaActive:
                Width = Constants.NotchMediaWidth;
                Height = Constants.NotchMediaHeight;
                break;
            case NotchState.ClipboardNotify:
            case NotchState.ScreenshotNotify:
                Width = Constants.NotchExpandedWidth;
                Height = 60;
                break;
        }

        UpdateWindowRegion(Width, Height);
    }

    // ═══════════════════════════════════════════════════════════════
    // SERVICE EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnForegroundWindowChanged(object? sender, ForegroundChangedEventArgs e)
    {
        // Fullscreen detection — auto-hide notch during fullscreen apps
        // WHY: Skip initial events to avoid hiding on startup
        if (!_initialized) return;

        bool isFullscreen = WindowHookManager.IsWindowFullscreen(e.WindowHandle);

        Dispatcher.Invoke(() =>
        {
            // Don't hide if it's our own window or Explorer
            if (e.ClassName == "Shell_TrayWnd" || e.ClassName == "WorkerW") return;

            if (isFullscreen && Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
            }
            else if (!isFullscreen && Visibility == Visibility.Hidden)
            {
                Visibility = Visibility.Visible;
            }
        });
    }

    private void OnClipboardNotification(object? sender, ClipboardNotification e)
    {
        Dispatcher.Invoke(() =>
        {
            ClipboardToastView.SetNotification(e);
            TransitionToState(NotchState.ClipboardNotify);

            // Auto-return to idle after flash duration
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Constants.ClipboardFlashDurationMs)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_currentState == NotchState.ClipboardNotify)
                    TransitionToState(NotchState.Idle);
            };
            timer.Start();
        });
    }

    private void OnClipboardImageNotification(object? sender, ClipboardImageNotification e)
    {
        Dispatcher.Invoke(() =>
        {
            ClipboardToastView.SetImageNotification(e);
            TransitionToState(NotchState.ClipboardNotify);

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Constants.ClipboardFlashDurationMs)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_currentState == NotchState.ClipboardNotify)
                    TransitionToState(NotchState.Idle);
            };
            timer.Start();
        });
    }

    private void OnFilesDropped(object? sender, DragDropEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DropZoneView.SetDroppedPaths(e.DroppedPaths);
        });
    }

    private void OnDragEntered(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => TransitionToState(NotchState.DragActive));
    }

    private void OnDragLeft(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentState == NotchState.DragActive)
                TransitionToState(NotchState.Idle);
        });
    }

    private void OnMediaSessionChanged(object? sender, MediaSessionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Session.HasSession)
            {
                MediaWidgetView.SetSessionInfo(e.Session);
                TransitionToState(NotchState.MediaActive);
            }
            else
            {
                if (_currentState == NotchState.MediaActive)
                    TransitionToState(NotchState.Idle);
            }
        });
    }

    private void OnWindowPinChanged(object? sender, WindowPinEventArgs e)
    {
        // Visual feedback for pin operations will be implemented in Phase 8
        System.Diagnostics.Debug.WriteLine(
            $"[MainWindow] Window {(e.IsPinned ? "pinned" : "unpinned")}: {e.WindowTitle}");
    }

    // ═══════════════════════════════════════════════════════════════
    // CONTEXT MENU
    // ═══════════════════════════════════════════════════════════════

    private void ShowContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "⚙️ Ayarlar" };
        settingsItem.Click += (_, _) =>
        {
            // Open settings window (Phase 9)
        };
        menu.Items.Add(settingsItem);

        var hideItem = new System.Windows.Controls.MenuItem { Header = "👁 Bu Oturumda Gizle (1 saat)" };
        hideItem.Click += (_, _) =>
        {
            HideNotchTemporarily();
        };
        menu.Items.Add(hideItem);

        var exitItem = new System.Windows.Controls.MenuItem { Header = "✕ Çıkış" };
        exitItem.Click += (_, _) =>
        {
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    /// <summary>
    /// Hides the notch for 1 hour.
    /// </summary>
    private void HideNotchTemporarily()
    {
        Visibility = Visibility.Hidden;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.TemporaryHideDurationMs)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Visibility = Visibility.Visible;
        };
        timer.Start();
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when the window is closing. Cleans up all resources.
    /// WHY: MUST unpin all windows and remove all Win32 hooks
    /// before the process exits. Otherwise, pinned windows stay topmost
    /// and clipboard listeners leak.
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Unpin all windows
        _windowPinService?.UnpinAll();

        // Stop all hooks
        _windowHookManager?.Dispose();
        _clipboardService?.Dispose();
        _mediaSessionService?.Dispose();
        _powerMonitorService?.Dispose();

        // Remove WndProc hook
        _hwndSource?.RemoveHook(WndProc);
    }
}
