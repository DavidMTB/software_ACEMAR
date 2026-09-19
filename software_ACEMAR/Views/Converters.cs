using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace software_ACEMAR.Views
{
    public static class Converters
    {
        public static readonly IValueConverter BooleanToVisibility = new BooleanToVisibilityConverterInternal(false);
        public static readonly IValueConverter InverseBooleanToVisibility = new BooleanToVisibilityConverterInternal(true);
    }

    public class BooleanToVisibilityConverterInternal : IValueConverter
    {
        private readonly bool _inverse;

        public BooleanToVisibilityConverterInternal(bool inverse)
        {
            _inverse = inverse;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolVal = value is bool b && b;
            if (_inverse) boolVal = !boolVal;
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool b = v == Visibility.Visible;
                return _inverse ? !b : b;
            }
            return false;
        }
    }
}
