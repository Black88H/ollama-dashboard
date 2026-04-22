using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OllamaDashboard.Models;

namespace OllamaDashboard.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inverted = value is bool b ? !b : true;

        // Allow usage as both bool→bool and bool→Visibility by checking parameter.
        if (targetType == typeof(Visibility) ||
            string.Equals(parameter as string, "toVis", StringComparison.OrdinalIgnoreCase))
        {
            return inverted ? Visibility.Visible : Visibility.Collapsed;
        }
        return inverted;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}

public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null &&
                       !(value is string s && string.IsNullOrEmpty(s));

        if (targetType == typeof(Visibility) ||
            string.Equals(parameter as string, "toVis", StringComparison.OrdinalIgnoreCase))
        {
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return hasValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ChatRoleToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatRole role)
        {
            return role switch
            {
                ChatRole.User      => Application.Current.Resources["UserBubbleBrush"]!,
                ChatRole.Assistant => Application.Current.Resources["AssistantBubbleBrush"]!,
                ChatRole.System    => Application.Current.Resources["SurfaceBrush"]!,
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class ChatRoleToAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatRole role)
        {
            return role switch
            {
                ChatRole.User      => HorizontalAlignment.Right,
                ChatRole.Assistant => HorizontalAlignment.Left,
                ChatRole.System    => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Left
            };
        }
        return HorizontalAlignment.Left;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>Formats a byte count as human-readable string (e.g. "4.7 GB").</summary>
public sealed class BytesToSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        double size;
        try { size = System.Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        catch { return string.Empty; }

        int unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {Units[unit]}";
    }

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>MultiBinding converter: returns true iff both string inputs are equal (case-insensitive).</summary>
public sealed class StringEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return false;
        var a = values[0]?.ToString() ?? string.Empty;
        var b = values[1]?.ToString() ?? string.Empty;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
