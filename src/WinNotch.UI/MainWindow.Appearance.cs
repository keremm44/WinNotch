using System.Windows;
using System.Windows.Controls;
using WinNotch.Common;

namespace WinNotch.UI;

public partial class MainWindow
{
    private DensityProfile _appearanceDensity = new(1.0, 1.0, 1.0, 1.0);
    private bool _appearanceLoadedHooked;

    public void ApplyAppearanceSettings()
    {
        _settings.Appearance ??= new AppearanceSettings();
        AppearanceResolver.NormalizeInPlace(_settings.Appearance);

        AppearancePalette palette = AppearanceThemeManager.Apply(_settings.Appearance);
        _appearanceDensity = AppearanceResolver.ResolveDensity(_settings.Appearance);
        SurfaceMotion.Configure(_settings.Appearance);
        _motionController.Configure(_settings.Appearance);

        ApplyIdleSignature();
        QuickPeekView.ApplyAppearance(_settings.Appearance);
        DropZoneView.ApplyAppearance(_settings.Appearance);
        ClipboardToastView.ApplyAppearance(_settings.Appearance);
        MediaWidgetView.ApplyAppearance(_settings.Appearance);
        RefreshAppearanceThemeBorder(palette.IsLightTheme);

        if (!_appearanceLoadedHooked)
        {
            Loaded += MainWindow_AppearanceLoaded;
            _appearanceLoadedHooked = true;
        }

        if (_initialized)
            ApplyDimensions(_currentState, force: true);
    }

    internal (double Width, double Height) ResolveAppearanceDimensions(NotchState state)
    {
        (double width, double height) = StateDimensions.GetDimensions(state);
        if (_appearanceDensity.SurfaceScale <= 1.01)
            return (width, height);

        double widthScale = state is NotchState.Idle or NotchState.Hover or NotchState.MediaAmbient
            ? 1.04
            : _appearanceDensity.SurfaceScale;
        double heightScale = state is NotchState.Idle or NotchState.Hover or NotchState.MediaAmbient
            ? 1.06
            : _appearanceDensity.ControlScale;

        return (
            Math.Round(width * widthScale),
            Math.Round(height * heightScale));
    }

    internal (double Width, double Height) ResolveAppearanceContextDimensions(double width, double height)
    {
        if (_appearanceDensity.SurfaceScale <= 1.01)
            return (width, height);

        return (
            Math.Round(width * _appearanceDensity.SurfaceScale),
            Math.Round(height * _appearanceDensity.ControlScale));
    }

    private void MainWindow_AppearanceLoaded(object sender, RoutedEventArgs e)
    {
        // Runs after the legacy Windows-theme border detection in MainWindow_Loaded,
        // so an explicit Paper preset always wins over the OS dark/light preference.
        AppearancePalette palette = AppearanceResolver.ResolvePalette(_settings.Appearance);
        RefreshAppearanceThemeBorder(palette.IsLightTheme);
        StartRuntimeReliabilityChecks();
    }

    private void ApplyIdleSignature()
    {
        string idle = _settings.Appearance.IdleStyle;
        bool dot = string.Equals(idle, "Dot", StringComparison.OrdinalIgnoreCase);
        bool ambient = string.Equals(idle, "Ambient", StringComparison.OrdinalIgnoreCase);

        IdleLine.Visibility = dot ? Visibility.Collapsed : Visibility.Visible;
        IdleDots.Visibility = dot ? Visibility.Visible : Visibility.Collapsed;
        IdleAmbientGlow.Visibility = ambient ? Visibility.Visible : Visibility.Collapsed;

        IdleLine.SetResourceReference(
            Border.BackgroundProperty,
            ambient ? "Brush.Accent.Primary" : "Brush.Notch.Handle");
    }

    private void RefreshAppearanceThemeBorder(bool explicitLightTheme)
    {
        bool showBorder = explicitLightTheme || IsWindowsLightTheme();
        ThemeBorder.Visibility = showBorder ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }
}
