// WinNotch.Tests/StateMachineTests.cs
// Tests for the NotchStateMachine — the core state management logic.
// These are pure logic tests — no Win32, no WPF, no UI.

using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class StateMachineTests
{
    // ═══════════════════════════════════════════════════════════════
    // BASIC TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

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
        Assert.Equal(NotchState.Hover, result.State);
        Assert.Equal(NotchState.Hover, sm.CurrentState);
    }

    [Fact]
    public void Transition_SameState_ReturnsShouldApplyFalse()
    {
        var sm = new NotchStateMachine();
        var result = sm.TryTransition(NotchState.Idle, StatePriority.None);

        Assert.False(result.ShouldApply);
        Assert.Equal(NotchState.Idle, sm.CurrentState);
    }

    // ═══════════════════════════════════════════════════════════════
    // PRIORITY SYSTEM
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void HigherPriority_CanInterrupt_LowerPriority()
    {
        var sm = new NotchStateMachine();

        // Start with media (priority 10)
        sm.TryTransition(NotchState.MediaActive, StatePriority.Media);
        Assert.Equal(NotchState.MediaActive, sm.CurrentState);

        // Clipboard (priority 20) should interrupt
        var result = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.ClipboardNotify, sm.CurrentState);
    }

    [Fact]
    public void LowerPriority_CannotInterrupt_HigherPriority()
    {
        var sm = new NotchStateMachine();

        // Start with drop result (priority 50)
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        Assert.Equal(NotchState.DropResult, sm.CurrentState);

        // Hover (priority 5) should NOT interrupt
        var result = sm.TryTransition(NotchState.Hover, StatePriority.Hover);
        Assert.False(result.ShouldApply);
        Assert.Equal(NotchState.DropResult, sm.CurrentState);
    }

    [Fact]
    public void Hover_IsAlwaysReplaceable()
    {
        var sm = new NotchStateMachine();

        // Start with hover (priority 5)
        sm.TryTransition(NotchState.Hover, StatePriority.Hover);
        Assert.Equal(NotchState.Hover, sm.CurrentState);

        // Clipboard (priority 20) should replace hover
        var result = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.ClipboardNotify, sm.CurrentState);
    }

    [Fact]
    public void DropResult_CanInterrupt_Clipboard()
    {
        var sm = new NotchStateMachine();

        // Start with clipboard (priority 20)
        sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);

        // DropResult (priority 50) should interrupt
        var result = sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.DropResult, sm.CurrentState);
    }

    // ═══════════════════════════════════════════════════════════════
    // FORCE TRANSITION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ForceTransition_OverridesPriority()
    {
        var sm = new NotchStateMachine();

        // Start with drop result (priority 50)
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);

        // Force to idle regardless of priority
        var result = sm.ForceTransition(NotchState.Idle);
        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.Idle, sm.CurrentState);
    }

    // ═══════════════════════════════════════════════════════════════
    // COALESCING (Rapid clipboard events)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ClipboardCoalescing_AllowsFirstEvents()
    {
        var sm = new NotchStateMachine();

        // First clipboard event should go through
        var r1 = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.True(r1.ShouldApply);

        // Rapid events should be coalesced (3 within 1 second)
        // We can't test timing precisely, but test the logic path
        // by doing same-state transitions (which return false anyway)
        var r2 = sm.TryTransition(NotchState.ClipboardNotify, StatePriority.Clipboard);
        Assert.False(r2.ShouldApply); // Same state
    }

    // ═══════════════════════════════════════════════════════════════
    // RETURN TO BEST STATE
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReturnToBest_WithMediaActive_ReturnsToMedia()
    {
        var sm = new NotchStateMachine();

        // Simulate: drop completed, media is playing
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        var result = sm.ReturnToBest(mediaActive: true);

        Assert.Equal(NotchState.MediaActive, result.State);
        Assert.True(result.ShouldApply);
    }

    [Fact]
    public void ReturnToBest_WithoutMedia_ReturnsToIdle()
    {
        var sm = new NotchStateMachine();

        // Simulate: drop completed, no media
        sm.TryTransition(NotchState.DropResult, StatePriority.DropResult);
        var result = sm.ReturnToBest(mediaActive: false);

        Assert.Equal(NotchState.Idle, result.State);
        Assert.True(result.ShouldApply);
    }

    [Fact]
    public void ReturnToIdle_SetsCorrectState()
    {
        var sm = new NotchStateMachine();
        sm.TryTransition(NotchState.MediaActive, StatePriority.Media);

        var result = sm.ReturnToIdle();
        Assert.Equal(NotchState.Idle, result.State);
        Assert.Equal(StatePriority.None, sm.CurrentPriority);
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE DIMENSIONS
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(NotchState.Idle, 130, 28)]
    [InlineData(NotchState.Hover, 150, 36)]
    [InlineData(NotchState.DragActive, 400, 120)]
    [InlineData(NotchState.DropResult, 400, 120)]
    [InlineData(NotchState.MediaActive, 350, 80)]
    [InlineData(NotchState.ClipboardNotify, 400, 60)]
    [InlineData(NotchState.ScreenshotNotify, 400, 60)]
    public void StateDimensions_ReturnCorrectValues(NotchState state, double expectedWidth, double expectedHeight)
    {
        var (w, h) = StateDimensions.GetDimensions(state);
        Assert.Equal(expectedWidth, w);
        Assert.Equal(expectedHeight, h);
    }

    // ═══════════════════════════════════════════════════════════════
    // TRANSITION RESULT
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TransitionResult_HasCorrectReturnState()
    {
        var sm = new NotchStateMachine();

        // DropResult should auto-return to idle
        var result = sm.TryTransition(NotchState.DropResult, StatePriority.DropResult,
            timeout: TimeSpan.FromSeconds(3), returnState: null);

        Assert.True(result.ShouldApply);
        Assert.Equal(NotchState.DropResult, result.State);
        Assert.Equal(NotchState.Idle, result.ReturnState); // Determined by DetermineReturnState
    }

    [Fact]
    public void TransitionResult_CanSpecifyExplicitReturnState()
    {
        var sm = new NotchStateMachine();

        var result = sm.TryTransition(NotchState.DragActive, StatePriority.DropTarget,
            timeout: TimeSpan.FromSeconds(2),
            returnState: NotchState.MediaActive);

        Assert.Equal(NotchState.MediaActive, result.ReturnState);
    }
}
