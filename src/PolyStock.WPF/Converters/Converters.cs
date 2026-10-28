using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PolyStock.WPF.Converters;

public class EqualityToBoolConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value?.ToString(), parameter?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? parameter?.ToString() ?? Binding.DoNothing : Binding.DoNothing;
    }

    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return false;
        return Equals(values[0]?.ToString(), values[1]?.ToString());
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class EqualityToVisibilityConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return Visibility.Collapsed;
        if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return Visibility.Collapsed;
        return Equals(values[0]?.ToString(), values[1]?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"₹ {d:N2}";
        }
        if (value is decimal m)
        {
            return $"₹ {m:N2}";
        }
        return "₹ 0.00";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class WeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d:N2} kg";
        }
        return "0.00 kg";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}
