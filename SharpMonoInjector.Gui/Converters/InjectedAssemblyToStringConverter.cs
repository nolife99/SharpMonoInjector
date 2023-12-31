using System;
using System.Globalization;
using System.Windows.Data;
using SharpMonoInjector.Gui.Models;

namespace SharpMonoInjector.Gui.Converters;

public class InjectedAssemblyToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return null;

        var asm = (InjectedAssembly)value;
        return $"[{(asm.Is64Bit ? $"0x{asm.Address.ToInt64():X16}" : $"0x{asm.Address.ToInt32():X8}")}] {asm.Name}";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}