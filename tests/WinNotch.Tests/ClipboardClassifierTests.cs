// WinNotch.Tests/ClipboardClassifierTests.cs
// Tests for clipboard content classification.
// These are pure string analysis tests — no UI, no Win32.

using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class ClipboardClassifierTests
{
    // ═══════════════════════════════════════════════════════════════
    // URL DETECTION
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("https://example.com", ClipboardContentType.Url)]
    [InlineData("http://localhost:3000", ClipboardContentType.Url)]
    [InlineData("www.github.com", ClipboardContentType.Url)]
    [InlineData("HTTPS://EXAMPLE.COM", ClipboardContentType.Url)]
    public void Classify_Urls_ReturnsUrl(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    // ═══════════════════════════════════════════════════════════════
    // FILE PATH DETECTION
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("C:\\Users\\test\\file.cs", ClipboardContentType.FilePath)]
    [InlineData("D:/projects/app/main.cs", ClipboardContentType.FilePath)]
    [InlineData("\\\\server\\share\\file.txt", ClipboardContentType.FilePath)]
    [InlineData("~/Documents/note.txt", ClipboardContentType.FilePath)]
    [InlineData("./relative/path.cs", ClipboardContentType.FilePath)]
    [InlineData("\"C:\\Users\\test\\file.cs\"", ClipboardContentType.FilePath)]
    [InlineData("\"D:/projects/app/main.cs\"", ClipboardContentType.FilePath)]
    [InlineData("\"C:\\Users\\test\\a-very-long-file-name-that-is-truncated...", ClipboardContentType.FilePath)]
    public void Classify_FilePaths_ReturnsFilePath(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    [Fact]
    public void Classify_MultipleCopyAsPathValues_DoesNotPretendTheyAreOneFile()
    {
        string multiplePaths = "\"C:\\one.txt\"\r\n\"C:\\two.txt\"";
        Assert.Equal(ClipboardContentType.Text, ClipboardClassifier.Classify(multiplePaths));
    }

    // ═══════════════════════════════════════════════════════════════
    // COLOR HEX DETECTION
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("#FF0000", ClipboardContentType.Color)]
    [InlineData("#000000", ClipboardContentType.Color)]
    [InlineData("#ABC", ClipboardContentType.Color)]
    [InlineData("#1e1e1e", ClipboardContentType.Color)]
    public void Classify_Colors_ReturnsColor(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    // ═══════════════════════════════════════════════════════════════
    // EMAIL DETECTION
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("user@example.com", ClipboardContentType.Email)]
    [InlineData("test@domain.org", ClipboardContentType.Email)]
    public void Classify_Emails_ReturnsEmail(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    // ═══════════════════════════════════════════════════════════════
    // PHONE DETECTION
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("+1-555-123-4567", ClipboardContentType.Phone)]
    [InlineData("555 123 4567", ClipboardContentType.Phone)]
    public void Classify_Phones_ReturnsPhone(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    // ═══════════════════════════════════════════════════════════════
    // TEXT / EMPTY / EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Hello, world!", ClipboardContentType.Text)]
    [InlineData("C# is great", ClipboardContentType.Text)]
    [InlineData("555", ClipboardContentType.Text)] // Too short for phone
    public void Classify_Text_ReturnsText(string text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    [Theory]
    [InlineData(null, ClipboardContentType.Unknown)]
    [InlineData("", ClipboardContentType.Unknown)]
    [InlineData("   ", ClipboardContentType.Unknown)]
    public void Classify_EmptyOrNull_ReturnsUnknown(string? text, ClipboardContentType expected)
    {
        Assert.Equal(expected, ClipboardClassifier.Classify(text));
    }

    // ═══════════════════════════════════════════════════════════════
    // CASE INSENSITIVITY
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Classify_Url_CaseInsensitive()
    {
        Assert.Equal(ClipboardContentType.Url, ClipboardClassifier.Classify("HTTP://EXAMPLE.COM"));
        Assert.Equal(ClipboardContentType.Url, ClipboardClassifier.Classify("Https://Example.Com"));
    }

    [Fact]
    public void Classify_FilePath_CaseInsensitive()
    {
        Assert.Equal(ClipboardContentType.FilePath, ClipboardClassifier.Classify("~/Documents/TEST.TXT"));
    }
}
