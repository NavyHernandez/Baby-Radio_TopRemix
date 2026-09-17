using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls;

/// <summary>
/// Convierte una clave de brush de categoría (p. ej. "CategoryMusic") al brush
/// de <c>Themes/ColorPalette.xaml</c>, o un hex directo ("#RRGGBB") de
/// categorías personalizadas. Uso: franjas, dots e iconos por categoría
/// en plantillas con <c>{Binding}</c>. Función de vista pura y reutilizable.
/// </summary>
public sealed class CategoryBrushConverter : IValueConverter
{
    /// <summary>Convierte clave → brush.</summary>
    /// <param name="value">Clave string del recurso.</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>El brush, o null si la clave no existe (franja transparente).</returns>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (Microsoft.UI.Xaml.Application.Current?.Resources.TryGetValue(key, out var resource) == true
            && resource is Brush brush)
        {
            return brush;
        }

        if (TryParseHex(key, out var color))
        {
            return new SolidColorBrush(color);
        }

        return null;
    }

    /// <summary>Interpreta un hex #RRGGBB (con o sin #).</summary>
    /// <param name="key">Texto a interpretar.</param>
    /// <param name="color">Color resultante.</param>
    /// <returns>True si es un hex válido.</returns>
    private static bool TryParseHex(string key, out Windows.UI.Color color)
    {
        color = default;
        var hex = key.StartsWith("#") ? key[1..] : key;
        if (hex.Length != 6 && hex.Length != 8)
        {
            return false;
        }

        try
        {
            var offset = 0;
            byte alpha = 255;
            if (hex.Length == 8)
            {
                alpha = System.Convert.ToByte(hex.Substring(0, 2), 16);
                offset = 2;
            }

            color = Windows.UI.Color.FromArgb(
                alpha,
                System.Convert.ToByte(hex.Substring(offset, 2), 16),
                System.Convert.ToByte(hex.Substring(offset + 2, 2), 16),
                System.Convert.ToByte(hex.Substring(offset + 4, 2), 16));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>No soportada (binding de vista de solo lectura).</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("CategoryBrushConverter es de solo lectura.");
}
