// WinNotch.Common/NotchStateMachine.cs
// Lightweight priority/state logic for contextual notch interactions.

namespace WinNotch.Common;

public enum StatePriority
{
    None = 0,
    Hover = 5,
    Media = 10,
    Shelf = 15,
    Clipboard = 20,
    Screenshot = 25,
    WindowPin = 30,
    DropTarget = 40,
    DropResult = 50
}

public sealed class StateTransition
{
    public required NotchState State { get; init; }
    public bool ShouldApply { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
    public NotchState? ReturnState { get; init; }
    public bool Force { get; init; }
}

public sealed class NotchStateMachine
{
    private NotchState _currentState = NotchState.Idle;
    private StatePriority _currentPriority = StatePriority.None;
    private DateTime _lastClipboardEvent = DateTime.MinValue;
    private int _clipboardEventCount;

    private const int CoalesceThreshold = 3;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(1000);

    public NotchState CurrentState => _currentState;
    public StatePriority CurrentPriority => _currentPriority;

    public StateTransition TryTransition(
        NotchState newState,
        StatePriority priority,
        TimeSpan? timeout = null,
        NotchState? returnState = null,
        bool force = false)
    {
        if (newState == _currentState)
            return new StateTransition { State = _currentState, ShouldApply = false };

        if (newState == NotchState.ClipboardNotify || newState == NotchState.ScreenshotNotify)
        {
            if (!TryCoalesceEvent())
                return new StateTransition { State = _currentState, ShouldApply = false };
        }

        if (!force && priority < _currentPriority && _currentState != NotchState.Hover)
            return new StateTransition { State = _currentState, ShouldApply = false };

        _currentState = newState;
        _currentPriority = priority;

        return new StateTransition
        {
            State = newState,
            ShouldApply = true,
            Timeout = timeout,
            ReturnState = returnState ?? DetermineReturnState(newState)
        };
    }

    public StateTransition ForceTransition(
        NotchState newState,
        TimeSpan? timeout = null,
        NotchState? returnState = null)
        => TryTransition(newState, StatePriority.DropResult, timeout, returnState, force: true);

    public StateTransition ReturnTo(NotchState state)
    {
        _currentState = state;
        _currentPriority = PriorityFor(state);
        return new StateTransition { State = state, ShouldApply = true };
    }

    public StateTransition ReturnToIdle() => ReturnTo(NotchState.Idle);

    public StateTransition ReturnToBest(bool mediaActive)
        => mediaActive ? ReturnTo(NotchState.MediaAmbient) : ReturnToIdle();

    private bool TryCoalesceEvent()
    {
        var now = DateTime.Now;
        if ((now - _lastClipboardEvent) > CoalesceWindow)
        {
            _clipboardEventCount = 1;
            _lastClipboardEvent = now;
            return true;
        }

        _clipboardEventCount++;
        _lastClipboardEvent = now;
        return _clipboardEventCount <= CoalesceThreshold;
    }

    private static NotchState DetermineReturnState(NotchState from) => from switch
    {
        NotchState.DropResult => NotchState.ShelfOccupied,
        NotchState.ShelfExpanded => NotchState.ShelfOccupied,
        NotchState.ShelfDraggingOut => NotchState.ShelfOccupied,
        _ => NotchState.Idle
    };

    public static StatePriority PriorityFor(NotchState state) => state switch
    {
        NotchState.Hover => StatePriority.Hover,
        NotchState.MediaActive or NotchState.MediaAmbient => StatePriority.Media,
        NotchState.ShelfOccupied or NotchState.ShelfExpanded or NotchState.ShelfDraggingOut => StatePriority.Shelf,
        NotchState.ClipboardNotify => StatePriority.Clipboard,
        NotchState.ScreenshotNotify => StatePriority.Screenshot,
        NotchState.WindowPinned => StatePriority.WindowPin,
        NotchState.DragActive => StatePriority.DropTarget,
        NotchState.DropResult => StatePriority.DropResult,
        _ => StatePriority.None
    };
}

public static class StateDimensions
{
    public static (double Width, double Height) GetDimensions(NotchState state) => state switch
    {
        NotchState.Idle => (Constants.NotchIdleWidth, Constants.NotchIdleHeight),
        NotchState.Hover => (Constants.NotchHoverWidth, Constants.NotchHoverHeight),
        NotchState.DragActive => (Constants.NotchDropTargetWidth, Constants.NotchDropTargetHeight),
        NotchState.DropResult => (Constants.NotchDropResultWidth, Constants.NotchDropResultHeight),
        NotchState.ShelfOccupied => (Constants.NotchShelfWidth, Constants.NotchShelfHeight),
        NotchState.ShelfExpanded or NotchState.ShelfDraggingOut => (Constants.NotchShelfExpandedWidth, Constants.NotchShelfExpandedHeight),
        NotchState.MediaActive => (Constants.NotchMediaExpandedWidth, Constants.NotchMediaExpandedHeight),
        NotchState.MediaAmbient => (Constants.NotchMediaAmbientWidth, Constants.NotchMediaAmbientHeight),
        NotchState.ClipboardNotify => (Constants.NotchClipboardWidth, Constants.NotchClipboardHeight),
        NotchState.ScreenshotNotify => (Constants.NotchScreenshotWidth, Constants.NotchScreenshotHeight),
        NotchState.WindowPinned => (Constants.NotchPinnedWidth, Constants.NotchPinnedHeight),
        _ => (Constants.NotchIdleWidth, Constants.NotchIdleHeight)
    };
}

public static class ClipboardClassifier
{
    public static ClipboardContentType Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ClipboardContentType.Unknown;

        text = text.Trim();

        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return ClipboardContentType.Url;

        if (text.Length >= 3 && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
            return ClipboardContentType.FilePath;
        if (text.StartsWith("\\\\", StringComparison.Ordinal) ||
            text.StartsWith("~/", StringComparison.Ordinal) ||
            text.StartsWith("./", StringComparison.Ordinal))
            return ClipboardContentType.FilePath;

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

        if (text.Contains('@') && !text.Contains(' ') && text.Length < 254)
            return ClipboardContentType.Email;

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

        return ClipboardContentType.Text;
    }
}

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
