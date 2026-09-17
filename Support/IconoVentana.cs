using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Aplica el icono de la ventana (titlebar + taskbar) en modo unpackaged,
/// donde WinUI 3 no lo expone directamente. Carga el .ico con ruta absoluta
/// y nunca lanza: si el icono falta, la app arranca igual.
/// </summary>
internal static class IconoVentana
{
    private const uint ImagenIcono = 1;
    private const uint CargarDesdeArchivo = 0x0010;
    private const int MensajePonerIcono = 0x0080;
    private const int IconoPequeno = 0;
    private const int IconoGrande = 1;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CargarImagen(
        nint instancia, string nombre, uint tipo, int ancho, int alto, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint EnviarMensaje(
        nint ventana, uint mensaje, nint parametro, nint icono);

    /// <summary>
    /// Fija el icono de la ventana desde Assets/AppIcon.ico.
    /// </summary>
    /// <param name="ventana">Ventana principal.</param>
    internal static void Aplicar(Window ventana)
    {
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(ventana);
            var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(ruta))
            {
                return;
            }

            var icono = CargarImagen(nint.Zero, ruta, ImagenIcono, 0, 0, CargarDesdeArchivo);
            if (icono == nint.Zero)
            {
                return;
            }

            EnviarMensaje(handle, MensajePonerIcono, (nint)IconoGrande, icono);
            EnviarMensaje(handle, MensajePonerIcono, (nint)IconoPequeno, icono);
        }
        catch
        {
            // El icono nunca debe impedir el arranque.
        }
    }
}
