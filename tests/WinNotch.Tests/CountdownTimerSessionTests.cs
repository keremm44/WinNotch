using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class CountdownTimerSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartAndUpdate_CompletesAtDeadline()
    {
        var timer = new CountdownTimerSession();
        Assert.True(timer.Start(TimeSpan.FromMinutes(5), Now));
        Assert.False(timer.Update(Now.AddMinutes(4)));
        Assert.True(timer.Update(Now.AddMinutes(5)));
        Assert.Equal(CountdownTimerStatus.Completed, timer.Status);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
    }

    [Fact]
    public void PauseAndResume_PreserveRemainingDuration()
    {
        var timer = new CountdownTimerSession();
        timer.Start(TimeSpan.FromMinutes(10), Now);
        Assert.True(timer.Pause(Now.AddMinutes(3)));
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Remaining);
        Assert.True(timer.Resume(Now.AddHours(1)));
        Assert.False(timer.Update(Now.AddHours(1).AddMinutes(6)));
        Assert.True(timer.Update(Now.AddHours(1).AddMinutes(7)));
    }

    [Fact]
    public void PauseAtDeadline_LeavesSessionCompleted()
    {
        var timer = new CountdownTimerSession();
        timer.Start(TimeSpan.FromSeconds(1), Now);
        Assert.False(timer.Pause(Now.AddSeconds(1)));
        Assert.Equal(CountdownTimerStatus.Completed, timer.Status);
    }

    [Fact]
    public void Cancel_ReleasesActiveCountdown()
    {
        var timer = new CountdownTimerSession();
        timer.Start(TimeSpan.FromMinutes(1), Now);
        timer.Cancel();
        Assert.Equal(CountdownTimerStatus.Idle, timer.Status);
        Assert.False(timer.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(86401)]
    public void Start_RejectsUnsafeDuration(int seconds)
    {
        var timer = new CountdownTimerSession();
        Assert.False(timer.Start(TimeSpan.FromSeconds(seconds), Now));
    }
}
