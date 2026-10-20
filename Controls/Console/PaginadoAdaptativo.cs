using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Comportamiento adjunto que adapta cuántas filas muestra la paleta según el
/// ALTO real disponible: 5 filas (25 carts) en pantallas normales y 6 filas
/// (30 carts) en pantallas grandes, con carts siempre cómodos de tocar.
/// </summary>
/// <remarks>
/// <para>
/// Uso (en la raíz de <c>PaletaPanel.xaml</c>):
/// <c>support:PaginadoAdaptativo.ViewModel="{x:Bind ViewModel, Mode=OneWay}"</c>.
/// Al asignar el ViewModel se engancha <see cref="FrameworkElement.SizeChanged"/>
/// del elemento y, en cada cambio de tamaño, se llama a
/// <see cref="PaletaViewModel.AjustarTamanoPagina"/>.
/// </para>
/// <para>
/// Solo para la paleta del modo normal: el modo operador fija su propio tamaño
/// (30) y NO debe usar este comportamiento.
/// </para>
/// </remarks>
public static class PaginadoAdaptativo
{
    /// <summary>Columnas de la paleta (deben coincidir con <c>UniformGridPanel.Columns</c>).</summary>
    public const int Columnas = 5;

    /// <summary>Filas en pantallas normales (25 carts por página).</summary>
    public const int FilasNormal = 5;

    /// <summary>Filas en pantallas grandes (30 carts por página).</summary>
    public const int FilasGrande = 6;

    /// <summary>
    /// Alto mínimo por cart (px) para PASAR a 6 filas. Con 6 filas de 78 px el
    /// título, el tiempo y el icono de slot vacío respiran sin apretarse.
    /// </summary>
    public const double AltoCartParaSubir = 78;

    /// <summary>
    /// Alto por cart (px) por debajo del cual se VUELVE a 5 filas. Es menor que
    /// <see cref="AltoCartParaSubir"/> a propósito (histéresis): evita que la
    /// paleta parpadee entre 25 y 30 al redimensionar justo en el umbral.
    /// </summary>
    public const double AltoCartParaBajar = 70;

    /// <summary>ViewModel de paleta a adaptar (propiedad adjunta).</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.RegisterAttached(
            "ViewModel",
            typeof(PaletaViewModel),
            typeof(PaginadoAdaptativo),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>Lee el ViewModel adjunto.</summary>
    /// <param name="objeto">Elemento que lo porta.</param>
    /// <returns>El ViewModel o null.</returns>
    public static PaletaViewModel? GetViewModel(DependencyObject objeto) =>
        (PaletaViewModel?)objeto.GetValue(ViewModelProperty);

    /// <summary>Asigna el ViewModel adjunto (activa el comportamiento).</summary>
    /// <param name="objeto">Elemento que lo porta.</param>
    /// <param name="valor">ViewModel de la paleta.</param>
    public static void SetViewModel(DependencyObject objeto, PaletaViewModel? valor) =>
        objeto.SetValue(ViewModelProperty, valor);

    /// <summary>
    /// Función pura: decide el tamaño de página según el alto disponible.
    /// </summary>
    /// <param name="altoDisponible">Alto de la paleta en px (0 si aún no se mide).</param>
    /// <param name="tamanoActual">Tamaño de página vigente (para la histéresis).</param>
    /// <returns>25 (5 filas) o 30 (6 filas); el actual si aún no hay medida.</returns>
    public static int CalcularTamanoPagina(double altoDisponible, int tamanoActual)
    {
        // Sin medida válida (primer layout): no tocar nada.
        if (altoDisponible <= 0)
        {
            return tamanoActual;
        }

        var esGrande = tamanoActual == FilasGrande * Columnas;
        var umbral = esGrande ? AltoCartParaBajar : AltoCartParaSubir;

        // Alto que tendría cada cart si se repartiera en 6 filas.
        var altoCartConSeisFilas = altoDisponible / FilasGrande;
        return altoCartConSeisFilas >= umbral
            ? FilasGrande * Columnas
            : FilasNormal * Columnas;
    }

    /// <summary>Engancha o suelta el evento de tamaño según haya ViewModel.</summary>
    /// <param name="d">Elemento (debe ser FrameworkElement).</param>
    /// <param name="e">Valor viejo y nuevo.</param>
    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement elemento)
        {
            return;
        }

        // Quitar primero evita suscripciones duplicadas si cambia el VM.
        elemento.SizeChanged -= OnSizeChanged;
        if (e.NewValue is PaletaViewModel)
        {
            elemento.SizeChanged += OnSizeChanged;
            Aplicar(elemento);
        }
    }

    /// <summary>Reevalúa las filas cada vez que cambia el tamaño del elemento.</summary>
    /// <param name="sender">Elemento observado.</param>
    /// <param name="e">Tamaños nuevo y anterior.</param>
    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement elemento)
        {
            Aplicar(elemento);
        }
    }

    /// <summary>Calcula el tamaño de página y lo aplica al ViewModel.</summary>
    /// <param name="elemento">Raíz de la paleta (su alto es el alto disponible).</param>
    private static void Aplicar(FrameworkElement elemento)
    {
        if (GetViewModel(elemento) is not { } vm)
        {
            return;
        }

        vm.AjustarTamanoPagina(CalcularTamanoPagina(elemento.ActualHeight, vm.TamanoPagina));
    }
}
