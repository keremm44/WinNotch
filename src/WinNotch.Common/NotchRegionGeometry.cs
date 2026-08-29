namespace WinNotch.Common;

/// <summary>
/// Pixel-space geometry used by the native HWND region. WPF dimensions and corner
/// radii are expressed in DIPs, while CreateRoundRectRgn consumes physical pixels.
/// Keeping that conversion explicit prevents the native silhouette from drifting
/// away from the WPF Border at 125%, 150%, 200% and mixed-DPI displays.
/// </summary>
public readonly record struct NotchRegionGeometry(
    int WidthPx,
    int HeightPx,
    int RegionRight,
    int RegionBottom,
    int CornerRadiusPx,
    int EllipseDiameterPx,
    int TopFillHeightPx);

public static class NotchRegionGeometryResolver
{
    private const double DefaultDpi = 96.0;

    public static NotchRegionGeometry Resolve(int widthPx, int heightPx, uint dpi)
    {
        int width = Math.Max(1, widthPx);
        int height = Math.Max(1, heightPx);
        double scale = (dpi == 0 ? DefaultDpi : dpi) / DefaultDpi;

        int requestedRadius = Math.Max(
            0,
            (int)Math.Round(
                Constants.NotchCornerRadius * scale,
                MidpointRounding.AwayFromZero));
        int radius = Math.Min(requestedRadius, height / 2);

        // GDI region right/bottom coordinates are exclusive. Extending by one keeps
        // the final client pixel inside the region and mirrors the existing WinNotch
        // region ownership contract without leaving a one-pixel bottom/right seam.
        int regionRight = width + 1;
        int regionBottom = height + 1;
        int ellipseDiameter = Math.Max(1, radius * 2);

        // The notch is flush with the top edge of the monitor, so only the bottom
        // corners are rounded. Fill through the end of the upper arc to square off
        // the two native top corners exactly.
        int topFillHeight = Math.Min(regionBottom, radius + 1);

        return new NotchRegionGeometry(
            width,
            height,
            regionRight,
            regionBottom,
            radius,
            ellipseDiameter,
            topFillHeight);
    }
}
