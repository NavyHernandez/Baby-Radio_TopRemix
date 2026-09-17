using System.Threading.Channels;

namespace BebeRadio.Support;

/// <summary>
/// Analiza audios en segundo plano con un único hilo dedicado de prioridad
/// baja (<c>BelowNormal</c>): nunca compite con el hilo de audio ni con la UI.
/// Encola rutas deduplicadas y, por cada una, calcula el loudness integrado
/// (BS.1770), lo cachea y detecta el fin efectivo (recorte de silencio final).
/// El motor de reproducción solo lee la caché; el análisis jamás corre en el
/// hilo de reproducción. Serial: como máximo usa un núcleo "sobrante".
/// </summary>
public sealed class ServicioAnalisisAudio
{
    /// <summary>Instancia única del servicio de análisis.</summary>
    public static ServicioAnalisisAudio Instancia { get; } = new();

    /// <summary>Capacidad máxima de la cola (excedente se descarta sin bloquear).</summary>
    private const int CapacidadCola = 256;

    /// <summary>Se eleva al terminar una entrada (hilo del worker).</summary>
    /// <remarks>Argumentos: ruta, fin efectivo (null = sin recorte), ganancia dB.</remarks>
    public event Action<string, TimeSpan?, double>? Preparado;

    private readonly Channel<(string Path, TimeSpan Duracion)> _cola =
        Channel.CreateBounded<(string, TimeSpan)>(new BoundedChannelOptions(CapacidadCola)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly HashSet<string> _pendientes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _candado = new();
    private Thread? _hilo;

    /// <summary>Crea el servicio (usar <see cref="Instancia"/>).</summary>
    private ServicioAnalisisAudio()
    {
    }

    /// <summary>
    /// Encola un audio para su análisis (una sola vez por ruta). Si ya está
    /// pendiente o en cola, no duplica trabajo. Nunca bloquea ni lanza.
    /// </summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="duracion">Duración conocida (para el recorte de silencio).</param>
    public void Encolar(string path, TimeSpan duracion)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        lock (_candado)
        {
            if (!_pendientes.Add(path))
            {
                return;
            }

            AsegurarHilo();
        }

        if (!_cola.Writer.TryWrite((path, duracion)))
        {
            lock (_candado)
            {
                _pendientes.Remove(path);
            }
        }
    }

    /// <summary>Crea el hilo del worker en la primera entrada.</summary>
    /// <remarks>Prioridad <c>BelowNormal</c>: el audio siempre gana el CPU.</remarks>
    private void AsegurarHilo()
    {
        if (_hilo is not null)
        {
            return;
        }

        _hilo = new Thread(Bucle)
        {
            IsBackground = true,
            Name = "BabyRadio.AnalisisAudio",
            Priority = ThreadPriority.BelowNormal,
        };
        _hilo.Start();
    }

    /// <summary>Bucle del worker: consume la cola y procesa una entrada a la vez.</summary>
    private void Bucle()
    {
        try
        {
            while (true)
            {
                var (path, duracion) = _cola.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
                try
                {
                    Procesar(path, duracion);
                }
                catch (Exception ex)
                {
                    RegistroErrores.Registrar(ex, "AnalisisAudio");
                }
                finally
                {
                    lock (_candado)
                    {
                        _pendientes.Remove(path);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "AnalisisAudio.Bucle");
        }
    }

    /// <summary>Analiza una entrada: loudness + recorte, cachea y avisa.</summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="duracion">Duración conocida.</param>
    /// <remarks>Si el loudness ya está cacheado y vigente, no se reanaliza.</remarks>
    private void Procesar(string path, TimeSpan duracion)
    {
        var info = new System.IO.FileInfo(path);
        if (!info.Exists)
        {
            return;
        }

        if (!LoudnessCache.TryObtenerGanancia(path, out var ganancia))
        {
            var (lufs, pico) = AnalizadorLoudness.Analizar(path, CancellationToken.None);
            ganancia = LoudnessCache.CalcularGanancia(lufs, pico);
            LoudnessCache.Registrar(path, lufs, pico, info.LastWriteTimeUtc, info.Length);
        }

        var finEfectivo = RecorteSilencio.DetectarFin(path, duracion);
        Preparado?.Invoke(path, finEfectivo, ganancia);
    }
}
