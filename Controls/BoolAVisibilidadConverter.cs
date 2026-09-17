using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace BebeRadio.Controls;

/// <summary>
/// Booleano → visibilidad (true = visible). Con <see cref="Invertir"/> en
/// true se invierte (útil para mostrar guías de slots vacíos: icono +,
/// aviso "Vacío"). Uso: fantasmas elegantes de paleta y riel.
/// </summary>
public sealed class BoolAVisibilidadConverter : IValueConverter
{
    /// <summary>True para mostrar cuando el valor es false.</summary>
    public bool Invertir { get; set; }

    /// <summary>Convierte bool → visibilidad (respeta <see cref="Invertir"/>).</summary>
    /// <param name="value">Booleano.</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>Visible o colapsado.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var activo = value is true;
        if (Invertir)
        {
            activo = !activo;
        }

        return activo ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>No soportada.</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("BoolAVisibilidadConverter es de solo lectura.");
}
