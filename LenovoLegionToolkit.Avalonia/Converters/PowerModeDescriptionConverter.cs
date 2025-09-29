using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class PowerModeDescriptionConverter : IValueConverter
    {
        public static readonly PowerModeDescriptionConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PowerMode mode)
            {
                return mode switch
                {
                    PowerMode.Quiet => "Low power, silent operation",
                    PowerMode.Balanced => "Balanced performance and efficiency",
                    PowerMode.Performance => "Maximum performance",
                    PowerMode.Custom => "User-defined settings",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}