using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class AppearanceTests
{
    [Fact]
    public void Defaults_AreControlledAndBackwardSafe()
    {
        var settings = new ModuleSettings();

        Assert.NotNull(settings.Appearance);
        Assert.Equal("Obsidian", settings.Appearance.ThemePreset);
        Assert.Equal("Blue", settings.Appearance.AccentPreset);
        Assert.Equal("Compact", settings.Appearance.DensityMode);
        Assert.Equal("Normal", settings.Appearance.MotionMode);
        Assert.Equal("Line", settings.Appearance.IdleStyle);
        Assert.Equal("Unified", settings.Appearance.StateAccentMode);
        Assert.Equal("Full", settings.Appearance.PrivacyPreviewMode);
    }

    [Fact]
    public void Normalize_InvalidValues_ReturnsSafeDefaults()
    {
        var appearance = new AppearanceSettings
        {
            ThemePreset = "CustomCss",
            AccentPreset = "Rainbow",
            DensityMode = "Huge",
            MotionMode = "Bounce",
            IdleStyle = "Laser",
            StateAccentMode = "Random",
            PrivacyPreviewMode = "LeakEverything"
        };

        AppearanceResolver.NormalizeInPlace(appearance);

        Assert.Equal("Obsidian", appearance.ThemePreset);
        Assert.Equal("Blue", appearance.AccentPreset);
        Assert.Equal("Compact", appearance.DensityMode);
        Assert.Equal("Normal", appearance.MotionMode);
        Assert.Equal("Line", appearance.IdleStyle);
        Assert.Equal("Unified", appearance.StateAccentMode);
        Assert.Equal("Full", appearance.PrivacyPreviewMode);
    }

    [Fact]
    public void Paper_ResolvesAsRealLightPalette()
    {
        var appearance = new AppearanceSettings { ThemePreset = "Paper" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance);

        Assert.True(palette.IsLightTheme);
        Assert.Equal("#FFF8F8FA", palette.NotchBase);
        Assert.Equal("#FF17181B", palette.TextPrimary);
    }

    [Fact]
    public void Aurora_RemainsDark()
    {
        var appearance = new AppearanceSettings { ThemePreset = "Aurora", AccentPreset = "Violet" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance);

        Assert.False(palette.IsLightTheme);
        Assert.Equal("#FF8C6CFF", palette.AccentPrimary);
    }

    [Fact]
    public void Monochrome_DoesNotLeakSelectedColor()
    {
        var appearance = new AppearanceSettings { ThemePreset = "Monochrome", AccentPreset = "Amber" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance);

        Assert.Equal("#FFE6E6E8", palette.AccentPrimary);
    }

    [Fact]
    public void SystemAccent_UsesValidatedSystemColor()
    {
        var appearance = new AppearanceSettings { AccentPreset = "System" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance, "#123456");

        Assert.Equal("#FF123456", palette.AccentPrimary);
    }

    [Fact]
    public void ComfortableDensity_IsLargerButBounded()
    {
        var appearance = new AppearanceSettings { DensityMode = "Comfortable" };
        DensityProfile profile = AppearanceResolver.ResolveDensity(appearance);

        Assert.InRange(profile.SurfaceScale, 1.01, 1.15);
        Assert.InRange(profile.FontScale, 1.01, 1.12);
    }

    [Fact]
    public void ReducedMotion_RemovesContentTranslation()
    {
        var appearance = new AppearanceSettings { MotionMode = "Reduced" };
        MotionProfile profile = AppearanceResolver.ResolveMotion(appearance);

        Assert.Equal(0, profile.ContentOffsetY);
        Assert.True(profile.ContainerDurationScale < 1);
    }

    [Fact]
    public void SemanticStateAccent_UsesStateColor()
    {
        var appearance = new AppearanceSettings { StateAccentMode = "Semantic", AccentPreset = "Blue" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance);

        string screenshot = AppearanceResolver.ResolveStateColor(appearance, palette, "screenshot");
        Assert.Equal(palette.StateScreenshot, screenshot);
        Assert.NotEqual(palette.AccentPrimary, screenshot);
    }

    [Fact]
    public void UnifiedStateAccent_UsesUserAccent()
    {
        var appearance = new AppearanceSettings { StateAccentMode = "Unified", AccentPreset = "Violet" };
        AppearancePalette palette = AppearanceResolver.ResolvePalette(appearance);

        Assert.Equal(palette.AccentPrimary,
            AppearanceResolver.ResolveStateColor(appearance, palette, "media"));
    }

    [Fact]
    public void MaskedPrivacy_HidesUrlPathAndEmailDetails()
    {
        var appearance = new AppearanceSettings { PrivacyPreviewMode = "Masked" };

        string url = PrivacyPreviewFormatter.Format(
            ClipboardContentType.Url,
            "https://github.com/openai/private?q=secret",
            "https://github.com/openai/private?q=secret",
            appearance);
        string path = PrivacyPreviewFormatter.Format(
            ClipboardContentType.FilePath,
            @"C:\Users\Faruk\Secret\report.pdf",
            @"C:\Users\Faruk\Secret\report.pdf",
            appearance);
        string email = PrivacyPreviewFormatter.Format(
            ClipboardContentType.Email,
            "faruk@example.com",
            "faruk@example.com",
            appearance);

        Assert.Equal("github.com/••••", url);
        Assert.Equal(@"…\Secret\report.pdf", path);
        Assert.Equal("f••••@example.com", email);
    }

    [Fact]
    public void TypeOnlyPrivacy_NeverShowsRawClipboardValue()
    {
        var appearance = new AppearanceSettings { PrivacyPreviewMode = "TypeOnly" };
        string preview = PrivacyPreviewFormatter.Format(
            ClipboardContentType.Url,
            "https://example.com/private",
            "https://example.com/private",
            appearance);

        Assert.Equal("Bağlantı kopyalandı", preview);
        Assert.DoesNotContain("example.com", preview);
        Assert.False(PrivacyPreviewFormatter.ShouldShowScreenshotThumbnail(appearance));
    }
}
