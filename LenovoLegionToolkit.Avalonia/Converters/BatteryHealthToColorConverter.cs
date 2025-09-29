using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class BatteryHealthToColorConverter : IValueConverter
    {
        public static readonly BatteryHealthToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double health)
            {
                if (health >= 90)
                    return new SolidColorBrush(Color.Parse("#4CAF50")); // Green
                else if (health >= 80)
                    return new SolidColorBrush(Color.Parse("#8BC34A")); // Light Green
                else if (health >= 70)
                    return new SolidColorBrush(Color.Parse("#FFC107")); // Amber
                else if (health >= 60)
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