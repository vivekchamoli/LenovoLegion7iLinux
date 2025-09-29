using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class RpmToPercentConverter : IValueConverter
    {
        public static readonly RpmToPercentConverter Instance = new();

        private const int MaxRpm = 5000; // Typical max fan speed

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int rpm)
            {
                var percentage = (rpm / (double)MaxRpm) * 100;
                return Math.Min(100, Math.Max(0, (int)percentage));
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int percentage)
            {
                var rpm = (percentage / 100.0) * MaxRpm;
                return (int)rpm;
            }
            return 0;
        }
    }
}