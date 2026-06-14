using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TCMine_Launcher.Converters;

/// <summary>
///     Mapeia o glifo de estado do botão "Jogar" (■ a correr / ▶ jogar / ⬇ instalar),
///     exposto como string pelo ViewModel, para a geometria vetorial correspondente.
///     Mantém o ViewModel livre de tipos da UI (continua a devolver uma string).
/// </summary>
public class PlayGlyphToGeometryConverter : IValueConverter
{
    public static readonly PlayGlyphToGeometryConverter Instance = new();

    private static readonly Geometry Play = Geometry.Parse("M8 5v14l11-7z");
    private static readonly Geometry Stop = Geometry.Parse("M6 6h12v12H6z");
    private static readonly Geometry Download = Geometry.Parse("M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            "■" => Stop,
            "⬇" => Download,
            _ => Play
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}