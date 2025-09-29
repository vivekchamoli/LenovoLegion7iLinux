using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter?.ToString()?.ToLower() == "invert";
                return (boolValue ^ invert) ? true : false;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool visibility)
            {
                bool invert = parameter?.ToString()?.ToLower() == "invert";
                return visibility ^ invert;
            }
            return false;
        }
    }
}