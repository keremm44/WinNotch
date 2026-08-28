using WinNotch.Core.Interop;
using Xunit;

namespace WinNotch.Tests;

public class FullscreenDetectionTests
{
    [Theory]
    [InlineData(0u, 8)]
    [InlineData(96u, 8)]
    [InlineData(120u, 10)]
    [InlineData(144u, 12)]
    [InlineData(192u, 16)]
    [InlineData(480u, 32)]
    public void GeometryTolerance_ScalesWithWindowDpi(uint dpi, int expected)
    {
        Assert.Equal(expected, WindowHookManager.GeometryToleranceForDpi(dpi));
    }

    [Fact]
    public void NormalMaximize_IsNotFullscreen_EvenWhenAutoHideMakesAllBoundsMatchMonitor()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: true,
            decoratedMaximized: true,
            shellFullscreen: false);

        Assert.False(result);
    }

    [Fact]
    public void UndecoratedChromiumClient_CoveringMonitor_IsFullscreen()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: true,
            decoratedMaximized: false,
            shellFullscreen: false);

        Assert.True(result);
    }

    [Fact]
    public void ShellFullscreen_BridgesChromiumGeometryTransition()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: false,
            clientCoversMonitor: false,
            decoratedMaximized: true,
            shellFullscreen: true);

        Assert.True(result);
    }

    [Fact]
    public void ChromiumClientCoverage_WinsBeforeDwmFrameCatchesUp()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: false,
            clientCoversMonitor: true,
            decoratedMaximized: false,
            shellFullscreen: false);

        Assert.True(result);
    }

    [Fact]
    public void DecoratedOuterFrameWithoutClientCoverage_IsNormalMaximize()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: false,
            decoratedMaximized: true,
            shellFullscreen: false);

        Assert.False(result);
    }
}
