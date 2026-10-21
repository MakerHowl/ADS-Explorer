using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TwinCatVariableExplorer.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // #2ECC71 Green
    public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // #E74C3C Red

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return TrueBrush;
        }

        return FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
