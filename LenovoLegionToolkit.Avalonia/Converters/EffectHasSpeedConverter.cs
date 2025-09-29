using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LenovoLegionToolkit.Avalonia.Models;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class EffectHasSpeedConverter : IValueConverter
    {
        public static readonly EffectHasSpeedConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RgbKeyboardEffect effect)
            {
                // Static effect doesn't have speed control
                return effect != RgbKeyboardEffect.Static;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}