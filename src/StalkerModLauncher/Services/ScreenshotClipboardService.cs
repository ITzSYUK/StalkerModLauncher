using System.Windows;
using System.Windows.Media.Imaging;

namespace StalkerModLauncher.Services;

public interface IScreenshotClipboardService
{
    void Copy(BitmapSource image);
}

public sealed class ScreenshotClipboardService : IScreenshotClipboardService
{
    public void Copy(BitmapSource image)
    {
        Clipboard.SetImage(image);
    }
}
