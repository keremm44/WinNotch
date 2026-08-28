using System.Windows.Threading;
using WinNotch.Common;

namespace WinNotch.UI;

public partial class MainWindow
{
    private readonly CountdownTimerSession _countdownTimer = new();
    private DispatcherTimer? _countdownDispatcherTimer;

    private void OnCommandHubTimerRequested(object? sender, EventArgs e)
        => RefreshCommandHubTimer();

    private void OnCommandHubTimerStartRequested(
        object? sender,
        Views.TimerStartRequestedEventArgs e)
    {
        if (_currentState != NotchState.CommandHub ||
            !_countdownTimer.Start(e.Duration, DateTimeOffset.Now))
            return;

        EnsureCountdownTicking();
        RefreshCommandHubTimer();
    }

    private void OnCommandHubTimerPauseResumeRequested(object? sender, EventArgs e)
    {
        if (_currentState != NotchState.CommandHub)
            return;

        if (_countdownTimer.Status == CountdownTimerStatus.Running)
        {
            if (_countdownTimer.Pause(DateTimeOffset.Now))
                StopCountdownTicking();
        }
        else if (_countdownTimer.Status == CountdownTimerStatus.Paused &&
                 _countdownTimer.Resume(DateTimeOffset.Now))
        {
            EnsureCountdownTicking();
        }

        RefreshCommandHubTimer();
    }

    private void OnCommandHubTimerCancelRequested(object? sender, EventArgs e)
    {
        if (_currentState != NotchState.CommandHub)
            return;

        _countdownTimer.Cancel();
        StopCountdownTicking();
        RefreshCommandHubTimer();
    }

    private void EnsureCountdownTicking()
    {
        if (_countdownDispatcherTimer == null)
        {
            _countdownDispatcherTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _countdownDispatcherTimer.Tick += CountdownDispatcherTimer_Tick;
        }

        _countdownDispatcherTimer.Start();
    }

    private void CountdownDispatcherTimer_Tick(object? sender, EventArgs e)
    {
        bool completed = _countdownTimer.Update(DateTimeOffset.Now);
        RefreshCommandHubTimer();
        if (!completed)
            return;

        StopCountdownTicking();
        TransitionToState(
            NotchState.TimerNotify,
            StatePriority.Timer,
            timeout: TimeSpan.FromSeconds(4),
            returnState: GetPersistentState());
    }

    private void RefreshCommandHubTimer()
    {
        TimeSpan remaining = _countdownTimer.Remaining;
        if (remaining > TimeSpan.Zero)
            remaining = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        _commandHubView?.SetTimerState(_countdownTimer.Status, remaining);
    }

    private void StopCountdownTicking()
    {
        if (_countdownDispatcherTimer == null)
            return;
        _countdownDispatcherTimer.Stop();
    }

    private void ReleaseCountdownTimer()
    {
        if (_countdownDispatcherTimer != null)
        {
            _countdownDispatcherTimer.Stop();
            _countdownDispatcherTimer.Tick -= CountdownDispatcherTimer_Tick;
            _countdownDispatcherTimer = null;
        }
        _countdownTimer.Cancel();
    }
}
