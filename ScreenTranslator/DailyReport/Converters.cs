using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScreenTranslator.DailyReport;

/// <summary>true → Collapsed, false → Visible (ngược với BooleanToVisibilityConverter).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}
