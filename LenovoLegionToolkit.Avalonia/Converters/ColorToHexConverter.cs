using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class ColorToHexConverter : IValueConverter
    {
        public static readonly ColorToHexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#808080";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex)
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    try
                    {
                        var r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        return Color.FromRgb(r, g, b);
                    }
                    catch
                    {
                        return Colors.Gray;
                    }
                }
            }
            return Colors.Gray;
        }
    }
}