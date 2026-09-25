using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace software_ACEMAR.Views
{
    public static class Converters
    {
        public static readonly IValueConverter BooleanToVisibility = new BooleanToVisibilityConverterInternal(false);
        public static readonly IValueConverter InverseBooleanToVisibility = new BooleanToVisibilityConverterInternal(true);
        public static readonly IValueConverter ActiveTabBackgroundConverter = new ActiveTabBackgroundConverter();
        public static readonly IValueConverter ActiveTabBorderConverter = new ActiveTabBorderConverter();
        public static readonly IValueConverter ActiveTabForegroundConverter = new ActiveTabForegroundConverter();
        public static readonly IValueConverter ActiveTabIconForegroundConverter = new ActiveTabIconForegroundConverter();
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

    public class ActiveTabBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;
            return isActive 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5D23"))  // Verde Militar Activo Destacado
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#293416"));  // Fondo Oscuro de Botón Inactivo
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveTabBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;
            return isActive 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A4C868"))  // Borde Brillante Amarillo-Verde en Tab Activa
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4D23"));  // Borde sutil para Inactivos
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveTabForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;
            // Garantizar texto 100% BLANCO / BLANCO CREMA ALTO CONTRASTE (Ratio > 10:1)
            return isActive 
                ? new SolidColorBrush(Colors.White) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F8EC"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveTabIconForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = value is bool b && b;
            return isActive 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBE3A6"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
