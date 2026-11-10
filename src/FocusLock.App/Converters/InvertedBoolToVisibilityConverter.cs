using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FocusLock.App.Converters;

/// <summary>Visible when false, collapsed when true. Used for empty-state text.</summary>
public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
