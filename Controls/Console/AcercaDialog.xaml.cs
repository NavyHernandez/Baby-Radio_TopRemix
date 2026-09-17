using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Acerca de Baby Radio: versión instalada y chequeo de actualizaciones
/// (Velopack + GitHub cuando el owner ancle la fuente).
/// </summary>
public sealed partial class AcercaDialog : ContentDialog
{
    private bool _ocupado;

    /// <summary>Inicializa el diálogo y muestra la versión.</summary>
    public AcercaDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        VersionText.Text = $"Versión {ContactoInfo.VersionInstalada}";
    }

    /// <summary>Busca, descarga y aplica actualizaciones desde GitHub.</summary>
    /// <param name="sender">Botón buscar.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Velopack contra GitHub Releases; errores con texto simple + log.</remarks>
    private async void OnBuscarClick(object sender, RoutedEventArgs e)
    {
        if (_ocupado)
        {
            return;
        }

        _ocupado = true;
        try
        {
            if (sender is Button boton)
            {
                boton.IsEnabled = false;
            }

            EstadoText.Text = "Buscando en GitHub…";
            var resultado = await ActualizadorBaby.Instancia.ComprobarAsync();
            if (!resultado.HayActualizacion)
            {
                EstadoText.Text = $"Estás al día (v{resultado.VersionInstalada}).";
                return;
            }

            var version = resultado.VersionDisponible;
            await ActualizadorBaby.Instancia.DescargarAsync(porcentaje =>
                DispatcherQueue.TryEnqueue(() =>
                    EstadoText.Text = $"Descargando v{version}… {porcentaje}%"));

            EstadoText.Text = "Aplicando y reiniciando…";
            ActualizadorBaby.Instancia.Aplicar();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Acerca.Buscar");
            EstadoText.Text = "No se pudo actualizar. Revisa tu conexión.";
        }
        finally
        {
            _ocupado = false;
            if (sender is Button botonFinal)
            {
                botonFinal.IsEnabled = true;
            }
        }
    }
}
