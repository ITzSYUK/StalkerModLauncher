using System.Windows;

namespace StalkerModLauncher.Services;

public static class TrayPanelPlacement
{
    public static Rect PixelsToDeviceIndependentUnits(
        Rect pixelBounds,
        double dpiScaleX,
        double dpiScaleY)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dpiScaleX, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dpiScaleY, 0);

        return new Rect(
            pixelBounds.Left / dpiScaleX,
            pixelBounds.Top / dpiScaleY,
            pixelBounds.Width / dpiScaleX,
            pixelBounds.Height / dpiScaleY);
    }

    public static Point Calculate(
        Rect workArea,
        Rect screenBounds,
        Size panelSize,
        double margin = 10)
    {
        var taskbarIsOnTop = workArea.Top > screenBounds.Top;
        var taskbarIsOnLeft = workArea.Left > screenBounds.Left;

        var left = taskbarIsOnLeft
            ? workArea.Left + margin
            : workArea.Right - panelSize.Width - margin;
        var top = taskbarIsOnTop
            ? workArea.Top + margin
            : workArea.Bottom - panelSize.Height - margin;

        var maximumLeft = Math.Max(workArea.Left, workArea.Right - panelSize.Width);
        var maximumTop = Math.Max(workArea.Top, workArea.Bottom - panelSize.Height);

        return new Point(
            Math.Clamp(left, workArea.Left, maximumLeft),
            Math.Clamp(top, workArea.Top, maximumTop));
    }
}
