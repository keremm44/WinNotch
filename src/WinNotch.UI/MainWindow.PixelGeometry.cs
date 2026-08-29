using System.Windows;
using WinNotch.Common;
using WinNotch.Core.Interop;

namespace WinNotch.UI;

public partial class MainWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // MainWindow_SourceInitialized runs from the SourceInitialized event raised
        // by base.OnSourceInitialized. Correct the legacy DIP-based native region
        // immediately afterwards, then repeat once after the first rendered frame.
        ApplyPixelAlignedWindowRegion();
        ContentRendered += MainWindow_PixelGeometryContentRendered;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // NotchMotionController updates the HWND region while animating. Its legacy
        // radius is DIP-based; re-apply the physical-pixel silhouette after layout so
        // the compositor never settles on a mismatched corner mask.
        ApplyPixelAlignedWindowRegion();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyPixelAlignedWindowRegion();
        RecenterOnMonitor();
    }

    private void MainWindow_PixelGeometryContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_PixelGeometryContentRendered;
        ApplyPixelAlignedWindowRegion();
    }

    private void ApplyPixelAlignedWindowRegion()
    {
        if (_hWnd == IntPtr.Zero || !User32.GetClientRect(_hWnd, out var clientRect))
            return;

        int width = Math.Max(1, clientRect.Right - clientRect.Left);
        int height = Math.Max(1, clientRect.Bottom - clientRect.Top);
        uint dpi = User32.GetDpiForWindow(_hWnd);
        NotchRegionGeometry geometry = NotchRegionGeometryResolver.Resolve(width, height, dpi);

        IntPtr region = geometry.CornerRadiusPx > 0
            ? User32.CreateRoundRectRgn(
                0,
                0,
                geometry.RegionRight,
                geometry.RegionBottom,
                geometry.EllipseDiameterPx,
                geometry.EllipseDiameterPx)
            : User32.CreateRectRgn(0, 0, geometry.RegionRight, geometry.RegionBottom);

        if (region == IntPtr.Zero)
            return;

        if (geometry.CornerRadiusPx > 0)
        {
            IntPtr topRect = User32.CreateRectRgn(
                0,
                0,
                geometry.RegionRight,
                geometry.TopFillHeightPx);
            if (topRect != IntPtr.Zero)
            {
                User32.CombineRgn(region, region, topRect, User32.RGN_OR);
                User32.DeleteObject(topRect);
            }
        }

        // On success Windows owns the region handle. Delete it only when ownership
        // was not transferred.
        if (!User32.SetWindowRgn(_hWnd, region, true))
            User32.DeleteObject(region);
    }
}
