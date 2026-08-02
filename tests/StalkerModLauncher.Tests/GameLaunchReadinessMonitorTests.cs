using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class GameLaunchReadinessMonitorTests
{
    [Fact]
    public void EvaluateReadySignal_AcceptsMainWindow()
    {
        var signal = GameLaunchReadinessMonitor.EvaluateReadySignal(
            [new GameProcessReadinessState(true, 4 * 1024 * 1024)],
            null);

        Assert.Contains("окно", signal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateReadySignal_RejectsSmallProcessWithoutWindowOrLog()
    {
        var signal = GameLaunchReadinessMonitor.EvaluateReadySignal(
            [new GameProcessReadinessState(false, 4 * 1024 * 1024)],
            null);

        Assert.Null(signal);
    }

    [Fact]
    public void EvaluateReadySignal_AcceptsFreshGameLog()
    {
        var signal = GameLaunchReadinessMonitor.EvaluateReadySignal([], @"C:\profile\logs\xray.log");

        Assert.Contains("xray.log", signal, StringComparison.OrdinalIgnoreCase);
    }
}
