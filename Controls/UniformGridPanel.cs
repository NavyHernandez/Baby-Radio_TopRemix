using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace BebeRadio.Controls;

/// <summary>
/// Panel de grilla uniforme: N columnas de igual ancho estirado, filas según
/// items. Los hijos ocupan toda la celda (alineación Stretch), por lo que la
/// parrilla llena el ancho disponible sin scroll ni huecos laterales. Cuando el
/// alto disponible es finito, las filas lo reparten en partes iguales (1fr);
/// con <see cref="MaxItems"/> y menos items de los que caben en una página
/// completa, la fila mide lo mismo que en página completa y el sobrante queda
/// libre al fondo (la paleta pone su marca de agua ahí). El reparto usa
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

    /// <summary>Items por página completa (0 = sin tope: filas reparten todo el alto, como siempre).</summary>
    /// <remarks>
    /// Con tope, si la página viene parcial (menos filas de las de página
    /// completa), la fila adopta la altura de página completa y el hueco
    /// sobrante queda libre al fondo del panel (su <c>Background</c> no lo pinta).
    /// </remarks>
    public static readonly DependencyProperty MaxItemsProperty =
        DependencyProperty.Register(
            nameof(MaxItems), typeof(int), typeof(UniformGridPanel),
            new PropertyMetadata(0));

    /// <summary>Obtiene o establece el tope de items por página completa.</summary>
    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    /// <summary>Calcula las filas de layout (>= filas de hijos; página completa si hay tope).</summary>
    /// <param name="rowsHijos">Filas que ocupan los hijos.</param>
    /// <param name="columns">Columnas.</param>
    /// <returns>Filas con las que se reparte el alto.</returns>
    private int FilasDeLayout(int rowsHijos, int columns)
    {
        var maxItems = MaxItems;
        if (maxItems <= 0)
        {
            return rowsHijos;
        }

        var filasPagina = Math.Max(1, (maxItems + columns - 1) / columns);
        return Math.Max(rowsHijos, filasPagina);
    }

    /// <summary>Mide hijos con el ancho de celda y calcula alto total.</summary>
    /// <param name="availableSize">Espacio disponible.</param>
    /// <returns>Tamaño deseado (ancho total, alto usado por las filas de hijos).</returns>
    /// <remarks>
    /// Con ancho infinito (scroll), la celda es el hijo más ancho.
    /// Con alto finito (modo normal/operador), las filas se reparten el alto
    /// disponible en partes iguales (1fr) respetando <see cref="MaxItems"/> y se
    /// devuelve el alto realmente usado, para que el sobrante quede libre.
    /// Con alto infinito, las filas quedan al alto natural del contenido.
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

        var rowHeight = availableSize.Height / FilasDeLayout(rows, columns);
        foreach (var child in Children)
        {
            child.Measure(new Size(cellWidth, rowHeight));
        }

        return new Size(availableSize.Width, rowHeight * rows);
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
    /// <returns>Alto realmente usado (ancho final); el sobrante queda libre.</returns>
    /// <remarks>
    /// Los bordes de celda se calculan de forma acumulativa con
    /// <see cref="Math.Floor(double)"/> sobre el tamaño final real, de modo
    /// que dos celdas vecinas comparten exactamente el mismo borde (la última
    /// fila/columna absorbe el píxel sobrante). Al ser valores idénticos, el
    /// redondeo de layout o del escalado DPI encaja ambas celdas por igual y
    /// no se abren líneas de fondo entre filas cuando la división del alto es
    /// fraccionaria. Con <see cref="MaxItems"/> y página parcial, las filas
    /// conservan la altura de página completa y el panel devuelve solo el alto
    /// usado: su <c>Background</c> no pinta el hueco inferior.
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var rows = Math.Max(1, (Children.Count + columns - 1) / columns);
        var rowHeight = finalSize.Height / FilasDeLayout(rows, columns);

        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var left = Math.Floor(column * finalSize.Width / columns);
            var right = Math.Floor((column + 1) * finalSize.Width / columns);
            var top = Math.Floor(row * rowHeight);
            var bottom = Math.Floor((row + 1) * rowHeight);
            Children[i].Arrange(new Rect(left, top, right - left, bottom - top));
        }

        return new Size(finalSize.Width, rowHeight * rows);
    }
}
