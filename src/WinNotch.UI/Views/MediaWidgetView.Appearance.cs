using WinNotch.Common;

namespace WinNotch.UI.Views;

public partial class MediaWidgetView
{
    public void ApplyAppearance(AppearanceSettings settings)
    {
        AppearanceResolver.NormalizeInPlace(settings);
        DensityProfile density = AppearanceResolver.ResolveDensity(settings);

        TitleText.FontSize = 11.5 * density.FontScale;
        SubtitleText.FontSize = 9.5 * density.FontScale;
        TitleText.MaxWidth = 142 * density.SurfaceScale;
        SubtitleText.MaxWidth = 142 * density.SurfaceScale;

        double artSize = 40 * density.ControlScale;
        AlbumArtSurface.Width = artSize;
        AlbumArtSurface.Height = artSize;

        double smallControl = 30 * density.ControlScale;
        PrevButton.Width = smallControl;
        PrevButton.Height = smallControl;
        NextButton.Width = smallControl;
        NextButton.Height = smallControl;
        PlayPauseButton.Width = 34 * density.ControlScale;
        PlayPauseButton.Height = 34 * density.ControlScale;
        PrevButton.FontSize = 13 * density.FontScale;
        NextButton.FontSize = 13 * density.FontScale;
        PlayPauseButton.FontSize = 14 * density.FontScale;

        ProgressTrack.Height = string.Equals(settings.DensityMode, "Comfortable", StringComparison.OrdinalIgnoreCase)
            ? 3.5
            : 3;
    }
}
