using System.ComponentModel;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Franja inferior de acciones. Los toggles van por comando al VM;
/// Stop y páginas invocan su comando (mezcla con la paleta) + pulso.
/// Mix titila continuo armado sin glow ni bordes (parpadeo = armado)
/// con un único timer; Operador conserva su glow persistente.
/// </summary>
public sealed partial class AccionesConsolaStrip : UserControl
{
    private TitileoArmado? _titileoMix;

    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AccionesConsolaViewModel),
            typeof(AccionesConsolaStrip),
            new PropertyMetadata(null));

    /// <summary>ViewModel de acciones (lo inyecta la shell).</summary>
    public AccionesConsolaViewModel? ViewModel
    {
        get => (AccionesConsolaViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Inicializa la tira y anexa sombras al cargar.</summary>
    public AccionesConsolaStrip()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Anexa sombras GPU y suscribe el armado de Mix.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        _titileoMix ??= new TitileoArmado(MixButton, DispatcherQueue);
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += OnAccionesCambiadas;
            _titileoMix.Sincronizar(ViewModel.IsMixArmed);
        }
    }

    /// <summary>Desuscribe y apaga el titileo de Mix al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnAccionesCambiadas;
        }

        _titileoMix?.Apagar();
    }

    /// <summary>Titileo continuo armado; fijo apagado al desarmar.</summary>
    /// <param name="sender">ViewModel.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnAccionesCambiadas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccionesConsolaViewModel.IsMixArmed)
            || ViewModel is null)
        {
            return;
        }

        _titileoMix?.Sincronizar(ViewModel.IsMixArmed);
    }

    /// <summary>Pulso de glow en botones de consola (feedback).</summary>
    /// <param name="sender">Botón pulsado.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Stop/páginas ya invocan su comando (mezcla con paleta).</remarks>
    private void OnConsolaFlashClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            ConsolaSombraHelper.FlashActive(button, DispatcherQueue);
        }
    }
}
