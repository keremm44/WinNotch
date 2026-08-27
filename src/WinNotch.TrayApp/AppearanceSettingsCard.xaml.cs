using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.TrayApp;

public partial class AppearanceSettingsCard : UserControl
{
    private AppearanceSettings _appearance = new();
    private bool _isLoading;

    public event EventHandler<AppearanceSettings>? AppearanceChanged;

    public AppearanceSettingsCard()
    {
        InitializeComponent();
    }

    public void LoadAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);

        _isLoading = true;
        try
        {
            SelectTaggedRadio(_appearance.ThemePreset,
                ThemeObsidianRadio, ThemeAuroraRadio, ThemeMonochromeRadio, ThemePaperRadio);
            SelectTaggedRadio(_appearance.AccentPreset,
                AccentBlueRadio, AccentVioletRadio, AccentCyanRadio,
                AccentAmberRadio, AccentGreenRadio, AccentSystemRadio);
            SelectTaggedRadio(_appearance.DensityMode,
                DensityCompactRadio, DensityComfortableRadio);
            SelectTaggedRadio(_appearance.MotionMode,
                MotionReducedRadio, MotionNormalRadio);
            SelectTaggedRadio(_appearance.IdleStyle,
                IdleLineRadio, IdleDotRadio, IdleAmbientRadio);
            SelectTaggedRadio(_appearance.StateAccentMode,
                StateUnifiedRadio, StateSemanticRadio);
            SelectTaggedRadio(_appearance.PrivacyPreviewMode,
                PrivacyFullRadio, PrivacyMaskedRadio, PrivacyTypeOnlyRadio);

            AppearancePreview.ApplyAppearance(_appearance);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not RadioButton { IsChecked: true } radio)
            return;

        string value = radio.Tag?.ToString() ?? string.Empty;
        switch (radio.GroupName)
        {
            case "ThemePreset":
                _appearance.ThemePreset = value;
                break;
            case "AccentPreset":
                _appearance.AccentPreset = value;
                break;
            case "DensityMode":
                _appearance.DensityMode = value;
                break;
            case "MotionMode":
                _appearance.MotionMode = value;
                break;
            case "IdleStyle":
                _appearance.IdleStyle = value;
                break;
            case "StateAccentMode":
                _appearance.StateAccentMode = value;
                break;
            case "PrivacyPreviewMode":
                _appearance.PrivacyPreviewMode = value;
                break;
            default:
                return;
        }

        AppearanceResolver.NormalizeInPlace(_appearance);
        AppearancePreview.ApplyAppearance(_appearance);
        AppearanceChanged?.Invoke(this, _appearance);
    }

    private static void SelectTaggedRadio(string value, params RadioButton[] radios)
    {
        RadioButton selected = radios.FirstOrDefault(r =>
            string.Equals(r.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            ?? radios[0];
        selected.IsChecked = true;
    }
}
