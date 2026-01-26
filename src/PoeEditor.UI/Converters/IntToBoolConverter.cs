using System.Globalization;
using System.Windows.Data;

namespace PoeEditor.UI.Converters;

/// <summary>
/// Converts between int and bool for RadioButton binding.
/// ConverterParameter should be the target int value.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out var targetValue))
        {
            return intValue == targetValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is string paramStr && int.TryParse(paramStr, out var targetValue))
        {
            return targetValue;
        }
        return Binding.DoNothing;
    }
}
