using BebeRadio.Support;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace BebeRadio;

/// <summary>
/// Parcial de <see cref="MainWindow"/> con el arranque de la ventana:
/// abrir MAXIMIZADA (el owner no quiere la ventana "chica") y fijar el
/// tamaño "restaurado" en DIPs reales convertidos a píxeles físicos.
/// Motivo histórico: el constructor hacía <c>AppWindow.Resize(1366, 768)</c>
/// pensando en píxeles lógicos, pero <c>AppWindow</c> usa físicos ⇒ a 150 %
/// la ventana salía de ~911x512 DIP (mitad de pantalla) y el layout caía en
/// Compact (<c>AdaptiveTriggerHelper</c> mide DIPs).
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>Ancho del tamaño restaurado en DIPs (valor lógico, no físico).</summary>
    private const double AnchoRestauradoDip = 1366;

    /// <summary>Alto del tamaño restaurado en DIPs (valor lógico, no físico).</summary>
    private const double AltoRestauradoDip = 768;

    /// <summary>Máximo de intentos del reaseguro de arranque (anti-bucle).</summary>
    private const int MaxIntentosArranque = 3;

    /// <summary>Intentos emitidos por <see cref="ReasegurarArranque"/>.</summary>
    private byte _intentosArranque;

    /// <summary>True cuando el tamaño restaurado ya se aplicó (idempotente).</summary>
    private bool _tamanoRestauradoAplicado;

    // --- DPI del display de la ventana (fallback cuando XamlRoot aún no existe) ---

    private const int MonitorDefaultNearest = 2;
    private const int MdtEffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, int modo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int tipo, out uint dpiX, out uint dpiY);

    /// <summary>
    /// Deja la ventana lista para abrir maximizada. Llamar en el constructor,
    /// antes de <c>Activate()</c> (el presentador ya existe con la ventana).
    /// </summary>
    /// <remarks>
    /// Un fallo aquí no aborta el arranque: <see cref="ReasegurarArranque"/>
    /// reintenta tras <c>Activate()</c> (bug conocido de WinAppSDK donde el
    /// presentador a veces no toma en la misma vuelta de mensajes).
    /// </remarks>
    private void PrepararArranque()
    {
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presentador)
            {
                presentador.Maximize();
            }

            RegistroErrores.Traza(
                "Ventana.Arranque",
                $"ctor: presenter={AppWindow.Presenter.Kind} state={(AppWindow.Presenter is OverlappedPresenter o ? o.State.ToString() : "-")}");
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Arranque.Maximizar");
        }

        if (!DispatcherQueue.TryEnqueue(ReasegurarArranque))
        {
            RegistroErrores.Traza("Ventana.Arranque", "ctor: TryEnqueue=false (callback no encolado)");
        }
    }

    /// <summary>
    /// Re-maximiza si el presentador no tomó (o quedó minimizado) y fija el
    /// tamaño restaurado en DIPs. Corre un frame más tarde, ya con
    /// <c>XamlRoot.RasterizationScale</c> real del display.
    /// </summary>
    /// <remarks>
    /// Se re-programa solo mientras <c>_intentosArranque &lt; MaxIntentosArranque</c>
    /// (sin bucle). Orden importante: primero se aplica el tamaño restaurado
    /// (<see cref="AplicarTamanoRestauradoDips"/>, que sale a Overlapped porque
    /// <c>Resize</c> en maximizado NO toma — verificado en WinAppSDK 2.2) y
    /// después se vuelve a maximizar en la MISMA vuelta de mensajes: no hay
    /// frame intermedio, así que no se ve parpadeo.
    /// </remarks>
    private void ReasegurarArranque()
    {
        try
        {
            var estado = AppWindow.Presenter is OverlappedPresenter o ? o.State.ToString() : AppWindow.Presenter.Kind.ToString();
            var escala = EscalaVentana();
            RegistroErrores.Traza(
                "Ventana.Arranque",
                $"intento {_intentosArranque}: estado={estado} escala={escala:0.###}");

            if (AplicarTamanoRestauradoDips(escala))
            {
                AsegurarMaximizada();
                ActualizarGlifoMaximizar();
                RegistroErrores.Traza(
                    "Ventana.Arranque",
                    $"OK maximizada; tamanoActual={AppWindow.Size.Width}x{AppWindow.Size.Height} fisicos");
                return;
            }

            AsegurarMaximizada();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Arranque.Reaseguro");
        }

        if (_intentosArranque < MaxIntentosArranque)
        {
            _intentosArranque++;
            DispatcherQueue.TryEnqueue(ReasegurarArranque);
        }
        else
        {
            RegistroErrores.Traza(
                "Ventana.Arranque",
                "agotados los intentos: tamaño restaurado NO aplicado (queda el que decida Windows)");
        }
    }

    /// <summary>Deja la ventana maximizada (saca de minimized si hace falta).</summary>
    private void AsegurarMaximizada()
    {
        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            if (presentador.State == OverlappedPresenterState.Minimized)
            {
                presentador.Restore();
            }

            if (presentador.State != OverlappedPresenterState.Maximized)
            {
                presentador.Maximize();
            }
        }
    }

    /// <summary>Escala de rasterización (DIP → físico) del display de la ventana.</summary>
    /// <returns>Escala &gt; 0; 0 si aún no se pudo determinar.</returns>
    /// <remarks>
    /// Primero <c>Content.XamlRoot.RasterizationScale</c>; si el XamlRoot aún
    /// no existe (arranque temprano), cae al DPI del monitor que contiene a la
    /// ventana vía <c>MonitorFromWindow</c> + <c>GetDpiForMonitor</c>.
    /// </remarks>
    private double EscalaVentana()
    {
        var escala = (Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 0;
        if (escala > 0)
        {
            return escala;
        }

        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var hMonitor = MonitorFromWindow(hWnd, MonitorDefaultNearest);
            if (hMonitor != IntPtr.Zero
                && GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Arranque.Escala");
        }

        return 0;
    }

    /// <summary>Fija el tamaño restaurado (1366x768 DIP) en píxeles físicos.</summary>
    /// <param name="escala">Escala DIP→físico del display (0 = aún no disponible).</param>
    /// <returns>True si se aplicó; false si la escala no está disponible.</returns>
    /// <remarks>
    /// El resultado se limita al <c>WorkArea</c> del display de la ventana
    /// (<see cref="GeometriaVentana"/>), así que en un monitor estrecho
    /// (p. ej. vertical de 900 px) la ventana restaurada nunca se sale.
    /// <c>AppWindow.Resize</c> en estado maximizado NO cambia el tamaño
    /// "restaurado" (verificado empíricamente en WinAppSDK 2.2: el resize se
    /// ignora y Restaurar devuelve el tamaño viejo), así que aquí se sale a
    /// Overlapped, se redimensiona y el caller vuelve a maximizar en la misma
    /// vuelta de mensajes (sin frame intermedio ⇒ sin parpadeo visible).
    /// </remarks>
    private bool AplicarTamanoRestauradoDips(double escala)
    {
        if (_tamanoRestauradoAplicado)
        {
            return true;
        }

        if (escala <= 0)
        {
            return false;
        }

        var fisico = GeometriaVentana.DipsAFisicos(AnchoRestauradoDip, AltoRestauradoDip, escala);
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        var limitado = GeometriaVentana.LimitarAlWorkArea(fisico, display.WorkArea);

        if (AppWindow.Presenter is OverlappedPresenter presentador
            && presentador.State == OverlappedPresenterState.Maximized)
        {
            presentador.Restore();
        }

        AppWindow.Resize(limitado);
        _tamanoRestauradoAplicado = true;
        return true;
    }
}
