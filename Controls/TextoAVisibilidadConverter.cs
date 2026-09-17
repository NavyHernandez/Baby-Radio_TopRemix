using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace BebeRadio.Controls;

/// <summary>
/// Convierte texto a visibilidad (vacío = colapsado). Para avisos
/// breves que solo ocupan lugar cuando dicen algo.
/// </summary>
public sealed class TextoAVisibilidadConverter : IValueConverter
{
    /// <summary>Convierte texto → visibilidad.</summary>
    /// <param name="value">Texto.</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>Visible si hay texto, colapsado si no.</returns>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string texto && !string.IsNullOrWhiteSpace(texto)
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>No soportada (binding de vista de solo lectura).</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("TextoAVisibilidadConverter es de solo lectura.");
}
