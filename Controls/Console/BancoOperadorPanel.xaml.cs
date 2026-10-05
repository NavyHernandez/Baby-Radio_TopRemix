using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Un banco del modo operador: selector de categoría propio + páginas propias
/// + grilla de 30 carts (PaletaPanel reutilizada). El code-behind solo enruta
/// clics al selector y a la paleta, y anexa sombras al cargar.
/// </summary>
public sealed partial class BancoOperadorPanel : UserControl
{
    /// <summary>Propiedad de dependencia de la paleta del banco.</summary>
    public static readonly DependencyProperty PaletaVMProperty =
        DependencyProperty.Register(
            nameof(PaletaVM),
            typeof(PaletaViewModel),
            typeof(BancoOperadorPanel),
            new PropertyMetadata(null));

    /// <summary>Paleta de este banco (instancia propia por banco).</summary>
    public PaletaViewModel? PaletaVM
    {
        get => (PaletaViewModel?)GetValue(PaletaVMProperty);
        set => SetValue(PaletaVMProperty, value);
    }

    /// <summary>Propiedad de dependencia del selector del banco.</summary>
    public static readonly DependencyProperty SelectorProperty =
        DependencyProperty.Register(
            nameof(Selector),
            typeof(SelectorCategoriaViewModel),
            typeof(BancoOperadorPanel),
            new PropertyMetadata(null));

    /// <summary>Selector de categoría de este banco (independiente).</summary>
    public SelectorCategoriaViewModel? Selector
    {
        get => (SelectorCategoriaViewModel?)GetValue(SelectorProperty);
        set => SetValue(SelectorProperty, value);
    }

    /// <summary>Propiedad de dependencia del layout de riel lateral.</summary>
    public static readonly DependencyProperty UsarRielLateralProperty =
        DependencyProperty.Register(
            nameof(UsarRielLateral),
            typeof(bool),
            typeof(BancoOperadorPanel),
            new PropertyMetadata(false, OnUsarRielLateralChanged));

    /// <summary>
    /// True en modo 1 banco: colapsa el header clásico y muestra el riel
    /// derecho (categorías + ▲▼/gestionar, sin Stop propio).
    /// </summary>
    public bool UsarRielLateral
    {
        get => (bool)GetValue(UsarRielLateralProperty);
        set => SetValue(UsarRielLateralProperty, value);
    }

    /// <summary>Alterna header clásico y riel derecho al cambiar el layout.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="args">Valor nuevo del layout.</param>
    private static void OnUsarRielLateralChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is BancoOperadorPanel banco)
        {
            banco.ActualizarDisposicion();
        }
    }

    /// <summary>Muestra el header clásico o el riel derecho (nunca ambos).</summary>
    private void ActualizarDisposicion()
    {
        HeaderClasico.Visibility = UsarRielLateral ? Visibility.Collapsed : Visibility.Visible;
        RielDerecho.Visibility = UsarRielLateral ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Se eleva al pedir gestionar categorías.</summary>
    public event Action? PideGestionarCategorias;

    /// <summary>Se eleva al pedir crear una categoría (guía sin categoría).</summary>
    public event Action? PideNuevaCategoria;

    /// <summary>Inicializa el banco y reenvía los eventos de su paleta.</summary>
    public BancoOperadorPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PaletaInterna.PideNuevaCategoria += () => PideNuevaCategoria?.Invoke();
    }

    /// <summary>Anexa sombras GPU al cargar y aplica el layout vigente.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        ActualizarDisposicion();
    }

    /// <summary>Despliega la categoría pulsada en este banco.</summary>
    /// <param name="sender">Botón de la tira.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnCategoriaClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button
            && button.DataContext is OpcionCategoria opcion
            && Selector is not null)
        {
            Selector.Seleccionar(opcion.Categoria);
        }
    }

    /// <summary>Va a la página anterior de este banco.</summary>
    /// <param name="sender">Flecha.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnPaginaAnterior(object sender, RoutedEventArgs e) =>
        PaletaVM?.PaginaAnterior();

    /// <summary>Va a la página siguiente de este banco.</summary>
    /// <param name="sender">Flecha.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnPaginaSiguiente(object sender, RoutedEventArgs e) =>
        PaletaVM?.PaginaSiguiente();

    /// <summary>Pide gestionar categorías (el operador abre el gestor).</summary>
    /// <param name="sender">Icono gestionar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnGestionarClick(object sender, RoutedEventArgs e) =>
        PideGestionarCategorias?.Invoke();
}
