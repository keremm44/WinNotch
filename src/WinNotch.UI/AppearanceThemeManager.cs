using System.Windows;
using System.Windows.Media;
using WinNotch.Common;

using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSystemColors = System.Windows.SystemColors;

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
        SetBrush(resources, "Brush.Notch.AmbientSurface", palette.SurfaceSoft);
        SetBrush(resources, "Brush.Notch.AmbientGlow", palette.AccentSubtle);
        SetBrush(resources, "Brush.Notch.AmbientIcon", palette.TextOnDarkPrimary);
        SetBrush(resources, "Brush.Notch.AmbientText", palette.TextOnDarkSecondary);
        SetBrush(resources, "Brush.Notch.Highlight", AppearanceResolver.ResolveNotchHighlightColor(settings, palette));
        SetBrush(resources, "Brush.Notch.InnerEdge", AppearanceResolver.ResolveNotchInnerEdgeColor(settings, palette));
        SetBrush(resources, "Brush.Notch.BottomShade",
            palette.IsLightTheme ? WithAlpha(palette.TextPrimary, 0x12) : "#52000000");

        SetBrush(resources, "Brush.Window.Base", palette.WindowBase);
        SetBrush(resources, "Brush.Window.BackdropTint", WithAlpha(palette.WindowBase, palette.IsLightTheme ? (byte)0xE2 : (byte)0xEA));
        SetBrush(resources, "Brush.Window.Header", palette.WindowHeader);
        SetBrush(resources, "Brush.Surface.Raised", palette.SurfaceRaised);
        SetBrush(resources, "Brush.Surface.Popup", palette.SurfacePopup);
        SetBrush(resources, "Brush.Surface.Control", palette.SurfaceControl);
        SetBrush(resources, "Brush.Surface.Soft", palette.SurfaceSoft);
        SetBrush(resources, "Brush.Surface.Softer", palette.SurfaceSofter);
        SetBrush(resources, "Brush.Surface.Hover", palette.SurfaceHover);
        SetBrush(resources, "Brush.Surface.Pressed", palette.SurfacePressed);
        SetBrush(resources, "Brush.Surface.Highlight", palette.IsLightTheme ? "#0E000000" : "#16FFFFFF");

        SetBrush(resources, "Brush.Border.Subtle", palette.BorderSubtle);
        SetBrush(resources, "Brush.Border.Strong", palette.BorderStrong);
        SetBrush(resources, "Brush.Border.OnDark", palette.BorderOnDark);
        SetBrush(resources, "Brush.Border.OnDarkStrong", palette.BorderOnDarkStrong);
        SetBrush(resources, "Brush.Border.LightTheme", palette.IsLightTheme ? "#26000000" : "#364A4A50");
        SetBrush(resources, "Brush.Border.Premium", palette.IsLightTheme ? "#24000000" : "#28FFFFFF");
        SetBrush(resources, "Brush.Focus", palette.IsLightTheme ? "#FF005FB8" : "#FF7AA7FF");
        SetBrush(resources, "Brush.Focus.Inner", palette.WindowBase);
        SetBrush(resources, "Brush.Shadow", palette.IsLightTheme ? "#38000000" : "#78000000");

        SetBrush(resources, "Brush.Text.Primary", palette.TextPrimary);
        SetBrush(resources, "Brush.Text.Secondary", palette.TextSecondary);
        SetBrush(resources, "Brush.Text.Muted", palette.TextMuted);
        SetBrush(resources, "Brush.Text.Faint", palette.TextFaint);
        SetBrush(resources, "Brush.Text.OnDarkPrimary", palette.TextOnDarkPrimary);
        SetBrush(resources, "Brush.Text.OnDarkSecondary", palette.TextOnDarkSecondary);
        SetBrush(resources, "Brush.Text.OnDarkMuted", palette.TextOnDarkMuted);

        SetBrush(resources, "Brush.Accent.Primary", palette.AccentPrimary);
        // The System choice in the selector must retain the real Windows color while
        // Primary follows whichever preset is currently selected.
        string resolvedSystemAccent = systemAccent ?? "#FF0078D4";
        SetBrush(resources, "Brush.Accent.System", resolvedSystemAccent);
        SetBrush(resources, "Brush.Accent.SystemForeground", ContrastForeground(resolvedSystemAccent));
        SetBrush(resources, "Brush.Accent.Foreground", ContrastForeground(palette.AccentPrimary));
        SetBrush(resources, "Brush.Accent.Hover", palette.AccentHover);
        SetBrush(resources, "Brush.Accent.Pressed", palette.AccentPressed);
        SetBrush(resources, "Brush.Accent.Selection", palette.AccentSelection);
        SetBrush(resources, "Brush.Accent.Subtle", palette.AccentSubtle);
        SetBrush(resources, "Brush.Accent.Border", palette.AccentBorder);
        SetBrush(resources, "Brush.Accent.Glow", WithAlpha(palette.AccentPrimary, 0x28));

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
        resources["Metric.Settings.TitleFontSize"] = comfortable ? 19d : 18d;
        resources["Metric.Settings.SectionFontSize"] = comfortable ? 14.5d : 14d;
        resources["Metric.Settings.BodyFontSize"] = comfortable ? 12.5d : 12d;
        resources["Metric.Settings.RowFontSize"] = comfortable ? 13.5d : 13d;
        resources["Metric.Settings.CaptionFontSize"] = comfortable ? 11.5d : 11d;
        resources["Metric.Settings.ControlHeight"] = comfortable ? 40d : 36d;

        // Legacy aliases stay synchronized until every remaining view is tokenized.
        SetBrush(resources, "BackgroundDark", palette.WindowBase);
        SetBrush(resources, "ForegroundLight", palette.TextPrimary);
        SetBrush(resources, "AccentBlue", palette.AccentPrimary);
        SetBrush(resources, "TextSecondary", palette.TextMuted);

        if (SystemParameters.HighContrast)
            ApplyHighContrast(resources);

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

    private static void ApplyHighContrast(ResourceDictionary resources)
    {
        string window = WpfSystemColors.WindowColor.ToString();
        string text = WpfSystemColors.WindowTextColor.ToString();
        string control = WpfSystemColors.ControlColor.ToString();
        string highlight = WpfSystemColors.HighlightColor.ToString();
        string highlightText = WpfSystemColors.HighlightTextColor.ToString();
        string gray = WpfSystemColors.GrayTextColor.ToString();

        foreach (string key in new[] { "Brush.Notch.Base", "Brush.Window.Base", "Brush.Window.BackdropTint", "Brush.Window.Header" })
            SetBrush(resources, key, window);
        foreach (string key in new[] { "Brush.Surface.Raised", "Brush.Surface.Popup", "Brush.Surface.Control" })
            SetBrush(resources, key, control);
        foreach (string key in new[] { "Brush.Surface.Soft", "Brush.Surface.Softer", "Brush.Surface.Hover", "Brush.Surface.Pressed" })
            SetBrush(resources, key, window);
        foreach (string key in new[] { "Brush.Text.Primary", "Brush.Text.Secondary", "Brush.Text.OnDarkPrimary", "Brush.Text.OnDarkSecondary" })
            SetBrush(resources, key, text);
        foreach (string key in new[] { "Brush.Text.Muted", "Brush.Text.Faint", "Brush.Text.OnDarkMuted" })
            SetBrush(resources, key, gray);
        foreach (string key in new[] { "Brush.Border.Subtle", "Brush.Border.Strong", "Brush.Border.Premium", "Brush.Border.OnDark", "Brush.Border.OnDarkStrong", "Brush.Notch.Edge", "Brush.Notch.InnerEdge" })
            SetBrush(resources, key, text);
        foreach (string key in new[] { "Brush.Accent.Primary", "Brush.Accent.Hover", "Brush.Accent.Pressed", "Brush.Accent.Border", "Brush.Focus" })
            SetBrush(resources, key, highlight);
        SetBrush(resources, "Brush.Accent.Foreground", highlightText);
        // Selected controls retain readable WindowText; focus/border carries Highlight.
        SetBrush(resources, "Brush.Accent.Selection", window);
        SetBrush(resources, "Brush.Accent.Subtle", window);
        SetBrush(resources, "Brush.Focus.Inner", window);
        SetBrush(resources, "Brush.Notch.Handle", highlight);
        SetBrush(resources, "Brush.Notch.AmbientSurface", window);
        SetBrush(resources, "Brush.Notch.AmbientGlow", window);
        SetBrush(resources, "Brush.Notch.AmbientIcon", text);
        SetBrush(resources, "Brush.Notch.AmbientText", text);
        SetBrush(resources, "Brush.Border.LightTheme", text);
        SetBrush(resources, "Brush.Accent.System", highlight);
        SetBrush(resources, "Brush.Accent.SystemForeground", highlightText);
        SetBrush(resources, "Brush.Notch.Highlight", "#00FFFFFF");
        SetBrush(resources, "Brush.Notch.BottomShade", "#00000000");
        SetBrush(resources, "Brush.Surface.Highlight", "#00FFFFFF");
        SetBrush(resources, "Brush.Accent.Glow", "#00FFFFFF");
        SetBrush(resources, "Brush.Shadow", "#00000000");

        foreach (string semantic in new[] { "Success", "Danger" })
        {
            SetBrush(resources, $"Brush.Semantic.{semantic}", highlight);
            SetBrush(resources, $"Brush.Semantic.{semantic}Subtle", window);
            SetBrush(resources, $"Brush.Semantic.{semantic}Border", text);
        }
        SetBrush(resources, "Brush.Semantic.Warning", highlight);

        foreach (string state in new[] { "File", "Clipboard", "Screenshot", "Media" })
        {
            SetBrush(resources, $"Brush.State.{state}", highlight);
            SetBrush(resources, $"Brush.State.{state}.Subtle", window);
            SetBrush(resources, $"Brush.State.{state}.Border", text);
        }

        SetBrush(resources, "BackgroundDark", window);
        SetBrush(resources, "ForegroundLight", text);
        SetBrush(resources, "AccentBlue", highlight);
        SetBrush(resources, "TextSecondary", gray);
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        WpfColor color = (WpfColor)WpfColorConverter.ConvertFromString(hex);
        if (resources[key] is SolidColorBrush existing && existing.Color == color)
            return;

        var brush = new SolidColorBrush(color);
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
