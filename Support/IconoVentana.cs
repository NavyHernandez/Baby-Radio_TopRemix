using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Aplica el icono de la ventana (titlebar + taskbar) en modo unpackaged,
/// donde WinUI 3 no lo expone directamente. Usa Assets/bbradioLogo.jpg
/// (vía HICON de System.Drawing; LoadImage no carga JPG) con fallback
/// silencioso al .ico. Nunca lanza: si el icono falta, la app arranca igual.
/// </summary>
internal static class IconoVentana
{
    private const int MensajePonerIcono = 0x0080;
    private const int IconoPequeno = 0;
    private const int IconoGrande = 1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint EnviarMensaje(
        nint ventana, uint mensaje, nint parametro, nint icono);

    /// <summary>Respaldo anclado (su Handle muere si el GC lo recoge).</summary>
    private static System.Drawing.Icon? _icoRespaldo;

    /// <summary>
    /// Fija el icono de la ventana desde Assets/bbradioLogo.jpg.
    /// </summary>
    /// <param name="ventana">Ventana principal.</param>
    internal static void Aplicar(Window ventana)
    {
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(ventana);
            var icono = CargarLogo();
            if (icono == nint.Zero)
            {
                icono = CargarIcoRespaldo();
            }

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

    /// <summary>Carga el logo JPG como HICON (vive toda la app).</summary>
    /// <returns>Handle o cero si falta/falla.</returns>
    private static nint CargarLogo()
    {
        try
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "bbradioLogo.jpg");
            if (!File.Exists(ruta))
            {
                return nint.Zero;
            }

            using var mapa = new System.Drawing.Bitmap(ruta);
            return mapa.GetHicon();
        }
        catch
        {
            return nint.Zero;
        }
    }

    /// <summary>Carga el .ico de respaldo por ruta absoluta.</summary>
    /// <returns>Handle o cero si falta/falla.</returns>
    private static nint CargarIcoRespaldo()
    {
        try
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(ruta))
            {
                return nint.Zero;
            }

            _icoRespaldo = new System.Drawing.Icon(ruta);
            return _icoRespaldo.Handle;
        }
        catch
        {
            return nint.Zero;
        }
    }
}
