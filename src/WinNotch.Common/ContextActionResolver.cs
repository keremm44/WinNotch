namespace WinNotch.Common;

/// <summary>
/// A small, explicit vocabulary for actions WinNotch is allowed to surface.
/// The resolver is intentionally conservative: unsupported or ambiguous content
/// produces no action instead of guessing.
/// </summary>
public enum ContextActionKind
{
    None = 0,
    OpenUrl,
    ShowInExplorer,
    ComposeEmail,
    SaveScreenshot
}

public sealed record ContextAction(
    ContextActionKind Kind,
    string Label,
    string Target,
    string? SuccessMessage = null);

public static class ContextActionResolver
{
    public static ContextAction? ResolveClipboard(
        ClipboardContentType contentType,
        string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        string value = rawText.Trim();

        return contentType switch
        {
            ClipboardContentType.Url => ResolveUrl(value),
            ClipboardContentType.FilePath => ResolveFilePath(value),
            ClipboardContentType.Email => ResolveEmail(value),
            _ => null
        };
    }

    public static ContextAction ResolveScreenshot()
        => new(
            ContextActionKind.SaveScreenshot,
            "Kaydet",
            string.Empty,
            "Kaydedildi");

    private static ContextAction? ResolveUrl(string value)
    {
        string normalized = value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? $"https://{value}"
            : value;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        return new ContextAction(
            ContextActionKind.OpenUrl,
            "Aç",
            uri.AbsoluteUri,
            "Bağlantı açıldı");
    }

    private static ContextAction? ResolveFilePath(string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            return null;

        string normalized = value;
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
            normalized = normalized[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return new ContextAction(
            ContextActionKind.ShowInExplorer,
            "Explorer'da göster",
            normalized,
            "Explorer açıldı");
    }

    private static ContextAction? ResolveEmail(string value)
    {
        if (value.Contains(' ') || value.Length > 254)
            return null;

        int atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
            return null;

        // Keep the address human-readable. Windows mailto handlers accept the
        // normal address form more consistently than an encoded '@' character.
        string target = $"mailto:{value}";
        return new ContextAction(
            ContextActionKind.ComposeEmail,
            "E-posta yaz",
            target,
            "E-posta uygulaması açıldı");
    }
}
