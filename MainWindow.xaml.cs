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

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Tema único oscuro; el título queda fijo en "Baby Radio" (sin alias).
        TemaConsola.AplicarGuardado(this);
        Title = "Baby Radio";
        AppTitleBar.Title = "Baby Radio";
        ValidadorRecursos.ValidarEsenciales();

        // Fase 1: la única pantalla es el showcase (paleta + player mock + carts).
        RootFrame.Navigate(typeof(Views.Phase1ShowcaseView));
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
