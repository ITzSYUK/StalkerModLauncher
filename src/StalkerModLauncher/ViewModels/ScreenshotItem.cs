using System.Windows.Media.Imaging;

namespace StalkerModLauncher.ViewModels;

public sealed class ScreenshotItem
{
    public string FilePath { get; }
    public BitmapImage Source { get; }
    public double ThumbnailWidth => 240;
    public double ThumbnailHeight { get; }

    public ScreenshotItem(string filePath)
    {
        FilePath = filePath;

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(filePath);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();

        ThumbnailHeight = 240.0 / ((double)image.PixelWidth / image.PixelHeight);

        image.Freeze();
        Source = image;
    }
}
