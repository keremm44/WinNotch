using WinNotch.Core.Services;
using Xunit;

namespace WinNotch.Tests;

public class ClipboardWriteSuppressionTests
{
    [Fact]
    public void ArmedText_SuppressesOnlyMatchingNextClipboardValue()
    {
        var guard = new ClipboardWriteSuppression();
        guard.ArmText("WinNotch output");

        Assert.True(guard.ConsumeText("WinNotch output"));
        Assert.False(guard.ConsumeText("WinNotch output"));
    }

    [Fact]
    public void DifferentText_IsNotSuppressed_AndConsumesStaleGuard()
    {
        var guard = new ClipboardWriteSuppression();
        guard.ArmText("WinNotch output");

        Assert.False(guard.ConsumeText("user copy"));
        Assert.False(guard.HasPendingText);
        Assert.False(guard.ConsumeText("WinNotch output"));
    }

    [Fact]
    public void CancelText_OnlyCancelsMatchingPreparedWrite()
    {
        var guard = new ClipboardWriteSuppression();
        guard.ArmText("newer");

        guard.CancelText("older");
        Assert.True(guard.HasPendingText);
        Assert.True(guard.ConsumeText("newer"));
    }

    [Fact]
    public void ImageSuppression_IsOneShot_AndCancelable()
    {
        var guard = new ClipboardWriteSuppression();
        guard.ArmImage();
        Assert.True(guard.ConsumeImage());
        Assert.False(guard.ConsumeImage());

        guard.ArmImage();
        guard.CancelImage();
        Assert.False(guard.ConsumeImage());
    }

    [Fact]
    public void Clear_RemovesPendingTextAndImage()
    {
        var guard = new ClipboardWriteSuppression();
        guard.ArmText("text");
        guard.ArmImage();

        guard.Clear();

        Assert.False(guard.HasPendingText);
        Assert.False(guard.ConsumeText("text"));
        Assert.False(guard.ConsumeImage());
    }
}
