using System.Windows;
using System.Windows.Media;
using WinNotch.Common;

using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace WinNotch.UI;

/// <summary>
/// Bridges the pure Common appearance resolver to WPF DynamicResource values.
/// One application-level override dictionary keeps MainWindow, settings, tray menus
/// and child views visually synchronized without per-view theme branching.
/// </summary>
public static class AppearanceThemeManager
{
    public static AppearancePalette Apply(AppearanceSettings settings)
    {
        ResourceDictionary resources = WpfApplication.Current?.Resources
            ?? throw new InvalidOperationException("Application resources are not available.");
        return Apply(resources, settings);
    }

    public static AppearancePalette Apply(ResourceDictionary resources, AppearanceSettings settings)
    {
        string? systemAccent = TryGetSystemAccentHex();
        AppearancePalette palette = AppearanceResolver.ResolvePalette(settings, systemAccent);
        DensityProfile density = AppearanceResolver.ResolveDensity(settings);

        SetBrush(resources, "Brush.Notch.Base", palette.NotchBase);
        SetBrush(resources, "Brush.Notch.Handle", AppearanceResolver.ResolveNotchHandleColor(settings, palette));
        SetBrush(resources, "Brush.Notch.Edge", AppearanceResolver.ResolveNotchEdgeColor(settings, palette));
        SetBrush(resources, "Brush.Notch.AmbientSurface", palette.IsLightTheme ? "#10000000" : "#18FFFFFF");
        SetBrush(resources, "Brush.Notch.AmbientGlow", palette.AccentSubtle);
        SetBrush(resources, "Brush.Notch.AmbientIcon", palette.IsLightTheme ? "#CC17181B" : "#D8FFFFFF");
        SetBrush(resources, "Brush.Notch.AmbientText", palette.IsLightTheme ? "#B817181B" : "#C8FFFFFF");

        SetBrush(resources, "Brush.Window.Base", palette.WindowBase);
        SetBrush(resources, "Brush.Window.Header", palette.WindowHeader);
        SetBrush(resources, "Brush.Surface.Raised", palette.SurfaceRaised);
        SetBrush(resources, "Brush.Surface.Popup", palette.SurfacePopup);
        SetBrush(resources, "Brush.Surface.Control", palette.SurfaceControl);
        SetBrush(resources, "Brush.Surface.Soft", palette.SurfaceSoft);
        SetBrush(resources, "Brush.Surface.Softer", palette.SurfaceSofter);
        SetBrush(resources, "Brush.Surface.Hover", palette.SurfaceHover);
        SetBrush(resources, "Brush.Surface.Pressed", palette.SurfacePressed);

        SetBrush(resources, "Brush.Border.Subtle", palette.BorderSubtle);
        SetBrush(resources, "Brush.Border.Strong", palette.BorderStrong);
        SetBrush(resources, "Brush.Border.OnDark", palette.BorderOnDark);
        SetBrush(resources, "Brush.Border.OnDarkStrong", palette.BorderOnDarkStrong);
        SetBrush(resources, "Brush.Border.LightTheme", palette.IsLightTheme ? "#26000000" : "#364A4A50");

        SetBrush(resources, "Brush.Text.Primary", palette.TextPrimary);
        SetBrush(resources, "Brush.Text.Secondary", palette.TextSecondary);
        SetBrush(resources, "Brush.Text.Muted", palette.TextMuted);
        SetBrush(resources, "Brush.Text.Faint", palette.TextFaint);
        SetBrush(resources, "Brush.Text.OnDarkPrimary", palette.TextOnDarkPrimary);
        SetBrush(resources, "Brush.Text.OnDarkSecondary", palette.TextOnDarkSecondary);
        SetBrush(resources, "Brush.Text.OnDarkMuted", palette.TextOnDarkMuted);

        SetBrush(resources, "Brush.Accent.Primary", palette.AccentPrimary);
        SetBrush(resources, "Brush.Accent.Foreground", ContrastForeground(palette.AccentPrimary));
        SetBrush(resources, "Brush.Accent.Hover", palette.AccentHover);
        SetBrush(resources, "Brush.Accent.Pressed", palette.AccentPressed);
        SetBrush(resources, "Brush.Accent.Selection", palette.AccentSelection);
        SetBrush(resources, "Brush.Accent.Subtle", palette.AccentSubtle);
        SetBrush(resources, "Brush.Accent.Border", palette.AccentBorder);

        SetBrush(resources, "Brush.Semantic.Success", palette.Success);
        SetBrush(resources, "Brush.Semantic.SuccessSubtle", WithAlpha(palette.Success, 0x18));
        SetBrush(resources, "Brush.Semantic.SuccessBorder", WithAlpha(palette.Success, 0x44));
        SetBrush(resources, "Brush.Semantic.Warning", palette.Warning);
        SetBrush(resources, "Brush.Semantic.Danger", palette.Danger);
        SetBrush(resources, "Brush.Semantic.DangerSubtle", WithAlpha(palette.Danger, 0x18));
        SetBrush(resources, "Brush.Semantic.DangerBorder", WithAlpha(palette.Danger, 0x44));

        bool semantic = string.Equals(settings.StateAccentMode, "Semantic", StringComparison.OrdinalIgnoreCase);
        SetStateBrushes(resources, "File", semantic ? palette.StateFile : palette.AccentPrimary);
        SetStateBrushes(resources, "Clipboard", semantic ? palette.StateClipboard : palette.AccentPrimary);
        SetStateBrushes(resources, "Screenshot", semantic ? palette.StateScreenshot : palette.AccentPrimary);
        SetStateBrushes(resources, "Media", semantic ? palette.StateMedia : palette.AccentPrimary);

        bool comfortable = density.SurfaceScale > 1.01;
        resources["Thickness.CardPadding"] = comfortable ? new Thickness(18) : new Thickness(16);
        resources["Thickness.CompactPadding"] = comfortable ? new Thickness(10, 5, 10, 5) : new Thickness(9, 4, 9, 4);
        resources["Thickness.ViewPadding"] = comfortable ? new Thickness(12, 8, 12, 8) : new Thickness(10, 6, 10, 6);
        resources["Metric.ControlHeight"] = comfortable ? 34d : 30d;
        resources["Metric.PrimaryFontSize"] = comfortable ? 12.25d : 11.5d;
        resources["Metric.SecondaryFontSize"] = comfortable ? 10.25d : 9.5d;

        // Legacy aliases stay synchronized until every remaining view is tokenized.
        SetBrush(resources, "BackgroundDark", palette.WindowBase);
        SetBrush(resources, "ForegroundLight", palette.TextPrimary);
        SetBrush(resources, "AccentBlue", palette.AccentPrimary);
        SetBrush(resources, "TextSecondary", palette.TextMuted);

        return palette;
    }

    public static bool IsLightTheme(AppearanceSettings settings)
        => AppearanceResolver.ResolvePalette(settings, TryGetSystemAccentHex()).IsLightTheme;

    private static void SetStateBrushes(ResourceDictionary resources, string state, string color)
    {
        SetBrush(resources, $"Brush.State.{state}", color);
        SetBrush(resources, $"Brush.State.{state}.Subtle", WithAlpha(color, 0x18));
        SetBrush(resources, $"Brush.State.{state}.Border", WithAlpha(color, 0x48));
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex));
        brush.Freeze();
        resources[key] = brush;
    }

    private static string? TryGetSystemAccentHex()
    {
        try
        {
            WpfColor color = SystemParameters.WindowGlassColor;
            return $"#FF{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return null;
        }
    }

    private static string ContrastForeground(string argb)
    {
        WpfColor color = (WpfColor)WpfColorConverter.ConvertFromString(argb);
        // WCAG-style relative luminance is unnecessary for two fixed foregrounds;
        // this weighted luma split keeps cyan/amber/green readable without changing accents.
        double luma = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luma >= 150 ? "#FF101114" : "#FFFFFFFF";
    }

    private static string WithAlpha(string argb, byte alpha)
    {
        string hex = argb.Trim().TrimStart('#');
        if (hex.Length == 6)
            return $"#{alpha:X2}{hex.ToUpperInvariant()}";
        if (hex.Length == 8)
            return $"#{alpha:X2}{hex[2..].ToUpperInvariant()}";
        return $"#{alpha:X2}2D7DFF";
    }
}
