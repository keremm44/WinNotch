using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class PrimaryInteractionTests
{
    [Theory]
    [InlineData(NotchState.Idle)]
    [InlineData(NotchState.Hover)]
    public void IdleLikeStates_OpenQuickPeek(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.OpenQuickPeek, decision.Kind);
        Assert.Equal(NotchState.QuickPeek, decision.TargetState);
    }

    [Theory]
    [InlineData(NotchState.ShelfOccupied)]
    [InlineData(NotchState.DropResult)]
    public void ShelfStates_ExpandShelf(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.ExpandShelf, decision.Kind);
        Assert.Equal(NotchState.ShelfExpanded, decision.TargetState);
    }

    [Fact]
    public void MediaAmbient_ExpandsMedia()
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(NotchState.MediaAmbient);
        Assert.Equal(PrimaryInteractionKind.ExpandMedia, decision.Kind);
        Assert.Equal(NotchState.MediaActive, decision.TargetState);
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

    [Theory]
    [InlineData(NotchState.QuickPeek)]
    [InlineData(NotchState.ShelfExpanded)]
    [InlineData(NotchState.MediaActive)]
    public void ExpandedStates_CollapseToPersistent(NotchState state)
    {
        PrimaryInteractionDecision decision = PrimaryInteractionController.Resolve(state);
        Assert.Equal(PrimaryInteractionKind.CollapseToPersistent, decision.Kind);
        Assert.Null(decision.TargetState);
    }

    [Theory]
    [InlineData(NotchState.DragActive)]
    [InlineData(NotchState.ShelfDraggingOut)]
    public void DragStates_IgnorePrimaryClick(NotchState state)
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
