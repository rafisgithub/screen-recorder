using System.Globalization;
using System.Windows.Data;

namespace ScreenRecorder.App.Converters;

/// <summary>Negates a boolean for one-way or two-way bindings.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
