using System.Windows.Media.Imaging;

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

        var dialog = new Microsoft.Win32.SaveFileDialog
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

            using var stream = new System.IO.FileStream(
                dialog.FileName,
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.None);
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
