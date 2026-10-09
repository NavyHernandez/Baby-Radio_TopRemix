using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace BebeRadio;

/// <summary>
/// Ventana principal: titlebar extendida + Frame. Toda la UI vive en páginas
/// (Fase 1: <c>Views/Phase1ShowcaseView</c>); aquí solo composición inicial.
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

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        Support.IconoVentana.Aplicar(this);

        // Tema único oscuro; el título queda fijo en "Baby Radio" (sin alias).
        TemaConsola.AplicarGuardado(this);
        Title = "Baby Radio";
        AppTitleBar.Title = "Baby Radio";
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

    /// <summary>Entra al modo operador (completa o ventana según ajuste).</summary>
    /// <param name="pantallaCompleta">True = fullscreen sin titlebar (2 paletas);
    /// false = ventana normal redimensionable (1 paleta, convive con otras apps).</param>
    public void EntrarOperador(bool pantallaCompleta = true)
    {
        if (_enOperador)
        {
            return;
        }

        if (!pantallaCompleta)
        {
            return;
        }

        _enOperador = true;
        try
        {
            _tamanoPrevio = AppWindow.Size;
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            AppTitleBar.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // La consola sigue usable en ventana normal.
        }
    }

    /// <summary>Sale del modo operador y restaura ventana y barra de título.</summary>
    public void SalirOperador()
    {
        if (!_enOperador)
        {
            return;
        }

        _enOperador = false;
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
            if (_tamanoPrevio.Width > 0 && _tamanoPrevio.Height > 0)
            {
                AppWindow.Resize(_tamanoPrevio);
            }

            AppTitleBar.Visibility = Visibility.Visible;
        }
        catch
        {
            // La consola sigue usable en el estado actual.
        }
    }

    /// <summary>Limpia el hook de mensajes al cerrar la ventana.</summary>
    private void OnClosed(object sender, WindowEventArgs e)
    {
        _messageHook?.Dispose();
        _messageHook = null;
    }
}
