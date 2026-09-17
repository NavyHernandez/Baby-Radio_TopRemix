using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BebeRadio.Support;

/// <summary>
/// Calcula picos normalizados para dibujar ondas (función de datos pura,
/// sin UI). Lee por saltos para no decodificar archivos largos enteros.
/// Con caché en memoria por ruta para abrir el editor sin esperas.
/// </summary>
public static class OndaPicos
{
    /// <summary>Columnas de la onda (resolución horizontal).</summary>
    public const int Columnas = 300;

    private static readonly Dictionary<string, (DateTime Modificado, float[] Picos)> Cache = new();
    private static readonly object Candado = new();

    /// <summary>Calcula (o recupera) los picos de un audio.</summary>
    /// <param name="path">Ruta del archivo.</param>
    /// <param name="columnas">Columnas deseadas.</param>
    /// <param name="ct">Cancelación (cambia de archivo en el editor).</param>
    /// <returns>Picos 0-1 (vacío si ilegible).</returns>
    public static Task<float[]> CalcularAsync(string path, int columnas = Columnas, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var modificado = System.IO.File.GetLastWriteTimeUtc(path);
            lock (Candado)
            {
                if (Cache.TryGetValue(path, out var entrada) && entrada.Modificado == modificado)
                {
                    return (float[])entrada.Picos.Clone();
                }
            }

            var picos = LeerPorSaltos(path, columnas, ct);
            if (picos.Length > 0)
            {
                lock (Candado)
                {
                    Cache[path] = (modificado, (float[])picos.Clone());
                }
            }

            return picos;
        }, ct);

    /// <summary>Lee una ventana por columna buscando el pico (rápido en largos).</summary>
    /// <param name="path">Ruta del archivo.</param>
    /// <param name="columnas">Columnas.</param>
    /// <param name="ct">Cancelación.</param>
    /// <returns>Picos normalizados.</returns>
    private static float[] LeerPorSaltos(string path, int columnas, CancellationToken ct)
    {
        try
        {
            using var lector = CrearLector(path);
            var muestras = lector.ToSampleProvider();
            var totalSeg = lector.TotalTime.TotalSeconds;
            if (totalSeg <= 0 || columnas <= 0)
            {
                return Array.Empty<float>();
            }

            var formato = lector.WaveFormat;
            var ventanaSeg = Math.Clamp(totalSeg / columnas / 2, 0.05, 0.5);
            var muestrasVentana = Math.Max(256, (int)(formato.SampleRate * ventanaSeg) * formato.Channels);
            var buffer = new float[muestrasVentana];
            var picos = new float[columnas];

            for (var i = 0; i < columnas; i++)
            {
                ct.ThrowIfCancellationRequested();
                lector.CurrentTime = TimeSpan.FromSeconds(totalSeg * i / columnas);
                var leidas = muestras.Read(buffer, 0, buffer.Length);
                var pico = 0f;
                for (var j = 0; j < leidas; j++)
                {
                    var valor = Math.Abs(buffer[j]);
                    if (valor > pico)
                    {
                        pico = valor;
                    }
                }

                picos[i] = pico;
            }

            var maximo = picos.Max();
            if (maximo > 0.001f)
            {
                for (var i = 0; i < picos.Length; i++)
                {
                    picos[i] /= maximo;
                }
            }

            return picos;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    /// <summary>Abre el lector adecuado (directo o MediaFoundation).</summary>
    /// <param name="path">Ruta del archivo.</param>
    /// <returns>Lector de muestras flotantes.</returns>
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
}
