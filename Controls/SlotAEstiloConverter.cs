using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace BebeRadio.Controls;

/// <summary>
/// Estilo del cart según contenido: con audio usa el botón macizo 3D
/// (el color personalizado prevalece) y vacío el aluminio esqueleto.
/// Binding por <c>TieneAudio</c> (refresca solo). Uso: parrilla de paleta.
/// </summary>
public sealed class SlotAEstiloConverter : IValueConverter
{
    /// <summary>Convierte tiene-audio → estilo del cart.</summary>
    /// <param name="value">Booleano.</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>Estilo macizo o claro (nunca null si existen).</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var recursos = Application.Current?.Resources;
        var claves = value is true
            ? new[] { "BebeCategoryButtonStyle", "BebeCartButtonStyle" }
            : new[] { "BebeCartButtonStyleClaro", "BebeCartButtonStyle" };
        if (recursos is not null)
        {
            foreach (var clave in claves)
            {
                if (recursos.TryGetValue(clave, out var estilo) && estilo is Style style)
                {
                    return style;
                }
            }
        }

        return new Style();
    }

    /// <summary>No soportada.</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("SlotAEstiloConverter es de solo lectura.");
}
