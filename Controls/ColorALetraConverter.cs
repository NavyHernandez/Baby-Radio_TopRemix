using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls;

/// <summary>
/// Letras adaptativas sobre fondos de color: recibe la clave de color
/// (token o #hex), calcula su luminancia relativa y devuelve tinta oscura
/// (<c>LightInk</c>) si el fondo es claro o tinta clara (<c>TextPrimary</c>)
/// si es oscuro. Uso: labels de botones de categoría a color.
/// Función de vista pura (sin estado).
/// </summary>
public sealed class ColorALetraConverter : IValueConverter
{
    /// <summary>Umbral de luminancia para tinta oscura (0-1).</summary>
    public double UmbralClaro { get; set; } = 0.55;

    private static readonly CategoryBrushConverter _base = new();

    /// <summary>Indica si una clave de color es clara (letras oscuras).</summary>
    /// <param name="clave">Token o #hex (null = oscuro).</param>
    /// <param name="umbral">Umbral de luminancia (defecto 0.55).</param>
    /// <returns>True si el fondo es claro.</returns>
    public static bool EsColorClaro(string? clave, double umbral = 0.55)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return false;
        }

        var brocha = _base.Convert(clave, typeof(Brush), new object(), string.Empty) as SolidColorBrush;
        return brocha is not null && Luminancia(brocha.Color) >= umbral;
    }

    /// <summary>Convierte clave de color → brush de letra.</summary>
    /// <param name="value">Clave string (token o #hex).</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>Tinta adaptativa (nunca null).</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var recursos = Application.Current?.Resources;
        var clara = recursos?["TextPrimary"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);
        var oscura = recursos?["LightInk"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Black);

        var brocha = _base.Convert(value, targetType, parameter, language) as SolidColorBrush;
        if (brocha is null)
        {
            return clara;
        }

        return Luminancia(brocha.Color) >= UmbralClaro ? oscura : clara;
    }

    /// <summary>Luminancia relativa sRGB (0 = negro, 1 = blanco).</summary>
    /// <param name="color">Color a medir.</param>
    /// <returns>Luminancia.</returns>
    private static double Luminancia(Windows.UI.Color color)
    {
        static double Canal(byte canal)
        {
            var lineal = canal / 255.0;
            return lineal <= 0.03928 ? lineal / 12.92 : Math.Pow((lineal + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(color.R) + 0.7152 * Canal(color.G) + 0.0722 * Canal(color.B);
    }

    /// <summary>No soportada.</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("ColorALetraConverter es de solo lectura.");
}
