using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Chrome de ventana: oculta la titlebar del sistema (botones min/max/close)
/// para dejar sitio a los redondos estilo macOS, y marca esos controles como
/// región Passthrough para que el puntero llegue a ellos por encima de la
/// zona de arrastre definida con <c>Window.SetTitleBar</c>.
/// </summary>
public static class ChromeVentana
{
    /// <summary>Quita la titlebar del sistema conservando el borde de la ventana.</summary>
    /// <param name="ventana">Ventana principal.</param>
    /// <remarks>
    /// Solo aplica al presentador <see cref="OverlappedPresenter"/>; cualquier
    /// fallo queda registrado en el log y la ventana sigue con sus botones
    /// nativos (los nuestros se superpondrían, así que es el caso a vigilar).
    /// </remarks>
    public static void OcultarBotonesDelSistema(Window ventana)
    {
        try
        {
            if (ventana.AppWindow.Presenter is OverlappedPresenter presentador)
            {
                presentador.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Chrome.OcultarBotones");
        }
    }

    /// <summary>Marca una zona de la titlebar como Passthrough (recibe clics).</summary>
    /// <param name="ventana">Ventana principal.</param>
    /// <param name="zona">Elemento de la titlebar que debe recibir el puntero.</param>
    /// <remarks>
    /// Las regiones se expresan en píxeles físicos: bounds DIU ×
    /// <c>XamlRoot.RasterizationScale</c> (patrón de Microsoft). Llamar en
    /// <c>Loaded</c> y <c>SizeChanged</c> de la titlebar; cada llamada
    /// reemplaza la región Passthrough anterior.
    /// </remarks>
    public static void PermitirClics(Window ventana, FrameworkElement zona)
    {
        try
        {
            if (zona.XamlRoot is null || zona.ActualWidth <= 0 || zona.ActualHeight <= 0)
            {
                return;
            }

            double escala = zona.XamlRoot.RasterizationScale;
            var transform = zona.TransformToVisual(null);
            var bounds = transform.TransformBounds(
                new Windows.Foundation.Rect(0, 0, zona.ActualWidth, zona.ActualHeight));
            var rect = new Windows.Graphics.RectInt32(
                (int)Math.Round(bounds.X * escala),
                (int)Math.Round(bounds.Y * escala),
                (int)Math.Round(bounds.Width * escala),
                (int)Math.Round(bounds.Height * escala));

            var fuente = InputNonClientPointerSource.GetForWindowId(ventana.AppWindow.Id);
            fuente.SetRegionRects(NonClientRegionKind.Passthrough, new[] { rect });
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Chrome.Passthrough");
        }
    }
}
