using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WinNotch.Common;
using WinNotch.Core.Interop;
using WinNotch.Core.Services;

using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfCursors = System.Windows.Input.Cursors;

namespace WinNotch.UI;

public partial class MainWindow
{
    private readonly LastMeaningfulClipboardContextCache _lastMeaningfulClipboard = new();
    private System.Windows.Threading.DispatcherTimer? _commandHubLeaveTimer;
    private readonly TemporaryNoteSession _temporaryNote = new();
    private bool _commandHubEditorActive;
    private bool _commandHubModalActionActive;
    private IntPtr _commandHubPreviousForeground;

    private void RootGrid_PrimaryMouseEnter(object sender, MouseEventArgs e)
    {
        CancelCommandHubLeave();
        if (_isDragging || _isDraggingOut)
            return;

        // Media remains exclusively hover-driven. A subsequent background click is
        // resolved independently and opens Command Hub.
        if (_currentState == NotchState.MediaAmbient)
        {
            TransitionToState(NotchState.MediaActive, force: true);
            return;
        }

        if (_currentState == NotchState.Idle)
            TransitionToState(NotchState.Hover, force: true);

        if (_currentState is NotchState.Idle or NotchState.Hover)
            SetIdleHoverAffordance(true);
    }

    private void RootGrid_PrimaryMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isDraggingOut)
            return;

        if (_currentState is NotchState.Idle or NotchState.Hover)
            SetIdleHoverAffordance(false);

        if (_currentState == NotchState.Hover)
            TransitionToState(GetPersistentState(), force: true);
        else if (_currentState == NotchState.CommandHub)
            ScheduleCommandHubCollapse();
        else if (_currentState == NotchState.ShelfExpanded)
            TransitionToState(NotchState.ShelfOccupied, force: true);
        else if (_currentState == NotchState.MediaActive)
            TransitionToState(GetPersistentState(), force: true);
    }

    private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (ConsumeManagedContextMenuDismissal())
        {
            e.Handled = true;
            return;
        }

        if (_isDragging || _isDraggingOut)
            return;

        // Transport, shelf and hub buttons retain their own actions.
        if (IsInteractiveChild(e.OriginalSource as DependencyObject))
            return;

        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(_currentState);
        switch (decision.Kind)
        {
            case PrimaryInteractionKind.OpenCommandHub:
                CancelCommandHubLeave();
                Views.CommandHubView hub = EnsureCommandHubView();
                hub.SetClipboardContext(_lastMeaningfulClipboard.Current);
                hub.SetShelfItemCount(_dropZoneView?.Items.Count ?? 0);
                TransitionToState(NotchState.CommandHub, force: true);
                break;

            case PrimaryInteractionKind.ExpandContextAction:
                EnsureClipboardToastView().RevealActionsFromPrimaryClick();
                break;

            case PrimaryInteractionKind.CollapseToPersistent:
                CancelCommandHubLeave();
                TransitionToState(GetStateAfterCommandHub(), force: true);
                break;

            case PrimaryInteractionKind.None:
            default:
                return;
        }

        e.Handled = true;
    }

    private void MainWindow_PrimarySizeChanged(object sender, SizeChangedEventArgs e)
    {
        // SizeChanged can fire while XAML is still constructing named children.
        if (RootGrid == null)
            return;

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

        _commandHubView?.SetClipboardContext(_lastMeaningfulClipboard.Current);
    }

    private void OnCommandHubClipboardRequested(object? sender, EventArgs e)
    {
        LastMeaningfulClipboardContext? context = _lastMeaningfulClipboard.Current;
        if (_currentState != NotchState.CommandHub || context == null)
            return;

        var notification = new ClipboardNotification
        {
            RawText = context.RawText,
            PreviewText = context.PreviewText,
            Timestamp = context.Timestamp,
            IsImage = false
        };

        EnsureClipboardToastView().SetNotification(notification, context.ContentType);
        TransitionToState(
            NotchState.ClipboardNotify,
            StatePriority.Clipboard,
            returnState: GetPersistentState(),
            force: true);
        EnsureClipboardToastView().RevealActionsFromPrimaryClick();
    }

    private void OnCommandHubShelfRequested(object? sender, EventArgs e)
    {
        if (_currentState != NotchState.CommandHub || _dropZoneView?.HasItems != true)
            return;

        TransitionToState(NotchState.ShelfExpanded, force: true);
    }

    private void OnCommandHubSettingsRequested(object? sender, EventArgs e)
    {
        if (_currentState != NotchState.CommandHub)
            return;

        TransitionToState(GetPersistentState(), force: true);
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommandHubSmartClipboardRequested(object? sender, EventArgs e)
    {
        if (_currentState != NotchState.CommandHub || sender is not Views.CommandHubView hub)
            return;

        ClipboardService.TryReadSafeText(out string? text);
        hub.SetSmartClipboardText(text);
    }

    private void OnCommandHubClipboardTextCopyRequested(
        object? sender,
        Views.ClipboardTextCopyRequestedEventArgs e)
    {
        if (_currentState != NotchState.CommandHub || string.IsNullOrEmpty(e.Text))
            return;

        try
        {
            System.Windows.Clipboard.SetText(e.Text);
            _clipboardService?.SuppressNextTextNotification(e.Text);
            e.Succeeded = true;
        }
        catch
        {
            // Clipboard contention is transient; keep the transformed output visible
            // so the user can retry without losing work.
        }
    }

    private void OnCommandHubTemporaryNoteRequested(object? sender, EventArgs e)
    {
        if (_currentState == NotchState.CommandHub && sender is Views.CommandHubView hub)
            hub.SetTemporaryNote(_temporaryNote.Text);
    }

    private void OnCommandHubTemporaryNoteChanged(
        object? sender,
        Views.TemporaryNoteChangedEventArgs e)
    {
        if (_currentState != NotchState.CommandHub)
            return;

        _temporaryNote.Update(e.Text);
    }

    private void OnCommandHubEditorModeChanged(
        object? sender,
        Views.CommandHubEditorModeEventArgs e)
        => SetCommandHubEditorActivation(e.IsActive);

    private void SetCommandHubEditorActivation(bool active)
    {
        if (_commandHubEditorActive == active || _hWnd == IntPtr.Zero)
            return;

        _commandHubEditorActive = active;
        int exStyle = User32.GetWindowLong(_hWnd, User32.GWL_EXSTYLE);
        if (active)
        {
            _commandHubPreviousForeground = User32.GetForegroundWindow();
            User32.SetExtendedStyle(_hWnd, exStyle & ~User32.WS_EX_NOACTIVATE);
            Activate();
            return;
        }

        User32.SetExtendedStyle(_hWnd, exStyle | User32.WS_EX_NOACTIVATE);
        if (User32.GetForegroundWindow() == _hWnd &&
            _commandHubPreviousForeground != IntPtr.Zero &&
            _commandHubPreviousForeground != _hWnd)
        {
            User32.SetForegroundWindow(_commandHubPreviousForeground);
        }
        _commandHubPreviousForeground = IntPtr.Zero;
    }

    private NotchState GetStateAfterCommandHub()
    {
        NotchState persistent = GetPersistentState();
        return persistent == NotchState.MediaAmbient && RootGrid.IsMouseOver
            ? NotchState.MediaActive
            : persistent;
    }

    private void ScheduleCommandHubCollapse()
    {
        CancelCommandHubLeave();
        if (_commandHubLeaveTimer == null)
        {
            _commandHubLeaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Constants.CommandHubLeaveGraceMs)
            };
            _commandHubLeaveTimer.Tick += CommandHubLeaveTimer_Tick;
        }
        _commandHubLeaveTimer.Start();
    }

    private void CommandHubLeaveTimer_Tick(object? sender, EventArgs e)
    {
        _commandHubLeaveTimer?.Stop();
        if (_currentState != NotchState.CommandHub || RootGrid.IsMouseOver ||
            _commandHubEditorActive)
            return;

        TransitionToState(GetPersistentState(), force: true);
    }

    private void MainWindow_CommandHubEditorDeactivated(object? sender, EventArgs e)
    {
        if (!_commandHubEditorActive || _commandHubModalActionActive ||
            _currentState != NotchState.CommandHub)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (!_commandHubEditorActive || _commandHubModalActionActive || IsActive ||
                _currentState != NotchState.CommandHub)
                return;

            SetCommandHubEditorActivation(false);
            TransitionToState(GetPersistentState(), force: true);
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void CancelCommandHubLeave() => _commandHubLeaveTimer?.Stop();

    private void ReleaseCommandHubLeaveTimer()
    {
        if (_commandHubLeaveTimer == null) return;
        _commandHubLeaveTimer.Stop();
        _commandHubLeaveTimer.Tick -= CommandHubLeaveTimer_Tick;
        _commandHubLeaveTimer = null;
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
            ? WpfCursors.Arrow
            : WpfCursors.Hand;
    }

    private static bool IsInteractiveChild(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or
                System.Windows.Controls.Primitives.Thumb or
                System.Windows.Controls.Primitives.Selector or
                System.Windows.Controls.TextBox)
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
