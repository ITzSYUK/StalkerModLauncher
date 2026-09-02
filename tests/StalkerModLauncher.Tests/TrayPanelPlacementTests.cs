using System.Windows;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class TrayPanelPlacementTests
{
    [Fact]
    public void PixelsToDeviceIndependentUnitsConvertsHighDpiMonitorBounds()
    {
        var bounds = TrayPanelPlacement.PixelsToDeviceIndependentUnits(
            new Rect(0, 0, 2880, 1800),
            dpiScaleX: 1.75,
            dpiScaleY: 1.75);

        Assert.Equal(new Rect(0, 0, 2880d / 1.75, 1800d / 1.75), bounds);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1040, 0, 0, 1920, 1080, 1610, 730)]
    [InlineData(0, 40, 1920, 1040, 0, 0, 1920, 1080, 1610, 50)]
    [InlineData(40, 0, 1880, 1080, 0, 0, 1920, 1080, 50, 770)]
    [InlineData(-1920, 0, 1920, 1040, -1920, 0, 1920, 1080, -310, 730)]
    public void CalculateAnchorsPanelInsideMonitorWorkArea(
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight,
        double screenLeft,
        double screenTop,
        double screenWidth,
        double screenHeight,
        double expectedLeft,
        double expectedTop)
    {
        var position = TrayPanelPlacement.Calculate(
            new Rect(workLeft, workTop, workWidth, workHeight),
            new Rect(screenLeft, screenTop, screenWidth, screenHeight),
            new Size(300, 300));

        Assert.Equal(new Point(expectedLeft, expectedTop), position);
    }

    [Fact]
    public void CalculateClampsOversizedPanelToWorkAreaOrigin()
    {
        var position = TrayPanelPlacement.Calculate(
            new Rect(100, 50, 200, 150),
            new Rect(100, 50, 200, 180),
            new Size(400, 300));

        Assert.Equal(new Point(100, 50), position);
    }
}
