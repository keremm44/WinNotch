using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class NotchRegionGeometryTests
{
    [Theory]
    [InlineData(96u, 11)]
    [InlineData(120u, 14)]
    [InlineData(144u, 17)]
    [InlineData(192u, 22)]
    public void Resolve_ScalesNativeCornerRadiusWithWindowDpi(uint dpi, int expectedRadius)
    {
        NotchRegionGeometry geometry = NotchRegionGeometryResolver.Resolve(340, 100, dpi);

        Assert.Equal(expectedRadius, geometry.CornerRadiusPx);
        Assert.Equal(expectedRadius * 2, geometry.EllipseDiameterPx);
    }

    [Fact]
    public void Resolve_ZeroDpiFallsBackToNinetySixDpi()
    {
        NotchRegionGeometry geometry = NotchRegionGeometryResolver.Resolve(100, 22, 0);

        Assert.Equal(11, geometry.CornerRadiusPx);
        Assert.Equal(22, geometry.EllipseDiameterPx);
    }

    [Fact]
    public void Resolve_CapsRadiusAtHalfOfPhysicalHeight()
    {
        NotchRegionGeometry geometry = NotchRegionGeometryResolver.Resolve(100, 20, 192);

        Assert.Equal(10, geometry.CornerRadiusPx);
        Assert.Equal(20, geometry.EllipseDiameterPx);
    }

    [Fact]
    public void Resolve_PreservesEveryClientPixelInExclusiveGdiBounds()
    {
        NotchRegionGeometry geometry = NotchRegionGeometryResolver.Resolve(325, 50, 120);

        Assert.Equal(325, geometry.WidthPx);
        Assert.Equal(50, geometry.HeightPx);
        Assert.Equal(326, geometry.RegionRight);
        Assert.Equal(51, geometry.RegionBottom);
        Assert.InRange(geometry.TopFillHeightPx, 1, geometry.RegionBottom);
    }
}
