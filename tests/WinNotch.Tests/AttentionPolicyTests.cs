// WinNotch.Tests/AttentionPolicyTests.cs
// Tests for the AttentionPolicy — ensures WinNotch doesn't become an annoyance engine.

using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class AttentionPolicyTests
{
    private readonly AttentionPolicy _policy = new();

    // ═══════════════════════════════════════════════════════════════
    // CLIPBOARD CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PlainTextClipboard_IsSilent()
    {
        var result = _policy.ClassifyClipboard(ClipboardContentType.Text, "Hello world");
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    [Fact]
    public void FilePathClipboard_IsActionable()
    {
        var result = _policy.ClassifyClipboard(ClipboardContentType.FilePath, @"C:\Users\test\file.cs");
        Assert.Equal(AttentionLevel.Actionable, result.Level);
        Assert.Equal(NotchState.ClipboardNotify, result.TargetState);
    }

    [Fact]
    public void UrlClipboard_IsSubtle()
    {
        var result = _policy.ClassifyClipboard(ClipboardContentType.Url, "https://example.com");
        Assert.Equal(AttentionLevel.Subtle, result.Level);
    }

    [Fact]
    public void ColorClipboard_IsSubtle()
    {
        var result = _policy.ClassifyClipboard(ClipboardContentType.Color, "#FF0000");
        Assert.Equal(AttentionLevel.Subtle, result.Level);
    }

    [Fact]
    public void UnknownClipboard_IsSilent()
    {
        var result = _policy.ClassifyClipboard(ClipboardContentType.Unknown, null);
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    // ═══════════════════════════════════════════════════════════════
    // SCREENSHOT CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Screenshot_IsAlwaysActionable()
    {
        var result = _policy.ClassifyScreenshot();
        Assert.Equal(AttentionLevel.Actionable, result.Level);
        Assert.Equal(NotchState.ScreenshotNotify, result.TargetState);
    }

    // ═══════════════════════════════════════════════════════════════
    // MEDIA CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MediaStarted_IsSubtle()
    {
        var result = _policy.ClassifyMediaChange(hasSession: true);
        Assert.Equal(AttentionLevel.Subtle, result.Level);
        Assert.Equal(NotchState.MediaAmbient, result.TargetState);
    }

    [Fact]
    public void MediaStopped_IsSilent()
    {
        var result = _policy.ClassifyMediaChange(hasSession: false);
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    // ═══════════════════════════════════════════════════════════════
    // DROP CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FileDrop_IsImportant()
    {
        var result = _policy.ClassifyDrop();
        Assert.Equal(AttentionLevel.Important, result.Level);
        Assert.Equal(NotchState.DropResult, result.TargetState);
    }

    // ═══════════════════════════════════════════════════════════════
    // CORE PRINCIPLE: Plain text should NEVER expand the notch
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ClipboardContentType.Text)]
    [InlineData(ClipboardContentType.Unknown)]
    public void NonActionableContent_NeverExpands(ClipboardContentType type)
    {
        var result = _policy.ClassifyClipboard(type, "some text");
        Assert.Equal(AttentionLevel.Silent, result.Level);
        // Must NOT transition to an expanded state
        Assert.NotEqual(NotchState.ClipboardNotify, result.TargetState);
        Assert.NotEqual(NotchState.ScreenshotNotify, result.TargetState);
        Assert.NotEqual(NotchState.DropResult, result.TargetState);
    }
}
