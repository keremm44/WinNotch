using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Common;

namespace WinNotch.TrayApp;

public partial class AppearanceSettingsCard : UserControl
{
    private AppearanceSettings _appearance = new();
    private bool _isLoading;
    private RadioButton _themeGraphiteRadio = null!;
    private RadioButton _themeFrostRadio = null!;

    public event EventHandler<AppearanceSettings>? AppearanceChanged;

    public AppearanceSettingsCard()
    {
        InitializeComponent();
        BuildThemeChooser();
    }

    public void LoadAppearance(AppearanceSettings settings)
    {
        _appearance = settings ?? new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_appearance);

        _isLoading = true;
        try
        {
            SelectTaggedRadio(_appearance.ThemePreset,
                ThemeObsidianRadio, ThemeAuroraRadio, _themeGraphiteRadio,
                ThemeMonochromeRadio, ThemePaperRadio, _themeFrostRadio);
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

    private void BuildThemeChooser()
    {
        if (ThemeObsidianRadio.Parent is not Grid grid)
            return;

        grid.ColumnDefinitions.Clear();
        for (int i = 0; i < 3; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.RowDefinitions.Clear();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        ConfigureThemeRadio(ThemeObsidianRadio, "Obsidyen", "#FF0B0B0D", row: 0, column: 0);
        ConfigureThemeRadio(ThemeAuroraRadio, "Aurora", "#FF0A1020", row: 0, column: 1);
        ConfigureThemeRadio(ThemeMonochromeRadio, "Tek renk", "#FF050505", row: 1, column: 0);
        ConfigureThemeRadio(ThemePaperRadio, "Açık", "#FFF8F8FA", row: 1, column: 1);

        _themeGraphiteRadio = CreateThemeRadio("Graphite", "Grafit", "#FF101215");
        Grid.SetRow(_themeGraphiteRadio, 0);
        Grid.SetColumn(_themeGraphiteRadio, 2);
        grid.Children.Add(_themeGraphiteRadio);

        _themeFrostRadio = CreateThemeRadio("Frost", "Frost", "#FFF2F6FB");
        Grid.SetRow(_themeFrostRadio, 1);
        Grid.SetColumn(_themeFrostRadio, 2);
        grid.Children.Add(_themeFrostRadio);
    }

    private RadioButton CreateThemeRadio(string tag, string label, string shellHex)
    {
        var radio = new RadioButton
        {
            GroupName = "ThemePreset",
            Tag = tag,
            Style = (Style)FindResource("ChoiceRadio")
        };
        radio.Checked += AppearanceRadio_Checked;
        AutomationProperties.SetName(radio, $"{label} teması");
        radio.Content = CreateThemeLabel(label, shellHex);
        return radio;
    }

    private static void ConfigureThemeRadio(
        RadioButton radio,
        string label,
        string shellHex,
        int row,
        int column)
    {
        Grid.SetRow(radio, row);
        Grid.SetColumn(radio, column);
        AutomationProperties.SetName(radio, $"{label} teması");
        radio.Content = CreateThemeLabel(label, shellHex);
    }

    private static StackPanel CreateThemeLabel(string label, string shellHex)
    {
        var swatch = new Border
        {
            Width = 11,
            Height = 11,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 6, 0),
            Background = (Brush)new BrushConverter().ConvertFromString(shellHex)!
        };
        swatch.SetResourceReference(Border.BorderBrushProperty, "Brush.Border.Strong");

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(swatch);
        content.Children.Add(text);
        return content;
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
                // Do not leave exclusivity to a visual template/resource refresh.
                // Explicitly clear every previous selection before the global theme
                // resources are replaced by the live-preview update.
                SelectTaggedRadio(value,
                    AccentBlueRadio, AccentVioletRadio, AccentCyanRadio,
                    AccentAmberRadio, AccentGreenRadio, AccentSystemRadio);
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

        // Setting only the new radio to true normally relies on WPF GroupName to
        // clear its peer. Be explicit so reloads and resource-driven re-templating
        // cannot preserve a stale checked visual on an old container.
        foreach (RadioButton radio in radios)
            radio.IsChecked = ReferenceEquals(radio, selected);
    }
}
