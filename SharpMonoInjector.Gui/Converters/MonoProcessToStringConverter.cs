using System;
using System.Globalization;
using System.Windows.Data;
using SharpMonoInjector.Gui.Models;

namespace SharpMonoInjector.Gui.Converters;

public readonly struct MonoProcessToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || value.Equals("")) return null;

        var proc = (MonoProcess)value;
        return $"[{proc.Id.ToString(culture)}] {proc.Name}";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}