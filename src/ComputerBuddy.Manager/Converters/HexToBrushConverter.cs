using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ComputerBuddy.Manager.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s))
        {
            return Brushes.Transparent;
        }

        try
        {
            var bc = new BrushConverter();
            return bc.ConvertFromString(s.Trim()) as Brush ?? Brushes.Transparent;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

