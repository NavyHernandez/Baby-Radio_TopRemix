using BebeRadio.Services;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Ajustes de la app (diálogo Config): transición suave al pasar a Siguiente.
/// Carga del JSON portable y persiste cada cambio al instante.
/// También controla el dispositivo de salida de audio (aplica al instante a
/// ambos motores y persiste el ID).
/// </summary>
public sealed partial class ConfiguracionViewModel : ObservableObject, IDisposable
{
    /// <summary>Duraciones de transición elegibles (segundos).</summary>
    public static IReadOnlyList<int> DuracionesTransicion { get; } = [3, 5, 7];

    /// <summary>Transición suave al pulsar Siguiente (default activada).</summary>
    [ObservableProperty]
    public partial bool TransicionActivada { get; set; } = true;

    /// <summary>Segundos de la transición (3, 5 o 7; default 5).</summary>
    [ObservableProperty]
    public partial int TransicionSegundos { get; set; } = 5;

    /// <summary>Modo operador en pantalla completa con 2 paletas (default).</summary>
    [ObservableProperty]
    public partial bool OperadorPantallaCompleta { get; set; } = true;

    /// <summary>Índice 0-1 del modo operador (puente TwoWay para RadioButtons).</summary>
    /// <remarks>Los índices negativos (sin selección al inicializar) se ignoran:
    /// antes convertían a completa y reescribían 2 pantallas en el JSON.</remarks>
    public int OperadorIndice
    {
        get => OperadorPantallaCompleta ? 0 : 1;
        set
        {
            if (value < 0)
            {
                return;
            }

            OperadorPantallaCompleta = value <= 0;
        }
    }

    /// <summary>Lista de dispositivos de salida de audio disponibles.</summary>
    [ObservableProperty]
    public partial List<DispositivoAudio> Dispositivos { get; set; } = [];

    /// <summary>Índice del dispositivo seleccionado (-1 = predeterminado).</summary>
    [ObservableProperty]
    public partial int DispositivoIndice { get; set; } = -1;

    /// <summary>Aviso de salida ocupada (vacío = oculto; enlazado en el diálogo).</summary>
    [ObservableProperty]
    public partial string AvisoDispositivo { get; set; } = string.Empty;

    /// <summary>Índice 0-2 de la duración (puente TwoWay para RadioButtons).</summary>
    public int TransicionIndice
    {
        get => IndiceDe(TransicionSegundos);
        set
        {
            if (value >= 0 && value < DuracionesTransicion.Count)
            {
                TransicionSegundos = DuracionesTransicion[value];
            }
        }
    }

    /// <summary>Suprime el guardado durante la carga inicial.</summary>
    /// <remarks>Sin esto, cada setter del constructor persistía con los demás
    /// valores aún en default y pisaba el JSON (p. ej. 1 pantalla → 2).</remarks>
    private bool _cargando = true;

    /// <summary>Suprime la aplicación al cambiar de dispositivo mientras se
    /// refresca la lista (evita que el empujón TwoWay -1 del ComboBox resetee
    /// la selección y reescriba el JSON).</summary>
    private bool _refrescandoLista;

    /// <summary>Cola de despacho del diálogo (para encolar eventos del hilo COM a UI).</summary>
    private DispatcherQueue? _dispatcher;

    /// <summary>Serie del último cambio de dispositivo solicitado (solo el último gana).</summary>
    private int _serieDispositivo;

    /// <summary>Fija la cola de despacho del diálogo (llamar antes de InitializeComponent).</summary>
    /// <param name="dispatcher">DispatcherQueue del diálogo Config.</param>
    /// <remarks>
    /// También suscribe aquí los eventos de salida ocupada (con el dispatcher
    /// ya fijado, cualquier aviso posterior se encola al hilo UI) y carga el
    /// aviso pendiente si la ocupación es anterior a abrir el diálogo.
    /// </remarks>
    public void FijarDispatcher(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        var recuperacion = RecuperacionSalidaAudio.Instancia;
        recuperacion.SalidaOcupada += OnSalidaOcupada;
        recuperacion.SalidaRecuperada += OnSalidaRecuperada;

        if (recuperacion.OcupadoActual is { } ocupado)
        {
            AvisoDispositivo = FormatoAviso(ocupado.Nombre);
        }
    }

    /// <summary>Texto del aviso de salida ocupada por otra aplicación.</summary>
    /// <param name="nombre">Nombre del dispositivo ocupado.</param>
    /// <returns>Mensaje para el operador.</returns>
    private static string FormatoAviso(string nombre) =>
        $"Salida ocupada: '{nombre}' está en uso por otra aplicación (p. ej. Serato DJ). " +
        "Se usa el respaldo y se volverá a ella sola al liberarse.";

    /// <summary>Muestra el aviso de ocupación en el hilo UI.</summary>
    /// <param name="dispositivoId">ID del dispositivo ocupado (no usado; firma del evento).</param>
    /// <param name="nombre">Nombre del dispositivo ocupado.</param>
    private void OnSalidaOcupada(string dispositivoId, string nombre) =>
        EnUI(() => AvisoDispositivo = FormatoAviso(nombre));

    /// <summary>Quita el aviso al recuperar la salida en el hilo UI.</summary>
    /// <param name="dispositivoId">ID del dispositivo recuperado.</param>
    private void OnSalidaRecuperada(string dispositivoId) =>
        EnUI(() => AvisoDispositivo = string.Empty);

    /// <summary>Ejecuta la acción en el hilo UI si hay dispatcher; si no, la omite.</summary>
    /// <param name="accion">Acción a encolar (eventos de recuperación llegan del threadpool).</param>
    private void EnUI(Action accion)
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            accion();
            return;
        }

        dispatcher.TryEnqueue(() => accion());
    }

    /// <summary>Carga los ajustes guardados (o defaults si no hay).</summary>
    public ConfiguracionViewModel()
    {
        try
        {
            var config = ConsolaStore.Cargar();
            TransicionActivada = config.TransicionSiguienteActivada;
            TransicionSegundos = NormalizarSegundos(config.TransicionSiguienteSegundos);
            OperadorPantallaCompleta = config.OperadorPantallaCompleta;
            CargarDispositivos();
            EstablecerDispositivoIndice(config.DispositivoSalidaId);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Cargar");
        }
        finally
        {
            _cargando = false;
        }

        EnumeradorDispositivosAudio.Instancia.DispositivosCambiaron += OnDispositivosCambiaron;
    }

    /// <summary>Carga la lista de dispositivos de salida de audio.</summary>
    private void CargarDispositivos()
    {
        Dispositivos = [.. EnumeradorDispositivosAudio.Instancia.ObtenerDispositivos()];
    }

    /// <summary>Establece el índice del dispositivo por su ID.</summary>
    /// <param name="deviceId">ID del dispositivo (vacío = predeterminado).</param>
    private void EstablecerDispositivoIndice(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            DispositivoIndice = -1;
            return;
        }

        var indice = Dispositivos.FindIndex(d => d.Id == deviceId);
        DispositivoIndice = indice >= 0 ? indice : -1;
    }

    /// <summary>Actualiza la lista cuando cambian los dispositivos del sistema.</summary>
    /// <remarks>El evento llega en hilo COM: se encola al hilo UI antes de mutar.</remarks>
    private void OnDispositivosCambiaron()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            RefrescarListaDispositivos();
            return;
        }

        dispatcher.TryEnqueue(RefrescarListaDispositivos);
    }

    /// <summary>Refresca la lista de dispositivos preservando la selección.</summary>
    /// <remarks>
    /// Si el dispositivo seleccionado desaparece, se vuelve al predeterminado
    /// (índice -1) y se aplica a los motores + persiste. El flag
    /// <see cref="_refrescandoLista"/> evita que el empujón TwoWay -1 del
    /// ComboBox dispare la aplicación antes de reasignar el índice correcto.
    /// </remarks>
    private void RefrescarListaDispositivos()
    {
        var seleccionado = DispositivoIndice >= 0 && DispositivoIndice < Dispositivos.Count
            ? Dispositivos[DispositivoIndice].Id
            : string.Empty;

        _refrescandoLista = true;
        try
        {
            CargarDispositivos();

            if (string.IsNullOrEmpty(seleccionado))
            {
                // No había selección explícita (predeterminado): nada que preservar.
                return;
            }

            var nuevoIndice = Dispositivos.FindIndex(d => d.Id == seleccionado);
            if (nuevoIndice >= 0)
            {
                DispositivoIndice = nuevoIndice;
            }
            else
            {
                // El dispositivo seleccionado desapareció: volver al predeterminado.
                DispositivoIndice = -1;
                AplicarDispositivo(-1);
            }
        }
        finally
        {
            _refrescandoLista = false;
        }
    }

    /// <summary>Persiste el toggle al cambiar.</summary>
    /// <param name="value">Nuevo estado.</param>
    partial void OnTransicionActivadaChanged(bool value) => Guardar();

    /// <summary>Valida y persiste los segundos al cambiar.</summary>
    /// <param name="value">Segundos elegidos.</param>
    partial void OnTransicionSegundosChanged(int value)
    {
        var normalizado = NormalizarSegundos(value);
        if (normalizado != value)
        {
            TransicionSegundos = normalizado;
            return;
        }

        OnPropertyChanged(nameof(TransicionIndice));
        Guardar();
    }

    /// <summary>Solo admite 3, 5 o 7 (el resto cae a 5).</summary>
    /// <param name="segundos">Valor a normalizar.</param>
    /// <returns>3, 5, 7 o 5 por defecto.</returns>
    public static int NormalizarSegundos(int segundos) =>
        segundos is 3 or 5 or 7 ? segundos : 5;

    /// <summary>Persiste el modo operador al cambiar.</summary>
    /// <param name="value">Nuevo modo.</param>
    partial void OnOperadorPantallaCompletaChanged(bool value)
    {
        OnPropertyChanged(nameof(OperadorIndice));
        Guardar();
    }

    /// <summary>Aplica el cambio de dispositivo de salida de audio.</summary>
    /// <param name="value">Índice del dispositivo (-1 = predeterminado).</param>
    partial void OnDispositivoIndiceChanged(int value)
    {
        if (_cargando || _refrescandoLista)
        {
            return;
        }

        AplicarDispositivo(value);
    }

    /// <summary>Aplica el dispositivo a ambos motores y persiste el ID.</summary>
    /// <param name="value">Índice del dispositivo (-1 = predeterminado).</param>
    /// <remarks>
    /// El Stop/Dispose/Init de los WasapiOut va en background: con drivers DJ
    /// (event-sync) puede tardar cientos de ms y congelaría la UI. Persiste al
    /// instante; la serie <see cref="_serieDispositivo"/> descarta cambios
    /// viejos si el usuario picas varias veces seguidas.
    /// </remarks>
    private void AplicarDispositivo(int value)
    {
        var deviceId = value >= 0 && value < Dispositivos.Count
            ? Dispositivos[value].Id
            : string.Empty;

        // Cambio manual del usuario: el aviso de ocupación deja de aplicarse
        // (si el nuevo destino sigue ocupado, el evento lo volverá a mostrar).
        AvisoDispositivo = string.Empty;

        Guardar();

        var serie = System.Threading.Interlocked.Increment(ref _serieDispositivo);
        _ = Task.Run(() =>
        {
            if (serie != System.Threading.Volatile.Read(ref _serieDispositivo))
            {
                return;
            }

            try
            {
                MotorAudio.Instancia.EstablecerDispositivo(deviceId);
                if (serie != System.Threading.Volatile.Read(ref _serieDispositivo))
                {
                    return;
                }

                MezcladorEfectos.Instancia.EstablecerDispositivo(deviceId);
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "Config.AplicarDispositivo");
            }
        });
    }

    /// <summary>Desuscribe el evento de dispositivos (libera el VM zombi).</summary>
    /// <remarks>Llamar al cerrar el diálogo; evita que VMs viejos sigan
    /// aplicando cambios de índice a los motores y al JSON.</remarks>
    public void Dispose()
    {
        EnumeradorDispositivosAudio.Instancia.DispositivosCambiaron -= OnDispositivosCambiaron;
        var recuperacion = RecuperacionSalidaAudio.Instancia;
        recuperacion.SalidaOcupada -= OnSalidaOcupada;
        recuperacion.SalidaRecuperada -= OnSalidaRecuperada;
    }

    /// <summary>Índice 0-2 de unos segundos normalizados.</summary>
    /// <param name="segundos">Duración (se normaliza primero).</param>
    /// <returns>0 para 3 s, 1 para 5 s, 2 para 7 s.</returns>
    public static int IndiceDe(int segundos) => NormalizarSegundos(segundos) switch
    {
        3 => 0,
        5 => 1,
        _ => 2,
    };

    /// <summary>Guarda los ajustes en el JSON portable (best-effort).</summary>
    /// <remarks>No persiste durante la carga inicial (ver <see cref="_cargando"/>).</remarks>
    private void Guardar()
    {
        if (_cargando)
        {
            return;
        }

        try
        {
            var config = ConsolaStore.Cargar();
            config.TransicionSiguienteActivada = TransicionActivada;
            config.TransicionSiguienteSegundos = TransicionSegundos;
            config.OperadorPantallaCompleta = OperadorPantallaCompleta;
            config.DispositivoSalidaId = DispositivoIndice >= 0 && DispositivoIndice < Dispositivos.Count
                ? Dispositivos[DispositivoIndice].Id
                : string.Empty;
            ConsolaStore.Guardar(config);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Guardar");
        }
    }
}
