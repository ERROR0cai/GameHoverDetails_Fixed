using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameHoverDetails
{
    /// <summary>Inverts a bool. When the target is <see cref="Visibility"/>, true (after invert) is Visible.</summary>
    public sealed class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var inverted = value is bool flag && !flag;
            if (targetType == typeof(Visibility) || targetType == typeof(Visibility?))
            {
                return inverted ? Visibility.Visible : Visibility.Collapsed;
            }

            return inverted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }

            return value is bool flag && !flag;
        }
    }
}
