using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TCMine_Launcher.Converters;

/// <summary>
///     MultiBinding [id, activeId] → cor da borda: laranja se a instância é a ativa,
///     senão a cor neutra do cartão. Usada para realçar o cartão selecionado.
/// </summary>
public class ActiveBorderConverter : IMultiValueConverter
{
    public static readonly ActiveBorderConverter Instance = new();

    private static readonly IBrush Active = new SolidColorBrush(Color.Parse("#F97316"));
    private static readonly IBrush Idle = new SolidColorBrush(Color.Parse("#1A1A2A"));

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Count == 2 && values[0] is string a && values[1] is string b && a == b
            ? Active
            : Idle;
    }
}