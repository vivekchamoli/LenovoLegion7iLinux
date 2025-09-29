using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace LenovoLegionToolkit.Avalonia.Converters
{
    public class NavigationItemSelectedConverter : IMultiValueConverter
    {
        public static readonly NavigationItemSelectedConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count != 2)
                return null;

            var currentItem = values[0];
            var selectedItem = values[1];

            if (currentItem == null || selectedItem == null)
                return null;

            return currentItem.Equals(selectedItem) ? "Selected" : null;
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}