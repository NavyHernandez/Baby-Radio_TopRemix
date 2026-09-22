using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Cuenta del operador: alias, web y pie de derechos (GH Dev Company).
/// </summary>
public sealed partial class UsuarioDialog : ContentDialog
{
    /// <summary>Inicializa el diálogo y carga el perfil del operador.</summary>
    public UsuarioDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        AliasBox.Text = TemaConsola.LeerPerfil().Alias;
    }

    /// <summary>Guarda el alias en el JSON portable (refresca la titlebar).</summary>
    /// <param name="sender">Botón Guardar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnGuardarPerfilClick(object sender, RoutedEventArgs e) =>
        TemaConsola.GuardarPerfil(AliasBox.Text);

    /// <summary>Abre la web de la emisora.</summary>
    /// <param name="sender">Link web.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnWebClick(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri(ContactoInfo.WebTopRemix));
}
