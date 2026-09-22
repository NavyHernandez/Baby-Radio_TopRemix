using System.ComponentModel;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Franja inferior de acciones. Los toggles van por comando al VM;
/// Stop y páginas invocan su comando (mezcla con la paleta) + pulso.
/// Mix y Ganancia titilan continuo armados sin glow ni bordes
/// (parpadeo = armado) con un timer cada uno; Operador conserva su glow.
/// </summary>
public sealed partial class AccionesConsolaStrip : UserControl
{
    private TitileoArmado? _titileoMix;
    private TitileoArmado? _titileoGanancia;

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

    /// <summary>Anexa sombras GPU y suscribe el armado de Mix y Ganancia.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        _titileoMix ??= new TitileoArmado(MixButton, DispatcherQueue);
        _titileoGanancia ??= new TitileoArmado(GananciaButton, DispatcherQueue);
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += OnAccionesCambiadas;
            _titileoMix.Sincronizar(ViewModel.IsMixArmed);
            _titileoGanancia.Sincronizar(ViewModel.IsGananciaArmada);
        }
    }

    /// <summary>Desuscribe y apaga los titileos al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnAccionesCambiadas;
        }

        _titileoMix?.Apagar();
        _titileoGanancia?.Apagar();
    }

    private bool _configAbierta;

    /// <summary>Abre la ventana de ajustes (un solo vuelo).</summary>
    /// <param name="sender">Botón Config.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo no cierra la app.</remarks>
    private async void OnConfigClick(object sender, RoutedEventArgs e)
    {
        if (_configAbierta)
        {
            return;
        }

        _configAbierta = true;
        try
        {
            if (sender is Button boton)
            {
                ConsolaSombraHelper.FlashActive(boton, DispatcherQueue);
            }

            await new ConfiguracionDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            BebeRadio.Support.RegistroErrores.Registrar(ex, "Acciones.Config");
        }
        finally
        {
            _configAbierta = false;
        }
    }

    /// <summary>Titileo continuo armado; fijo apagado al desarmar.</summary>
    /// <param name="sender">ViewModel.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnAccionesCambiadas(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(AccionesConsolaViewModel.IsMixArmed))
        {
            _titileoMix?.Sincronizar(ViewModel.IsMixArmed);
        }
        else if (e.PropertyName == nameof(AccionesConsolaViewModel.IsGananciaArmada))
        {
            _titileoGanancia?.Sincronizar(ViewModel.IsGananciaArmada);
        }
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
