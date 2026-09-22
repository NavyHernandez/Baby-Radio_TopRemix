using BebeRadio.Support;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace BebeRadio;

/// <summary>
/// Ventana principal: titlebar extendida + Frame. Toda la UI vive en páginas
/// (Fase 1: <c>Views/Phase1ShowcaseView</c>); aquí solo composición inicial.
/// </summary>
public sealed partial class MainWindow : Window
{
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

        // Fase 1: la única pantalla es el showcase (paleta + player mock + carts).
        RootFrame.Navigate(typeof(Views.Phase1ShowcaseView));

        // Confirma el cierre si hay una canción sonando en la lista.
        AppWindow.Closing += OnCierreVentana;
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

    /// <summary>Entra al modo operador: pantalla completa sin barra de título.</summary>
    public void EntrarOperador()
    {
        if (_enOperador)
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
}
