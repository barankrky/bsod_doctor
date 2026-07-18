using System.Globalization;
using System.Windows.Data;

namespace BsodDoctor;

/// <summary>
/// Boolea değerini tersine çeviren converter (true → false, false → true).
/// </summary>
public class BoolInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}
