using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace StalkerModLauncher.Infrastructure;

public sealed class BoolToRadiationConverter : IValueConverter
{
    private static readonly BitmapImage OnImage = LoadImage("Resources/radiation_on.png");
    private static readonly BitmapImage OffImage = LoadImage("Resources/radiation_off.png");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? OnImage : OffImage;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static BitmapImage LoadImage(string path)
    {
        var uri = new Uri($"pack://application:,,,/{path}", UriKind.RelativeOrAbsolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
