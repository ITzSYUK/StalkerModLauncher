using StalkerModLauncher.Infrastructure;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class DebouncedAsyncActionTests
{
    [Fact]
    public async Task ScheduleCollapsesRapidCallsIntoSingleAction()
    {
        var executionCount = 0;
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var action = new DebouncedAsyncAction(
            () =>
            {
                Interlocked.Increment(ref executionCount);
                executed.TrySetResult();
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(40));

        action.Schedule();
        action.Schedule();
        action.Schedule();
        await executed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task CancelPreventsPendingAction()
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
