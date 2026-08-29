using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;
using WinNotch.Common;

namespace WinNotch.UI;

internal sealed record QrCodeRenderResult(bool Success, byte[]? PngBytes, BitmapSource? Image, string? Error);

internal static class QrCodeGeneratorService
{
    public static QrCodeRenderResult Generate(string? input)
    {
        QrPayloadValidation validation = QrPayloadValidator.Validate(input);
        if (!validation.IsValid)
            return new(false, null, null, validation.Error);

        try
        {
            using QRCodeData data = QRCodeGenerator.GenerateQrCode(
                validation.Value,
                QRCodeGenerator.ECCLevel.Q);
            using var qr = new PngByteQRCode(data);
            byte[] png = qr.GetGraphic(8, drawQuietZones: true);

            var image = new BitmapImage();
            using var stream = new MemoryStream(png, writable: false);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return new(true, png, image, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] Generation failed: {ex.Message}");
            return new(false, null, null, "QR oluşturulamadı; metni kısaltıp tekrar deneyin.");
        }
    }
}
