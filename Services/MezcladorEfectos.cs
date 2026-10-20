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
/// se desvincula al instante y cae por debajo de la nueva. El fin natural
/// (cue o final del archivo) también baja suave en ese mismo tiempo antes de
/// cortar: no hay corte seco.
/// </summary>
public sealed class MezcladorEfectos
{
    /// <summary>Instancia única (un solo mezclador de efectos).</summary>
    public static MezcladorEfectos Instancia { get; } = new();

    /// <summary>Voces simultáneas máximas (el 7º disparo se rechaza).</summary>
    public const int MaxVoces = 6;

    /// <summary>Duración del fundido de salida al reemplazar, detener o terminar (ms).</summary>
    /// <remarks>
    /// Caída suave anti-clic: la voz saliente baja en este tiempo mientras la
    /// nueva ya suena. Rige también el fin natural (cue o final del archivo).
    /// </remarks>
    public const int FundidoSalidaMs = 120;

    /// <summary>Paso de la rampa del fundido (ms).</summary>
    private const int PasoFundidoMs = 10;

    /// <summary>Latencia de cada disparo de voz (buffer del WasapiOut, ms).</summary>
    /// <remarks>30 ms: disparo casi inmediato con CPU modesta; subir a 40-50 si se oyen tirones.</remarks>
    private const int LatenciaVozMs = 30;

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
    /// <param name="gananciaDb">Ganancia en dB (volumen lineal capado a 8 = +18 dB).</param>
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
        string dispositivoId;
        lock (_candado)
        {
            maestro = _maestroLineal;
            dispositivoId = _dispositivoId;
        }

        if (!voz.Arrancar(path, desde, hasta, gananciaDb, maestro, dispositivoId))
        {
            return null;
        }

        var saturada = false;
        lock (_candado)
        {
            if (_voces.Count >= MaxVoces)
            {
                saturada = true;
            }
            else
            {
                _voces[id] = voz;
            }
        }

        if (saturada)
        {
            // Stop/Dispose fuera del candado: el hilo de render toma _candado
            // en OnNivelVoz y un Stop event-sync puede esperar a ese hilo.
            voz.Soltar();
            return null;
        }

        return id;
    }

    /// <summary>Detiene una voz sin avisar (segundo clic en su cart).</summary>
    /// <param name="id">Voz a detener.</param>
    /// <remarks>Corte inmediato; para salida suave usar <see cref="DetenerConFundido"/>.</remarks>
    public void Detener(Guid id)
    {
        Voz? voz = null;
        Voz? fundida = null;
        lock (_candado)
        {
            if (_voces.Remove(id, out voz))
            {
                _nivelesVoces.Remove(id);
            }
            else if (_fundidos.Remove(id, out var fundido))
            {
                // Puede estar fundiéndose: cancelarla y cortar ya.
                fundido.Vigilante.Dispose();
                fundida = fundido.Voz;
            }
        }

        var objetivo = voz ?? fundida;
        if (objetivo is null)
        {
            return;
        }

        // Fuera del candado: Stop/Dispose no deben bloquear OnNivelVoz.
        objetivo.Silenciar();
        objetivo.Soltar();
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
        List<Voz> fundidas;
        lock (_candado)
        {
            voces = _voces.Values.ToList();
            _voces.Clear();
            _nivelesVoces.Clear();
            fundidas = QuitarFundidosBloqueado();
        }

        // Stop/Dispose fuera del candado (patrón de Detener/Liberar).
        foreach (var voz in voces.Concat(fundidas))
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

    /// <summary>
    /// Retira todos los fundidos en curso y devuelve sus voces para cortarlas
    /// fuera del candado (requiere candado; no llama Stop/Dispose).
    /// </summary>
    /// <returns>Voces que estaban en fundido (liberar fuera del candado).</returns>
    private List<Voz> QuitarFundidosBloqueado()
    {
        var fundidos = _fundidos.Values.ToList();
        _fundidos.Clear();
        var voces = new List<Voz>(fundidos.Count);
        foreach (var (voz, vigilante) in fundidos)
        {
            vigilante.Dispose();
            voces.Add(voz);
        }

        return voces;
    }

    /// <summary>
    /// Cambia el dispositivo de salida de audio para todas las voces.
    /// </summary>
    /// <param name="deviceId">ID del dispositivo (vacío = predeterminado).</param>
    /// <remarks>
    /// Cada voz se reabre en el nuevo dispositivo CONSERVANDO su Guid (el
    /// rastreo de PaletaViewModel y el titileo siguen válidos). El reintento
    /// con el predeterminado vive en
    /// <see cref="AdaptadorSalidaAudio.CrearInicializadaConFallback"/>: aunque
    /// el nuevo dispositivo falle al Init, la voz (y los próximos disparos)
    /// siguen sonando por el default. Un fallo aislado no aborta el bucle:
    /// la voz que no pueda reabrirse se retira y se avisa fin natural.
    /// </remarks>
    public void EstablecerDispositivo(string deviceId)
    {
        List<Voz> vocesActivas;
        lock (_candado)
        {
            // Fijar ANTES del snapshot: un Disparar concurrente debe montar ya
            // en el dispositivo nuevo (o al menos ser incluido en el snapshot).
            _dispositivoId = deviceId;
            vocesActivas = _voces.Values.ToList();
        }

        foreach (var voz in vocesActivas)
        {
            bool reabierta;
            try
            {
                reabierta = voz.ReabrirEnDispositivo(deviceId);
            }
            catch (Exception ex)
            {
                // Un driver que lance no debe abortar la migración de las demás.
                RegistroErrores.Registrar(ex, "MezcladorEfectos.EstablecerDispositivo");
                reabierta = false;
            }

            if (reabierta)
            {
                continue;
            }

            // Falló el reabrir: retirar la voz y avisar al dueño (limpia slot).
            lock (_candado)
            {
                _voces.Remove(voz.Id);
                _nivelesVoces.Remove(voz.Id);
            }

            OnVozTerminada(voz.Id);
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
        private System.Threading.Timer? _faderFin;
        private bool _cierreManual;
        private int _generacion;

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
                Montar(path, desde, hasta, gananciaDb, maestroLineal, dispositivoId);
                return true;
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "MezcladorEfectos.Voz.Arrancar");
                Soltar();
                return false;
            }
        }

        /// <summary>
        /// Reabre la voz en otro dispositivo de salida conservando su Guid,
        /// posición actual, cue, ganancia y volumen maestro.
        /// </summary>
        /// <param name="dispositivoId">ID del nuevo dispositivo (vacío = predeterminado).</param>
        /// <returns>True si se reabrió; false si falló (la voz queda liberada).</returns>
        /// <remarks>
        /// El GUID no cambia: el dueño (PaletaViewModel) sigue rastreando la voz
        /// y el titileo se apaga solo cuando termine natural. Si el nuevo
        /// dispositivo falla, se libera la voz y se devuelve false para que el
        /// llamador la retire del mezclador y avise al dueño.
        /// </remarks>
        public bool ReabrirEnDispositivo(string dispositivoId)
        {
            var path = PathActual;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var desde = PosicionActual;
            var hasta = HastaActual;
            var gananciaDb = GananciaDbActual;
            var maestroLineal = _maestro?.Volume ?? 1f;

            // Invalidar la generación ANTES de soltar: el Stop del viejo no debe
            // disparar fin natural sobre la voz ya reabierta.
            _generacion++;
            _cierreManual = true;
            try
            {
                Soltar();
            }
            catch (Exception ex)
            {
                // Soltar ya es best-effort, pero un driver extremo no debe
                // impedir el reabrir ni dejar _cierreManual trabado.
                RegistroErrores.Registrar(ex, "MezcladorEfectos.Voz.ReabrirEnDispositivo.Soltar");
            }
            finally
            {
                _cierreManual = false;
            }

            try
            {
                Montar(path, desde, hasta, gananciaDb, maestroLineal, dispositivoId);
                return true;
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "MezcladorEfectos.Voz.ReabrirEnDispositivo");
                try
                {
                    Soltar();
                }
                catch
                {
                    // Ya liberado: no enmascarar el fallo real.
                }

                return false;
            }
        }

        /// <summary>Monta la cadena (lector → ganancia → maestro → medidor) y la pone a sonar.</summary>
        /// <param name="path">Ruta del audio.</param>
        /// <param name="desde">Posición inicial.</param>
        /// <param name="hasta">Auto-stop o null.</param>
        /// <param name="gananciaDb">Ganancia en dB.</param>
        /// <param name="maestroLineal">Volumen maestro del fader (0…1).</param>
        /// <param name="dispositivoId">ID del dispositivo (vacío = predeterminado).</param>
        /// <remarks>Lanza si el dispositivo no puede abrir; el llamador decide el cleanup.</remarks>
        private void Montar(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb, float maestroLineal, string dispositivoId)
        {
            var lector = CrearLector(path);
            lector.CurrentTime = desde < TimeSpan.Zero ? TimeSpan.Zero : desde;

            // Ganancia uniforme (también para MediaFoundation) + etapa maestra
            // del fader total: multiplica sin tocar la ganancia de jugada.
            // Tope +18 dB, igual que MotorAudio: el editor permite hasta +6 dB
            // y antes quedaba silenciado en 1.0 (0 dB).
            var ganancia = new VolumeSampleProvider(lector.ToSampleProvider())
            {
                Volume = (float)Math.Clamp(Math.Pow(10, gananciaDb / 20), 0, 8),
            };
            _maestro = new VolumeSampleProvider(ganancia)
            {
                Volume = Math.Clamp(maestroLineal, 0f, 1f),
            };

            var medidor = new MedidorPicos(_maestro);
            medidor.Niveles += (izq, der) => alNivel(izq, der);

            // Latencia mínima para disparo inmediato (LatenciaVozMs). El adaptador
            // inicializa la salida (Init) adaptada al dispositivo elegido.
            _salida = CrearSalida(dispositivoId, LatenciaVozMs, medidor);
            _generacion++;
            var generacion = _generacion;
            _salida.PlaybackStopped += (_, _) =>
            {
                if (generacion == _generacion)
                {
                    AvisarTermino();
                }
            };
            _lector = lector;
            PathActual = path;
            HastaActual = hasta;
            GananciaDbActual = gananciaDb;
            _salida.Play();

            // Fin natural: con cue o hasta el final del archivo. El corte
            // no es seco: arranca la caída FundidoSalidaMs antes y corta
            // recién cuando el volumen llega a 0.
            var finEfectivo = hasta.HasValue
                ? TimeSpan.FromTicks(Math.Min(hasta.Value.Ticks, lector.TotalTime.Ticks))
                : lector.TotalTime;
            var restanteMs = (finEfectivo - desde).TotalMilliseconds;
            if (restanteMs > 0)
            {
                var fundido = (int)Math.Min(FundidoSalidaMs, restanteMs / 2);
                var inicioMs = Math.Max(0, (long)(restanteMs - fundido));
                _parada = new System.Threading.Timer(
                    _ => IniciarFinConFundido(fundido), null, inicioMs, Timeout.Infinite);
            }
        }

        /// <summary>Crea e inicializa la salida WasapiOut con el dispositivo seleccionado.</summary>
        /// <param name="deviceId">ID del dispositivo (vacío = predeterminado).</param>
        /// <param name="latenciaMs">Latencia en milisegundos.</param>
        /// <param name="fuente">Cadena de audio a reproducir (con medidor).</param>
        /// <returns>La salida de audio inicializada (lista para Play).</returns>
        /// <remarks>Si el dispositivo elegido falla, el adaptador reintenta con el predeterminado.</remarks>
        private static IWavePlayer CrearSalida(string deviceId, int latenciaMs, ISampleProvider fuente) =>
            AdaptadorSalidaAudio.CrearInicializadaConFallback(
                deviceId, fuente, latenciaMs, "MezcladorEfectos.Voz");

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
            LiberarFaderFin();
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
            LiberarFaderFin();
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

        /// <summary>Libera salida, lector y timers (idempotente, seguro con drivers rotos).</summary>
        /// <remarks>
        /// Nunca llamar dentro del callback de fin: lo invoca el dueño. Los
        /// <c>Dispose</c> van en try/catch (COMException de endpoints
        /// desconectados/controladores DJ) y el estado se limpia igualmente.
        /// </remarks>
        public void Soltar()
        {
            _parada?.Dispose();
            _parada = null;
            _fader?.Dispose();
            _fader = null;
            LiberarFaderFin();
            try
            {
                _salida?.Stop();
            }
            catch
            {
                // Salida ya cerrada.
            }

            try
            {
                _salida?.Dispose();
            }
            catch
            {
                // Driver roto: best-effort, no enmascara el flujo.
            }

            _salida = null;
            try
            {
                _lector?.Dispose();
            }
            catch
            {
                // Lector ya liberado.
            }

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

        /// <summary>
        /// Baja el volumen hasta 0 en <paramref name="totalMs"/> y luego corta.
        /// </summary>
        /// <param name="totalMs">Duración de la caída.</param>
        /// <remarks>
        /// Fin natural suave (anti-clic): NO marca <see cref="_cierreManual"/>,
        /// así que el Stop final dispara <see cref="AvisarTermino"/> y el dueño
        /// libera la voz — el mismo camino que el corte seco, solo que llega
        /// cuando el audio ya está en silencio. Usa su propio temporizador
        /// (<see cref="_faderFin"/>) para no pisar un fundido manual en curso;
        /// si otro cierre tomó el control, se retira sin cortar.
        /// </remarks>
        private void IniciarFinConFundido(int totalMs)
        {
            if (_cierreManual)
            {
                // Un Stop/reemplazo manual ya se está haciendo cargo de la voz.
                return;
            }

            _parada?.Dispose();
            _parada = null;
            var maestro = _maestro;
            if (maestro is null || totalMs <= 0 || PasoFundidoMs <= 0)
            {
                PararAlFin();
                return;
            }

            float inicial;
            try
            {
                inicial = maestro.Volume;
            }
            catch
            {
                PararAlFin();
                return;
            }

            if (inicial <= 0)
            {
                PararAlFin();
                return;
            }

            var pasos = Math.Max(1, totalMs / PasoFundidoMs);
            var restantes = pasos;
            _faderFin?.Dispose();
            _faderFin = new System.Threading.Timer(_ =>
            {
                if (_cierreManual)
                {
                    // Otro cierre tomó el control: soltar este temporizador y no cortar.
                    LiberarFaderFin();
                    return;
                }

                restantes--;
                var factor = (float)restantes / pasos;
                try
                {
                    maestro.Volume = Math.Clamp(inicial * factor, 0f, 1f);
                }
                catch
                {
                    // Cadena ya liberada: cortar ya.
                    restantes = 0;
                }

                if (restantes <= 0)
                {
                    LiberarFaderFin();
                    PararAlFin();
                }
            }, null, PasoFundidoMs, PasoFundidoMs);
        }

        /// <summary>Libera el temporizador de la caída natural (idempotente).</summary>
        private void LiberarFaderFin()
        {
            _faderFin?.Dispose();
            _faderFin = null;
        }

        /// <summary>Corta al llegar al fin del tramo (ya sin volumen, tras la caída).</summary>
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
