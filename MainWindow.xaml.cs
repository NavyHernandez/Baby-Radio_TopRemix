using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace BebeRadio;

/// <summary>
/// Ventana principal: titlebar extendida + Frame. Toda la UI vive en páginas
/// (Fase 1: <c>Views/Phase1ShowcaseView</c>); aquí solo composición inicial,
/// confirmación de cierre y el presentador del modo operador (fullscreen
/// anclado a UNA pantalla, con restauración de posición/tamaño previos).
/// </summary>
public sealed partial class MainWindow : Window
{
    private WindowMessageHook? _messageHook;

    /// <summary>
    /// Inicializa la ventana, extiende el contenido a la titlebar y navega
    /// el Frame a la pantalla de la fase actual.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Botones estilo macOS: ocultan los del sistema y habilitan los clics
        // sobre la fila propia (región Passthrough de la titlebar).
        ChromeVentana.OcultarBotonesDelSistema(this);
        AppTitleBar.Loaded += (_, _) => ChromeVentana.PermitirClics(this, FilaBotonesVentana);
        AppTitleBar.SizeChanged += (_, _) => ChromeVentana.PermitirClics(this, FilaBotonesVentana);
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange)
            {
                DispatcherQueue.TryEnqueue(ActualizarGlifoMaximizar);
            }
        };

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        Support.IconoVentana.Aplicar(this);

        // Tema único oscuro; el título queda fijo en "Baby Radio" (sin alias).
        TemaConsola.AplicarGuardado(this);
        Title = "Baby Radio";
        BarraTitulo.Title = "Baby Radio";
        ValidadorRecursos.ValidarEsenciales();

        // Tamaño inicial de ventana (1366x768) para layout adaptativo
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1366, 768));

        // Obtener handle de la ventana para hotkeys globales
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Inicializar servicio de mapeo con el handle de la ventana
        KeyMappingService.Instancia.Inicializar(hWnd);

        // Inicializar servicio de sonidos (.wav de captura)
        SonidoService.Instancia.Inicializar("ms-appx:///Assets/Sounds/beep.wav");

        // Hook de mensajes para hotkeys globales (RegisterHotKey).
        _messageHook = new WindowMessageHook(this);
        _messageHook.HotkeyReceived += id => KeyMappingService.Instancia.ProcesarHotkey(id);

        // Fase 1: la única pantalla es el showcase (paleta + player mock + carts).
        RootFrame.Navigate(typeof(Views.Phase1ShowcaseView));

        // Confirma el cierre si hay una canción sonando en la lista.
        AppWindow.Closing += OnCierreVentana;
        Closed += OnClosed;
    }

    private bool _cierreConfirmado;

    /// <summary>Pide confirmación al cerrar con música sonando (cancelable).</summary>
    /// <param name="sender">Ventana de la app.</param>
    /// <param name="e">Args cancelables del cierre.</param>
    /// <remarks>En silencio cierra directo; blindado: un fallo deja cerrar.</remarks>
    private async void OnCierreVentana(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
    {
        if (_cierreConfirmado || !EstaSonandoLista())
        {
            return;
        }

        // Sin deferral en WinAppSDK: se cancela en síncrono y el diálogo
        // decide después (con flag anti-loop para el Close programado).
        e.Cancel = true;
        try
        {
            var confirmar = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                XamlRoot = RootFrame.XamlRoot,
                Style = (Microsoft.UI.Xaml.Style)Microsoft.UI.Xaml.Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = "Cerrar Baby Radio",
                Content = "Hay una canción sonando. ¿Seguro que quieres cerrar?",
                PrimaryButtonText = "Cerrar",
                CloseButtonText = "Cancelar",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
            };
            if (await confirmar.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                _cierreConfirmado = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.Cierre");
        }
    }

    /// <summary>Indica si la lista de reproducción está sonando.</summary>
    /// <returns>True con música en curso (false si falla).</returns>
    private bool EstaSonandoLista()
    {
        try
        {
            return (RootFrame.Content as Views.Phase1ShowcaseView)?.ViewModel.Reproductor.IsPlaying == true;
        }
        catch
        {
            return false;
        }
    }

    private bool _enOperador;
    private Windows.Graphics.SizeInt32 _tamanoPrevio;
    private Windows.Graphics.PointInt32 _posicionPrevia;

    /// <summary>True cuando ya se capturó tamaño/posición previos (anti-pérdida en reintentos).</summary>
    private bool _estadoPrevioGuardado;

    /// <summary>True mientras corre el reintento único de fullscreen (anti-bucle).</summary>
    private bool _reintentoPendiente;

    /// <summary>Entra al modo operador (completa o ventana según ajuste).</summary>
    /// <param name="pantallaCompleta">True = fullscreen sin titlebar (2 paletas);
    /// false = ventana normal redimensionable (1 paleta, convive con otras apps).</param>
    /// <remarks>
    /// El fullscreen se ancla antes a UNA sola pantalla (<see cref="AnclarEnUnaPantalla"/>),
    /// para que no se resuelva en otro monitor ni a caballo entre dos. El flag
    /// <c>_enOperador</c> solo se marca cuando el presentador quedó realmente activo
    /// (<see cref="IntentarFullscreenOperador"/>): si el primer intento no aplica,
    /// se reintenta un frame más tarde (<see cref="ProgramarReintentoOperador"/>) y,
    /// si vuelve a fallar, la consola queda en ventana registrada en el log en vez
    /// de bloqueada. Debe llamarse en el hilo UI.
    /// </remarks>
    public void EntrarOperador(bool pantallaCompleta = true)
    {
        if (_enOperador || !pantallaCompleta)
        {
            return;
        }

        try
        {
            // Se captura una sola vez: un reintento no debe pisar el estado original
            // con la geometría ya anclada.
            if (!_estadoPrevioGuardado)
            {
                _tamanoPrevio = AppWindow.Size;
                _posicionPrevia = AppWindow.Position;
                _estadoPrevioGuardado = true;
            }

            AnclarEnUnaPantalla();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.AnclarPantalla");
        }

        if (IntentarFullscreenOperador())
        {
            _reintentoPendiente = false;
            return;
        }

        ProgramarReintentoOperador();
    }

    /// <summary>Sale del modo operador y restaura ventana y barra de título.</summary>
    /// <remarks>
    /// Devuelve la ventana a la posición y el tamaño previos a entrar (incluida
    /// la pantalla de origen) y restaura la titlebar en su propio bloque: un fallo
    /// del presentador nunca deja la barra de título oculta.
    /// </remarks>
    public void SalirOperador()
    {
        if (!_enOperador)
        {
            return;
        }

        _enOperador = false;
        _reintentoPendiente = false;
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            if (_estadoPrevioGuardado && _tamanoPrevio.Width > 0 && _tamanoPrevio.Height > 0)
            {
                AppWindow.Move(_posicionPrevia);
                AppWindow.Resize(_tamanoPrevio);
            }

            _estadoPrevioGuardado = false;
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.SalirOperador");
        }

        try
        {
            AppTitleBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.SalirOperador.TitleBar");
        }
    }

    /// <summary>Aplica el fullscreen del modo operador y confirma que quedó activo.</summary>
    /// <returns>True si el presentador de la ventana terminó en FullScreen.</returns>
    /// <remarks>
    /// Único punto que marca <c>_enOperador</c>: un intento fallido no deja estado
    /// semipuesto que bloquee la siguiente entrada. Registra la excepción en vez
    /// de tragarla.
    /// </remarks>
    private bool IntentarFullscreenOperador()
    {
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.EntrarOperador");
        }

        if (AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
        {
            return false;
        }

        _enOperador = true;
        AppTitleBar.Visibility = Visibility.Collapsed;
        return true;
    }

    /// <summary>Reintenta el fullscreen un frame más tarde (como máximo una vez).</summary>
    /// <remarks>
    /// El presentador a veces no toma en la misma vuelta de mensajes (bug conocido
    /// de WinAppSDK al entrar a fullscreen). El callback se desactiva solo al
    /// ejecutarse, así que no puede formar bucle.
    /// </remarks>
    private void ProgramarReintentoOperador()
    {
        if (_reintentoPendiente)
        {
            return;
        }

        _reintentoPendiente = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _reintentoPendiente = false;
            if (_enOperador)
            {
                return;
            }

            if (!IntentarFullscreenOperador())
            {
                RegistroErrores.Traza(
                    "Ventana.EntrarOperador",
                    "fullscreen no aplicó tras el reintento: se queda en ventana");
            }
        });
    }

    /// <summary>Recoloca la ventana dentro de UNA sola pantalla.</summary>
    /// <remarks>
    /// Resuelve el display con mayor solape con la ventana (<see cref="AreaDeVentana"/>)
    /// y, si la ventana no cabe entero en sus <c>OuterBounds</c> (p. ej. a caballo
    /// entre dos monitores), la centra ahí con el mismo tamaño limitado al display.
    /// No hace nada si la ventana ya está dentro de una sola pantalla.
    /// </remarks>
    private void AnclarEnUnaPantalla()
    {
        var area = AreaDeVentana();
        if (area is null)
        {
            return;
        }

        var limite = area.OuterBounds;
        var posicion = AppWindow.Position;
        var tamano = AppWindow.Size;
        var cabeEnX = posicion.X >= limite.X
            && posicion.X + tamano.Width <= limite.X + limite.Width;
        var cabeEnY = posicion.Y >= limite.Y
            && posicion.Y + tamano.Height <= limite.Y + limite.Height;
        if (cabeEnX && cabeEnY)
        {
            return;
        }

        var ancho = Math.Min(tamano.Width, limite.Width);
        var alto = Math.Min(tamano.Height, limite.Height);
        var x = limite.X + ((limite.Width - ancho) / 2);
        var y = limite.Y + ((limite.Height - alto) / 2);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, ancho, alto));
    }

    /// <summary>Pantalla con mayor solape con la ventana actual.</summary>
    /// <returns>DisplayArea resuelto o null si no se pudo determinar.</returns>
    private Microsoft.UI.Windowing.DisplayArea? AreaDeVentana()
    {
        var posicion = AppWindow.Position;
        var tamano = AppWindow.Size;
        var rect = new Windows.Graphics.RectInt32(
            posicion.X, posicion.Y, tamano.Width, tamano.Height);
        return Microsoft.UI.Windowing.DisplayArea.GetFromRect(
            rect, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
    }

    /// <summary>Limpia el hook de mensajes al cerrar la ventana.</summary>
    private void OnClosed(object sender, WindowEventArgs e)
    {
        _messageHook?.Dispose();
        _messageHook = null;
    }
}
