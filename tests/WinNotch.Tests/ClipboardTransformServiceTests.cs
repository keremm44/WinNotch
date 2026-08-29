using System.Globalization;
using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class ClipboardTransformServiceTests
{
    [Fact]
    public void CleanWhitespace_PreservesParagraphsAndRemovesNoise()
    {
        ClipboardTransformResult result = ClipboardTransformService.Transform(
            "  ilk\t satır  \r\n\r\n\r\n ikinci   satır ",
            ClipboardTransformKind.CleanWhitespace);

        Assert.True(result.Success);
        Assert.Equal($"ilk satır{Environment.NewLine}{Environment.NewLine}ikinci satır", result.Output);
    }

    [Fact]
    public void TurkishUppercase_UsesRequestedCulture()
    {
        ClipboardTransformResult result = ClipboardTransformService.Transform(
            "istanbul ısparta",
            ClipboardTransformKind.Uppercase,
            CultureInfo.GetCultureInfo("tr-TR"));

        Assert.Equal("İSTANBUL ISPARTA", result.Output);
    }

    [Fact]
    public void PrettyJson_IndentsValidDocument()
    {
        ClipboardTransformResult result = ClipboardTransformService.Transform(
            "{\"name\":\"WinNotch\",\"active\":true}",
            ClipboardTransformKind.PrettyJson);

        Assert.True(result.Success);
        Assert.Contains("\n", result.Output);
        Assert.Contains("  \"name\": \"WinNotch\"", result.Output);
    }

    [Fact]
    public void PrettyJson_LeavesInvalidInputAndReturnsError()
    {
        const string input = "{not-json}";
        ClipboardTransformResult result = ClipboardTransformService.Transform(
            input,
            ClipboardTransformKind.PrettyJson);

        Assert.False(result.Success);
        Assert.Equal(input, result.Output);
        Assert.Equal("Geçerli bir JSON metni değil.", result.Error);
    }

    [Theory]
    [InlineData(ClipboardTransformKind.Uppercase)]
    [InlineData(ClipboardTransformKind.Lowercase)]
    [InlineData(ClipboardTransformKind.TitleCase)]
    [InlineData(ClipboardTransformKind.UrlEncode)]
    [InlineData(ClipboardTransformKind.UrlDecode)]
    public void EmptyClipboard_IsRejected(ClipboardTransformKind kind)
    {
        ClipboardTransformResult result = ClipboardTransformService.Transform(string.Empty, kind);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public void UrlEncodeAndDecode_RoundTripUnicodeText()
    {
        const string input = "WinNotch araçları / test";
        string encoded = ClipboardTransformService.Transform(
            input, ClipboardTransformKind.UrlEncode).Output;
        ClipboardTransformResult decoded = ClipboardTransformService.Transform(
            encoded, ClipboardTransformKind.UrlDecode);

        Assert.True(decoded.Success);
        Assert.Equal(input, decoded.Output);
    }
}
