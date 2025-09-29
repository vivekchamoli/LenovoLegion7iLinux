using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class TemperatureToColorConverter : IValueConverter
    {
        public static readonly TemperatureToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double temp)
            {
                if (temp < 50)
                    return new SolidColorBrush(Color.Parse("#4CAF50")); // Green - Cool
                else if (temp < 60)
                    return new SolidColorBrush(Color.Parse("#8BC34A")); // Light Green - Normal
                else if (temp < 70)
                    return new SolidColorBrush(Color.Parse("#FFC107")); // Amber - Warm
                else if (temp < 80)
                    return new SolidColorBrush(Color.Parse("#FF9800")); // Orange - Hot
                else if (temp < 90)
                    return new SolidColorBrush(Color.Parse("#FF5722")); // Deep Orange - Very Hot
                else
                    return new SolidColorBrush(Color.Parse("#F44336")); // Red - Critical
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}