using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class PrimaryInteractionTests
{
    [Theory]
    [InlineData(NotchState.Idle)]
    [InlineData(NotchState.Hover)]
    [InlineData(NotchState.MediaAmbient)]
    [InlineData(NotchState.MediaActive)]
    [InlineData(NotchState.ShelfOccupied)]
    [InlineData(NotchState.ShelfExpanded)]
    [InlineData(NotchState.DropResult)]
    [InlineData(NotchState.TimerNotify)]
    public void UnclaimedPrimaryClick_OpensCommandHub(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.OpenCommandHub, decision.Kind);
        Assert.Equal(NotchState.CommandHub, decision.TargetState);
    }

    [Fact]
    public void MediaClick_OpensHub_WithoutBecomingAMediaExpansionCommand()
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(NotchState.MediaActive);
        Assert.Equal(PrimaryInteractionKind.OpenCommandHub, decision.Kind);
        Assert.Equal(NotchState.CommandHub, decision.TargetState);
    }

    [Theory]
    [InlineData(NotchState.ClipboardNotify)]
    [InlineData(NotchState.ScreenshotNotify)]
    public void NotificationStates_OnlyRevealActions(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.ExpandContextAction, decision.Kind);
        Assert.Equal(state, decision.TargetState);
    }

    [Fact]
    public void CommandHub_ClickCollapsesToPersistentState()
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(NotchState.CommandHub);
        Assert.Equal(PrimaryInteractionKind.CollapseToPersistent, decision.Kind);
        Assert.Null(decision.TargetState);
    }

    [Theory]
    [InlineData(NotchState.DragActive)]
    [InlineData(NotchState.ShelfDraggingOut)]
    public void DragOwnedStates_IgnorePrimaryClick(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.None, decision.Kind);
    }

    [Fact]
    public void ClipboardContextCache_RemembersOnlyActionableValues()
    {
        var cache = new LastMeaningfulClipboardContextCache();

        Assert.False(cache.TryRemember(
            ClipboardContentType.Text,
            "hello",
            "hello",
            DateTime.UtcNow));
        Assert.Null(cache.Current);

        DateTime timestamp = DateTime.UtcNow;
        Assert.True(cache.TryRemember(
            ClipboardContentType.Url,
            "https://example.com/private",
            "https://example.com/private",
            timestamp));

        Assert.NotNull(cache.Current);
        Assert.Equal(ClipboardContentType.Url, cache.Current!.ContentType);
        Assert.Equal("https://example.com/private", cache.Current.RawText);
        Assert.Equal(ContextActionKind.OpenUrl, cache.Current.Action.Kind);
        Assert.Equal(timestamp, cache.Current.Timestamp);
    }

    [Fact]
    public void ClipboardContextCache_ReplacesOlderActionableValue()
    {
        var cache = new LastMeaningfulClipboardContextCache();
        cache.TryRemember(
            ClipboardContentType.Url,
            "https://first.example",
            "https://first.example",
            DateTime.UtcNow.AddSeconds(-1));

        cache.TryRemember(
            ClipboardContentType.Email,
            "user@example.com",
            "user@example.com",
            DateTime.UtcNow);

        Assert.Equal(ClipboardContentType.Email, cache.Current!.ContentType);
        Assert.Equal("user@example.com", cache.Current.RawText);
    }
}
