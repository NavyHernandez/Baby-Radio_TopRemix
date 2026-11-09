using BebeRadio.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace BebeRadio;

/// <summary>
/// Aplicación Baby Radio: registra recursos globales (ver App.xaml) y abre
/// <see cref="MainWindow"/> al lanzar. Sin lógica de negocio.
/// Maneja además instancia única (doble clic en canciones con la app abierta
/// redirige a la instancia existente) y activación por archivo.
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
    /// <remarks>
    /// Aplica actualizaciones Velopack pendientes antes de abrir, registra las
    /// asociaciones de audio (solo instalado: hooks OnFirstRun/OnRestarted),
    /// y si otra instancia ya corre redirige la activación (archivos del
    /// Explorador) y sale sin abrir segunda ventana.
    /// </remarks>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // El log de diagnóstico solo conserva la sesión actual (best-effort).
        Support.RegistroErrores.IniciarSesion();

        try
        {
            // Los hooks corren dentro de Run(); los procesos --veloapp-* salen
            // ahí mismos. OnFirstRun/OnRestarted registran asociaciones de audio
            // (per-user, HKCU) — nunca en copias debug/portable.
            Velopack.VelopackApp.Build()
                .SetAutoApplyOnStartup(true)
                .OnFirstRun(_ => Support.AsociadorArchivos.Registrar())
                .OnRestarted(_ => Support.AsociadorArchivos.Registrar())
                .OnBeforeUninstallFastCallback(_ => Support.AsociadorArchivos.Desregistrar())
                .Run();
        }
        catch
        {
            // Velopack no disponible (debug / primera ejecución sin instalar): arranque normal.
        }

        // Instancia única: si ya corre una instancia dueña de la clave,
        // redirigir esta activación (doble clic en canciones) y salir.
        try
        {
            var instancia = AppInstance.FindOrRegisterForKey("baby-radio");
            if (!instancia.IsCurrent)
            {
                var activacion = AppInstance.GetCurrent().GetActivatedEventArgs();
                await instancia.RedirectActivationToAsync(activacion);
                Environment.Exit(0);
                return;
            }

            instancia.Activated += OnActivacionRedirigida;
        }
        catch (Exception ex)
        {
            // Sin instancia única la app sigue abriendo (multi-instancia normal).
            Support.RegistroErrores.Registrar(ex, "App.InstanciaUnica");
        }

        _window = new MainWindow();
        _window.Activate();
        _window.Closed += (_, _) => KeyMappingService.Instancia.Dispose();

        // Archivos del doble clic del Explorador (activación inicial).
        try
        {
            ProcesarArchivosActivados(AppInstance.GetCurrent().GetActivatedEventArgs());
        }
        catch (Exception ex)
        {
            Support.RegistroErrores.Registrar(ex, "App.ActivacionInicial");
            // Fallback: args crudos de la línea de comandos del ProgID.
            ProcesarArchivosActivados(null);
        }

        // Recuperación de salida ocupada: debe estar suscrita ANTES de aplicar
        // el dispositivo guardado (si Serato retiene el controlador en exclusivo,
        // el primer fallback dispara su vigilancia).
        _ = Support.RecuperacionSalidaAudio.Instancia;

        // Aplicar el dispositivo de salida guardado (fire-and-forget: sin audio
        // cargado solo fija el id; nunca bloquea el arranque).
        _ = Task.Run(() =>
        {
            try
            {
                var id = Support.ConsolaStore.Cargar().DispositivoSalidaId;
                if (!string.IsNullOrEmpty(id))
                {
                    Services.MotorAudio.Instancia.EstablecerDispositivo(id);
                    Services.MezcladorEfectos.Instancia.EstablecerDispositivo(id);
                }

                // Calienta el endpoint (aunque no haya dispositivo guardado) para
                // que el primer Play o el primer disparo de paleta no arrastren
                // el retardo de arranque en frío del motor de audio.
                Services.MotorAudio.Instancia.Precalentar();
                Services.MezcladorEfectos.Instancia.Precalentar();
            }
            catch (Exception ex)
            {
                Support.RegistroErrores.Registrar(ex, "App.DispositivoSalida");
            }
        });

        // Telemetría fire-and-forget (nunca bloquea el arranque).
        _ = Support.Telemetria.TelemetriaFirebase.ReportarAsync();
    }

    /// <summary>
    /// Atiende activaciones redirigidas (app ya abierta + doble clic en
    /// canciones): encola los archivos y trae la ventana al frente.
    /// </summary>
    /// <param name="sender">Instancia de app.</param>
    /// <param name="e">Args de la activación redirigida.</param>
    private void OnActivacionRedirigida(object? sender, AppActivationArguments e) =>
        _window?.DispatcherQueue.TryEnqueue(() => ProcesarArchivosActivados(e));

    /// <summary>
    /// Encola en la consola los archivos de la activación y reproduce el
    /// primero del lote (doble clic = suena). Sin archivos solo trae la
    /// ventana al frente.
    /// </summary>
    /// <param name="activacion">Args de activación (File o Launch con args).</param>
    private void ProcesarArchivosActivados(AppActivationArguments? activacion)
    {
        try
        {
            if (_window is not MainWindow ventana)
            {
                return;
            }

            var rutas = Support.ActivacionArchivos.ExtraerRutas(activacion);
            if (rutas.Count == 0 && activacion is null)
            {
                // Fallback del arranque: línea de comandos cruda del ProgID.
                rutas = Support.ActivacionArchivos.ExtraerDeCommandLine();
            }

            ventana.AbrirArchivosActivados(rutas);
        }
        catch (Exception ex)
        {
            Support.RegistroErrores.Registrar(ex, "App.ActivacionArchivos");
        }
    }
}
