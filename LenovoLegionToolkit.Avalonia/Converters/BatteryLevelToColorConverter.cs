using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class BatteryLevelToColorConverter : IValueConverter
    {
        public static readonly BatteryLevelToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                if (level >= 60)
                    return new SolidColorBrush(Color.Parse("#4CAF50")); // Green
                else if (level >= 30)
                    return new SolidColorBrush(Color.Parse("#FFC107")); // Amber
                else if (level >= 15)
                    return new SolidColorBrush(Color.Parse("#FF9800")); // Orange
                else
                    return new SolidColorBrush(Color.Parse("#F44336")); // Red
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}