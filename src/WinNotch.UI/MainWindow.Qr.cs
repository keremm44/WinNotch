using System.IO;
using System.Windows.Media.Imaging;
using WinNotch.Common;
using WinNotch.Core.Services;

namespace WinNotch.UI;

public partial class MainWindow
{
    private void OnCommandHubQrClipboardTextRequested(
        object? sender,
        Views.QrClipboardTextRequestedEventArgs e)
    {
        if (_currentState == NotchState.CommandHub &&
            ClipboardService.TryReadSafeText(out string? text))
        {
            e.Text = text;
        }
    }

    private void OnCommandHubQrImageActionRequested(
        object? sender,
        Views.QrImageActionRequestedEventArgs e)
    {
        if (_currentState != NotchState.CommandHub || e.PngBytes.Length == 0)
            return;

        try
        {
            if (e.SaveToFile)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "QR kodunu kaydet",
                    Filter = "PNG görseli (*.png)|*.png",
                    DefaultExt = ".png",
                    AddExtension = true,
                    FileName = $"WinNotch-QR-{DateTime.Now:yyyyMMdd-HHmmss}.png"
                };
                if (dialog.ShowDialog(this) != true)
                    return;
                File.WriteAllBytes(dialog.FileName, e.PngBytes);
            }
            else
            {
                BitmapSource image = DecodePng(e.PngBytes);
                System.Windows.Clipboard.SetImage(image);
                _clipboardService?.SuppressNextImageNotification();
            }

            e.Succeeded = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] Output failed: {ex.Message}");
        }
    }

    private void OnCommandHubPreferredSizeChanged(
        object? sender,
        Views.CommandHubSizeChangedEventArgs e)
    {
        if (_currentState == NotchState.CommandHub)
            ApplyDimensions(NotchState.CommandHub);
    }

    private static BitmapSource DecodePng(byte[] pngBytes)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(pngBytes, writable: false);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
