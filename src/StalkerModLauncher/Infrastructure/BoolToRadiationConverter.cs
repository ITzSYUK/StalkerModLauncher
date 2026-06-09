using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StalkerModLauncher.Infrastructure;

public sealed class BoolToRadiationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is true ? "RadiationOnImage" : "RadiationOffImage";
        return Application.Current?.TryFindResource(resourceKey) as ImageSource
            ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
