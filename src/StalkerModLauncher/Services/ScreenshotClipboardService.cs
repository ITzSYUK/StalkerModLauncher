using System.Windows;
using System.Windows.Media.Imaging;

namespace StalkerModLauncher.Services;

public interface IScreenshotClipboardService
{
    void Copy(string filePath);
}

public sealed class ScreenshotClipboardService : IScreenshotClipboardService
{
    public void Copy(string filePath)
    {
        // The clipboard needs the original image, but the thumbnail grid should
        // never keep full-size game screenshots alive in launcher memory.
        var image = ScreenshotImageLoader.LoadFullResolution(filePath);
        Clipboard.SetImage(image);
    }
}
