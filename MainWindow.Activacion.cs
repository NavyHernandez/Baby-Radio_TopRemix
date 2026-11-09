using BebeRadio.Support;
using WinRT.Interop;

namespace BebeRadio;

/// <summary>
/// Parcial de <see cref="MainWindow"/>: activación por archivo (doble clic en
/// canciones del Explorador). App.xaml.cs extrae las rutas (instancia única o
/// arranque) y las pasa a <see cref="AbrirArchivosActivados"/>.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>
    /// Encola archivos abiertos desde el Explorador (doble clic / "Abrir con")
    /// y reproduce el primero del lote. Sin archivos solo trae la ventana
    /// al frente. Debe llamarse en el hilo UI.
    /// </summary>
    /// <param name="rutas">Rutas de audio (ya filtradas por el inspector).</param>
    /// <remarks>
    /// El resto del lote queda en la cola; el usuario puede ordenar con
    /// Arriba/Abajo o seguir arrastrando más audios.
    /// </remarks>
    public void AbrirArchivosActivados(IReadOnlyList<string> rutas)
    {
        try
        {
            if (rutas.Count > 0
                && RootFrame.Content is Views.Phase1ShowcaseView consola)
            {
                var indice = consola.ViewModel.Queue.Count;
                consola.ViewModel.AddFiles(rutas);
                if (consola.ViewModel.Queue.Count > indice)
                {
                    // Primera insertada de este lote: suena desde cero.
                    consola.ViewModel.PlayCommand.Execute(consola.ViewModel.Queue[indice]);
                }
            }

            TraerAlFrente();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.AbrirArchivos");
        }
    }

    /// <summary>
    /// Muestra y activa la ventana (doble clic en canciones con la app
    /// abierta: redirige y hay que ponerla al frente).
    /// </summary>
    private void TraerAlFrente()
    {
        try
        {
            AppWindow.Show();
            Activate();

            // Best-effort: Windows puede negar el foreground si la app no tiene
            // foco reciente; Show+Activate cubre el caso normal.
            var hwnd = WindowNative.GetWindowHandle(this);
            _ = Win32SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Ventana.TraerAlFrente");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    private static extern bool Win32SetForegroundWindow(IntPtr hWnd);
}
