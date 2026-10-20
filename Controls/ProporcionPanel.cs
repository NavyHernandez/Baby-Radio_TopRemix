using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace BebeRadio.Controls;

/// <summary>
/// Panel de UN solo hijo cuyo ALTO se deriva del ANCHO disponible, de modo que
/// cada celda de una grilla interna conserve siempre la misma proporción
/// (p. ej. botones verticales 1 : 1.25) en cualquier pantalla.
/// </summary>
/// <remarks>
/// <para>
/// Fórmula: <c>alto = (ancho / Columnas) × RelacionAlto</c>, limitado por
/// <see cref="AltoMaximo"/> y por <see cref="FraccionAltoVentana"/> del alto de
/// la ventana (para que en monitores ultra anchos la tira no se coma la
/// pantalla), y nunca menor que <see cref="AltoMinimo"/> (piso que evita
/// recortar contenido en ventanas bajas).
/// </para>
/// <para>
/// Uso: colocar en una fila <c>Auto</c> del Grid padre; el panel dicta el alto
/// y el hijo (que debe estirarse) llena ese rectángulo.
/// Precondición: usa el primer hijo; los demás se ignoran.
/// </para>
/// </remarks>
public sealed class ProporcionPanel : Panel
{
    /// <summary>Raíz XAML a la que estamos suscritos (alto de ventana); null si no cargado.</summary>
    private XamlRoot? _raiz;

    /// <summary>
    /// Crea el panel y engancha el ciclo de vida para reaccionar a cambios de
    /// alto de la ventana (que no alteran el ancho y por tanto no re-miden solos).
    /// </summary>
    public ProporcionPanel()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Número de columnas de la grilla interna (cada celda mide ancho/Columnas).</summary>
    public int Columnas
    {
        get => (int)GetValue(ColumnasProperty);
        set => SetValue(ColumnasProperty, value);
    }

    /// <summary>Propiedad de dependencia de <see cref="Columnas"/> (por defecto 1).</summary>
    public static readonly DependencyProperty ColumnasProperty =
        DependencyProperty.Register(nameof(Columnas), typeof(int), typeof(ProporcionPanel),
            new PropertyMetadata(1, OnMedidaCambiada));

    /// <summary>Relación alto/ancho de cada celda (1.25 = 25 % más alta que ancha → rectángulo vertical).</summary>
    public double RelacionAlto
    {
        get => (double)GetValue(RelacionAltoProperty);
        set => SetValue(RelacionAltoProperty, value);
    }

    /// <summary>Propiedad de dependencia de <see cref="RelacionAlto"/> (por defecto 1 = cuadrado).</summary>
    public static readonly DependencyProperty RelacionAltoProperty =
        DependencyProperty.Register(nameof(RelacionAlto), typeof(double), typeof(ProporcionPanel),
            new PropertyMetadata(1.0, OnMedidaCambiada));

    /// <summary>Piso de alto en px: el panel nunca es más bajo que esto.</summary>
    public double AltoMinimo
    {
        get => (double)GetValue(AltoMinimoProperty);
        set => SetValue(AltoMinimoProperty, value);
    }

    /// <summary>Propiedad de dependencia de <see cref="AltoMinimo"/> (por defecto 0).</summary>
    public static readonly DependencyProperty AltoMinimoProperty =
        DependencyProperty.Register(nameof(AltoMinimo), typeof(double), typeof(ProporcionPanel),
            new PropertyMetadata(0.0, OnMedidaCambiada));

    /// <summary>Tope absoluto de alto en px (por defecto sin tope).</summary>
    public double AltoMaximo
    {
        get => (double)GetValue(AltoMaximoProperty);
        set => SetValue(AltoMaximoProperty, value);
    }

    /// <summary>Propiedad de dependencia de <see cref="AltoMaximo"/> (por defecto infinito).</summary>
    public static readonly DependencyProperty AltoMaximoProperty =
        DependencyProperty.Register(nameof(AltoMaximo), typeof(double), typeof(ProporcionPanel),
            new PropertyMetadata(double.PositiveInfinity, OnMedidaCambiada));

    /// <summary>
    /// Tope relativo: fracción (0–1) del alto de la VENTANA que el panel puede
    /// ocupar como máximo (p. ej. 0.28 = 28 %). Con 1 no limita nada.
    /// </summary>
    public double FraccionAltoVentana
    {
        get => (double)GetValue(FraccionAltoVentanaProperty);
        set => SetValue(FraccionAltoVentanaProperty, value);
    }

    /// <summary>Propiedad de dependencia de <see cref="FraccionAltoVentana"/> (por defecto 1).</summary>
    public static readonly DependencyProperty FraccionAltoVentanaProperty =
        DependencyProperty.Register(nameof(FraccionAltoVentana), typeof(double), typeof(ProporcionPanel),
            new PropertyMetadata(1.0, OnMedidaCambiada));

    /// <summary>
    /// Función pura: calcula el alto del panel a partir del ancho y los límites.
    /// </summary>
    /// <param name="ancho">Ancho disponible del panel en px.</param>
    /// <param name="columnas">Columnas de la grilla interna (mínimo 1).</param>
    /// <param name="relacionAlto">Alto/ancho deseado de cada celda.</param>
    /// <param name="altoMinimo">Piso de alto en px (gana sobre cualquier tope).</param>
    /// <param name="altoMaximo">Tope absoluto en px (puede ser infinito).</param>
    /// <param name="altoVentana">Alto de la ventana en px; 0 si aún se desconoce.</param>
    /// <param name="fraccionVentana">Fracción máxima del alto de la ventana (0–1).</param>
    /// <returns>Alto final en px, siempre ≥ <paramref name="altoMinimo"/>.</returns>
    public static double CalcularAlto(
        double ancho, int columnas, double relacionAlto,
        double altoMinimo, double altoMaximo,
        double altoVentana, double fraccionVentana)
    {
        double alto = ancho / Math.Max(1, columnas) * relacionAlto;

        // Tope efectivo: el menor entre el absoluto y el relativo a la ventana.
        double tope = altoMaximo;
        if (altoVentana > 0 && fraccionVentana > 0)
        {
            tope = Math.Min(tope, altoVentana * fraccionVentana);
        }

        return Math.Max(Math.Min(alto, tope), altoMinimo);
    }

    /// <summary>
    /// Mide el hijo con el alto proporcional al ancho recibido.
    /// </summary>
    /// <param name="availableSize">Espacio ofrecido por el padre.</param>
    /// <returns>Tamaño deseado: todo el ancho × alto proporcional.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement? hijo = Children.Count > 0 ? Children[0] : null;
        double ancho = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;

        if (hijo is null)
        {
            return new Size(ancho, 0);
        }

        // Ancho infinito (p. ej. dentro de un ScrollViewer): usar el natural del hijo.
        if (ancho <= 0)
        {
            hijo.Measure(availableSize);
            ancho = hijo.DesiredSize.Width;
        }

        double alto = CalcularAlto(
            ancho, Columnas, RelacionAlto, AltoMinimo, AltoMaximo,
            _raiz?.Size.Height ?? 0, FraccionAltoVentana);

        hijo.Measure(new Size(ancho, alto));
        return new Size(ancho, alto);
    }

    /// <summary>
    /// Coloca el hijo ocupando todo el rectángulo final del panel.
    /// </summary>
    /// <param name="finalSize">Tamaño final asignado por el padre.</param>
    /// <returns>El mismo <paramref name="finalSize"/>.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count > 0)
        {
            Children[0].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        }

        return finalSize;
    }

    /// <summary>Reevalúa el layout cuando cambia cualquier parámetro de proporción.</summary>
    private static void OnMedidaCambiada(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ProporcionPanel)d).InvalidateMeasure();
    }

    /// <summary>Se suscribe a los cambios de tamaño de la ventana (XamlRoot) al cargar.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        _raiz = XamlRoot;
        _raiz.Changed += OnRaizChanged;
        InvalidateMeasure();
    }

    /// <summary>Libera la suscripción a XamlRoot al descargar (evita fugas).</summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_raiz is not null)
        {
            _raiz.Changed -= OnRaizChanged;
            _raiz = null;
        }
    }

    /// <summary>Re-mide cuando cambia el alto de la ventana (el ancho solo no lo detecta).</summary>
    private void OnRaizChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        InvalidateMeasure();
    }
}
