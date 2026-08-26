// WinNotch.UI/Views/ClipboardToastView.xaml.cs
// WHY: Displays brief clipboard notification with text preview or image thumbnail.
// Auto-dismisses after 1.5 seconds (ClipboardFlashDurationMs).
// When hidden (Collapsed), zero layout/render cost.
//
// PRIVACY: This view only receives notifications that passed the
// privacy filter in ClipboardService (no password manager content).

using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

/// <summary>
/// Interaction logic for ClipboardToastView.xaml.
/// Displays clipboard change notifications with optional image preview.
/// </summary>
public partial class ClipboardToastView : UserControl
{
    public ClipboardToastView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets a text clipboard notification for display.
    /// </summary>
    public void SetNotification(ClipboardNotification notification, ClipboardContentType contentType = ClipboardContentType.Unknown)
    {
        Dispatcher.Invoke(() =>
        {
            // WHY: Use contextual icons based on content classification.
            // Deterministic, cheap prefix/suffix checks — no regex, no AI.
            StatusIcon.Text = contentType switch
            {
                ClipboardContentType.Url => "🔗",
                ClipboardContentType.FilePath => "📁",
                ClipboardContentType.Color => "🎨",
                ClipboardContentType.Email => "📧",
                ClipboardContentType.Phone => "📞",
                _ => "📋"
            };

            PreviewText.Text = notification.PreviewText ?? "Panoya kopyalandı ✓";
            ImagePreviewBorder.Visibility = Visibility.Collapsed;
            PreviewText.Visibility = Visibility.Visible;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);
        });
    }

    /// <summary>
    /// Sets an image clipboard notification for display.
    /// </summary>
    public void SetImageNotification(ClipboardImageNotification notification)
    {
        Dispatcher.Invoke(() =>
        {
            StatusIcon.Text = "🖼️";

            if (notification.Image != null)
            {
                // Show image thumbnail
                ImagePreview.Source = notification.Image;
                ImagePreviewBorder.Visibility = Visibility.Visible;
                PreviewText.Text = "Panoya kopyalandı ✓";
            }
            else
            {
                ImagePreviewBorder.Visibility = Visibility.Collapsed;
                PreviewText.Text = "Panoya kopyalandı ✓";
            }

            PreviewText.Visibility = Visibility.Visible;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);
        });
    }

    /// <summary>
    /// Formats a timestamp as a relative time string.
    /// </summary>
    private static string FormatTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.Now - timestamp;
        if (elapsed.TotalSeconds < 5) return "şimdi";
        if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds}s önce";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}dk önce";
        return timestamp.ToString("HH:mm");
    }
}
