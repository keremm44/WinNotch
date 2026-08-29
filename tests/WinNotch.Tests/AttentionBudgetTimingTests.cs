using WinNotch.Common;
using Xunit;

namespace WinNotch.Tests;

public class AttentionBudgetTimingTests
{
    [Fact]
    public void Cooldown_ExpiresExactlyAtConfiguredInterval()
    {
        var clock = new ManualTimeProvider();
        var policy = new AttentionPolicy(clock);

        AttentionDecision first = policy.ClassifyScreenshot();
        AttentionDecision immediate = policy.ClassifyScreenshot();
        clock.Advance(TimeSpan.FromMilliseconds(Constants.MinNotificationIntervalMs));
        AttentionDecision afterCooldown = policy.ClassifyScreenshot();

        Assert.False(first.Suppressed);
        Assert.True(immediate.Suppressed);
        Assert.False(afterCooldown.Suppressed);
    }

    [Fact]
    public void MinuteBudget_SuppressesSixthEvent_ThenRecoversWhenOldestExpires()
    {
        var clock = new ManualTimeProvider();
        var policy = new AttentionPolicy(clock);

        for (int i = 0; i < Constants.MaxAttentionEventsPerMinute; i++)
        {
            AttentionDecision decision = policy.ClassifyScreenshot();
            Assert.False(decision.Suppressed);
            clock.Advance(TimeSpan.FromMilliseconds(Constants.MinNotificationIntervalMs));
        }

        AttentionDecision overBudget = policy.ClassifyScreenshot();
        Assert.True(overBudget.Suppressed);

        TimeSpan elapsed = TimeSpan.FromMilliseconds(
            Constants.MaxAttentionEventsPerMinute * Constants.MinNotificationIntervalMs);
        TimeSpan untilOldestExpires = TimeSpan.FromMinutes(1) - elapsed;
        clock.Advance(untilOldestExpires);

        AttentionDecision recovered = policy.ClassifyScreenshot();
        Assert.False(recovered.Suppressed);
    }

    [Fact]
    public void SilentEvents_DoNotConsumeAttentionBudget()
    {
        var clock = new ManualTimeProvider();
        var policy = new AttentionPolicy(clock);

        for (int i = 0; i < 20; i++)
        {
            AttentionDecision silent = policy.ClassifyClipboard(
                ClipboardContentType.Text,
                "plain text",
                "Balanced");
            Assert.Equal(AttentionLevel.Silent, silent.Level);
        }

        AttentionDecision screenshot = policy.ClassifyScreenshot();
        Assert.False(screenshot.Suppressed);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(duration));
            _timestamp += duration.Ticks;
        }
    }
}
