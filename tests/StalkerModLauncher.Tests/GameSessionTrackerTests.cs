using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class GameSessionTrackerTests
{
    [Fact]
    public void CreateResult_RecordsSessionsAtLeastFiveSecondsLong()
    {
        var started = new DateTime(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        var result = GameSessionTracker.CreateResult(started, started.AddSeconds(5), 7);

        Assert.True(result.ShouldRecord);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal(started, result.StartedAtUtc);
    }

    [Fact]
    public void CreateResult_IgnoresVeryShortSessions()
    {
        var started = new DateTime(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        var result = GameSessionTracker.CreateResult(started, started.AddSeconds(4));

        Assert.False(result.ShouldRecord);
    }

    [Fact]
    public void CreateResult_ClampsNegativeDuration()
    {
        var started = new DateTime(2026, 6, 7, 10, 0, 0, DateTimeKind.Utc);

        var result = GameSessionTracker.CreateResult(started, started.AddSeconds(-1));

        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.False(result.ShouldRecord);
    }
}
