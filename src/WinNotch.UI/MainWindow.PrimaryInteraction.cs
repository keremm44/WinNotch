using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WinNotch.Common;
using WinNotch.Core.Services;

namespace WinNotch.UI;

public partial class MainWindow
{
    private readonly LastMeaningfulClipboardContextCache _lastMeaningfulClipboard = new();
    private System.Windows.Threading.DispatcherTimer? _quickPeekLeaveTimer;

    private void RootGrid_PrimaryMouseEnter(object sender, MouseEventArgs e)
    {
        CancelQuickPeekLeave();
        if (_isDragging || _isDraggingOut)
            return;

        if (_currentState == NotchState.Idle)
            TransitionToState(NotchState.Hover, force: true);

        if (_currentState is NotchState.Idle or NotchState.Hover)
            SetIdleHoverAffordance(true);
    }

    private void RootGrid_PrimaryMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isDraggingOut)
            return;

        SetIdleHoverAffordance(false);

        if (_currentState == NotchState.Hover)
            TransitionToState(GetPersistentState(), force: true);
        else if (_currentState == NotchState.QuickPeek)
            ScheduleQuickPeekCollapse();
        else if (_currentState == NotchState.ShelfExpanded)
            TransitionToState(NotchState.ShelfOccupied, force: true);
        else if (_currentState == NotchState.MediaActive)
            TransitionToState(GetPersistentState(), force: true);
    }

    private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _isDragging || _isDraggingOut)
            return;

        if (IsInteractiveChild(e.OriginalSource as DependencyObject))
            return;

        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(_currentState);
        switch (decision.Kind)
        {
            case PrimaryInteractionKind.OpenQuickPeek:
                CancelQuickPeekLeave();
                QuickPeekView.SetContext(_lastMeaningfulClipboard.Current);
                TransitionToState(NotchState.QuickPeek, force: true);
                break;

            case PrimaryInteractionKind.ExpandShelf:
            case PrimaryInteractionKind.ExpandMedia:
                if (decision.TargetState is NotchState target)
                    TransitionToState(target, force: true);
                break;

            case PrimaryInteractionKind.ExpandContextAction:
                ClipboardToastView.RevealActionsFromPrimaryClick();
                break;

            case PrimaryInteractionKind.CollapseToPersistent:
                CancelQuickPeekLeave();
                TransitionToState(GetPersistentState(), force: true);
                break;

            case PrimaryInteractionKind.None:
            default:
                return;
        }

        e.Handled = true;
    }

    private void MainWindow_PrimarySizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool quickPeek = _currentState == NotchState.QuickPeek;
        QuickPeekView.Visibility = quickPeek ? Visibility.Visible : Visibility.Collapsed;
        if (quickPeek)
            QuickPeekView.SetContext(_lastMeaningfulClipboard.Current);

        UpdatePrimaryCursor(_currentState);
    }

    private void ClipboardToastView_MeaningfulContextAvailable(
        object? sender,
        LastMeaningfulClipboardContext context)
    {
        _lastMeaningfulClipboard.TryRemember(
            context.ContentType,
            context.RawText,
            context.PreviewText,
            context.Timestamp);

        if (_currentState == NotchState.QuickPeek)
            QuickPeekView.SetContext(_lastMeaningfulClipboard.Current);
    }

    private void OnQuickPeekContextRequested(object? sender, EventArgs e)
    {
        LastMeaningfulClipboardContext? context = _lastMeaningfulClipboard.Current;
        if (_currentState != NotchState.QuickPeek || context == null)
            return;

        var notification = new ClipboardNotification
        {
            RawText = context.RawText,
            PreviewText = context.PreviewText,
            Timestamp = context.Timestamp,
            IsImage = false
        };

        ClipboardToastView.SetNotification(notification, context.ContentType);
        TransitionToState(
            NotchState.ClipboardNotify,
            StatePriority.Clipboard,
            returnState: GetPersistentState(),
            force: true);
        ClipboardToastView.RevealActionsFromPrimaryClick();
    }

    private void ScheduleQuickPeekCollapse()
    {
        CancelQuickPeekLeave();
        _quickPeekLeaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Constants.QuickPeekLeaveGraceMs)
        };

        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (_quickPeekLeaveTimer != null && handler != null)
                _quickPeekLeaveTimer.Tick -= handler;
            _quickPeekLeaveTimer?.Stop();
            _quickPeekLeaveTimer = null;

            if (_currentState != NotchState.QuickPeek || RootGrid.IsMouseOver)
                return;

            TransitionToState(GetPersistentState(), force: true);
        };

        _quickPeekLeaveTimer.Tick += handler;
        _quickPeekLeaveTimer.Start();
    }

    private void CancelQuickPeekLeave()
    {
        _quickPeekLeaveTimer?.Stop();
        _quickPeekLeaveTimer = null;
    }

    private void SetIdleHoverAffordance(bool hovered)
    {
        IdleLine.Width = hovered ? 27 : 18;
        IdleAmbientGlow.Width = hovered ? 42 : 34;
        IdleDots.Opacity = hovered ? 1.0 : 0.82;
    }

    private void UpdatePrimaryCursor(NotchState state)
    {
        RootGrid.Cursor = state is NotchState.DragActive or NotchState.ShelfDraggingOut
            ? Cursors.Arrow
            : Cursors.Hand;
    }

    private static bool IsInteractiveChild(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or
                System.Windows.Controls.Primitives.Thumb or
                System.Windows.Controls.TextBox or
                System.Windows.Controls.ComboBox)
                return true;

            current = GetParent(current);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        if (current is Visual || current is Visual3D)
            return VisualTreeHelper.GetParent(current);
        return LogicalTreeHelper.GetParent(current);
    }
}
