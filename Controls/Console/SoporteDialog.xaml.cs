using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Contacto de soporte: QR de WhatsApp y botón directo.
/// </summary>
public sealed partial class SoporteDialog : ContentDialog
{
    /// <summary>Inicializa el diálogo y carga el QR de soporte.</summary>
    public SoporteDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        QrImage.Source = new BitmapImage(new Uri(ContactoInfo.QrSoportePath));
    }

    /// <summary>Muestra el fallback si falta el PNG.</summary>
    /// <param name="sender">Imagen del QR.</param>
    /// <param name="e">Args del fallo.</param>
    private void OnQrFailed(object sender, ExceptionRoutedEventArgs e) =>
        QrFallback.Visibility = Visibility.Visible;

    /// <summary>Abre el WhatsApp directo.</summary>
    /// <param name="sender">Botón chat.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnChatClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ContactoInfo.ChatUrl))
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(ContactoInfo.ChatUrl));
        }
    }
}
