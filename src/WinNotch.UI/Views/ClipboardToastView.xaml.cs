using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinNotch.Common;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView : UserControl
{
    private static readonly Brush NeutralSurface = CreateBrush("#14FFFFFF");
    private static readonly Brush NeutralBorder = CreateBrush("#20FFFFFF");
    private static readonly Brush UrlSurface = CreateBrush("#182D7DFF");
    private static readonly Brush UrlBorder = CreateBrush("#482D7DFF");
    private static readonly Brush FileSurface = CreateBrush("#1839B980");
    private static readonly Brush FileBorder = CreateBrush("#4439B980");
    private static readonly Brush EmailSurface = CreateBrush("#188C6CFF");
    private static readonly Brush EmailBorder = CreateBrush("#448C6CFF");
    private static readonly Brush ScreenshotSurface = CreateBrush("#183BA9FF");
    private static readonly Brush ScreenshotBorder = CreateBrush("#443BA9FF");

    private ContextAction? _currentAction;
    private BitmapSource? _currentImage;
    private bool _isExpanded;

    public event EventHandler<ContextActionRequestedEventArgs>? ActionRequested;
    public event EventHandler<ContextSurfaceInteractionEventArgs>? InteractionChanged;

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
            CollapseActions(notify: false);
            _currentImage = null;

            string? rawText = notification.RawText ?? notification.PreviewText;
            ApplyClipboardAppearance(contentType, rawText);

            PreviewText.Text = string.IsNullOrWhiteSpace(notification.PreviewText)
                ? "Panoya alındı"
                : notification.PreviewText;

            DetailText.Text = contentType switch
            {
                ClipboardContentType.Url => "Bağlantı panoya alındı",
                ClipboardContentType.FilePath => "Dosya yolu panoya alındı",
                ClipboardContentType.Color => "Renk değeri panoya alındı",
                ClipboardContentType.Email => "E-posta panoya alındı",
                ClipboardContentType.Phone => "Telefon panoya alındı",
                ClipboardContentType.Text => "Metin panoya alındı",
                _ => "Pano güncellendi"
            };

            ImagePreview.Source = null;
            ImagePreviewBorder.Visibility = Visibility.Collapsed;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);

            SetAction(ContextActionResolver.ResolveClipboard(contentType, rawText));
        });
    }

    public void SetImageNotification(ClipboardImageNotification notification)
    {
        Dispatcher.Invoke(() =>
        {
            CollapseActions(notify: false);
            _currentImage = notification.Image;

            StatusIcon.Text = "▣";
            StatusIcon.Visibility = Visibility.Visible;
            ColorSwatch.Visibility = Visibility.Collapsed;
            StatusSurface.Background = ScreenshotSurface;
            StatusSurface.BorderBrush = ScreenshotBorder;

            PreviewText.Text = "Ekran görüntüsü hazır";
            DetailText.Text = "Panoya alındı";
            ImagePreview.Source = notification.Image;
            ImagePreviewBorder.Visibility = notification.Image != null
                ? Visibility.Visible
                : Visibility.Collapsed;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);

            SetAction(notification.Image != null
                ? ContextActionResolver.ResolveScreenshot()
                : null);
        });
    }

    public void ShowActionFeedback(string message)
    {
        Dispatcher.Invoke(() =>
        {
            DetailText.Text = message;
            CollapseActions(notify: true);
        });
    }

    private void ApplyClipboardAppearance(ClipboardContentType contentType, string? rawText)
    {
        StatusIcon.Visibility = Visibility.Visible;
        ColorSwatch.Visibility = Visibility.Collapsed;
        StatusSurface.Background = NeutralSurface;
        StatusSurface.BorderBrush = NeutralBorder;

        switch (contentType)
        {
            case ClipboardContentType.Url:
                StatusIcon.Text = "↗";
                StatusSurface.Background = UrlSurface;
                StatusSurface.BorderBrush = UrlBorder;
                break;

            case ClipboardContentType.FilePath:
                StatusIcon.Text = "F";
                StatusSurface.Background = FileSurface;
                StatusSurface.BorderBrush = FileBorder;
                break;

            case ClipboardContentType.Color:
                StatusIcon.Visibility = Visibility.Collapsed;
                ColorSwatch.Visibility = Visibility.Visible;
                ColorSwatch.Background = TryCreateColorBrush(rawText) ?? NeutralSurface;
                break;

            case ClipboardContentType.Email:
                StatusIcon.Text = "@";
                StatusSurface.Background = EmailSurface;
                StatusSurface.BorderBrush = EmailBorder;
                break;

            case ClipboardContentType.Phone:
                StatusIcon.Text = "☎";
                break;

            case ClipboardContentType.Text:
                StatusIcon.Text = "C";
                break;

            default:
                StatusIcon.Text = "C";
                break;
        }
    }

    private void SetAction(ContextAction? action)
    {
        _currentAction = action;
        if (action == null)
        {
            PrimaryActionButton.Visibility = Visibility.Collapsed;
            ActionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PrimaryActionButton.Visibility = Visibility.Visible;
        PrimaryActionButton.Content = action.Label;
        ActionHintText.Text = action.Kind switch
        {
            ContextActionKind.OpenUrl => "Tarayıcıda aç",
            ContextActionKind.ShowInExplorer => "Dosya konumuna git",
            ContextActionKind.ComposeEmail => "E-posta uygulamasını aç",
            ContextActionKind.SaveScreenshot => "PNG olarak kaydet",
            _ => "Aksiyon hazır"
        };
    }

    private void Surface_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_currentAction == null || _isExpanded)
            return;

        _isExpanded = true;
        ActionPanel.Visibility = Visibility.Visible;
        InteractionChanged?.Invoke(this, new ContextSurfaceInteractionEventArgs
        {
            IsExpanded = true
        });
    }

    private void Surface_MouseLeave(object sender, MouseEventArgs e)
        => CollapseActions(notify: true);

    private void CollapseActions(bool notify)
    {
        if (!_isExpanded)
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _isExpanded = false;
        ActionPanel.Visibility = Visibility.Collapsed;

        if (notify)
        {
            InteractionChanged?.Invoke(this, new ContextSurfaceInteractionEventArgs
            {
                IsExpanded = false
            });
        }
    }

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAction == null)
            return;

        e.Handled = true;
        ActionRequested?.Invoke(this, new ContextActionRequestedEventArgs
        {
            Action = _currentAction,
            Image = _currentImage
        });
    }

    private static Brush? TryCreateColorBrush(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        string hex = rawText.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));

        if (hex.Length == 6)
            hex = "FF" + hex;

        if (hex.Length != 8 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return null;

        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
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

public sealed class ContextSurfaceInteractionEventArgs : EventArgs
{
    public bool IsExpanded { get; init; }
}

public sealed class ContextActionRequestedEventArgs : EventArgs
{
    public required ContextAction Action { get; init; }
    public BitmapSource? Image { get; init; }
}
