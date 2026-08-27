using System.IO;

namespace WinNotch.Common;

/// <summary>
/// Formats only what is shown on-screen. The original clipboard payload is never
/// changed, so context actions continue to execute against the full raw value.
/// </summary>
public static class PrivacyPreviewFormatter
{
    public static string Format(
        ClipboardContentType contentType,
        string? rawValue,
        string? previewValue,
        AppearanceSettings settings)
    {
        AppearanceResolver.NormalizeInPlace(settings);

        string preview = string.IsNullOrWhiteSpace(previewValue)
            ? FallbackLabel(contentType)
            : previewValue.Trim();

        if (string.Equals(settings.PrivacyPreviewMode, "Full", StringComparison.OrdinalIgnoreCase))
            return preview;

        if (string.Equals(settings.PrivacyPreviewMode, "TypeOnly", StringComparison.OrdinalIgnoreCase))
            return FallbackLabel(contentType);

        string raw = (rawValue ?? previewValue ?? string.Empty).Trim();
        return contentType switch
        {
            ClipboardContentType.Url => MaskUrl(raw),
            ClipboardContentType.FilePath => MaskPath(raw),
            ClipboardContentType.Email => MaskEmail(raw),
            ClipboardContentType.Phone => MaskPhone(raw),
            ClipboardContentType.Color => preview,
            ClipboardContentType.Text => "Metin kopyalandı",
            _ => FallbackLabel(contentType)
        };
    }

    public static bool ShouldShowScreenshotThumbnail(AppearanceSettings settings)
    {
        AppearanceResolver.NormalizeInPlace(settings);
        return string.Equals(settings.PrivacyPreviewMode, "Full", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskUrl(string value)
    {
        string candidate = value;
        if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            candidate = "https://" + candidate;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            bool hasDetail = uri.AbsolutePath is { Length: > 1 } || !string.IsNullOrWhiteSpace(uri.Query);
            return hasDetail ? $"{uri.Host}/••••" : uri.Host;
        }

        return "Bağlantı kopyalandı";
    }

    private static string MaskEmail(string value)
    {
        int at = value.IndexOf('@');
        if (at <= 0 || at >= value.Length - 1)
            return "E-posta kopyalandı";

        string local = value[..at];
        string domain = value[(at + 1)..];
        return $"{local[0]}••••@{domain}";
    }

    private static string MaskPhone(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 2
            ? $"••• ••• •• {digits[^2..]}"
            : "Telefon kopyalandı";
    }

    private static string MaskPath(string value)
    {
        string path = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return "Dosya yolu kopyalandı";

        string normalized = path.Replace('/', '\');
        string[] parts = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "Dosya yolu kopyalandı";

        if (parts.Length == 1)
            return $"…\\{parts[0]}";

        return $"…\\{parts[^2]}\\{parts[^1]}";
    }

    private static string FallbackLabel(ClipboardContentType type)
        => type switch
        {
            ClipboardContentType.Url => "Bağlantı kopyalandı",
            ClipboardContentType.FilePath => "Dosya yolu kopyalandı",
            ClipboardContentType.Color => "Renk kopyalandı",
            ClipboardContentType.Email => "E-posta kopyalandı",
            ClipboardContentType.Phone => "Telefon kopyalandı",
            ClipboardContentType.Text => "Metin kopyalandı",
            _ => "Pano güncellendi"
        };
}
