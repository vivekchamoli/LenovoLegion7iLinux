using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class DisplayTypeConverter : IValueConverter
    {
        public static readonly DisplayTypeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInternal)
            {
                return isInternal ? "Internal Display" : "External Display";
            }
            return "Unknown";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DisplayTypeIconConverter : IValueConverter
    {
        public static readonly DisplayTypeIconConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInternal)
            {
                return isInternal ? "💻" : "🖥️";
            }
            return "📺";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}