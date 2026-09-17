using Microsoft.UI.Xaml.Data;

namespace BebeRadio.Controls;

/// <summary>
/// Booleano → opacidad (seleccionado 1.0, resto atenuado). Uso: estado visual
/// de categorías primarias/laterales y badge EN VIVO de la cola.
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    /// <summary>Opacidad cuando el valor es false (defecto 0.55).</summary>
    public double DimOpacity { get; set; } = 0.55;

    /// <summary>Convierte bool → opacidad.</summary>
    /// <param name="value">Booleano.</param>
    /// <param name="targetType">Tipo destino (ignorado).</param>
    /// <param name="parameter">Parámetro (ignorado).</param>
    /// <param name="language">Idioma (ignorado).</param>
    /// <returns>1.0 si true, <see cref="DimOpacity"/> si no.</returns>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 1.0 : DimOpacity;

    /// <summary>No soportada.</summary>
    /// <param name="value">Valor.</param>
    /// <param name="targetType">Tipo destino.</param>
    /// <param name="parameter">Parámetro.</param>
    /// <param name="language">Idioma.</param>
    /// <returns>Nunca retorna.</returns>
    /// <exception cref="NotSupportedException">Siempre.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("BoolToOpacityConverter es de solo lectura.");
}
