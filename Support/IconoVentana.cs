using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Aplica el icono de la ventana (titlebar + taskbar) en modo unpackaged,
/// donde WinUI 3 no lo expone directamente. Usa
/// Assets/bbradioLogo-removebg-preview.png (RGBA con transparencia, vía HICON
/// de System.Drawing) con fallback silencioso al JPG legado y al .ico.
/// Recorta cuadrado centrado y genera 16/32 px en alta calidad para que el
/// icono pequeño no se vea borroso. Nunca lanza: si el icono falta, la app
/// arranca igual.
/// </summary>
internal static class IconoVentana
{
    private const string NombrePng = "bbradioLogo-removebg-preview.png";
    private const string NombreJpgLegado = "bbradioLogo.jpg";
    private const int LadoIconoPequeno = 16;
    private const int LadoIconoGrande = 32;
    private const int MensajePonerIcono = 0x0080;
    private const int IconoPequeno = 0;
    private const int IconoGrande = 1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint EnviarMensaje(
        nint ventana, uint mensaje, nint parametro, nint icono);

    /// <summary>Respaldo anclado (su Handle muere si el GC lo recoge).</summary>
    private static System.Drawing.Icon? _icoRespaldo;

    /// <summary>
    /// Fija el icono de la ventana desde el PNG con transparencia (16 + 32 px).
    /// </summary>
    /// <param name="ventana">Ventana principal.</param>
    internal static void Aplicar(Window ventana)
    {
        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(ventana);
            var chico = CargarLogo(LadoIconoPequeno);
            var grande = CargarLogo(LadoIconoGrande);
            if (chico == nint.Zero && grande == nint.Zero)
            {
                var respaldo = CargarIcoRespaldo();
                if (respaldo == nint.Zero)
                {
                    return;
                }

                EnviarMensaje(handle, MensajePonerIcono, (nint)IconoGrande, respaldo);
                EnviarMensaje(handle, MensajePonerIcono, (nint)IconoPequeno, respaldo);
                return;
            }

            if (grande != nint.Zero)
            {
                EnviarMensaje(handle, MensajePonerIcono, (nint)IconoGrande, grande);
            }

            if (chico != nint.Zero)
            {
                EnviarMensaje(handle, MensajePonerIcono, (nint)IconoPequeno, chico);
            }
        }
        catch
        {
            // El icono nunca debe impedir el arranque.
        }
    }

    /// <summary>
    /// Carga el logo PNG como HICON del tamaño pedido, recortado cuadrado
    /// centrado y reescalado en alta calidad (conserva la transparencia).
    /// </summary>
    /// <param name="ladoPx">Lado del icono (16 o 32).</param>
    /// <returns>Handle o cero si falta/falla.</returns>
    private static nint CargarLogo(int ladoPx)
    {
        try
        {
            var ruta = ResolverRutaLogo();
            if (ruta is null)
            {
                return nint.Zero;
            }

            using var original = new System.Drawing.Bitmap(ruta);
            var lado = Math.Min(original.Width, original.Height);
            if (lado <= 0)
            {
                return nint.Zero;
            }

            var recorteX = (original.Width - lado) / 2;
            var recorteY = (original.Height - lado) / 2;
            using var recorte = original.Clone(
                new System.Drawing.Rectangle(recorteX, recorteY, lado, lado),
                original.PixelFormat);
            using var destino = new System.Drawing.Bitmap(ladoPx, ladoPx, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graficos = System.Drawing.Graphics.FromImage(destino))
            {
                graficos.CompositingQuality = CompositingQuality.HighQuality;
                graficos.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graficos.SmoothingMode = SmoothingMode.HighQuality;
                graficos.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graficos.Clear(System.Drawing.Color.Transparent);
                graficos.DrawImage(recorte, new System.Drawing.Rectangle(0, 0, ladoPx, ladoPx));
            }

            return destino.GetHicon();
        }
        catch
        {
            return nint.Zero;
        }
    }

    /// <summary>
    /// Resuelve la ruta del logo: PNG con transparencia primero, JPG legado
    /// después (por si el PNG aún no viajó al directorio de salida).
    /// </summary>
    /// <returns>Ruta existente o null si no hay logo.</returns>
    private static string? ResolverRutaLogo()
    {
        var png = Path.Combine(AppContext.BaseDirectory, "Assets", NombrePng);
        if (File.Exists(png))
        {
            return png;
        }

        var jpg = Path.Combine(AppContext.BaseDirectory, "Assets", NombreJpgLegado);
        return File.Exists(jpg) ? jpg : null;
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
