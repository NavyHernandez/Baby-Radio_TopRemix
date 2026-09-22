using Microsoft.UI.Xaml;

namespace BebeRadio;

/// <summary>
/// Aplicación Baby Radio: registra recursos globales (ver App.xaml) y abre
/// <see cref="MainWindow"/> al lanzar. Sin lógica de negocio.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Ventana principal (para tema en vivo desde diálogos).</summary>
    public Window? VentanaPrincipal => _window;

    /// <summary>
    /// Inicializa el objeto de aplicación (equivalente a main/WinMain).
    /// </summary>
    /// <remarks>Suscribe la red global de diagnóstico (log sin cerrar).</remarks>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnExcepcionGlobal;
        TaskScheduler.UnobservedTaskException += OnTareaSinObservar;
    }

    /// <summary>Registra excepciones no capturadas (hilo UI y render).</summary>
    /// <param name="sender">Aplicación.</param>
    /// <param name="e">Excepción (se marca manejada para no cerrar).</param>
    private void OnExcepcionGlobal(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            Support.RegistroErrores.Registrar(e.Exception, "App.Global");
            e.Handled = true;
        }
        catch
        {
            // Si el registro falla, se deja el comportamiento por defecto.
        }
    }

    /// <summary>Registra excepciones de tareas en background (picos, audio).</summary>
    /// <param name="sender">Planificador.</param>
    /// <param name="e">Excepción (se marca observada).</param>
    private static void OnTareaSinObservar(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            Support.RegistroErrores.Registrar(e.Exception, "App.Tarea");
            e.SetObserved();
        }
        catch
        {
            // Diagnóstico best-effort.
        }
    }

    /// <summary>
    /// Crea y activa la ventana principal al lanzar la app.
    /// </summary>
    /// <param name="args">Detalles de la solicitud de lanzamiento.</param>
    /// <remarks>Aplica actualizaciones Velopack pendientes antes de abrir.</remarks>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            Velopack.VelopackApp.Build()
                .SetAutoApplyOnStartup(true)
                .Run();
        }
        catch
        {
            // Velopack no disponible (debug / primera ejecución sin instalar): arranque normal.
        }

        _window = new MainWindow();
        _window.Activate();

        // Telemetría fire-and-forget (nunca bloquea el arranque).
        _ = Support.Telemetria.TelemetriaFirebase.ReportarAsync();
    }
}
