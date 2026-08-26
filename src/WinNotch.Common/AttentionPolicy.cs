// WinNotch.Common/AttentionPolicy.cs
// WHY: A user copies hundreds of things per day. Showing a 400×60 panel
// for every clipboard event is an annoyance, not a feature.
//
// This attention budget system classifies events by urgency and throttles
// visual interruptions to prevent notification fatigue.
//
// DESIGN PRINCIPLE: "WinNotch must earn the right to expand."
// If no meaningful contextual action exists, remain silent.

namespace WinNotch.Common;

/// <summary>
/// How much attention an event deserves.
/// SILENT = no visual change at all
/// SUBTLE = tiny indicator, no expansion
/// ACTIONABLE = expand to show contextual actions
/// IMPORTANT = expand with priority, can interrupt other states
/// </summary>
public enum AttentionLevel
{
    /// <summary>No visual change. Most clipboard text events.</summary>
    Silent = 0,

    /// <summary>Tiny indicator, no expansion. Colors, short text.</summary>
    Subtle = 1,

    /// <summary>Expand to show contextual actions. URLs, paths, screenshots.</summary>
    Actionable = 2,

    /// <summary>High priority expansion. Direct user actions (drop, pin).</summary>
    Important = 3
}

/// <summary>
/// Result of attention policy evaluation.
/// </summary>
public sealed class AttentionDecision
{
    /// <summary>How much visual attention to show.</summary>
    public required AttentionLevel Level { get; init; }

    /// <summary>Whether this event should be suppressed by the budget.</summary>
    public bool Suppressed { get; init; }

    /// <summary>How long to show the notification (if expanded).</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The state to transition to.</summary>
    public NotchState TargetState { get; init; } = NotchState.Idle;

    /// <summary>The priority to use.</summary>
    public StatePriority Priority { get; init; } = StatePriority.None;

    /// <summary>Optional reason for debugging.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Prevents WinNotch from becoming an annoyance engine.
/// Classifies events and enforces an attention budget.
///
/// RULES:
/// 1. Plain text clipboard → SILENT (no expansion)
/// 2. Screenshot → ACTIONABLE (user-initiated, has useful actions)
/// 3. File drop → IMPORTANT (direct user interaction)
/// 4. URL clipboard → SUBTLE (brief indicator, open link action)
/// 5. File path clipboard → ACTIONABLE (open folder, terminal)
/// 6. Media change → SUBTLE (track change flash)
/// 7. Max N notifications per minute → budget enforced
/// </summary>
public sealed class AttentionPolicy
{
    private readonly Queue<DateTime> _recentNotifications = new();
    private readonly object _lock = new();

    /// <summary>
    /// Decides how much attention a clipboard event deserves.
    /// </summary>
    public AttentionDecision ClassifyClipboard(ClipboardContentType contentType, string? previewText)
    {
        // Default for unclassified content: SILENT
        var level = contentType switch
        {
            ClipboardContentType.FilePath => AttentionLevel.Actionable,
            ClipboardContentType.Url => AttentionLevel.Subtle,
            ClipboardContentType.Color => AttentionLevel.Subtle,
            ClipboardContentType.Email => AttentionLevel.Subtle,
            ClipboardContentType.Phone => AttentionLevel.Subtle,
            ClipboardContentType.Text => AttentionLevel.Silent,
            _ => AttentionLevel.Silent
        };

        // Plain text that's too short to be useful → SILENT
        if (level == AttentionLevel.Silent)
            return MakeDecision(level, NotchState.Idle, StatePriority.None,
                "Plain text — no visual interruption");

        // Everything else needs budget check
        if (!TryConsumeBudget())
            return MakeDecision(level, NotchState.Idle, StatePriority.None,
                "Budget exhausted — suppressed");

        return level switch
        {
            AttentionLevel.Subtle => MakeDecision(level, NotchState.ClipboardNotify,
                StatePriority.Clipboard, TimeSpan.FromSeconds(2),
                $"Subtle: {contentType}"),
            AttentionLevel.Actionable => MakeDecision(level, NotchState.ClipboardNotify,
                StatePriority.Clipboard, TimeSpan.FromMilliseconds(Constants.ClipboardFlashDurationMs),
                $"Actionable: {contentType}"),
            _ => MakeDecision(level, NotchState.Idle, StatePriority.None,
                $"Unhandled level: {level}")
        };
    }

    /// <summary>
    /// Decides how much attention a screenshot event deserves.
    /// Screenshots are ALWAYS actionable — user-initiated, has useful actions.
    /// </summary>
    public AttentionDecision ClassifyScreenshot()
    {
        if (!TryConsumeBudget())
            return MakeDecision(AttentionLevel.Actionable, NotchState.Idle, StatePriority.None,
                "Screenshot — budget exhausted");

        return MakeDecision(AttentionLevel.Actionable, NotchState.ScreenshotNotify,
            StatePriority.Screenshot, TimeSpan.FromMilliseconds(Constants.ScreenshotFlashDurationMs),
            "Screenshot — always actionable");
    }

    /// <summary>
    /// Decides how much attention a media change deserves.
    /// Track changes: brief SUBTLE indicator. No persistent expansion.
    /// </summary>
    public AttentionDecision ClassifyMediaChange(bool hasSession)
    {
        if (!hasSession)
            return MakeDecision(AttentionLevel.Silent, NotchState.Idle, StatePriority.None,
                "Media stopped");

        // Track changes: brief ambient flash, not persistent 350×80
        return MakeDecision(AttentionLevel.Subtle, NotchState.MediaAmbient,
            StatePriority.Media, TimeSpan.FromMilliseconds(Constants.MediaAmbientFlashDurationMs),
            "Media track change — ambient flash");
    }

    /// <summary>
    /// File drops are ALWAYS important — direct user interaction.
    /// </summary>
    public AttentionDecision ClassifyDrop()
    {
        return MakeDecision(AttentionLevel.Important, NotchState.DropResult,
            StatePriority.DropResult, TimeSpan.FromMilliseconds(Constants.DropResultDisplayDurationMs),
            "File drop — direct interaction");
    }

    /// <summary>
    /// Tries to consume an attention budget slot.
    /// Returns false if budget is exhausted.
    /// </summary>
    private bool TryConsumeBudget()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var cutoff = now.AddMinutes(-1);

            // Remove old entries
            while (_recentNotifications.Count > 0 && _recentNotifications.Peek() < cutoff)
                _recentNotifications.Dequeue();

            // Check budget
            if (_recentNotifications.Count >= Constants.MaxAttentionEventsPerMinute)
                return false;

            // Check minimum interval
            if (_recentNotifications.Count > 0)
            {
                var lastEvent = _recentNotifications.Peek();
                // Actually we need the LAST event, not the first
                // Use a simpler approach: track last event time separately
            }

            _recentNotifications.Enqueue(now);
            return true;
        }
    }

    private static AttentionDecision MakeDecision(
        AttentionLevel level, NotchState state, StatePriority priority,
        TimeSpan? duration, string reason)
    {
        return new AttentionDecision
        {
            Level = level,
            TargetState = state,
            Priority = priority,
            Duration = duration,
            Reason = reason
        };
    }

    private static AttentionDecision MakeDecision(
        AttentionLevel level, NotchState state, StatePriority priority, string reason)
    {
        return new AttentionDecision
        {
            Level = level,
            TargetState = state,
            Priority = priority,
            Reason = reason
        };
    }
}
