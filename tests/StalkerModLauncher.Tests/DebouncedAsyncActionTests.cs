using StalkerModLauncher.Infrastructure;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class DebouncedAsyncActionTests
{
    [Fact]
    public async Task Schedule_CollapsesRapidCallsIntoSingleAction()
    {
        var executionCount = 0;
        using var action = new DebouncedAsyncAction(
            () =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(40));

        action.Schedule();
        action.Schedule();
        action.Schedule();
        await Task.Delay(150);

        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task Cancel_PreventsPendingAction()
    {
        var executionCount = 0;
        using var action = new DebouncedAsyncAction(
            () =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(40));

        action.Schedule();
        action.Cancel();
        await Task.Delay(120);

        Assert.Equal(0, executionCount);
    }
}
