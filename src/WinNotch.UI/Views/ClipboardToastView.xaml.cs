using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using WinNotch.Common;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView : UserControl
{
    private const int HoverGraceMs = 280;

    private ContextAction? _currentAction;
    private BitmapSource? _currentImage;
    private bool _isExpanded;
    private System.Windows.Threading.DispatcherTimer? _collapseGraceTimer;

    public ClipboardToastView()
    {
        InitializeComponent();
        IsVisibleChanged += ClipboardToastView_RevealVisibilityChanged;
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
            StatusSurface.Background = FindBrush("Brush.Accent.Subtle");
            StatusSurface.BorderBrush = FindBrush("Brush.Accent.Border");

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

    private MainWindow? GetHostWindow() => Window.GetWindow(this) as MainWindow;

    private void ClipboardToastView_RevealVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this);
    }

    private void ApplyClipboardAppearance(ClipboardContentType contentType, string? rawText)
    {
        StatusIcon.Visibility = Visibility.Visible;
        ColorSwatch.Visibility = Visibility.Collapsed;
        StatusSurface.Background = FindBrush("Brush.Surface.Soft");
        StatusSurface.BorderBrush = FindBrush("Brush.Border.OnDark");

        switch (contentType)
        {
            case ClipboardContentType.Url:
                StatusIcon.Text = "↗";
                StatusSurface.Background = FindBrush("Brush.Accent.Subtle");
                StatusSurface.BorderBrush = FindBrush("Brush.Accent.Border");
                break;

            case ClipboardContentType.FilePath:
                StatusIcon.Text = "F";
                StatusSurface.Background = FindBrush("Brush.Semantic.SuccessSubtle");
                StatusSurface.BorderBrush = FindBrush("Brush.Semantic.SuccessBorder");
                break;

            case ClipboardContentType.Color:
                StatusIcon.Visibility = Visibility.Collapsed;
                ColorSwatch.Visibility = Visibility.Visible;
                ColorSwatch.Background = TryCreateColorBrush(rawText) ?? FindBrush("Brush.Surface.Soft");
                break;

            case ClipboardContentType.Email:
                StatusIcon.Text = "@";
                StatusSurface.Background = FindBrush("Brush.Semantic.VioletSubtle");
                StatusSurface.BorderBrush = FindBrush("Brush.Semantic.VioletBorder");
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
        CancelCollapseGrace();

        if (_currentAction == null || _isExpanded)
            return;

        _isExpanded = true;
        ActionPanel.Visibility = Visibility.Visible;
        SurfaceMotion.Reveal(ActionPanel, 1.5, 95);
        GetHostWindow()?.SetContextSurfaceExpanded(true);
    }

    private void Surface_MouseLeave(object sender, MouseEventArgs e)
        => ScheduleCollapseGrace();

    private void ScheduleCollapseGrace()
    {
        CancelCollapseGrace();

        _collapseGraceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(HoverGraceMs)
        };

        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (_collapseGraceTimer != null && handler != null)
                _collapseGraceTimer.Tick -= handler;
            _collapseGraceTimer?.Stop();
            _collapseGraceTimer = null;

            if (IsMouseOver || ActionPanel.IsMouseOver || PrimaryActionButton.IsMouseOver)
                return;

            CollapseActions(notify: true);
        };

        _collapseGraceTimer.Tick += handler;
        _collapseGraceTimer.Start();
    }

    private void CancelCollapseGrace()
    {
        _collapseGraceTimer?.Stop();
        _collapseGraceTimer = null;
    }

    private void CollapseActions(bool notify)
    {
        CancelCollapseGrace();

        if (!_isExpanded)
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _isExpanded = false;
        ActionPanel.Visibility = Visibility.Collapsed;

        if (notify)
            GetHostWindow()?.SetContextSurfaceExpanded(false);
    }

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAction == null)
            return;

        CancelCollapseGrace();
        e.Handled = true;
        GetHostWindow()?.ExecuteContextAction(_currentAction, _currentImage);
    }

    private Brush FindBrush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

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

    private static string FormatTimestamp(DateTime timestamp)
    {
        var elapsed = DateTime.Now - timestamp;
        if (elapsed.TotalSeconds < 5) return "şimdi";
        if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds} sn";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} dk";
        return timestamp.ToString("HH:mm");
    }
}
