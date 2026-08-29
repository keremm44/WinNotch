namespace WinNotch.Common;

/// <summary>
/// Controlled personalization settings. Values are intentionally preset-based;
/// arbitrary geometry/font/glow sliders are not part of the product contract.
/// </summary>
public sealed class AppearanceSettings
{
    /// <summary>Obsidian, Aurora, Graphite, Monochrome, Paper or Frost.</summary>
    public string ThemePreset { get; set; } = "Obsidian";

    /// <summary>Blue, Violet, Cyan, Amber, Green or System.</summary>
    public string AccentPreset { get; set; } = "Blue";

    /// <summary>Compact or Comfortable.</summary>
    public string DensityMode { get; set; } = "Compact";

    /// <summary>Reduced or Normal.</summary>
    public string MotionMode { get; set; } = "Normal";

    /// <summary>Line, Dot or Ambient.</summary>
    public string IdleStyle { get; set; } = "Line";

    /// <summary>Unified or Semantic.</summary>
    public string StateAccentMode { get; set; } = "Unified";

    /// <summary>Full, Masked or TypeOnly.</summary>
    public string PrivacyPreviewMode { get; set; } = "Full";
}
