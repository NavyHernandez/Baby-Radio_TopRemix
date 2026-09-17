using System.ComponentModel;
using BebeRadio.Controls.Console;
using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Views;

/// <summary>
/// Shell de la consola Baby Radio: hospeda el layout normal (5 partes) y el
/// layout operador (dos bancos + tira compacta) que se crea al primer ingreso.
/// Sin lógica de negocio: la mezcla vive en <see cref="ConsolaViewModel"/>.
/// </summary>
public sealed partial class Phase1ShowcaseView : Page
{
    /// <summary>Orquestador (lista + paleta + categorías + acciones + bancos).</summary>
    public ConsolaViewModel ViewModel { get; } = new();

    private OperadorConsolaPanel? _operador;

    /// <summary>Inicializa la shell, anexa sombras y observa el modo operador.</summary>
    public Phase1ShowcaseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnDescargada;
        ViewModel.Acciones.PropertyChanged += OnAccionesCambiadas;
    }

    /// <summary>Anexa sombras GPU a todos los botones de la consola.</summary>
    /// <param name="sender">Esta página.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e) =>
        ConsolaSombraHelper.AttachAllShadows(this);

    /// <summary>Desuscribe el modo operador al descargar.</summary>
    /// <param name="sender">Esta página.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnDescargada(object sender, RoutedEventArgs e) =>
        ViewModel.Acciones.PropertyChanged -= OnAccionesCambiadas;

    /// <summary>Alterna los layouts al activar o salir del modo operador.</summary>
    /// <param name="sender">Acciones.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnAccionesCambiadas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccionesConsolaViewModel.IsOperatorMode))
        {
            RefrescarModo();
        }
    }

    /// <summary>Muestra el layout operador (perezoso) u oculta y restaura.</summary>
    private void RefrescarModo()
    {
        var activo = ViewModel.Acciones.IsOperatorMode;
        if (activo)
        {
            try
            {
                ViewModel.PrepararOperador();
                _operador ??= new OperadorConsolaPanel { Consola = ViewModel };
                if (_operador.Parent is null)
                {
                    Raiz.Children.Add(_operador);
                }

                _operador.Visibility = Visibility.Visible;
                LayoutNormal.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // Blindaje: si la pantalla operador no carga (p. ej. recurso XAML
                // faltante), se registra y se vuelve al modo normal en vez de
                // dejar la consola sin mostrar nada.
                RegistroErrores.Registrar(ex, "Shell.Operador");
                ViewModel.Acciones.IsOperatorMode = false;
                return;
            }
        }
        else if (_operador is not null)
        {
            _operador.Visibility = Visibility.Collapsed;
            LayoutNormal.Visibility = Visibility.Visible;
        }

        if ((Application.Current as App)?.VentanaPrincipal is MainWindow ventana)
        {
            if (activo)
            {
                ventana.EntrarOperador();
            }
            else
            {
                ventana.SalirOperador();
            }
        }
    }

    /// <summary>Tras importar, recarga categorías + paleta y sombras.</summary>
    private void OnConfiguracionImportada()
    {
        ViewModel.RecargarTodo();
        ConsolaSombraHelper.AttachAllShadows(this);
    }

    /// <summary>La paleta pide crear categoría: abre el flujo del riel.</summary>
    private async void OnPideNuevaCategoria() => await Riel.IniciarCreacionAsync();
}
