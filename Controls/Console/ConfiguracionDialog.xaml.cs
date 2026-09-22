using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Ajustes de la app: transición suave al pasar a Siguiente (toggle + 3/5/7 s).
/// La vista solo enlaza; la lógica y el guardado viven en el ViewModel.
/// </summary>
public sealed partial class ConfiguracionDialog : ContentDialog
{
    /// <summary>Ajustes editados (enlace TwoWay de la vista).</summary>
    public ConfiguracionViewModel ViewModel { get; }

    /// <summary>Inicializa el diálogo y carga los ajustes guardados.</summary>
    public ConfiguracionDialog()
    {
        ViewModel = new ConfiguracionViewModel();
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
    }
}
