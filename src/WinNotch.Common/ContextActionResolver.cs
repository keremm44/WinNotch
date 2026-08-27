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
            ClipboardContentType.FilePath => new ContextAction(
                ContextActionKind.ShowInExplorer,
                "Explorer'da göster",
                value,
                "Explorer açıldı"),
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

    private static ContextAction? ResolveEmail(string value)
    {
        if (value.Contains(' ') || value.Length > 254)
            return null;

        int atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
            return null;

        string target = $"mailto:{Uri.EscapeDataString(value)}";
        return new ContextAction(
            ContextActionKind.ComposeEmail,
            "E-posta yaz",
            target,
            "E-posta uygulaması açıldı");
    }
}
