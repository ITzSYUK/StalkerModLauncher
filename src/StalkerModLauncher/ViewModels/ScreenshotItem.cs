using System.Windows.Media.Imaging;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ScreenshotItem
{
    public string FilePath { get; }
    public BitmapImage Thumbnail { get; }
    public double ThumbnailWidth => 240;
    public double ThumbnailHeight { get; }

    public ScreenshotItem(string filePath)
    {
        FilePath = filePath;
        Thumbnail = ScreenshotImageLoader.LoadThumbnail(filePath);
        ThumbnailHeight = ThumbnailWidth / ((double)Thumbnail.PixelWidth / Thumbnail.PixelHeight);
    }

    public BitmapImage LoadForDisplay() => ScreenshotImageLoader.LoadForDisplay(FilePath);
}
