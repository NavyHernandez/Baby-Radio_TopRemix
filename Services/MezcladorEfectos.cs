using BebeRadio.Support;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BebeRadio.Services;

/// <summary>
/// Mezclador de efectos de paleta: hasta 6 voces simultáneas con salidas
/// independientes (mezcla real cuando Mix está armado). Cada voz aplica
/// cue + ganancia y auto-stop; avisa su fin natural con su id y reporta
/// niveles (máximo agregado a ~20 Hz para el VU).
/// El <see cref="MotorAudio"/> no se toca (cola + pre-escucha del editor).
/// Sin UI: los eventos llegan en hilo de audio (marshalar al hilo UI).
/// </summary>
public sealed class MezcladorEfectos
{
    /// <summary>Instancia única (un solo mezclador de efectos).</summary>
    public static MezcladorEfectos Instancia { get; } = new();

    /// <summary>Voces simultáneas máximas (el 7º disparo se rechaza).</summary>
    public const int MaxVoces = 6;

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
    private readonly Dictionary<Guid, (float Izq, float Der)> _nivelesVoces = new();
    private long _ultimoAvisoTicks;

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
        if (!voz.Arrancar(path, desde, hasta, gananciaDb))
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
    public void Detener(Guid id)
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

        voz.Silenciar();
        voz.Soltar();
    }

    /// <summary>Detiene todas las voces (botón Stop: solo efectos).</summary>
    public void DetenerTodos()
    {
        List<Voz> voces;
        lock (_candado)
        {
            voces = _voces.Values.ToList();
            _voces.Clear();
            _nivelesVoces.Clear();
        }

        foreach (var voz in voces)
        {
            voz.Silenciar();
            voz.Soltar();
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
        private WaveOutEvent? _salida;
        private WaveStream? _lector;
        private System.Threading.Timer? _parada;
        private bool _cierreManual;

        /// <summary>Abre el audio y lo pone a sonar.</summary>
        /// <param name="path">Ruta.</param>
        /// <param name="desde">Inicio del cue.</param>
        /// <param name="hasta">Auto-stop o null.</param>
        /// <param name="gananciaDb">Ganancia.</param>
        /// <returns>True si arrancó.</returns>
        public bool Arrancar(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb)
        {
            try
            {
                var lector = CrearLector(path);
                lector.CurrentTime = desde < TimeSpan.Zero ? TimeSpan.Zero : desde;
                if (lector is AudioFileReader directo)
                {
                    directo.Volume = (float)Math.Clamp(Math.Pow(10, gananciaDb / 20), 0, 1);
                }

                // Latencia baja para disparo inmediato (80 ms: equilibrio con CPU).
                _salida = new WaveOutEvent { DesiredLatency = 80 };
                _salida.PlaybackStopped += (_, _) => AvisarTermino();
                var medidor = new MedidorPicos(lector.ToSampleProvider());
                medidor.Niveles += (izq, der) => alNivel(izq, der);
                _salida.Init(medidor.ToWaveProvider());
                _lector = lector;
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

        /// <summary>Corta sin avisar (detención manual).</summary>
        public void Silenciar()
        {
            _cierreManual = true;
            try
            {
                _salida?.Stop();
            }
            catch
            {
                // Salida ya cerrada.
            }
        }

        /// <summary>Libera salida, lector y timer (idempotente).</summary>
        /// <remarks>Nunca llamar dentro del callback de fin: lo invoca el dueño.</remarks>
        public void Soltar()
        {
            _parada?.Dispose();
            _parada = null;
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
