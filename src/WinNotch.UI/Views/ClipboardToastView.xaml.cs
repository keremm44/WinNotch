using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using WinNotch.Common;
using WinNotch.Core.Services;

using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Geometry = System.Windows.Media.Geometry;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace WinNotch.UI.Views;

public partial class ClipboardToastView : UserControl
{
    private const int HoverGraceMs = 280;

    private ContextAction? _currentAction;
    private BitmapSource? _currentImage;
    private AppearanceSettings _appearance = new();
    private bool _isExpanded;
    private System.Windows.Threading.DispatcherTimer? _collapseGraceTimer;

    public ClipboardToastView()
    {
        InitializeComponent();
        IsVisibleChanged += ClipboardToastView_RevealVisibilityChanged;
    }

    public void ApplyAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);
        DensityProfile density = AppearanceResolver.ResolveDensity(_appearance);

        PreviewText.FontSize = 10.5 * density.FontScale;
        DetailText.FontSize = 8.5 * density.FontScale;
        TimestampText.FontSize = 8.0 * density.FontScale;
        ActionHintText.FontSize = 9.0 * density.FontScale;
        PrimaryActionButton.FontSize = 9.5 * density.FontScale;

        if (_currentImage != null)
        {
            bool showThumbnail = PrivacyPreviewFormatter.ShouldShowScreenshotThumbnail(_appearance);
            ImagePreview.Source = showThumbnail ? _currentImage : null;
            ImagePreviewBorder.Visibility = showThumbnail ? Visibility.Visible : Visibility.Collapsed;
        }
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

            PreviewText.Text = PrivacyPreviewFormatter.Format(
                contentType,
                rawText,
                notification.PreviewText,
                _appearance);

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

            SetStatusIcon("Icon.Screenshot");
            StatusIconPath.Visibility = Visibility.Visible;
            ColorSwatch.Visibility = Visibility.Collapsed;
            StatusSurface.Background = FindBrush("Brush.State.Screenshot.Subtle");
            StatusSurface.BorderBrush = FindBrush("Brush.State.Screenshot.Border");

            PreviewText.Text = "Ekran görüntüsü hazır";
            DetailText.Text = string.Equals(_appearance.PrivacyPreviewMode, "Full", StringComparison.OrdinalIgnoreCase)
                ? "Panoya alındı"
                : "Önizleme gizlendi";

            bool showThumbnail = notification.Image != null &&
                                 PrivacyPreviewFormatter.ShouldShowScreenshotThumbnail(_appearance);
            ImagePreview.Source = showThumbnail ? notification.Image : null;
            ImagePreviewBorder.Visibility = showThumbnail ? Visibility.Visible : Visibility.Collapsed;
            TimestampText.Text = FormatTimestamp(notification.Timestamp);

            SetAction(notification.Image != null
                ? ContextActionResolver.ResolveScreenshot()
                : null);
        });
    }

    public void ShowActionFeedback(string message, bool succeeded)
    {
        Dispatcher.Invoke(() =>
        {
            PreviewText.Text = message;
            DetailText.Text = succeeded ? "İşlem tamamlandı" : "İşlem tamamlanamadı";
            SetStatusIcon(succeeded ? "Icon.Check" : "Icon.Error");
            StatusIconPath.Visibility = Visibility.Visible;
            ColorSwatch.Visibility = Visibility.Collapsed;
            StatusSurface.Background = FindBrush(
                succeeded ? "Brush.Semantic.SuccessSubtle" : "Brush.Semantic.DangerSubtle");
            StatusSurface.BorderBrush = FindBrush(
                succeeded ? "Brush.Semantic.SuccessBorder" : "Brush.Semantic.DangerBorder");
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
        StatusIconPath.Visibility = Visibility.Visible;
        ColorSwatch.Visibility = Visibility.Collapsed;
        StatusSurface.Background = FindBrush("Brush.Surface.Soft");
        StatusSurface.BorderBrush = FindBrush("Brush.Border.OnDark");
        SetStatusIcon("Icon.Copy");

        switch (contentType)
        {
            case ClipboardContentType.Url:
                SetStatusIcon("Icon.Link");
                StatusSurface.Background = FindBrush("Brush.State.Clipboard.Subtle");
                StatusSurface.BorderBrush = FindBrush("Brush.State.Clipboard.Border");
                break;

            case ClipboardContentType.FilePath:
                SetStatusIcon("Icon.File");
                StatusSurface.Background = FindBrush("Brush.State.File.Subtle");
                StatusSurface.BorderBrush = FindBrush("Brush.State.File.Border");
                break;

            case ClipboardContentType.Color:
                StatusIconPath.Visibility = Visibility.Collapsed;
                ColorSwatch.Visibility = Visibility.Visible;
                ColorSwatch.Background = TryCreateColorBrush(rawText) ?? FindBrush("Brush.Surface.Soft");
                break;

            case ClipboardContentType.Email:
                SetStatusIcon("Icon.Mail");
                StatusSurface.Background = FindBrush("Brush.State.Clipboard.Subtle");
                StatusSurface.BorderBrush = FindBrush("Brush.State.Clipboard.Border");
                break;

            case ClipboardContentType.Phone:
            case ClipboardContentType.Text:
            default:
                SetStatusIcon("Icon.Copy");
                break;
        }
    }

    private void SetStatusIcon(string resourceKey)
        => StatusIconPath.Data = TryFindResource(resourceKey) as Geometry;

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
