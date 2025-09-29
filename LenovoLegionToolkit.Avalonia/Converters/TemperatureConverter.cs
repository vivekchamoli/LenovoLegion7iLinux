using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class TemperatureConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double celsius)
            {
                var unit = parameter?.ToString()?.ToLower() ?? "c";

                switch (unit)
                {
                    case "f":
                    case "fahrenheit":
                        var fahrenheit = celsius * 9 / 5 + 32;
                        return $"{fahrenheit:F1}°F";
                    case "k":
                    case "kelvin":
                        var kelvin = celsius + 273.15;
                        return $"{kelvin:F1}K";
                    default:
                        return $"{celsius:F1}°C";
                }
            }
            return "--°C";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}