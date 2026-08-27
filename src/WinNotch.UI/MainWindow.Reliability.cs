using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Core.Interop;

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

        RootGrid.PreviewMouseRightButtonUp += Reliability_PreviewMouseRightButtonUp;

        _fullscreenFallbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
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
