using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WinNotch.UI;

internal enum ScreenshotSaveResult
{
    Saved,
    Cancelled,
    Failed
}

internal static class ScreenshotSaveService
{
    public static ScreenshotSaveResult TrySave(BitmapSource? image, out string? error)
    {
        error = null;
        if (image == null)
        {
            error = "Görüntü bulunamadı";
            return ScreenshotSaveResult.Failed;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Ekran görüntüsünü kaydet",
            Filter = "PNG görüntüsü (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png"
        };

        if (dialog.ShowDialog() != true)
            return ScreenshotSaveResult.Cancelled;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new FileStream(
                dialog.FileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            encoder.Save(stream);
            return ScreenshotSaveResult.Saved;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenshotSave] Save failed: {ex.Message}");
            error = "Kaydetme başarısız";
            return ScreenshotSaveResult.Failed;
        }
    }
}
