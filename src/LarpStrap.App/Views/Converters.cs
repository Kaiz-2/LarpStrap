using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LarpStrap.App.Views;

/// <summary>
/// Option buttons: checked when the bound value equals the parameter; checking one writes the parameter back,
/// so mouse, keyboard and UI Automation (screen readers) all apply the choice the same way.
/// </summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(System.Convert.ToString(value, CultureInfo.InvariantCulture), System.Convert.ToString(parameter, CultureInfo.InvariantCulture), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        var text = System.Convert.ToString(parameter, CultureInfo.InvariantCulture)!;
        return targetType.IsEnum ? Enum.Parse(targetType, text) : System.Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
