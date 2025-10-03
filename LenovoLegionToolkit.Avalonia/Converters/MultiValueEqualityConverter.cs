using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class MultiValueEqualityConverter : IMultiValueConverter
    {
        public static readonly MultiValueEqualityConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            var first = values[0];
            var second = values[1];

            if (first == null && second == null)
                return "Active";

            if (first == null || second == null)
                return null;

            return first.Equals(second) ? "Active" : (object?)null;
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}