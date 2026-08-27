using WinNotch.Core.Interop;
using Xunit;

namespace WinNotch.Tests;

public class FullscreenDetectionTests
{
    [Fact]
    public void NormalMaximize_IsNotFullscreen_EvenWhenOuterBoundsCoverMonitor()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: false,
            decoratedMaximized: true,
            shellFullscreen: false);

        Assert.False(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChromiumClient_CoveringMonitor_IsFullscreen_EvenIfWindowKeepsDecoratedStyle(
        bool decoratedMaximized)
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: true,
            decoratedMaximized: decoratedMaximized,
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
            decoratedMaximized: true,
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
