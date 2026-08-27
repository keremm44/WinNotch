using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.TrayApp;

public partial class AppearancePreviewView : UserControl
{
    private AppearanceSettings _appearance = new();
    private bool _initialized;

    public AppearancePreviewView()
    {
        InitializeComponent();
        Loaded += AppearancePreviewView_Loaded;
    }

    public void ApplyAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);
        if (!IsLoaded)
            return;

        ApplyStructure();
    }

    private void AppearancePreviewView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        ApplyStructure();
    }

    private void PreviewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { IsChecked: true })
            return;
        ApplyStructure();
    }

    private void ApplyStructure()
    {
        DensityProfile density = AppearanceResolver.ResolveDensity(_appearance);
        string mode = PreviewIdleRadio.IsChecked == true
            ? "Idle"
            : PreviewMediaRadio.IsChecked == true
                ? "Media"
                : "Action";

        IdlePreview.Visibility = mode == "Idle" ? Visibility.Visible : Visibility.Collapsed;
        ActionPreview.Visibility = mode == "Action" ? Visibility.Visible : Visibility.Collapsed;
        MediaPreview.Visibility = mode == "Media" ? Visibility.Visible : Visibility.Collapsed;

        // Keep preview geometry aligned with the corresponding runtime states.
        PreviewNotch.Width = Math.Round((mode == "Idle" ? 100 : mode == "Media" ? 336 : 300) * density.SurfaceScale);
        PreviewNotch.Height = Math.Round((mode == "Idle" ? 22 : mode == "Media" ? 64 : 74) * density.ControlScale);

        bool dot = string.Equals(_appearance.IdleStyle, "Dot", StringComparison.OrdinalIgnoreCase);
        bool ambient = string.Equals(_appearance.IdleStyle, "Ambient", StringComparison.OrdinalIgnoreCase);
        PreviewIdleLine.Visibility = dot ? Visibility.Collapsed : Visibility.Visible;
        PreviewIdleDots.Visibility = dot ? Visibility.Visible : Visibility.Collapsed;
        PreviewIdleGlow.Visibility = ambient ? Visibility.Visible : Visibility.Collapsed;

        PreviewIdleLine.SetResourceReference(
            Border.BackgroundProperty,
            ambient ? "Brush.Accent.Primary" : "Brush.Notch.Handle");
    }
}
