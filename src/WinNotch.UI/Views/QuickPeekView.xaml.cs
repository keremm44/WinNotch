using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Common;

using UserControl = System.Windows.Controls.UserControl;

namespace WinNotch.UI.Views;

public partial class QuickPeekView : UserControl
{
    private AppearanceSettings _appearance = new();
    private LastMeaningfulClipboardContext? _context;

    public event EventHandler? ContextRequested;

    public QuickPeekView()
    {
        InitializeComponent();
        IsVisibleChanged += QuickPeekView_IsVisibleChanged;
    }

    public void ApplyAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);

        DensityProfile density = AppearanceResolver.ResolveDensity(_appearance);
        ContextPreviewText.FontSize = 10.5 * density.FontScale;
        ContextKindText.FontSize = 8.5 * density.FontScale;
        RenderContext();
    }

    public void SetContext(LastMeaningfulClipboardContext? context)
    {
        _context = context;
        RenderContext();
    }

    private void RenderContext()
    {
        bool hasContext = _context != null;
        ContextButton.Visibility = hasContext ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = hasContext ? Visibility.Collapsed : Visibility.Visible;

        if (_context == null)
            return;

        ContextPreviewText.Text = PrivacyPreviewFormatter.Format(
            _context.ContentType,
            _context.RawText,
            _context.PreviewText,
            _appearance);

        ContextKindText.Text = _context.ContentType switch
        {
            ClipboardContentType.Url => "Son bağlantı · detayı göster",
            ClipboardContentType.FilePath => "Son dosya yolu · detayı göster",
            ClipboardContentType.Email => "Son e-posta · detayı göster",
            _ => "Son pano · detayı göster"
        };

        string iconKey;
        string stateKey;
        switch (_context.ContentType)
        {
            case ClipboardContentType.FilePath:
                iconKey = "Icon.File";
                stateKey = "File";
                break;
            case ClipboardContentType.Email:
                iconKey = "Icon.Mail";
                stateKey = "Clipboard";
                break;
            default:
                iconKey = "Icon.Link";
                stateKey = "Clipboard";
                break;
        }

        ContextIconPath.Data = TryFindResource(iconKey) as Geometry;
        ContextIconPath.SetResourceReference(
            System.Windows.Shapes.Shape.StrokeProperty,
            $"Brush.State.{stateKey}");
        ContextStatusSurface.SetResourceReference(
            Border.BackgroundProperty,
            $"Brush.State.{stateKey}.Subtle");
        ContextStatusSurface.SetResourceReference(
            Border.BorderBrushProperty,
            $"Brush.State.{stateKey}.Border");
    }

    private void ContextButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ContextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuickPeekView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            SurfaceMotion.Reveal(this, 1.5, 100);
    }
}
