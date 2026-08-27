using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class ContextActionResolverTests
{
    [Fact]
    public void Url_ResolvesToOpenAction()
    {
        ContextAction? action = ContextActionResolver.ResolveClipboard(
            ClipboardContentType.Url,
            "https://example.com/path");

        Assert.NotNull(action);
        Assert.Equal(ContextActionKind.OpenUrl, action!.Kind);
        Assert.Equal("Aç", action.Label);
        Assert.Equal("https://example.com/path", action.Target);
    }

    [Fact]
    public void WwwUrl_IsNormalizedToHttps()
    {
        ContextAction? action = ContextActionResolver.ResolveClipboard(
            ClipboardContentType.Url,
            "www.example.com/docs");

        Assert.NotNull(action);
        Assert.Equal("https://www.example.com/docs", action!.Target);
    }

    [Fact]
    public void InvalidUrl_ReturnsNoAction()
    {
        ContextAction? action = ContextActionResolver.ResolveClipboard(
            ClipboardContentType.Url,
            "https://");

        Assert.Null(action);
    }

    [Fact]
    public void FilePath_ResolvesToExplorerAction()
    {
        ContextAction? action = ContextActionResolver.ResolveClipboard(
            ClipboardContentType.FilePath,
            @"C:\Users\test\report.pdf");

        Assert.NotNull(action);
        Assert.Equal(ContextActionKind.ShowInExplorer, action!.Kind);
        Assert.Equal("Explorer'da göster", action.Label);
    }

    [Fact]
    public void Email_ResolvesToComposeAction()
    {
        ContextAction? action = ContextActionResolver.ResolveClipboard(
            ClipboardContentType.Email,
            "hello@example.com");

        Assert.NotNull(action);
        Assert.Equal(ContextActionKind.ComposeEmail, action!.Kind);
        Assert.StartsWith("mailto:", action.Target, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ClipboardContentType.Text, "hello")]
    [InlineData(ClipboardContentType.Color, "#2D7DFF")]
    [InlineData(ClipboardContentType.Phone, "+90 555 111 22 33")]
    [InlineData(ClipboardContentType.Unknown, "anything")]
    public void UnsupportedClipboardTypes_DoNotGuessActions(
        ClipboardContentType type,
        string text)
    {
        Assert.Null(ContextActionResolver.ResolveClipboard(type, text));
    }

    [Fact]
    public void EmptyClipboardValue_ReturnsNoAction()
    {
        Assert.Null(ContextActionResolver.ResolveClipboard(ClipboardContentType.Url, "   "));
    }

    [Fact]
    public void Screenshot_ResolvesToSaveAction()
    {
        ContextAction action = ContextActionResolver.ResolveScreenshot();

        Assert.Equal(ContextActionKind.SaveScreenshot, action.Kind);
        Assert.Equal("Kaydet", action.Label);
    }
}
