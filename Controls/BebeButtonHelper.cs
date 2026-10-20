using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls;

/// <summary>
/// Attached properties de los botones físico 3D estilo consola: color de
/// categoría, variante circular, glow de actividad y geometría de la cara
/// (esquinas, aro y glow confinado) para que los tiles de la tira se peguen
/// sin costuras. La sombra GPU vive en <c>BebeButtonHelper.Sombra.cs</c>.
/// Ver AGENTS.md §3.3 y Themes/ButtonStyles.xaml.
/// </summary>
public static partial class BebeButtonHelper
{
    /// <summary>Color de acento de categoría (tinte + glow en carts).</summary>
    public static readonly DependencyProperty CategoryColorProperty =
        DependencyProperty.RegisterAttached(
            "CategoryColor",
            typeof(Brush),
            typeof(BebeButtonHelper),
            new PropertyMetadata(null));

    /// <summary>Obtiene el color de categoría anexado.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>Brush de acento o null.</returns>
    public static Brush? GetCategoryColor(DependencyObject element) =>
        (Brush?)element.GetValue(CategoryColorProperty);

    /// <summary>Establece el color de categoría anexado.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">Brush de acento.</param>
    public static void SetCategoryColor(DependencyObject element, Brush? value) =>
        element.SetValue(CategoryColorProperty, value);

    /// <summary>Marca si el botón usa variante circular (transporte).</summary>
    public static readonly DependencyProperty IsCircularProperty =
        DependencyProperty.RegisterAttached(
            "IsCircular",
            typeof(bool),
            typeof(BebeButtonHelper),
            new PropertyMetadata(false));

    /// <summary>Obtiene si el botón es circular.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>True si es transporte circular.</returns>
    public static bool GetIsCircular(DependencyObject element) =>
        (bool)element.GetValue(IsCircularProperty);

    /// <summary>Establece la variante circular.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">True para circular.</param>
    public static void SetIsCircular(DependencyObject element, bool value) =>
        element.SetValue(IsCircularProperty, value);

    /// <summary>
    /// Estado de actividad (glow de color persistente en carts). Al cambiar,
    /// navega el grupo <c>ActivityStates</c> del template (Active/Inactive).
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive",
            typeof(bool),
            typeof(BebeButtonHelper),
            new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>Obtiene si el botón está activo (glow visible).</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>True si reproduce/armado.</returns>
    public static bool GetIsActive(DependencyObject element) =>
        (bool)element.GetValue(IsActiveProperty);

    /// <summary>Establece el estado activo (muestra u oculta el glow).</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">True para glow persistente.</param>
    public static void SetIsActive(DependencyObject element, bool value) =>
        element.SetValue(IsActiveProperty, value);

    /// <summary>Propaga el cambio de IsActive al VisualStateManager.</summary>
    /// <param name="target">Botón.</param>
    /// <param name="args">Valor nuevo.</param>
    private static void OnIsActiveChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is Button button && args.NewValue is bool active)
        {
            VisualStateManager.GoToState(button, active ? "Active" : "Inactive", true);
        }
    }

    /// <summary>Esquinas del cuerpo físico (<c>SurfaceBorder</c>) del template.</summary>
    /// <remarks>
    /// Defecto 6 en las cuatro: idéntico al valor fijo de siempre. Los tiles
    /// de la tira declaran <c>6,0,0,6</c> (primero) o <c>0,6,6,0</c> (último)
    /// para que solo redondeen los extremos de la barra.
    /// </remarks>
    public static readonly DependencyProperty SurfaceCornersProperty =
        DependencyProperty.RegisterAttached(
            "SurfaceCorners",
            typeof(CornerRadius),
            typeof(BebeButtonHelper),
            new PropertyMetadata(new CornerRadius(6)));

    /// <summary>Obtiene las esquinas del cuerpo físico.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>CornerRadius vigente (defecto 6).</returns>
    public static CornerRadius GetSurfaceCorners(DependencyObject element) =>
        (CornerRadius)element.GetValue(SurfaceCornersProperty);

    /// <summary>Establece las esquinas del cuerpo físico.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">Esquinas a aplicar.</param>
    public static void SetSurfaceCorners(DependencyObject element, CornerRadius value) =>
        element.SetValue(SurfaceCornersProperty, value);

    /// <summary>Grosor del aro del cuerpo físico (<c>SurfaceBorder.BorderThickness</c>).</summary>
    /// <remarks>
    /// Defecto 2 en los cuatro lados: igual que siempre. En la tira, el lado
    /// derecho de cada tile va a 0 y el izquierdo de su vecino a 1, de modo que
    /// dos tiles vecinos comparten UN filete de 1px (sin doble aro ni hueco).
    /// </remarks>
    public static readonly DependencyProperty SurfaceBorderThicknessProperty =
        DependencyProperty.RegisterAttached(
            "SurfaceBorderThickness",
            typeof(Thickness),
            typeof(BebeButtonHelper),
            new PropertyMetadata(new Thickness(2)));

    /// <summary>Obtiene el grosor del aro del cuerpo físico.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>Thickness vigente (defecto 2).</returns>
    public static Thickness GetSurfaceBorderThickness(DependencyObject element) =>
        (Thickness)element.GetValue(SurfaceBorderThicknessProperty);

    /// <summary>Establece el grosor del aro del cuerpo físico.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">Grosor por lado (Izq,Arriba,Derecha,Abajo).</param>
    public static void SetSurfaceBorderThickness(DependencyObject element, Thickness value) =>
        element.SetValue(SurfaceBorderThicknessProperty, value);

    /// <summary>
    /// Pinta el glow de actividad POR ENCIMA de la cara (modo tile) y oculta
    /// el halo exterior original.
    /// </summary>
    /// <remarks>
    /// El halo vive detrás de la superficie opaca y solo se ve derramándose
    /// fuera del botón; con los tiles de la tira pegados, el vecino derecho lo
    /// tapa y el glow sale en "C" abierta. Con esto, el glow se pinta encima
    /// y queda confinado a la geometría del propio tile (simétrico).
    /// </remarks>
    public static readonly DependencyProperty GlowOnSurfaceProperty =
        DependencyProperty.RegisterAttached(
            "GlowOnSurface",
            typeof(bool),
            typeof(BebeButtonHelper),
            new PropertyMetadata(false));

    /// <summary>Obtiene si el glow se pinta sobre la cara.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <returns>True si el glow va encima (modo tile).</returns>
    public static bool GetGlowOnSurface(DependencyObject element) =>
        (bool)element.GetValue(GlowOnSurfaceProperty);

    /// <summary>Establece si el glow se pinta sobre la cara.</summary>
    /// <param name="element">Botón objetivo.</param>
    /// <param name="value">True para glow encima (modo tile).</param>
    public static void SetGlowOnSurface(DependencyObject element, bool value) =>
        element.SetValue(GlowOnSurfaceProperty, value);
}
