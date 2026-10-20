using BebeRadio.Services;

namespace BebeRadio.Support;

/// <summary>
/// Vigila la liberación del dispositivo de salida preferido cuando una otra
/// aplicación (p. ej. Serato DJ sobre un controlador DDJ-FLX4) lo retuvo en
/// modo exclusivo y forzó el respaldo a otro endpoint.
/// Al producirse el respaldo (<see cref="AdaptadorSalidaAudio.RespaldoPorOcupacion"/>),
/// sondea el preferido cada pocos segundos con un Init descartable —no
/// interrumpe el audio que está sonando en el respaldo— y, cuando ya está
/// libre, reaplica el dispositivo guardado a ambos motores.
/// </summary>
/// <remarks>
/// Solo recupera si el ID sigue siendo el guardado en
/// <see cref="ConsolaStore.Cargar"/>: si el usuario eligió otro destino,
/// abandona la vigilancia.
/// </remarks>
internal sealed class RecuperacionSalidaAudio
{
    private static readonly Lazy<RecuperacionSalidaAudio> _lazy = new(() => new());

    /// <summary>Instancia única (suscribe el evento del adaptador al crearse).</summary>
    public static RecuperacionSalidaAudio Instancia => _lazy.Value;

    /// <summary>Intervalo entre sondas de liberación del dispositivo.</summary>
    private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromSeconds(5);

    /// <summary>Serie de vigilancia: solo el último respaldo gana (los viejos se abandonan).</summary>
    private int _serie;

    private readonly object _candado = new();
    private (string Id, string Nombre)? _ocupadoActual;

    /// <summary>Se dispara al caer a respaldo por ocupación (ID, nombre) — para la UI.</summary>
    public event Action<string, string>? SalidaOcupada;

    /// <summary>Se dispara al regresar al dispositivo preferido (ID) — para la UI.</summary>
    public event Action<string>? SalidaRecuperada;

    /// <summary>
    /// Dispositivo ocupado pendiente de recuperar, si lo hay (para que la UI
    /// muestre el aviso al abrirse, aunque la ocupación sea anterior).
    /// </summary>
    public (string Id, string Nombre)? OcupadoActual
    {
        get
        {
            lock (_candado)
            {
                return _ocupadoActual;
            }
        }
    }

    /// <summary>Crea la instancia y suscribe el evento de respaldo del adaptador.</summary>
    private RecuperacionSalidaAudio()
    {
        AdaptadorSalidaAudio.RespaldoPorOcupacion += OnRespaldoPorOcupacion;
    }

    /// <summary>Arranca (o reinicia) la vigilancia para el dispositivo ocupado.</summary>
    /// <param name="dispositivoId">ID del dispositivo preferido a recuperar.</param>
    /// <param name="nombre">Nombre amigable (para el aviso de UI y el log).</param>
    private void OnRespaldoPorOcupacion(string dispositivoId, string nombre)
    {
        if (string.IsNullOrWhiteSpace(dispositivoId))
        {
            return;
        }

        lock (_candado)
        {
            _ocupadoActual = (dispositivoId, nombre);
        }

        var serie = System.Threading.Interlocked.Increment(ref _serie);
        SalidaOcupada?.Invoke(dispositivoId, nombre);
        _ = VigilarLiberacionAsync(dispositivoId, nombre, serie);
    }

    /// <summary>Bucle de sondeo: al liberarse, reaplica el dispositivo guardado a los motores.</summary>
    /// <param name="dispositivoId">ID del dispositivo preferido.</param>
    /// <param name="nombre">Nombre amigable para el log.</param>
    /// <param name="serie">Serie de vigilancia de esta pasada.</param>
    /// <returns>Tarea del bucle (fire-and-forget desde el evento).</returns>
    private async Task VigilarLiberacionAsync(string dispositivoId, string nombre, int serie)
    {
        try
        {
            while (serie == System.Threading.Volatile.Read(ref _serie))
            {
                await Task.Delay(IntervaloSondeo).ConfigureAwait(false);
                if (serie != System.Threading.Volatile.Read(ref _serie))
                {
                    return;
                }

                if (!string.Equals(ConsolaStore.Cargar().DispositivoSalidaId, dispositivoId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // El usuario eligió otro destino: abandonar la vigilancia.
                    LimpiarOcupado(dispositivoId);
                    return;
                }

                if (!AdaptadorSalidaAudio.EstaLibre(dispositivoId))
                {
                    continue;
                }

                await Task.Run(() =>
                {
                    MotorAudio.Instancia.EstablecerDispositivo(dispositivoId);
                    MezcladorEfectos.Instancia.EstablecerDispositivo(dispositivoId);
                }).ConfigureAwait(false);

                LimpiarOcupado(dispositivoId);
                RegistroErrores.Traza("RecuperacionSalidaAudio",
                    $"'{nombre}' liberada; se recuperó la salida preferida.");
                SalidaRecuperada?.Invoke(dispositivoId);
                return;
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "RecuperacionSalidaAudio.VigilarLiberacion");
        }
    }

    /// <summary>Olvida la ocupación pendiente solo si sigue siendo la indicada.</summary>
    /// <param name="dispositivoId">ID de la ocupación a limpiar.</param>
    private void LimpiarOcupado(string dispositivoId)
    {
        lock (_candado)
        {
            if (_ocupadoActual is { } dato &&
                string.Equals(dato.Id, dispositivoId, StringComparison.OrdinalIgnoreCase))
            {
                _ocupadoActual = null;
            }
        }
    }
}
