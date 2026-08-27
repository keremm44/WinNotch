using WinNotch.Common;

namespace WinNotch.UI;

public partial class MainWindow
{
    private System.Windows.Threading.DispatcherTimer? _runtimeReliabilityTimer;
    private bool _runtimeReliabilityCloseHooked;

    private void UpdateRuntimeReliabilityChecks()
    {
        if (_settings.ModuleC_Media && ShouldRunModuleServices())
            StartRuntimeReliabilityChecks();
        else
            StopRuntimeReliabilityChecks();
    }

    private void StartRuntimeReliabilityChecks()
    {
        if (!_settings.ModuleC_Media || !ShouldRunModuleServices() ||
            _runtimeReliabilityTimer != null)
            return;

        _runtimeReliabilityTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _runtimeReliabilityTimer.Tick += RuntimeReliabilityTimer_Tick;
        _runtimeReliabilityTimer.Start();

        if (!_runtimeReliabilityCloseHooked)
        {
            Closed += MainWindow_RuntimeReliabilityClosed;
            _runtimeReliabilityCloseHooked = true;
        }
    }

    private void MainWindow_RuntimeReliabilityClosed(object? sender, EventArgs e)
    {
        StopRuntimeReliabilityChecks();
        Closed -= MainWindow_RuntimeReliabilityClosed;
        _runtimeReliabilityCloseHooked = false;
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

        VerifyPersistentMediaState();
    }

    private void VerifyPersistentMediaState()
    {
        if (!_settings.ModuleC_Media || !ShouldShowMediaAmbient())
            return;

        if (_dropZoneView?.HasItems == true || _isDragging || _isDraggingOut)
            return;

        if (_currentState is NotchState.Idle or NotchState.Hover)
        {
            TransitionToState(
                RootGrid.IsMouseOver ? NotchState.MediaActive : NotchState.MediaAmbient,
                force: true);
        }
    }
}
