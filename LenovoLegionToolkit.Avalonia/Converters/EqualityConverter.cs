using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class EqualityConverter : IValueConverter
    {
        public static readonly EqualityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
                return true;

            if (value == null || parameter == null)
                return false;

            return value.Equals(parameter) || value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}