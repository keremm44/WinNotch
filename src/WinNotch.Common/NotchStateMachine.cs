// WinNotch.Common/NotchStateMachine.cs
// WHY: Independent event handlers fighting over width/visibility is a bug factory.
// This state machine provides:
// - Priority ordering (user interaction > drop > screenshot > clipboard > media)
// - Event coalescing (10 clipboard changes in 1 second → 1 notification)
// - Timeout management (auto-return to idle after configurable duration)
// - Return state logic (after drop, return to media if active, else idle)
//
// DESIGN: Lightweight. No timers running when idle. Timers created on-demand
// for specific state transitions and stopped when state changes.

namespace WinNotch.Common;

/// <summary>
/// Priority levels for state transitions.
/// Higher number = higher priority = can interrupt lower priority states.
/// </summary>
public enum StatePriority
{
    /// <summary>No state — idle.</summary>
    None = 0,

    /// <summary>Background media playing. Lowest priority — can be interrupted by anything.</summary>
    Media = 10,

    /// <summary>Clipboard notification. Brief flash, auto-dismiss.</summary>
    Clipboard = 20,

    /// <summary>Screenshot captured. Slightly higher than clipboard (user-initiated).</summary>
    Screenshot = 25,

    /// <summary>Window pin operation. User-initiated, higher priority.</summary>
    WindowPin = 30,

    /// <summary>File being dragged over notch. Active user interaction.</summary>
    DropTarget = 40,

    /// <summary>File dropped, showing actions. Highest interactive priority.</summary>
    DropResult = 50,

    /// <summary>Mouse hovering. Very low priority — any event replaces it.</summary>
    Hover = 5
}

/// <summary>
/// Result of a state transition attempt.
/// </summary>
public sealed class StateTransition
{
    /// <summary>The state to transition to.</summary>
    public required NotchState State { get; init; }

    /// <summary>Whether this transition should actually happen.</summary>
    public bool ShouldApply { get; init; } = true;

    /// <summary>How long to stay in this state before auto-returning (null = no timeout).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>State to return to after timeout (null = return to Idle or best available).</summary>
    public NotchState? ReturnState { get; init; }

    /// <summary>Whether to force this transition even if priority is lower.</summary>
    public bool Force { get; init; }
}

/// <summary>
/// Lightweight state machine for the notch widget.
/// Manages priorities, coalescing, and timeout-driven state returns.
/// </summary>
public sealed class NotchStateMachine
{
    private NotchState _currentState = NotchState.Idle;
    private StatePriority _currentPriority = StatePriority.None;
    private DateTime _lastTransition = DateTime.MinValue;
    private DateTime _lastClipboardEvent = DateTime.MinValue;
    private int _clipboardEventCount;

    // Coalescing: if more than N events arrive within M ms, coalesce
    private const int CoalesceThreshold = 3;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(1000);

    /// <summary>Current state of the notch.</summary>
    public NotchState CurrentState => _currentState;

    /// <summary>Priority of the current state.</summary>
    public StatePriority CurrentPriority => _currentPriority;

    /// <summary>
    /// Attempts a state transition.
    /// Returns a TransitionResult that the UI should apply.
    /// </summary>
    public StateTransition TryTransition(NotchState newState, StatePriority priority, TimeSpan? timeout = null, NotchState? returnState = null, bool force = false)
    {
        // Same state — no transition needed
        if (newState == _currentState)
            return new StateTransition { State = _currentState, ShouldApply = false };

        // Coalescing: clipboard events within rapid succession
        if (newState == NotchState.ClipboardNotify || newState == NotchState.ScreenshotNotify)
        {
            if (!TryCoalesceEvent())
            {
                // Too many events too fast — skip this transition
                return new StateTransition { State = _currentState, ShouldApply = false };
            }
        }

        // Priority check: can this state interrupt the current one?
        if (!force && priority < _currentPriority)
        {
            // Lower priority cannot interrupt higher priority
            // Exception: Hover is always replaceable
            if (_currentState != NotchState.Hover)
            {
                return new StateTransition { State = _currentState, ShouldApply = false };
            }
        }

        // Apply transition
        var previousState = _currentState;
        _currentState = newState;
        _currentPriority = priority;
        _lastTransition = DateTime.Now;

        // Determine return state if not specified
        NotchState effectiveReturn = returnState ?? DetermineReturnState(newState);

        return new StateTransition
        {
            State = newState,
            ShouldApply = true,
            Timeout = timeout,
            ReturnState = effectiveReturn
        };
    }

    /// <summary>
    /// Forces a transition regardless of priority.
    /// Used for user-initiated actions (right-click menu, tray toggle).
    /// </summary>
    public StateTransition ForceTransition(NotchState newState, TimeSpan? timeout = null, NotchState? returnState = null)
    {
        return TryTransition(newState, StatePriority.DropResult, timeout, returnState, force: true);
    }

    /// <summary>
    /// Returns to idle state. Called when timeout expires or user dismisses.
    /// </summary>
    public StateTransition ReturnToIdle()
    {
        _currentState = NotchState.Idle;
        _currentPriority = StatePriority.None;
        return new StateTransition { State = NotchState.Idle, ShouldApply = true };
    }

    /// <summary>
    /// Returns to the best available state (media if active, else idle).
    /// Called after drop result timeout.
    /// </summary>
    public StateTransition ReturnToBest(bool mediaActive)
    {
        if (mediaActive)
        {
            _currentState = NotchState.MediaActive;
            _currentPriority = StatePriority.Media;
            return new StateTransition { State = NotchState.MediaActive, ShouldApply = true };
        }

        return ReturnToIdle();
    }

    /// <summary>
    /// Checks if a clipboard event should be coalesced (too many too fast).
    /// Returns true if the event should proceed, false if it should be skipped.
    /// </summary>
    private bool TryCoalesceEvent()
    {
        var now = DateTime.Now;

        if ((now - _lastClipboardEvent) > CoalesceWindow)
        {
            // Outside coalesce window — reset counter
            _clipboardEventCount = 1;
            _lastClipboardEvent = now;
            return true;
        }

        _clipboardEventCount++;
        _lastClipboardEvent = now;

        // Allow first N events, then coalesce
        return _clipboardEventCount <= CoalesceThreshold;
    }

    /// <summary>
    /// Determines the best state to return to after a temporary state ends.
    /// </summary>
    private static NotchState DetermineReturnState(NotchState from)
    {
        // After drop operations, return to idle
        // (user was interacting, not passively watching media)
        return from switch
        {
            NotchState.DragActive => NotchState.Idle,
            NotchState.DropResult => NotchState.Idle,
            NotchState.ClipboardNotify => NotchState.Idle,
            NotchState.ScreenshotNotify => NotchState.Idle,
            NotchState.WindowPinned => NotchState.Idle,
            _ => NotchState.Idle
        };
    }
}

/// <summary>
/// Maps NotchState to visual dimensions.
/// Content-driven: each state gets the MINIMUM practical size.
/// Idle must be nearly invisible. Expanded only when useful.
/// </summary>
public static class StateDimensions
{
    public static (double Width, double Height) GetDimensions(NotchState state) => state switch
    {
        NotchState.Idle => (Constants.NotchIdleWidth, Constants.NotchIdleHeight),
        NotchState.Hover => (Constants.NotchHoverWidth, Constants.NotchHoverHeight),
        NotchState.DragActive => (Constants.NotchDropTargetWidth, Constants.NotchDropTargetHeight),
        NotchState.DropResult => (Constants.NotchDropResultWidth, Constants.NotchDropResultHeight),
        NotchState.MediaActive => (Constants.NotchMediaExpandedWidth, Constants.NotchMediaExpandedHeight),
        NotchState.MediaAmbient => (Constants.NotchMediaAmbientWidth, Constants.NotchMediaAmbientHeight),
        NotchState.ClipboardNotify => (Constants.NotchClipboardWidth, Constants.NotchClipboardHeight),
        NotchState.ScreenshotNotify => (Constants.NotchScreenshotWidth, Constants.NotchScreenshotHeight),
        NotchState.WindowPinned => (Constants.NotchPinnedWidth, Constants.NotchPinnedHeight),
        _ => (Constants.NotchIdleWidth, Constants.NotchIdleHeight)
    };
}

/// <summary>
/// Classifies clipboard content for contextual display.
/// Deterministic, cheap, event-triggered. No regex, no AI.
/// </summary>
public static class ClipboardClassifier
{
    /// <summary>
    /// Classifies clipboard text into a content type.
    /// </summary>
    public static ClipboardContentType Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ClipboardContentType.Unknown;

        text = text.Trim();

        // URL detection (simple prefix check — cheap, no regex)
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return ClipboardContentType.Url;

        // File path detection
        if (text.Length >= 3 && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
            return ClipboardContentType.FilePath;
        if (text.StartsWith("\\\\", StringComparison.Ordinal))
            return ClipboardContentType.FilePath;
        if (text.StartsWith("~/", StringComparison.Ordinal) || text.StartsWith("./", StringComparison.Ordinal))
            return ClipboardContentType.FilePath;

        // Color hex detection (#RRGGBB or #RGB)
        if (text.Length is 4 or 7 or 9 && text[0] == '#')
        {
            bool isHex = true;
            for (int i = 1; i < text.Length; i++)
            {
                char c = text[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    isHex = false;
                    break;
                }
            }
            if (isHex) return ClipboardContentType.Color;
        }

        // Email detection (simple @ check)
        if (text.Contains('@') && !text.Contains(' ') && text.Length < 254)
            return ClipboardContentType.Email;

        // Phone number (digits, spaces, dashes, plus)
        bool allPhoneChars = true;
        foreach (char c in text)
        {
            if (!((c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '+' || c == '(' || c == ')'))
            {
                allPhoneChars = false;
                break;
            }
        }
        if (allPhoneChars && text.Length >= 7 && text.Length <= 20)
            return ClipboardContentType.Phone;

        // Default: plain text
        return ClipboardContentType.Text;
    }
}

/// <summary>
/// Types of clipboard content for contextual display.
/// </summary>
public enum ClipboardContentType
{
    Unknown,
    Text,
    Url,
    FilePath,
    Color,
    Email,
    Phone
}
