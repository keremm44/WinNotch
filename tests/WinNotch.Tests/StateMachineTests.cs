using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class StateMachineTests
{
    [Fact]
    public void InitialState_IsIdle()
    {
        var sm = new NotchStateMachine();
        Assert.Equal(NotchState.Idle, sm.CurrentState);
        Assert.Equal(StatePriority.None, sm.CurrentPriority);
    }

    [Fact]
    public void Transition_FromIdle_ToHover_Succeeds()
    {
        var sm = new NotchStateMachine();
        var result = sm.TryTransition(NotchState.Hover, StatePriority.Hover);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.Hover, sm.CurrentState);
    }

    [Fact]
    public void Transition_SameState_ReturnsShouldApplyFalse()
    {
        var sm = new NotchStateMachine();
        var result = sm.TryTransition(NotchState.Idle, StatePriority.None);
        Assert.False(result.ShouldApply);
    }

    [Fact]
    public void HigherPriority_CanInterrupt_LowerPriority()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.MediaAmbient, StatePriority.Media);
        var result = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.ClipboardNotify, sm.CurrentState);
    }

    [Fact]
    public void LowerPriority_CannotInterrupt_HigherPriority()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        var result = sm.TryTransition(NotchState.Hover, StatePriority.Hover);
        Assert.False(result.ShouldApply);
        Assert.Equal(NotchState.DropResult, sm.CurrentState);
    }

    [Fact]
    public void Shelf_CanBeInterruptedByActionableClipboard()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.ShelfOccupied, StatePriority.Shelf);
        var result = sm.TryTransition(
            NotchState.ClipboardNotify,
            StatePriority.Clipboard,
            returnState: NotchState.ShelfOccupied);

        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.ShelfOccupied, result.ReturnState);
    }

    [Fact]
    public void DropResult_DefaultReturnState_IsShelfOccupied()
    {
        var sm = new NotchStateMachine();
        var result = sm.TryTransition(
            NotchState.DropResult,
            StatePriority.DropResult,
            timeout: TimeSpan.FromMilliseconds(900));

        Assert.Equal(NotchState.ShelfOccupied, result.ReturnState);
    }

    [Fact]
    public void ForceTransition_OverridesPriority_AndUsesDestinationPriority()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);

        var result = sm.ForceTransition(NotchState.Idle);

        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.Idle, sm.CurrentState);
        Assert.Equal(StatePriority.None, sm.CurrentPriority);
    }

    [Fact]
    public void ForceTransition_ToShelf_DoesNotPoisonFutureClipboardPriority()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        sm.ForceTransition(NotchState.ShelfOccupied);

        Assert.Equal(StatePriority.Shelf, sm.CurrentPriority);

        var clipboard = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.True(clipboard.ShouldApply);
        Assert.Equal(NotchState.ClipboardNotify, sm.CurrentState);
    }

    [Fact]
    public void ReturnTo_Shelf_SetsShelfPriority()
    {
        var sm = new NotchStateMachine();
        var result = sm.ReturnTo(NotchState.ShelfOccupied);
        Assert.Equal(NotchState.ShelfOccupied, result.State);
        Assert.Equal(StatePriority.Shelf, sm.CurrentPriority);
    }

    [Fact]
    public void ReturnToBest_WithMediaActive_ReturnsAmbientMedia()
    {
        var sm = new NotchStateMachine();
        var result = sm.ReturnToBest(mediaActive: true);
        Assert.Equal(NotchState.MediaAmbient, result.State);
    }

    [Fact]
    public void ReturnToBest_WithoutMedia_ReturnsIdle()
    {
        var sm = new NotchStateMachine();
        var result = sm.ReturnToBest(mediaActive: false);
        Assert.Equal(NotchState.Idle, result.State);
    }

    [Fact]
    public void ReturnToIdle_SetsCorrectState()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.MediaActive, StatePriority.Media);
        sm.ReturnToIdle();
        Assert.Equal(NotchState.Idle, sm.CurrentState);
        Assert.Equal(StatePriority.None, sm.CurrentPriority);
    }

    [Theory]
    [InlineData(NotchState.Idle, 100, 22)]
    [InlineData(NotchState.Hover, 118, 28)]
    [InlineData(NotchState.DragActive, 280, 60)]
    [InlineData(NotchState.DropResult, 320, 72)]
    [InlineData(NotchState.ShelfOccupied, 220, 36)]
    [InlineData(NotchState.ShelfExpanded, 330, 66)]
    [InlineData(NotchState.ShelfDraggingOut, 330, 66)]
    [InlineData(NotchState.MediaActive, 300, 64)]
    [InlineData(NotchState.MediaAmbient, 120, 28)]
    [InlineData(NotchState.ClipboardNotify, 220, 36)]
    [InlineData(NotchState.ScreenshotNotify, 260, 40)]
    [InlineData(NotchState.WindowPinned, 130, 28)]
    public void StateDimensions_ReturnCorrectValues(NotchState state, double expectedWidth, double expectedHeight)
    {
        var (w, h) = StateDimensions.GetDimensions(state);
        Assert.Equal(expectedWidth, w);
        Assert.Equal(expectedHeight, h);
    }

    [Fact]
    public void ExplicitReturnState_IsPreserved()
    {
        var sm = new NotchStateMachine();
        var result = sm.TryTransition(
            NotchState.ScreenshotNotify,
            StatePriority.Screenshot,
            timeout: TimeSpan.FromSeconds(2),
            returnState: NotchState.ShelfOccupied);

        Assert.Equal(NotchState.ShelfOccupied, result.ReturnState);
    }
}
