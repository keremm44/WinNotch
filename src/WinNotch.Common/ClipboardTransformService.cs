using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinNotch.Common;

public enum ClipboardTransformKind
{
    CleanWhitespace,
    Uppercase,
    Lowercase,
    TitleCase,
    PrettyJson,
    UrlEncode,
    UrlDecode
}

public sealed record ClipboardTransformResult(bool Success, string Output, string? Error = null);

/// <summary>
/// Pure, dependency-free text transformations used by Command Hub. The service has
/// no listener, timer or retained payload, so it adds no idle background cost.
/// </summary>
public static partial class ClipboardTransformService
{
    public const int MaxInputLength = 1_000_000;

    public static ClipboardTransformResult Transform(
        string? input,
        ClipboardTransformKind kind,
        CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(input))
            return new(false, string.Empty, "Panoda dönüştürülecek metin yok.");

        if (input.Length > MaxInputLength)
            return new(false, input, "Metin güvenli dönüştürme sınırını aşıyor.");

        culture ??= CultureInfo.CurrentCulture;

        try
        {
            string output = kind switch
            {
                ClipboardTransformKind.CleanWhitespace => CleanWhitespace(input),
                ClipboardTransformKind.Uppercase => input.ToUpper(culture),
                ClipboardTransformKind.Lowercase => input.ToLower(culture),
                ClipboardTransformKind.TitleCase => culture.TextInfo.ToTitleCase(input.ToLower(culture)),
                ClipboardTransformKind.PrettyJson => PrettyJson(input),
                ClipboardTransformKind.UrlEncode => Uri.EscapeDataString(input),
                ClipboardTransformKind.UrlDecode => Uri.UnescapeDataString(input.Replace("+", " ")),
                _ => input
            };

            return new(true, output);
        }
        catch (JsonException)
        {
            return new(false, input, "Geçerli bir JSON metni değil.");
        }
        catch (UriFormatException)
        {
            return new(false, input, "URL dönüşümü tamamlanamadı.");
        }
    }

    private static string CleanWhitespace(string input)
    {
        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var result = new StringBuilder(normalized.Length);
        int blankLines = 0;

        foreach (string sourceLine in lines)
        {
            string line = HorizontalWhitespaceRegex().Replace(sourceLine.Trim(), " ");
            if (line.Length == 0)
            {
                blankLines++;
                if (blankLines > 1 || result.Length == 0)
                    continue;
            }
            else
            {
                blankLines = 0;
            }

            if (result.Length > 0)
                result.Append(Environment.NewLine);
            result.Append(line);
        }

        return result.ToString();
    }

    private static string PrettyJson(string input)
    {
        using JsonDocument document = JsonDocument.Parse(input);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    [GeneratedRegex(@"[\t\p{Zs}]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespaceRegex();
}
