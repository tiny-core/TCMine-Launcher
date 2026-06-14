using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TCMine_Launcher.Converters;

/// <summary>
///     Converte uma string de cor hex (ex.: "#22C55E") num <see cref="IBrush" />.
///     Permite manter cores de estado como strings nas ViewModels (sem referenciar
///     tipos de Media), resolvendo-as no binding da View.
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && Color.TryParse(hex, out var color))
            return new SolidColorBrush(color);
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}