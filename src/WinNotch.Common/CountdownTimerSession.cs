namespace WinNotch.Common;

public enum CountdownTimerStatus
{
    Idle,
    Running,
    Paused,
    Completed
}

/// <summary>Pure countdown state; the UI owns the only runtime tick source.</summary>
public sealed class CountdownTimerSession
{
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    private DateTimeOffset _endsAt;
    private TimeSpan _pausedRemaining;

    public CountdownTimerStatus Status { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool IsActive => Status is CountdownTimerStatus.Running or CountdownTimerStatus.Paused;

    public bool Start(TimeSpan duration, DateTimeOffset now)
    {
        if (duration < MinimumDuration || duration > MaximumDuration)
            return false;

        Remaining = duration;
        _endsAt = now + duration;
        _pausedRemaining = TimeSpan.Zero;
        Status = CountdownTimerStatus.Running;
        return true;
    }

    public bool Pause(DateTimeOffset now)
    {
        if (Status != CountdownTimerStatus.Running)
            return false;

        Update(now);
        if (Status == CountdownTimerStatus.Completed)
            return false;

        _pausedRemaining = Remaining;
        Status = CountdownTimerStatus.Paused;
        return true;
    }

    public bool Resume(DateTimeOffset now)
    {
        if (Status != CountdownTimerStatus.Paused || _pausedRemaining <= TimeSpan.Zero)
            return false;

        Remaining = _pausedRemaining;
        _endsAt = now + Remaining;
        Status = CountdownTimerStatus.Running;
        return true;
    }

    public bool Update(DateTimeOffset now)
    {
        if (Status != CountdownTimerStatus.Running)
            return false;

        Remaining = _endsAt - now;
        if (Remaining > TimeSpan.Zero)
            return false;

        Remaining = TimeSpan.Zero;
        _pausedRemaining = TimeSpan.Zero;
        Status = CountdownTimerStatus.Completed;
        return true;
    }

    public void Cancel()
    {
        Status = CountdownTimerStatus.Idle;
        Remaining = TimeSpan.Zero;
        _pausedRemaining = TimeSpan.Zero;
        _endsAt = default;
    }
}
