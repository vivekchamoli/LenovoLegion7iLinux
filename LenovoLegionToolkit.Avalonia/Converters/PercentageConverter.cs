using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return $"{doubleValue:F0}%";
            }
            else if (value is int intValue)
            {
                return $"{intValue}%";
            }
            return "--%";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                stringValue = stringValue.Replace("%", "").Trim();
                if (double.TryParse(stringValue, out var result))
                {
                    return result;
                }
            }
            return 0;
        }
    }
}