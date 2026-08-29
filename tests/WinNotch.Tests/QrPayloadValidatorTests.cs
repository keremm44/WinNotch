using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class QrPayloadValidatorTests
{
    [Fact]
    public void Validate_TrimsAndAcceptsUnicodeUrl()
    {
        QrPayloadValidation result = QrPayloadValidator.Validate("  https://örnek.com/ara?q=notch  ");
        Assert.True(result.IsValid);
        Assert.Equal("https://örnek.com/ara?q=notch", result.Value);
    }

    [Fact]
    public void Validate_RejectsEmptyPayload()
    {
        QrPayloadValidation result = QrPayloadValidator.Validate("   ");
        Assert.False(result.IsValid);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Validate_UsesUtf8ByteLimitRatherThanCharacterCount()
    {
        string value = new('ş', (QrPayloadValidator.MaxUtf8Bytes / 2) + 1);
        QrPayloadValidation result = QrPayloadValidator.Validate(value);
        Assert.False(result.IsValid);
        Assert.Contains("UTF-8", result.Error);
    }
}
