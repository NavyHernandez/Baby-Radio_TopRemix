using BebeRadio.Models;
using BebeRadio.Support;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BebeRadio.Services;

/// <summary>
/// Única salida de audio de Baby Radio (cola o tramo del editor).
/// Modo Cola: entrada completa a volumen 1. Modo Tramo: cue con ganancia.
/// Abrir un tramo pausa la cola; al cerrar no reanuda solo.
/// Sin UI: avisa por eventos (la vista marshala al hilo UI).
/// </summary>
public sealed class MotorAudio : IDisposable
{
    /// <summary>Instancia única (una sola WaveOut).</summary>
    public static MotorAudio Instancia { get; } = new();

    private WaveOutEvent? _salida;
    private WaveStream? _lector;
    private VolumeSampleProvider? _volumenMaestro;
    private float _maestroLineal = 1f;
    private float _usuarioLineal = 1f;
    private float _transicionLineal = 1f;
    private System.Threading.Timer? _parada;
    private bool _disposed;
    private bool _cierreManual;
    private int _generacion;

    /// <summary>Modo actual de reproducción.</summary>
    public ModoMotor Modo { get; private set; } = ModoMotor.Detenido;

    /// <summary>True mientras suena (cola o tramo).</summary>
    public bool Reproduciendo => _salida?.PlaybackState == PlaybackState.Playing;

    /// <summary>True en pausa (reanudable en posición).</summary>
    public bool EnPausa => _salida?.PlaybackState == PlaybackState.Paused;

    /// <summary>Posición actual (para tiempos y playhead).</summary>
    public TimeSpan Posicion => _lector?.CurrentTime ?? TimeSpan.Zero;

    /// <summary>Duración del audio abierto.</summary>
    public TimeSpan Duracion => _lector?.TotalTime ?? TimeSpan.Zero;

    /// <summary>Se eleva al terminar natural (indica el modo que terminó).</summary>
    public event Action<ModoMotor>? TerminadoNatural;

    /// <summary>Niveles crudos L/R a 20 Hz (el VU suaviza).</summary>
    public event Action<float, float>? Niveles;

    /// <summary>
    /// Fija el volumen maestro en vivo (ducking de Ganancia).
    /// </summary>
    /// <param name="lineal">Factor 0…1 (1 = sin atenuar).</param>
    /// <remarks>
    /// Se aplica al instante si algo suena y se conserva para las próximas
    /// reproducciones hasta que se vuelva a fijar. Asignación atómica de
    /// float: segura desde cualquier hilo.
    /// </remarks>
    public void FijarMaestro(double lineal)
    {
        _maestroLineal = (float)Math.Clamp(lineal, 0, 1);
        AplicarCombinado();
    }

    /// <summary>Volumen de usuario del fader maestro (0…1).</summary>
    /// <returns>Factor actual del fader.</returns>
    public double VolumenUsuario => _usuarioLineal;

    /// <summary>
    /// Fija el volumen del fader maestro en vivo (control total de salida).
    /// </summary>
    /// <param name="lineal">Factor 0…1 (1 = máximo).</param>
    /// <remarks>
    /// Se multiplica con el ducking y la transición; no los sustituye.
    /// Segura desde cualquier hilo (asignación atómica de float).
    /// </remarks>
    public void FijarMaestroUsuario(double lineal)
    {
        _usuarioLineal = (float)Math.Clamp(lineal, 0, 1);
        AplicarCombinado();
    }

    /// <summary>
    /// Ejecuta la transición suave hacia un destino en el tiempo pedido (rampa ~20 Hz).
    /// </summary>
    /// <param name="destinoLineal">Factor final 0…1.</param>
    /// <param name="duracion">Duración de la transición.</param>
    /// <param name="cancelacion">Cancela la rampa (conserva lo alcanzado).</param>
    /// <remarks>No toca el maestro (ducking): ambos factores se multiplican.</remarks>
    public async Task TransicionAsync(double destinoLineal, TimeSpan duracion, CancellationToken cancelacion = default)
    {
        var destino = (float)Math.Clamp(destinoLineal, 0, 1);
        const int pasosPorSegundo = 20;
        var pasos = Math.Max(1, (int)(duracion.TotalSeconds * pasosPorSegundo));
        var inicio = _transicionLineal;
        try
        {
            for (var i = 1; i <= pasos; i++)
            {
                await Task.Delay(duracion / pasos, cancelacion);
                _transicionLineal = inicio + ((destino - inicio) * i / pasos);
                AplicarCombinado();
            }
        }
        catch (OperationCanceledException)
        {
            // Se conserva el nivel alcanzado; el llamador decide.
        }
    }

    /// <summary>Aplica usuario × maestro × transición al provider vigente (si hay).</summary>
    private void AplicarCombinado()
    {
        if (_volumenMaestro is not null)
        {
            _volumenMaestro.Volume = Math.Clamp(_usuarioLineal * _maestroLineal * _transicionLineal, 0f, 1f);
        }
    }

    /// <summary>Crea el motor (usar <see cref="Instancia"/>).</summary>
    public MotorAudio()
    {
    }

    /// <summary>Reproduce una entrada desde cero (corta en su fin efectivo).</summary>
    /// <param name="entry">Entrada con FilePath existente.</param>
    /// <param name="gananciaDb">Ganancia de normalización en dB (0 = sin ajuste).</param>
    /// <returns>True si arrancó.</returns>
    public bool ReproducirCola(QueueEntry entry, double gananciaDb = 0)
    {
        if (string.IsNullOrWhiteSpace(entry.FilePath) || !System.IO.File.Exists(entry.FilePath))
        {
            return false;
        }

        return Abrir(entry.FilePath, TimeSpan.Zero, entry.FinEfectivo, gananciaDb, ModoMotor.Cola);
    }

    /// <summary>Reproduce un tramo del editor (pausa la cola).</summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="desde">Inicio del cue.</param>
    /// <param name="hasta">Fin o null (hasta el final).</param>
    /// <param name="gananciaDb">Ganancia en dB (volumen capado a 1).</param>
    /// <returns>True si arrancó.</returns>
    public bool ReproducirTramo(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb) =>
        Abrir(path, desde, hasta, gananciaDb, ModoMotor.Tramo);

    /// <summary>Pausa (reanudable).</summary>
    public void Pausar()
    {
        try
        {
            if (Reproduciendo)
            {
                _salida?.Pause();
            }
        }
        catch
        {
            // Salida ya cerrada.
        }
    }

    /// <summary>Reanuda tras pausar.</summary>
    public void Reanudar()
    {
        try
        {
            if (EnPausa)
            {
                _salida?.Play();
            }
        }
        catch
        {
            // Salida ya cerrada.
        }
    }

    /// <summary>Detiene y libera el audio abierto (sin avisar fin natural).</summary>
    public void Detener()
    {
        _cierreManual = true;
        Cerrar();
    }

    /// <summary>Libera la salida (al cerrar la app).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cerrar();
    }

    /// <summary>Abre un audio en el modo pedido.</summary>
    /// <param name="path">Ruta.</param>
    /// <param name="desde">Posición inicial.</param>
    /// <param name="hasta">Auto-stop o null.</param>
    /// <param name="gananciaDb">Ganancia.</param>
    /// <param name="modo">Modo.</param>
    /// <returns>True si arrancó.</returns>
    private bool Abrir(string path, TimeSpan desde, TimeSpan? hasta, double gananciaDb, ModoMotor modo)
    {
        _cierreManual = true;
        Cerrar();
        if (_disposed)
        {
            return false;
        }

        try
        {
            var lector = CrearLector(path);
            lector.CurrentTime = desde < TimeSpan.Zero ? TimeSpan.Zero : desde;

            // Ganancia uniforme para cualquier lector (incluido MediaFoundation):
            // AudioFileReader.Volume no aplica en el fallback. Tope en +18 dB para
            // la normalización (+12 dB), sin tocar el archivo en disco.
            var volumen = new VolumeSampleProvider(lector.ToSampleProvider())
            {
                Volume = (float)Math.Clamp(Math.Pow(10, gananciaDb / 20), 0, 8),
            };

            // Etapa maestra en vivo (ducking × transición): multiplica a la
            // ganancia de jugada sin tocarla; vive en campo para ajustarla.
            // Toda apertura arranca a transición plena (la siguiente entra fuerte).
            var maestro = new VolumeSampleProvider(volumen);
            _volumenMaestro = maestro;
            _transicionLineal = 1f;
            AplicarCombinado();

            var medidor = new MedidorPicos(maestro);
            medidor.Niveles += (izq, der) => Niveles?.Invoke(izq, der);
            _salida = new WaveOutEvent { DesiredLatency = 150 };
            _generacion++;
            var generacion = _generacion;
            _salida.PlaybackStopped += (_, _) =>
            {
                if (generacion == _generacion)
                {
                    AvisarTermino();
                }
            };
            _salida.Init(medidor.ToWaveProvider());
            _lector = lector;
            Modo = modo;
            _salida.Play();
            _cierreManual = false;

            if (hasta.HasValue && hasta.Value > desde)
            {
                var ms = (long)(hasta.Value - desde).TotalMilliseconds;
                _parada = new System.Threading.Timer(_ => PararAlFin(), null, ms, Timeout.Infinite);
            }

            return true;
        }
        catch
        {
            Cerrar();
            return false;
        }
    }

    /// <summary>Abre el lector adecuado (directo o MediaFoundation).</summary>
    /// <param name="path">Ruta.</param>
    /// <returns>Lector posicionable.</returns>
    private static WaveStream CrearLector(string path)
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

    /// <summary>Avisa el fin natural (manual no avisa).</summary>
    /// <remarks>Solo la generación vigente avisa (tardíos se ignoran).</remarks>
    private void AvisarTermino()
    {
        var modo = Modo;
        Modo = ModoMotor.Detenido;
        if (_cierreManual)
        {
            _cierreManual = false;
            return;
        }

        TerminadoNatural?.Invoke(modo);
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
            Modo = ModoMotor.Detenido;
            TerminadoNatural?.Invoke(ModoMotor.Tramo);
        }
    }

    /// <summary>Corta y libera (idempotente, seguro en callbacks).</summary>
    private void Cerrar()
    {
        Modo = ModoMotor.Detenido;
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

        // No disponer la salida dentro de su propio callback:
        // se libera al abrir lo siguiente o al Dispose.
        _salida?.Dispose();
        _salida = null;
        _lector?.Dispose();
        _lector = null;
        _volumenMaestro = null;
    }
}
