using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace BebeRadio.Controls;

/// <summary>
/// Panel de grilla uniforme: N columnas de igual ancho estirado, filas según
/// items. Los hijos ocupan toda la celda (alineación Stretch), por lo que la
/// parrilla llena el ancho disponible sin scroll ni huecos laterales. Cuando el
/// alto disponible es finito, las filas lo reparten en partes iguales (1fr);
/// con alto infinito quedan al alto natural del contenido. El reparto usa
/// bordes enteros compartidos entre celdas vecinas, así no quedan costuras
/// de 1px entre filas aunque la división del alto sea fraccionaria
/// (pantallas grandes o escalado DPI).
/// </summary>
public sealed class UniformGridPanel : Panel
{
    /// <summary>Número de columnas (defecto 5).</summary>
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns), typeof(int), typeof(UniformGridPanel),
            new PropertyMetadata(5));

    /// <summary>Obtiene o establece las columnas.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Mide hijos con el ancho de celda y calcula alto total.</summary>
    /// <param name="availableSize">Espacio disponible.</param>
    /// <returns>Tamaño deseado (ancho total, alto por filas).</returns>
    /// <remarks>
    /// Con ancho infinito (scroll), la celda es el hijo más ancho.
    /// Con alto finito (modo operador), las filas se reparten el alto
    /// disponible en partes iguales (1fr) y se devuelve exactamente ese alto,
    /// para que ninguna fila se corte. Con alto infinito (modo normal en
    /// StackPanel), las filas quedan al alto natural del contenido.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);

        if (double.IsInfinity(availableSize.Width) || availableSize.Width <= 0)
        {
            return MeasureUnbounded(columns);
        }

        var cellWidth = availableSize.Width / columns;
        var rows = Math.Max(1, (Children.Count + columns - 1) / columns);

        if (double.IsInfinity(availableSize.Height) || availableSize.Height <= 0)
        {
            var naturalRowHeight = 0.0;
            foreach (var child in Children)
            {
                child.Measure(new Size(cellWidth, availableSize.Height));
                naturalRowHeight = Math.Max(naturalRowHeight, child.DesiredSize.Height);
            }

            return new Size(availableSize.Width, naturalRowHeight * rows);
        }

        var rowHeight = availableSize.Height / rows;
        foreach (var child in Children)
        {
            child.Measure(new Size(cellWidth, rowHeight));
        }

        return new Size(availableSize.Width, availableSize.Height);
    }

    /// <summary>Mide sin ancho límite: cada celda adopta el hijo más ancho.</summary>
    /// <param name="columns">Número de columnas.</param>
    /// <returns>Tamaño deseado total.</returns>
    private Size MeasureUnbounded(int columns)
    {
        var cellWidth = 0.0;
        var rowHeight = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            cellWidth = Math.Max(cellWidth, child.DesiredSize.Width);
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        var rows = (Children.Count + columns - 1) / columns;
        return new Size(cellWidth * columns, rowHeight * rows);
    }

    /// <summary>Coloca cada hijo estirado en su celda, sin costuras.</summary>
    /// <param name="finalSize">Tamaño final asignado.</param>
    /// <returns>Tamaño final.</returns>
    /// <remarks>
    /// Los bordes de celda se calculan de forma acumulativa con
    /// <see cref="Math.Floor(double)"/> sobre el tamaño final real, de modo
    /// que dos celdas vecinas comparten exactamente el mismo borde (la última
    /// fila/columna absorbe el píxel sobrante). Al ser valores idénticos, el
    /// redondeo de layout o del escalado DPI encaja ambas celdas por igual y
    /// no se abren líneas de fondo entre filas cuando la división del alto es
    /// fraccionaria. No depende del alto medido en caché: si el tamaño final
    /// difiere del medido, la grilla igual cubre el 100 % del espacio.
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var rows = Math.Max(1, (Children.Count + columns - 1) / columns);

        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var left = Math.Floor(column * finalSize.Width / columns);
            var right = Math.Floor((column + 1) * finalSize.Width / columns);
            var top = Math.Floor(row * finalSize.Height / rows);
            var bottom = Math.Floor((row + 1) * finalSize.Height / rows);
            Children[i].Arrange(new Rect(left, top, right - left, bottom - top));
        }

        return finalSize;
    }
}
