using System.Globalization;
using System.Windows.Data;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CodexUsageWidget;

public sealed class RemainingBrushConverter : IValueConverter
{
    private static readonly MediaBrush Green = FrozenBrush("#FF4ECB71");
    private static readonly MediaBrush Amber = FrozenBrush("#FFF0B44D");
    private static readonly MediaBrush Red = FrozenBrush("#FFF05C5C");
    private static readonly MediaBrush Neutral = FrozenBrush("#FF59616D");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double? remaining = value switch
        {
            int intValue => intValue,
            double doubleValue when !double.IsNaN(doubleValue) => doubleValue,
            _ => null,
        };

        if (remaining is null)
        {
            return Neutral;
        }

        return remaining > 50d ? Green : remaining >= 20d ? Amber : Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static MediaBrush FrozenBrush(string color)
    {
        var brush = new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
