using System.Text.Json;

namespace BebeRadio.Support;

/// <summary>
/// Caché de loudness por archivo (una sola vez, sin tocar el audio original).
/// Guarda en un JSON local propio (no el portable de la consola) el LUFS
/// integrado y el pico, junto a <c>mtime</c> y longitud para invalidarla si el
/// archivo cambia. El resultado se lee en reproducción para calcular una
/// ganancia simple; nunca se reanaliza al sonar. Tolerante a fallos: si el
/// archivo falta o está corrupto, cae a una caché vacía.
/// </summary>
public static class LoudnessCache
{
    /// <summary>Nombre del archivo local de caché de loudness.</summary>
    public const string NombreArchivo = "baby-radio-loudness.json";

    /// <summary>Objetivo de normalización en reproducción (Spotify/YT Music).</summary>
    public const double ObjetivoLufs = -14.0;

    /// <summary>Ganancia máxima aplicada (dB).</summary>
    private const double GananciaMaximaDb = 12.0;

    /// <summary>Ganancia mínima aplicada (dB).</summary>
    private const double GananciaMinimaDb = -12.0;

    /// <summary>Margen bajo 0 dBFS para evitar clipping inter-muestra (dB).</summary>
    private const double MargenPicoDb = 0.5;

    /// <summary>Retardo del guardado en disco tras cada análisis (ms).</summary>
    private const int RetardoGuardadoMs = 2000;

    private static readonly object Candado = new();
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static Dictionary<string, EntradaLoudness>? _entradas;
    private static System.Threading.Timer? _guardado;

    /// <summary>Entrada de caché de un archivo (LUFS, pico y firma del archivo).</summary>
    public sealed class EntradaLoudness
    {
        /// <summary>Loudness integrado en LUFS.</summary>
        public double Lufs { get; set; }

        /// <summary>Pico muestral 0..N.</summary>
        public double Pico { get; set; }

        /// <summary>Última fecha de modificación observada (UTC).</summary>
        public DateTime ModificadoUtc { get; set; }

        /// <summary>Longitud del archivo en bytes (detección de cambios).</summary>
        public long Longitud { get; set; }
    }

    /// <summary>Ruta completa del archivo local de caché.</summary>
    /// <returns>Ruta en la carpeta de datos (independiente de identidad MSIX).</returns>
    public static string RutaArchivo() =>
        System.IO.Path.Combine(ConsolaStore.CarpetaDatos(), NombreArchivo);

    /// <summary>
    /// Devuelve la ganancia de normalización cacheada de un archivo, si existe
    /// y sigue vigente (mismo <c>mtime</c> y longitud).
    /// </summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="gananciaDb">Ganancia en dB a aplicar (0 si no hay dato).</param>
    /// <returns>True si hay una entrada válida; false si hay que analizar.</returns>
    public static bool TryObtenerGanancia(string path, out double gananciaDb)
    {
        gananciaDb = 0;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        lock (Candado)
        {
            if (_entradas is null)
            {
                Cargar();
            }

            if (_entradas is null || !_entradas.TryGetValue(path, out var entrada))
            {
                return false;
            }

            if (!Vigente(path, entrada))
            {
                return false;
            }

            gananciaDb = CalcularGanancia(entrada.Lufs, entrada.Pico);
            return true;
        }
    }

    /// <summary>
    /// Registra (o actualiza) el análisis de un archivo y programa el guardado.
    /// </summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="lufs">Loudness integrado (puede ser NaN si no hubo señal).</param>
    /// <param name="pico">Pico muestral 0..N.</param>
    /// <param name="modificadoUtc">Fecha de modificación del archivo.</param>
    /// <param name="longitud">Longitud del archivo en bytes.</param>
    public static void Registrar(string path, double lufs, double pico, DateTime modificadoUtc, long longitud)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        lock (Candado)
        {
            _entradas ??= new Dictionary<string, EntradaLoudness>(StringComparer.OrdinalIgnoreCase);
            _entradas[path] = new EntradaLoudness
            {
                Lufs = lufs,
                Pico = pico,
                ModificadoUtc = modificadoUtc,
                Longitud = longitud,
            };

            _guardado ??= new System.Threading.Timer(
                _ => Guardar(), null, Timeout.Infinite, Timeout.Infinite);
            _guardado.Change(RetardoGuardadoMs, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Calcula la ganancia de reproducción para un LUFS/pico dados, recortando
    /// por el techo de pico para no saturar.
    /// </summary>
    /// <param name="lufs">Loudness integrado en LUFS (NaN si no medible).</param>
    /// <param name="pico">Pico muestral 0..N.</param>
    /// <returns>Ganancia en dB dentro de [−12, +12].</returns>
    public static double CalcularGanancia(double lufs, double pico)
    {
        if (double.IsNaN(lufs) || double.IsInfinity(lufs))
        {
            return 0;
        }

        var ganancia = Math.Clamp(ObjetivoLufs - lufs, GananciaMinimaDb, GananciaMaximaDb);
        if (pico > 0.0001)
        {
            var techoPico = (-20 * Math.Log10(pico)) - MargenPicoDb;
            if (ganancia > techoPico)
            {
                ganancia = techoPico;
            }
        }

        return Math.Clamp(ganancia, GananciaMinimaDb, GananciaMaximaDb);
    }

    /// <summary>Comprueba si la entrada sigue correspondiendo al archivo en disco.</summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="entrada">Entrada cacheada.</param>
    /// <returns>True si la firma (mtime + longitud) coincide.</returns>
    private static bool Vigente(string path, EntradaLoudness entrada)
    {
        try
        {
            var info = new System.IO.FileInfo(path);
            if (!info.Exists)
            {
                return false;
            }

            return info.Length == entrada.Longitud
                && info.LastWriteTimeUtc == entrada.ModificadoUtc;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Carga la caché desde disco (una vez).</summary>
    private static void Cargar()
    {
        try
        {
            var ruta = RutaArchivo();
            if (!System.IO.File.Exists(ruta))
            {
                _entradas = new Dictionary<string, EntradaLoudness>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = System.IO.File.ReadAllText(ruta);
            _entradas = JsonSerializer.Deserialize<Dictionary<string, EntradaLoudness>>(json, Opciones)
                ?? new Dictionary<string, EntradaLoudness>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _entradas = new Dictionary<string, EntradaLoudness>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Escribe la caché completa en disco (best-effort).</summary>
    private static void Guardar()
    {
        try
        {
            string json;
            lock (Candado)
            {
                if (_entradas is null)
                {
                    return;
                }

                json = JsonSerializer.Serialize(_entradas, Opciones);
            }

            System.IO.File.WriteAllText(RutaArchivo(), json);
        }
        catch
        {
            // La caché es opcional: si falla el disco se reanaliza en otra sesión.
        }
    }
}
