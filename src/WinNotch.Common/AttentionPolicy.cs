// WinNotch.Common/AttentionPolicy.cs
// Central policy for deciding when WinNotch is allowed to interrupt the user.

namespace WinNotch.Common;

public enum AttentionLevel
{
    Silent = 0,
    Subtle = 1,
    Actionable = 2,
    Important = 3
}

public sealed class AttentionDecision
{
    public required AttentionLevel Level { get; init; }
    public bool Suppressed { get; init; }
    public TimeSpan? Duration { get; init; }
    public NotchState TargetState { get; init; } = NotchState.Idle;
    public StatePriority Priority { get; init; } = StatePriority.None;
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Keeps WinNotch contextual instead of turning it into a notification stream.
/// Reaction levels:
/// Quiet    -> subtle events stay invisible; direct/actionable events still work.
/// Balanced -> product default.
/// Active   -> useful plain text may surface subtly in addition to normal events.
/// </summary>
public sealed class AttentionPolicy
{
    private readonly Queue<DateTime> _recentNotifications = new();
    private readonly object _lock = new();
    private DateTime _lastNotificationAt = DateTime.MinValue;

    public AttentionDecision ClassifyClipboard(
        ClipboardContentType contentType,
        string? previewText,
        string reactionLevel = "Balanced")
    {
        AttentionLevel level = contentType switch
        {
            ClipboardContentType.FilePath => AttentionLevel.Actionable,
            ClipboardContentType.Url => AttentionLevel.Subtle,
            ClipboardContentType.Color => AttentionLevel.Subtle,
            ClipboardContentType.Email => AttentionLevel.Subtle,
            ClipboardContentType.Phone => AttentionLevel.Subtle,
            ClipboardContentType.Text when IsActive(reactionLevel) => AttentionLevel.Subtle,
            ClipboardContentType.Text => AttentionLevel.Silent,
            _ => AttentionLevel.Silent
        };

        if (IsQuiet(reactionLevel) && level == AttentionLevel.Subtle)
            level = AttentionLevel.Silent;

        if (level == AttentionLevel.Silent)
            return Decision(level, NotchState.Idle, StatePriority.None,
                "Reaction policy kept this clipboard event silent");

        if (!TryConsumeBudget())
            return Suppressed(level, "Attention budget exhausted");

        return level switch
        {
            AttentionLevel.Subtle => Decision(
                level,
                NotchState.ClipboardNotify,
                StatePriority.Clipboard,
                TimeSpan.FromMilliseconds(Constants.ClipboardFlashDurationMs),
                $"Subtle clipboard: {contentType}"),

            AttentionLevel.Actionable => Decision(
                level,
                NotchState.ClipboardNotify,
                StatePriority.Clipboard,
                TimeSpan.FromMilliseconds(Constants.ClipboardFlashDurationMs),
                $"Actionable clipboard: {contentType}"),

            _ => Decision(level, NotchState.Idle, StatePriority.None,
                $"Unhandled clipboard attention level: {level}")
        };
    }

    public AttentionDecision ClassifyScreenshot()
    {
        if (!TryConsumeBudget())
            return Suppressed(AttentionLevel.Actionable, "Screenshot attention budget exhausted");

        return Decision(
            AttentionLevel.Actionable,
            NotchState.ScreenshotNotify,
            StatePriority.Screenshot,
            TimeSpan.FromMilliseconds(Constants.ScreenshotFlashDurationMs),
            "Screenshot — direct user action");
    }

    public AttentionDecision ClassifyMediaChange(
        bool hasSession,
        string reactionLevel = "Balanced")
    {
        if (!hasSession || IsQuiet(reactionLevel))
            return Decision(AttentionLevel.Silent, NotchState.Idle, StatePriority.None,
                hasSession ? "Quiet mode hides media changes" : "Media stopped");

        return Decision(
            AttentionLevel.Subtle,
            NotchState.MediaAmbient,
            StatePriority.Media,
            TimeSpan.FromMilliseconds(Constants.MediaAmbientFlashDurationMs),
            "Media session changed");
    }

    public AttentionDecision ClassifyDrop()
        => Decision(
            AttentionLevel.Important,
            NotchState.DropResult,
            StatePriority.DropResult,
            TimeSpan.FromMilliseconds(Constants.DropResultDisplayDurationMs),
            "File drop — direct interaction");

    private bool TryConsumeBudget()
    {
        lock (_lock)
        {
            DateTime now = DateTime.Now;
            DateTime cutoff = now.AddMinutes(-1);

            while (_recentNotifications.Count > 0 && _recentNotifications.Peek() < cutoff)
                _recentNotifications.Dequeue();

            if (_recentNotifications.Count >= Constants.MaxAttentionEventsPerMinute)
                return false;

            if (_lastNotificationAt != DateTime.MinValue &&
                (now - _lastNotificationAt).TotalMilliseconds < Constants.MinNotificationIntervalMs)
                return false;

            _recentNotifications.Enqueue(now);
            _lastNotificationAt = now;
            return true;
        }
    }

    private static bool IsQuiet(string? value)
        => string.Equals(value, "Quiet", StringComparison.OrdinalIgnoreCase);

    private static bool IsActive(string? value)
        => string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase);

    private static AttentionDecision Suppressed(AttentionLevel level, string reason)
        => new()
        {
            Level = level,
            Suppressed = true,
            TargetState = NotchState.Idle,
            Priority = StatePriority.None,
            Reason = reason
        };

    private static AttentionDecision Decision(
        AttentionLevel level,
        NotchState state,
        StatePriority priority,
        TimeSpan? duration,
        string reason)
        => new()
        {
            Level = level,
            TargetState = state,
            Priority = priority,
            Duration = duration,
            Reason = reason
        };

    private static AttentionDecision Decision(
        AttentionLevel level,
        NotchState state,
        StatePriority priority,
        string reason)
        => Decision(level, state, priority, null, reason);
}
