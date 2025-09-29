using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class EnumToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || !value.GetType().IsEnum)
                return string.Empty;

            var enumValue = value as Enum;
            var field = enumValue?.GetType().GetField(enumValue.ToString());
            var descriptionAttribute = field?.GetCustomAttribute<DescriptionAttribute>();

            return descriptionAttribute?.Description ?? enumValue?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && targetType.IsEnum)
            {
                foreach (var field in targetType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>();
                    if (descriptionAttribute?.Description == stringValue || field.Name == stringValue)
                    {
                        return field.GetValue(null);
                    }
                }
            }
            return null;
        }
    }
}