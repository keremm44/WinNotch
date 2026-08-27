using System.Windows;
using WinNotch.Common;
using WinNotch.Core.Interop;

namespace WinNotch.UI;

public partial class MainWindow
{
    private System.Windows.Threading.DispatcherTimer? _runtimeReliabilityTimer;

    private void StartRuntimeReliabilityChecks()
    {
        if (_runtimeReliabilityTimer != null)
            return;

        _runtimeReliabilityTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _runtimeReliabilityTimer.Tick += RuntimeReliabilityTimer_Tick;
        _runtimeReliabilityTimer.Start();
    }

    private void StopRuntimeReliabilityChecks()
    {
        if (_runtimeReliabilityTimer == null)
            return;

        _runtimeReliabilityTimer.Stop();
        _runtimeReliabilityTimer.Tick -= RuntimeReliabilityTimer_Tick;
        _runtimeReliabilityTimer = null;
    }

    private void RuntimeReliabilityTimer_Tick(object? sender, EventArgs e)
    {
        if (!_initialized)
            return;

        VerifyAutomaticVisibility();
        VerifyPersistentMediaState();
    }

    private void VerifyAutomaticVisibility()
    {
        if (!string.Equals(_settings.VisibilityMode, "Auto", StringComparison.OrdinalIgnoreCase))
            return;

        IntPtr foreground = User32.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == _hWnd)
            return;

        string className = WindowHookManager.GetWindowClassName(foreground);
        if (className is "Shell_TrayWnd" or "WorkerW" or "Shell_SecondaryTrayWnd")
            return;

        bool fullscreen = WindowHookManager.IsWindowFullscreen(foreground);
        if (fullscreen)
        {
            if (Visibility == Visibility.Visible)
                Visibility = Visibility.Hidden;
            return;
        }

        if (!_manuallyHidden && Visibility == Visibility.Hidden)
            Visibility = Visibility.Visible;
    }

    private void VerifyPersistentMediaState()
    {
        if (!_settings.ModuleC_Media || !ShouldShowMediaAmbient())
            return;

        if (DropZoneView.HasItems || _isDragging || _isDraggingOut)
            return;

        if (_currentState is NotchState.Idle or NotchState.Hover)
            TransitionToState(NotchState.MediaAmbient, force: true);
    }
}
