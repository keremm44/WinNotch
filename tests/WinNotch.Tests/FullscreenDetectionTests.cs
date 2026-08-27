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
            clientCoversMonitor: true,
            decoratedMaximized: true,
            shellFullscreen: false);

        Assert.False(result);
    }

    [Fact]
    public void BorderlessChromiumWindow_CoveringMonitor_IsFullscreen()
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
    public void BorderlessWindow_WithOnlyOuterShadowCoverage_IsNotFullscreen()
    {
        bool result = WindowHookManager.ClassifyFullscreen(
            coversMonitor: true,
            clientCoversMonitor: false,
            decoratedMaximized: false,
            shellFullscreen: false);

        Assert.False(result);
    }
}
