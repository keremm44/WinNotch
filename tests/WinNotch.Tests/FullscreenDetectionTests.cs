using WinNotch.Core.Interop;
using Xunit;

namespace WinNotch.Tests;

public class FullscreenDetectionTests
{
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
