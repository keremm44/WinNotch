using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class AttentionPolicyTests
{
    private readonly AttentionPolicy _policy = new();

    [Fact]
    public void PlainTextClipboard_IsSilent_InBalancedMode()
    {
        var result = _policy.ClassifyClipboard(
            ClipboardContentType.Text,
            "Hello world",
            "Balanced");
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    [Fact]
    public void PlainTextClipboard_IsSubtle_InActiveMode()
    {
        var result = _policy.ClassifyClipboard(
            ClipboardContentType.Text,
            "Hello world",
            "Active");
        Assert.Equal(AttentionLevel.Subtle, result.Level);
        Assert.Equal(NotchState.ClipboardNotify, result.TargetState);
    }

    [Theory]
    [InlineData(ClipboardContentType.Url, AttentionLevel.Subtle)]
    [InlineData(ClipboardContentType.FilePath, AttentionLevel.Actionable)]
    [InlineData(ClipboardContentType.Email, AttentionLevel.Subtle)]
    [InlineData(ClipboardContentType.Phone, AttentionLevel.Subtle)]
    [InlineData(ClipboardContentType.Color, AttentionLevel.Subtle)]
    public void Balanced_ActionableClipboardMatrix_UsesExpectedAttention(
        ClipboardContentType contentType,
        AttentionLevel expectedLevel)
    {
        var result = _policy.ClassifyClipboard(contentType, "sample", "Balanced");

        Assert.Equal(expectedLevel, result.Level);
        Assert.False(result.Suppressed);
        Assert.Equal(NotchState.ClipboardNotify, result.TargetState);
    }

    [Fact]
    public void UrlClipboard_IsSilent_InQuietMode()
    {
        var result = _policy.ClassifyClipboard(
            ClipboardContentType.Url,
            "https://example.com",
            "Quiet");
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    [Fact]
    public void FilePathClipboard_RemainsActionable_InQuietMode()
    {
        var result = _policy.ClassifyClipboard(
            ClipboardContentType.FilePath,
            @"C:\Users\test\file.cs",
            "Quiet");
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

    [Fact]
    public void Screenshot_IsAlwaysActionable()
    {
        var result = _policy.ClassifyScreenshot();
        Assert.Equal(AttentionLevel.Actionable, result.Level);
        Assert.Equal(NotchState.ScreenshotNotify, result.TargetState);
    }

    [Fact]
    public void MediaStarted_IsSubtle_InBalancedMode()
    {
        var result = _policy.ClassifyMediaChange(hasSession: true, reactionLevel: "Balanced");
        Assert.Equal(AttentionLevel.Subtle, result.Level);
        Assert.Equal(NotchState.MediaAmbient, result.TargetState);
    }

    [Fact]
    public void MediaStarted_IsSilent_InQuietMode()
    {
        var result = _policy.ClassifyMediaChange(hasSession: true, reactionLevel: "Quiet");
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    [Fact]
    public void MediaStopped_IsSilent()
    {
        var result = _policy.ClassifyMediaChange(hasSession: false);
        Assert.Equal(AttentionLevel.Silent, result.Level);
    }

    [Fact]
    public void FileDrop_IsImportant()
    {
        var result = _policy.ClassifyDrop();
        Assert.Equal(AttentionLevel.Important, result.Level);
        Assert.Equal(NotchState.DropResult, result.TargetState);
    }

    [Theory]
    [InlineData(ClipboardContentType.Text)]
    [InlineData(ClipboardContentType.Unknown)]
    public void NonActionableContent_NeverExpands_InBalancedMode(ClipboardContentType type)
    {
        var result = _policy.ClassifyClipboard(type, "some text", "Balanced");
        Assert.Equal(AttentionLevel.Silent, result.Level);
        Assert.NotEqual(NotchState.ClipboardNotify, result.TargetState);
        Assert.NotEqual(NotchState.ScreenshotNotify, result.TargetState);
        Assert.NotEqual(NotchState.DropResult, result.TargetState);
    }

    [Fact]
    public void RapidSecondNotification_IsMarkedSuppressed()
    {
        var first = _policy.ClassifyClipboard(
            ClipboardContentType.Url,
            "https://example.com/1",
            "Balanced");
        var second = _policy.ClassifyClipboard(
            ClipboardContentType.Url,
            "https://example.com/2",
            "Balanced");

        Assert.False(first.Suppressed);
        Assert.True(second.Suppressed);
        Assert.Equal(NotchState.Idle, second.TargetState);
    }
}
