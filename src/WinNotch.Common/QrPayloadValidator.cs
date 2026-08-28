using System.Text;

namespace WinNotch.Common;

public sealed record QrPayloadValidation(bool IsValid, string Value, string? Error = null);

public static class QrPayloadValidator
{
    // Conservative bound for reliable error-correction and responsive rendering.
    public const int MaxUtf8Bytes = 1_500;

    public static QrPayloadValidation Validate(string? input)
    {
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return new(false, string.Empty, "QR için metin girin.");

        if (Encoding.UTF8.GetByteCount(value) > MaxUtf8Bytes)
            return new(false, value, $"Metin {MaxUtf8Bytes:N0} UTF-8 bayt sınırını aşıyor.");

        return new(true, value);
    }
}
