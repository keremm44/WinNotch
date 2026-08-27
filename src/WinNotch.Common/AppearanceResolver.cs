using System.Globalization;

namespace WinNotch.Common;

public sealed record AppearancePalette(
    string NotchBase,
    string WindowBase,
    string WindowHeader,
    string SurfaceRaised,
    string SurfacePopup,
    string SurfaceControl,
    string SurfaceSoft,
    string SurfaceSofter,
    string SurfaceHover,
    string SurfacePressed,
    string BorderSubtle,
    string BorderStrong,
    string BorderOnDark,
    string BorderOnDarkStrong,
    string TextPrimary,
    string TextSecondary,
    string TextMuted,
    string TextFaint,
    string TextOnDarkPrimary,
    string TextOnDarkSecondary,
    string TextOnDarkMuted,
    string AccentPrimary,
    string AccentHover,
    string AccentPressed,
    string AccentSelection,
    string AccentSubtle,
    string AccentBorder,
    string Success,
    string Warning,
    string Danger,
    string StateFile,
    string StateClipboard,
    string StateScreenshot,
    string StateMedia,
    bool IsLightTheme);

public sealed record DensityProfile(
    double SurfaceScale,
    double ContentScale,
    double FontScale,
    double ControlScale);

public sealed record MotionProfile(
    double ContainerDurationScale,
    int ContentDurationMs,
    double ContentOffsetY);

/// <summary>
/// Pure resolver for personalization presets. It intentionally has no WPF dependency,
/// so invalid settings can be normalized and regression-tested in WinNotch.Common.
/// </summary>
public static class AppearanceResolver
{
    private static readonly string[] Themes = ["Obsidian", "Aurora", "Monochrome", "Paper"];
    private static readonly string[] Accents = ["Blue", "Violet", "Cyan", "Amber", "Green", "System"];
    private static readonly string[] Densities = ["Compact", "Comfortable"];
    private static readonly string[] Motions = ["Reduced", "Normal"];
    private static readonly string[] IdleStyles = ["Line", "Dot", "Ambient"];
    private static readonly string[] StateAccents = ["Unified", "Semantic"];
    private static readonly string[] PrivacyModes = ["Full", "Masked", "TypeOnly"];

    public static void NormalizeInPlace(AppearanceSettings? settings)
    {
        if (settings == null) return;
        settings.ThemePreset = Normalize(settings.ThemePreset, Themes, "Obsidian");
        settings.AccentPreset = Normalize(settings.AccentPreset, Accents, "Blue");
        settings.DensityMode = Normalize(settings.DensityMode, Densities, "Compact");
        settings.MotionMode = Normalize(settings.MotionMode, Motions, "Normal");
        settings.IdleStyle = Normalize(settings.IdleStyle, IdleStyles, "Line");
        settings.StateAccentMode = Normalize(settings.StateAccentMode, StateAccents, "Unified");
        settings.PrivacyPreviewMode = Normalize(settings.PrivacyPreviewMode, PrivacyModes, "Full");
    }

    public static DensityProfile ResolveDensity(AppearanceSettings settings)
    {
        NormalizeInPlace(settings);
        return string.Equals(settings.DensityMode, "Comfortable", StringComparison.OrdinalIgnoreCase)
            ? new DensityProfile(1.08, 1.15, 1.08, 1.12)
            : new DensityProfile(1.00, 1.00, 1.00, 1.00);
    }

    public static MotionProfile ResolveMotion(AppearanceSettings settings)
    {
        NormalizeInPlace(settings);
        return string.Equals(settings.MotionMode, "Reduced", StringComparison.OrdinalIgnoreCase)
            ? new MotionProfile(0.45, 70, 0.0)
            : new MotionProfile(1.00, 110, 2.0);
    }

    public static AppearancePalette ResolvePalette(
        AppearanceSettings settings,
        string? systemAccentHex = null)
    {
        NormalizeInPlace(settings);

        string accent = ResolveAccent(settings.AccentPreset, systemAccentHex);
        bool monochrome = string.Equals(settings.ThemePreset, "Monochrome", StringComparison.OrdinalIgnoreCase);
        if (monochrome)
            accent = "#FFE6E6E8";

        string hover = ShiftRgb(accent, monochrome ? 10 : 22);
        string pressed = ShiftRgb(accent, monochrome ? -22 : -20);
        string selection = WithAlpha(accent, (byte)(monochrome ? 0x22 : 0x34));
        string subtle = WithAlpha(accent, 0x18);
        string accentBorder = WithAlpha(accent, 0x52);

        return settings.ThemePreset switch
        {
            "Aurora" => new AppearancePalette(
                "#FF090B12", "#FF0E111A", "#FF121622", "#FF151A26", "#FF19202E", "#FF202735",
                "#16FFFFFF", "#0CFFFFFF", "#26FFFFFF", "#36FFFFFF",
                "#FF283142", "#FF3A465A", "#22FFFFFF", "#3AFFFFFF",
                "#FFF4F5FA", "#FFBCC2D0", "#FF8C94A5", "#FF727A89",
                "#F4FFFFFF", "#A4FFFFFF", "#70FFFFFF",
                accent, hover, pressed, selection, subtle, accentBorder,
                "#FF43CF93", "#FFFFB956", "#FFFF6575",
                "#FF35B8D4", "#FF9A7AFF", "#FFF0AA46", "#FF46C98D", false),

            "Paper" => new AppearancePalette(
                "#FFF8F8FA", "#FFF4F5F7", "#FFFFFFFF", "#FFFFFFFF", "#FFFFFFFF", "#FFF0F1F4",
                "#10000000", "#08000000", "#12000000", "#1C000000",
                "#FFD9DCE2", "#FFC4C8D0", "#18000000", "#2A000000",
                "#FF17181B", "#FF4C5058", "#FF6F747E", "#FF8B9099",
                "#FF17181B", "#FF4C5058", "#FF6F747E",
                accent, hover, pressed, selection, subtle, accentBorder,
                "#FF228A5C", "#FFB56B13", "#FFC93D4F",
                "#FF168AA5", "#FF7559D9", "#FFC27816", "#FF23845C", true),

            "Monochrome" => new AppearancePalette(
                "#FF090909", "#FF101010", "#FF141414", "#FF181818", "#FF1D1D1D", "#FF232323",
                "#14FFFFFF", "#0BFFFFFF", "#22FFFFFF", "#32FFFFFF",
                "#FF2A2A2A", "#FF3B3B3B", "#20FFFFFF", "#36FFFFFF",
                "#FFF3F3F3", "#FFBDBDBD", "#FF8C8C8C", "#FF737373",
                "#F4FFFFFF", "#9AFFFFFF", "#68FFFFFF",
                accent, hover, pressed, selection, subtle, accentBorder,
                "#FFE6E6E8", "#FFBDBDBD", "#FFFFFFFF",
                "#FFD5D5D5", "#FFE0E0E0", "#FFC8C8C8", "#FFE8E8E8", false),

            _ => new AppearancePalette(
                "#FF0B0B0D", "#FF101012", "#FF141416", "#FF18181B", "#FF1C1C20", "#FF222226",
                "#14FFFFFF", "#0CFFFFFF", "#24FFFFFF", "#34FFFFFF",
                "#FF29292E", "#FF3A3A42", "#20FFFFFF", "#38FFFFFF",
                "#FFF5F5F7", "#FFBDBDC5", "#FF8D8D96", "#FF777780",
                "#F4FFFFFF", "#9AFFFFFF", "#6CFFFFFF",
                accent, hover, pressed, selection, subtle, accentBorder,
                "#FF35C98A", "#FFFFB547", "#FFFF5D6C",
                "#FF2FAFC7", "#FF8C6CFF", "#FFE5A13A", "#FF35B982", false)
        };
    }

    public static string ResolveStateColor(
        AppearanceSettings settings,
        AppearancePalette palette,
        string state)
    {
        NormalizeInPlace(settings);
        if (!string.Equals(settings.StateAccentMode, "Semantic", StringComparison.OrdinalIgnoreCase))
            return palette.AccentPrimary;

        return state.ToLowerInvariant() switch
        {
            "file" => palette.StateFile,
            "clipboard" => palette.StateClipboard,
            "screenshot" => palette.StateScreenshot,
            "media" => palette.StateMedia,
            _ => palette.AccentPrimary
        };
    }

    private static string ResolveAccent(string preset, string? systemAccentHex)
        => preset switch
        {
            "Violet" => "#FF8C6CFF",
            "Cyan" => "#FF23B7C9",
            "Amber" => "#FFF0A43C",
            "Green" => "#FF35B982",
            "System" when IsHexColor(systemAccentHex) => NormalizeHex(systemAccentHex!),
            _ => "#FF2D7DFF"
        };

    private static string Normalize(string? value, IEnumerable<string> allowed, string fallback)
        => allowed.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private static bool IsHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8)) return false;
        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }

    private static string NormalizeHex(string value)
    {
        string hex = value.Trim().TrimStart('#').ToUpperInvariant();
        return hex.Length == 6 ? "#FF" + hex : "#" + hex;
    }

    private static string WithAlpha(string argb, byte alpha)
    {
        string normalized = NormalizeHex(argb);
        return $"#{alpha:X2}{normalized[3..]}";
    }

    private static string ShiftRgb(string argb, int delta)
    {
        string normalized = NormalizeHex(argb);
        byte r = byte.Parse(normalized.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(normalized.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(normalized.Substring(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        r = (byte)Math.Clamp(r + delta, 0, 255);
        g = (byte)Math.Clamp(g + delta, 0, 255);
        b = (byte)Math.Clamp(b + delta, 0, 255);
        return $"#FF{r:X2}{g:X2}{b:X2}";
    }
}
