// WinNotch.UI/Views/ClipboardToastView.xaml.cs

using System.Windows;
using WinNotch.Common;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView : UserControl
{
    public ClipboardToastView()
    {
        InitializeComponent();
    }

    public void SetNotification(
        ClipboardNotification notification,
        ClipboardContentType contentType = ClipboardContentType.Unknown)
    {
        Dispatcher.Invoke(() =>
        {
            (StatusIcon.Text, DetailText.Text) = contentType switch
            {
                ClipboardContentType.Url => ("↗", "Bağlantı panoya alındı"),
                ClipboardContentType.FilePath => ("F", "Dosya yolu panoya alındı"),
                ClipboardContentType.Color => ("#", "Renk değeri panoya alındı"),
                ClipboardContentType.Email => ("@", "E-posta panoya alındı"),
                ClipboardContentType.Phone => ("☎", "Telefon panoya alındı"),
                ClipboardContentType.Text => ("C", "Metin panoya alındı"),
                _ => ("C", "Pano güncellendi")
            };

            PreviewText.Text = string.IsNullOrWhiteSpace(notification.PreviewText)
                ? "Panoya alındı"
                : notification.PreviewText;
            ImagePreview.Source = null;
            ImagePreviewBorder.Visibility = Visibility.Collapsed;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);
        });
    }

    public void SetImageNotification(ClipboardImageNotification notification)
    {
        Dispatcher.Invoke(() =>
        {
            StatusIcon.Text = "▣";
            PreviewText.Text = "Ekran görüntüsü hazır";
            DetailText.Text = "Panoya alındı";

            ImagePreview.Source = notification.Image;
            ImagePreviewBorder.Visibility = notification.Image != null
                ? Visibility.Visible
                : Visibility.Collapsed;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);
        });
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.Now - timestamp;
        if (elapsed.TotalSeconds < 5) return "şimdi";
        if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds} sn";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} dk";
        return timestamp.ToString("HH:mm");
    }
}
