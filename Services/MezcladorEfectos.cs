using BebeRadio.Support;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Wasapi;

namespace BebeRadio.Services;

/// <summary>
/// Mezclador de efectos de paleta: hasta 6 voces simultáneas con salidas
/// independientes (mezcla real cuando Mix está armado). Cada voz aplica
/// cue + ganancia y auto-stop; avisa su fin natural con su id y reporta
/// niveles (máximo agregado a ~20 Hz para el VU).
/// El <see cref="MotorAudio"/> no se toca (cola + pre-escucha del editor).
/// Sin UI: los eventos llegan en hilo de audio (marshalar al hilo UI).
/// Reemplazos y stops salen con fundido de 120 ms (anti-clic); la voz saliente
/// se desvincula al instante y cae por debajo de la nueva.
/// </summary>
public sealed class MezcladorEfectos
{
    /// <summary>Instancia única (un solo mezclador de efectos).</summary>
    public static MezcladorEfectos Instancia { get; } = new();

    /// <summary>Voces simultáneas máximas (el 7º disparo se rechaza).</summary>
    public const int MaxVoces = 6;

    /// <summary>Duración del fundido de salida al reemplazar o detener (ms).</summary>
    /// <remarks>Caída suave anti-clic: la voz saliente baja en este tiempo mientras la nueva ya suena.</remarks>
    public const int FundidoSalidaMs = 120;

    /// <summary>Paso de la rampa del fundido (ms).</summary>
    private const int PasoFundidoMs = 10;

    /// <summary>Mínimo entre avisos de niveles (20 Hz para el VU).</summary>
    private static readonly TimeSpan IntervaloNiveles = TimeSpan.FromMilliseconds(50);

    /// <summary>Se eleva al terminar natural una voz (hilo de audio).</summary>
    public event Action<Guid>? VozTerminada;

    /// <summary>Niveles máximos L/R de las voces a 20 Hz (hilo de audio).</summary>
    public event Action<float, float>? Niveles;

    /// <summary>Crea el mezclador (usar <see cref="Instancia"/>).</summary>
    public MezcladorEfectos()
    {
    }

    private readonly object _candado = new();
    private readonly Dictionary<Guid, Voz> _voces = new();
    private readonly Dictionary<Guid, (Voz Voz, System.Threading.Timer Vigilante)> _fundidos = new();
    private readonly Dictionary<Guid, (float Izq, float Der)> _nivelesVoces = new();
    private long _ultimoAvisoTicks;
    private float _maestroLineal = 1f;
    private string _dispositivoId = string.Empty;

    /// <summary>Voces sonando ahora.</summary>
    public int VocesActivas
    {
        get
        {
            lock (_candado)
            {
                return _voces.Count;
            }
        }
    }

    /// <summary>Dispara un efecto en una voz libre.</summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="desde">Inicio del cue.</param>
    /// <param name="hasta">Fin o null (hasta el final).</param>
    /// <param name="gananciaDb">Ganancia en dB (volumen capado a 1).</param>
    /// <returns>Id de voz o null (saturado o archivo ilegible).</returns>
    public Guid? Disparar(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb)
    {
        lock (_candado)
        {
            if (_voces.Count >= MaxVoces)
            {
                return null;
            }
        }

        var id = Guid.NewGuid();
        var voz = new Voz(id, OnVozTerminada, (izq, der) => OnNivelVoz(id, izq, der));
        float maestro;
        lock (_candado)
        {
            maestro = _maestroLineal;
        }

        if (!voz.Arrancar(path, desde, hasta, gananciaDb, maestro, _dispositivoId))
        {
            return null;
        }

        lock (_candado)
        {
            if (_voces.Count >= MaxVoces)
            {
                voz.Soltar();
                return null;
            }

            _voces[id] = voz;
        }

        return id;
    }

    /// <summary>Detiene una voz sin avisar (segundo clic en su cart).</summary>
    /// <param name="id">Voz a detener.</param>
    /// <remarks>Corte inmediato; para salida suave usar <see cref="DetenerConFundido"/>.</remarks>
    public void Detener(Guid id)
    {
        Voz? voz;
        lock (_candado)
        {
            if (!_voces.Remove(id, out voz))
            {
                // Puede estar fundiéndose: cancelarla y cortar ya.
                if (CancelarFundidoBloqueado(id))
                {
                    return;
                }

                return;
            }

            _nivelesVoces.Remove(id);
        }

        voz.Silenciar();
        voz.Soltar();
    }

    /// <summary>
    /// Detiene una voz con fundido de salida (caída suave hasta silencio y stop).
    /// </summary>
    /// <param name="id">Voz a detener.</param>
    /// <param name="ms">Duración del fundido (por defecto <see cref="FundidoSalidaMs"/>).</param>
    /// <remarks>
    /// La voz sale de <see cref="VocesActivas"/> al instante (libera slot y VU);
    /// el audio cae por debajo mientras otras voces ya suenan.
    /// </remarks>
    public void DetenerConFundido(Guid id, int ms = FundidoSalidaMs)
    {
        Voz? voz;
        lock (_candado)
        {
            if (!_voces.Remove(id, out voz))
            {
                return;
            }

            _nivelesVoces.Remove(id);
        }

        IniciarFundido(id, voz, ms);
    }

    /// <summary>Volumen de usuario del fader maestro (0…1).</summary>
    /// <returns>Factor actual del fader.</returns>
    public double VolumenUsuario
    {
        get
        {
            lock (_candado)
            {
                return _maestroLineal;
            }
        }
    }

    /// <summary>
    /// Fija el volumen maestro de los efectos en vivo (fader total de salida).
    /// </summary>
    /// <param name="lineal">Factor 0…1 (1 = máximo).</param>
    /// <remarks>
    /// Aplica a voces nuevas y a las que ya suenan. Hilo-seguro.
    /// </remarks>
    public void FijarMaestro(double lineal)
    {
        var fijado = (float)Math.Clamp(lineal, 0, 1);
        List<Voz> voces;
        lock (_candado)
        {
            _maestroLineal = fijado;
            voces = _voces.Values.ToList();
        }

        foreach (var voz in voces)
        {
            voz.FijarMaestro(fijado);
        }
    }

    /// <summary>Detiene todas las voces (botón Stop: solo efectos).</summary>
    /// <remarks>Corte inmediato; para salida suave usar <see cref="DetenerTodosConFundido"/>.</remarks>
    public void DetenerTodos()
    {
        List<Voz> voces;
        lock (_candado)
        {
            voces = _voces.Values.ToList();
            _voces.Clear();
            _nivelesVoces.Clear();
            CancelarFundidosBloqueado();
        }

        foreach (var voz in voces)
        {
            voz.Silenciar();
            voz.Soltar();
        }
    }

    /// <summary>
    /// Detiene todas las voces con fundido de salida (Stop suave).
    /// </summary>
    /// <param name="ms">Duración del fundido (por defecto <see cref="FundidoSalidaMs"/>).</param>
    /// <remarks>
    /// Las voces salen del conteo al instante; el audio cae por debajo en <paramref name="ms"/>.
    /// Un nuevo disparo durante el fundido convive con la cola (solape breve y suave).
    /// </remarks>
    public void DetenerTodosConFundido(int ms = FundidoSalidaMs)
    {
        List<KeyValuePair<Guid, Voz>> voces;
        lock (_candado)
        {
            voces = _voces.ToList();
            _voces.Clear();
            _nivelesVoces.Clear();
        }

        foreach (var (id, voz) in voces)
        {
            IniciarFundido(id, voz, ms);
        }
    }

    /// <summary>Inicia el fundido de una voz ya desvinculada del conteo.</summary>
    /// <param name="id">Id de la voz.</param>
    /// <param name="voz">Voz a fundir.</param>
    /// <param name="ms">Duración del fundido.</param>
    private void IniciarFundido(Guid id, Voz voz, int ms)
    {
        if (ms <= 0)
        {
            voz.Silenciar();
            voz.Soltar();
            return;
        }

        System.Threading.Timer? vigilante = null;
        vigilante = new System.Threading.Timer(
            _ =>
            {
                lock (_candado)
                {
                    _fundidos.Remove(id);
                }

                vigilante?.Dispose();
            },
            null,
            ms + 50,
            Timeout.Infinite);

        voz.FundirYSoltar(ms, PasoFundidoMs);
        lock (_candado)
        {
            _fundidos[id] = (voz, vigilante);
        }
    }

    /// <summary>Cancela un fundido en curso y corta la voz ya (requiere candado).</summary>
    /// <param name="id">Voz en fundido.</param>
    /// <returns>True si había fundido y se canceló.</returns>
    private bool CancelarFundidoBloqueado(Guid id)
    {
        if (!_fundidos.Remove(id, out var fundido))
        {
            return false;
        }

        fundido.Vigilante.Dispose();
        fundido.Voz.Silenciar();
        fundido.Voz.Soltar();
        return true;
    }

    /// <summary>Cancela todos los fundidos en curso con corte inmediato (requiere candado).</summary>
    private void CancelarFundidosBloqueado()
    {
        var fundidos = _fundidos.Values.ToList();
        _fundidos.Clear();
        foreach (var (voz, vigilante) in fundidos)
        {
            vigilante.Dispose();
            voz.Silenciar();
            voz.Soltar();
        }
    }

    /// <summary>
    /// Cambia el dispositivo de salida de audio para todas las voces.
    /// </summary>
    /// <param name="deviceId">ID del dispositivo (vacío = predeterminado).</param>
    /// <remarks>
    /// Si hay voces activas, se detienen y se recrean con el nuevo dispositivo
    /// desde su posición actual. Si no hay voces, solo se actualiza el dispositivo.
    /// </remarks>
    public void EstablecerDispositivo(string deviceId)
    {
        List<(Guid Id, string Path, TimeSpan Desde, TimeSpan? Hasta, double GananciaDb)> vocesActivas;
        lock (_candado)
        {
            vocesActivas = _voces.Values
                .Select(v => (v.Id, v.PathActual, v.PosicionActual, v.HastaActual, v.GananciaDbActual))
                .ToList();
        }

        foreach (var (id, _, _, _, _) in vocesActivas)
        {
            Detener(id);
        }

        _dispositivoId = deviceId;

        foreach (var (id, path, desde, hasta, gananciaDb) in vocesActivas)
        {
            Disparar(path, desde, hasta, gananciaDb);
        }
    }

    /// <summary>Libera los recursos de una voz ya terminada.</summary>
    /// <param name="id">Voz a liberar (ignora si no existe).</param>
    /// <remarks>Llamar en el hilo UI tras el fin natural.</remarks>
    public void Liberar(Guid id)
    {
        Voz? voz;
        lock (_candado)
        {
            if (!_voces.Remove(id, out voz))
            {
                return;
            }

            _nivelesVoces.Remove(id);
        }

        voz.Soltar();
    }

    /// <summary>Propaga el fin natural (los recursos los libera el dueño).</summary>
    /// <param name="id">Voz terminada.</param>
    private void OnVozTerminada(Guid id) => VozTerminada?.Invoke(id);

    /// <summary>Agrega el nivel de una voz y avisa el máximo a 20 Hz.</summary>
    /// <param name="id">Voz que reporta (hilo de audio).</param>
    /// <param name="izq">Pico izquierdo 0-1.</param>
    /// <param name="der">Pico derecho 0-1.</param>
    private void OnNivelVoz(Guid id, float izq, float der)
    {
        float maxIzq = 0;
        float maxDer = 0;
        var avisar = false;
        lock (_candado)
        {
            if (!_voces.ContainsKey(id))
            {
                return;
            }

            _nivelesVoces[id] = (izq, der);
            var ahora = DateTime.UtcNow.Ticks;
            if (new TimeSpan(ahora - _ultimoAvisoTicks) >= IntervaloNiveles)
            {
                _ultimoAvisoTicks = ahora;
                avisar = true;
                foreach (var nivel in _nivelesVoces.Values)
                {
                    maxIzq = Math.Max(maxIzq, nivel.Izq);
                    maxDer = Math.Max(maxDer, nivel.Der);
                }
            }
        }

        if (avisar)
        {
            Niveles?.Invoke(maxIzq, maxDer);
        }
    }

    /// <summary>Abre el lector adecuado (directo o MediaFoundation).</summary>
    /// <param name="path">Ruta.</param>
    /// <returns>Lector posicionable.</returns>
    internal static WaveStream CrearLector(string path)
    {
        try
        {
            return new AudioFileReader(path);
        }
        catch
        {
            return new MediaFoundationReader(path);
        }
    }

    /// <summary>Una voz del mezclador (salida propia + auto-stop).</summary>
    /// <param name="id">Identificador de la voz.</param>
    /// <param name="alTerminar">Callback de fin natural.</param>
    /// <param name="alNivel">Callback de niveles (medidor de la voz).</param>
    private sealed class Voz(Guid id, Action<Guid> alTerminar, Action<float, float> alNivel)
    {
        /// <summary>Identificador único de la voz.</summary>
        public Guid Id => id;

        private IWavePlayer? _salida;
        private WaveStream? _lector;
        private VolumeSampleProvider? _maestro;
        private System.Threading.Timer? _parada;
        private System.Threading.Timer? _fader;
        private bool _cierreManual;

        /// <summary>Path del audio actualmente cargado en la voz.</summary>
        public string PathActual { get; private set; } = string.Empty;

        /// <summary>Posición actual de reproducción en la voz.</summary>
        public TimeSpan PosicionActual => _lector?.CurrentTime ?? TimeSpan.Zero;

        /// <summary>Fin efectivo del audio en la voz (null = hasta el final).</summary>
        public TimeSpan? HastaActual { get; private set; }

        /// <summary>Ganancia en dB aplicada a la voz.</summary>
        public double GananciaDbActual { get; private set; }

        /// <summary>Abre el audio y lo pone a sonar.</summary>
        /// <param name="path">Ruta del audio.</param>
        /// <param name="desde">Inicio del cue.</param>
        /// <param name="hasta">Auto-stop o null.</param>
        /// <param name="gananciaDb">Ganancia.</param>
        /// <param name="maestroLineal">Volumen maestro del fader (0…1).</param>
        /// <param name="dispositivoId">ID del dispositivo de salida (vacío = predeterminado).</param>
        /// <returns>True si arrancó.</returns>
        public bool Arrancar(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb, float maestroLineal, string dispositivoId)
        {
            try
            {
                var lector = CrearLector(path);
                lector.CurrentTime = desde < TimeSpan.Zero ? TimeSpan.Zero : desde;

                // Ganancia uniforme (también para MediaFoundation) + etapa maestra
                // del fader total: multiplica sin tocar la ganancia de jugada.
                var ganancia = new VolumeSampleProvider(lector.ToSampleProvider())
                {
                    Volume = (float)Math.Clamp(Math.Pow(10, gananciaDb / 20), 0, 1),
                };
                _maestro = new VolumeSampleProvider(ganancia)
                {
                    Volume = Math.Clamp(maestroLineal, 0f, 1f),
                };

                // Latencia baja para disparo inmediato (80 ms: equilibrio con CPU).
                _salida = CrearSalida(dispositivoId, 80);
                _salida.PlaybackStopped += (_, _) => AvisarTermino();
                var medidor = new MedidorPicos(_maestro);
                medidor.Niveles += (izq, der) => alNivel(izq, der);
                _salida.Init(medidor.ToWaveProvider());
                _lector = lector;
                PathActual = path;
                HastaActual = hasta;
                GananciaDbActual = gananciaDb;
                _salida.Play();

                if (hasta.HasValue && hasta.Value > desde)
                {
                    var ms = (long)(hasta.Value - desde).TotalMilliseconds;
                    _parada = new System.Threading.Timer(_ => PararAlFin(), null, ms, Timeout.Infinite);
                }

                return true;
            }
            catch
            {
                Soltar();
                return false;
            }
        }

        /// <summary>Crea la salida WasapiOut con el dispositivo seleccionado.</summary>
        /// <param name="deviceId">ID del dispositivo (vacío = predeterminado).</param>
        /// <param name="latenciaMs">Latencia en milisegundos.</param>
        /// <returns>La salida de audio lista para Init.</returns>
        private static IWavePlayer CrearSalida(string deviceId, int latenciaMs)
        {
            var dispositivo = string.IsNullOrEmpty(deviceId)
                ? EnumeradorDispositivosAudio.Instancia.ObtenerDispositivoPorDefecto()
                : EnumeradorDispositivosAudio.Instancia.ObtenerMMDevice(deviceId);

            if (dispositivo is null)
            {
                dispositivo = EnumeradorDispositivosAudio.Instancia.ObtenerDispositivoPorDefecto();
            }

            return new WasapiOut(dispositivo, AudioClientShareMode.Shared, true, latenciaMs);
        }

        /// <summary>Ajusta el maestro en vivo de esta voz.</summary>
        /// <param name="lineal">Factor 0…1.</param>
        public void FijarMaestro(float lineal)
        {
            if (_maestro is not null)
            {
                _maestro.Volume = Math.Clamp(lineal, 0f, 1f);
            }
        }

        /// <summary>Corta sin avisar (detención manual).</summary>
        public void Silenciar()
        {
            _cierreManual = true;
            _fader?.Dispose();
            _fader = null;
            try
            {
                _salida?.Stop();
            }
            catch
            {
                // Salida ya cerrada.
            }
        }

        /// <summary>
        /// Funde la voz hasta silencio y la suelta (salida suave anti-clic).
        /// </summary>
        /// <param name="totalMs">Duración del fundido.</param>
        /// <param name="pasoMs">Paso de la rampa.</param>
        /// <remarks>
        /// No avisa fin (cierre manual). Rampa lineal sobre la etapa maestra
        /// propia de la voz; al terminar, para y libera como <see cref="Silenciar"/> + <see cref="Soltar"/>.
        /// </remarks>
        public void FundirYSoltar(int totalMs, int pasoMs)
        {
            _cierreManual = true;
            var maestro = _maestro;
            if (maestro is null || totalMs <= 0 || pasoMs <= 0)
            {
                Silenciar();
                Soltar();
                return;
            }

            var pasos = Math.Max(1, totalMs / pasoMs);
            var restantes = pasos;
            float inicial;
            try
            {
                inicial = maestro.Volume;
            }
            catch
            {
                Silenciar();
                Soltar();
                return;
            }

            if (inicial <= 0)
            {
                Silenciar();
                Soltar();
                return;
            }

            _fader?.Dispose();
            _fader = new System.Threading.Timer(_ =>
            {
                restantes--;
                var factor = (float)restantes / pasos;
                try
                {
                    maestro.Volume = Math.Clamp(inicial * factor, 0f, 1f);
                }
                catch
                {
                    // Cadena ya liberada: terminar.
                    restantes = 0;
                }

                if (restantes <= 0)
                {
                    Silenciar();
                    Soltar();
                }
            }, null, pasoMs, pasoMs);
        }

        /// <summary>Libera salida, lector y timers (idempotente).</summary>
        /// <remarks>Nunca llamar dentro del callback de fin: lo invoca el dueño.</remarks>
        public void Soltar()
        {
            _parada?.Dispose();
            _parada = null;
            _fader?.Dispose();
            _fader = null;
            try
            {
                _salida?.Stop();
            }
            catch
            {
                // Salida ya cerrada.
            }

            _salida?.Dispose();
            _salida = null;
            _lector?.Dispose();
            _lector = null;
            _maestro = null;
            PathActual = string.Empty;
            HastaActual = null;
            GananciaDbActual = 0;
        }

        /// <summary>Avisa el fin natural (manual no avisa).</summary>
        private void AvisarTermino()
        {
            if (_cierreManual)
            {
                return;
            }

            _cierreManual = true;
            alTerminar(id);
        }

        /// <summary>Corta al llegar al fin del tramo.</summary>
        private void PararAlFin()
        {
            try
            {
                _salida?.Stop();
            }
            catch
            {
                _cierreManual = true;
                alTerminar(id);
            }
        }
    }
}
