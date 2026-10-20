using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio;

/// <summary>
/// Botones de ventana estilo macOS (parcial de <see cref="MainWindow"/>):
/// minimizar, maximizar/restaurar y cerrar sobre la titlebar propia.
/// Los handlers solo enrutan a <see cref="OverlappedPresenter"/>; el cierre
/// conserva la confirmación de <c>OnCierreVentana</c> porque va por
/// <c>Window.Close()</c> → <c>AppWindow.Closing</c>.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>Minimiza la ventana al sistema.</summary>
    /// <param name="sender">Botón redondo de la titlebar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BotonMinimizar_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            presentador.Minimize();
        }
    }

    /// <summary>Alterna maximizar/restaurar y sincroniza el glifo.</summary>
    /// <param name="sender">Botón redondo de la titlebar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BotonMaximizar_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            if (presentador.State == OverlappedPresenterState.Maximized)
            {
                presentador.Restore();
            }
            else
            {
                presentador.Maximize();
            }
        }

        ActualizarGlifoMaximizar();
    }

    /// <summary>Cierra la ventana (respeta la confirmación con música sonando).</summary>
    /// <param name="sender">Botón redondo de la titlebar.</param>
    /// <param name="e">Datos del clic.</param>
    private void BotonCerrar_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Sincroniza glifo, tooltip y nombre de accesibilidad del botón.</summary>
    /// <remarks>
    /// Se invoca tras cada cambio de presentador (clic, teclado, arrastre):
    /// glifo <c>Copy</c> (cuadrados solapados) cuando está maximizado y
    /// <c>Maximize</c> en caso contrario.
    /// </remarks>
    private void ActualizarGlifoMaximizar()
    {
        bool maximizado =
            AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        string nombre = maximizado ? "Restaurar" : "Maximizar";

        GlifoMaximizar.Symbol = maximizado
            ? FluentIcons.Common.Symbol.Copy
            : FluentIcons.Common.Symbol.Maximize;
        AutomationProperties.SetName(BotonMaximizar, nombre);
        ToolTipService.SetToolTip(BotonMaximizar, nombre);
    }
}
