using BebeRadio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls;

/// <summary>
/// Fader maestro horizontal (volumen total de salida).
/// El code-behind solo expone el ViewModel inyectado; el audio y la
/// persistencia viven en <see cref="PlayerViewModel"/>.
/// </summary>
public sealed partial class MasterFaderControl : UserControl
{
    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(PlayerViewModel),
            typeof(MasterFaderControl),
            new PropertyMetadata(null));

    private PlayerViewModel? _local;

    /// <summary>ViewModel del reproductor (lo inyecta el padre; defecto local).</summary>
    public PlayerViewModel ViewModel
    {
        get
        {
            if (GetValue(ViewModelProperty) is PlayerViewModel inyectado)
            {
                return inyectado;
            }

            _local ??= new PlayerViewModel();
            return _local;
        }

        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Inicializa el fader.</summary>
    public MasterFaderControl()
    {
        InitializeComponent();
    }
}
