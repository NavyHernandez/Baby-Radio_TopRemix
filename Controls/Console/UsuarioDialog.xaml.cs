using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Cuenta del operador: alias, QR, chat directo y web.
/// </summary>
public sealed partial class UsuarioDialog : ContentDialog
{
    /// <summary>Inicializa el diálogo, carga perfil y QR.</summary>
    public UsuarioDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        AliasBox.Text = TemaConsola.LeerPerfil().Alias;
        QrImage.Source = new BitmapImage(new Uri(ContactoInfo.QrUsuarioPath));
        if (string.IsNullOrWhiteSpace(ContactoInfo.ChatUrl))
        {
            ChatBoton.IsEnabled = false;
            ChatBoton.Content = "Chat pendiente (sin número)";
        }
    }

    /// <summary>Guarda el alias en el JSON portable (refresca la titlebar).</summary>
    /// <param name="sender">Botón Guardar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnGuardarPerfilClick(object sender, RoutedEventArgs e) =>
        TemaConsola.GuardarPerfil(AliasBox.Text);

    /// <summary>Muestra el fallback si falta el PNG.</summary>
    /// <param name="sender">Imagen del QR.</param>
    /// <param name="e">Args del fallo.</param>
    private void OnQrFailed(object sender, ExceptionRoutedEventArgs e) =>
        QrFallback.Visibility = Visibility.Visible;

    /// <summary>Abre el chat directo de WhatsApp.</summary>
    /// <param name="sender">Botón chat.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnChatClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ContactoInfo.ChatUrl))
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(ContactoInfo.ChatUrl));
        }
    }

    /// <summary>Abre la web de la emisora.</summary>
    /// <param name="sender">Link web.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnWebClick(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri(ContactoInfo.WebTopRemix));
}
