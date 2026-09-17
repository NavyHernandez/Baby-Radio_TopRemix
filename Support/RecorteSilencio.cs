using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BebeRadio.Support;

/// <summary>
/// Detecta dónde termina el sonido real (recorta silencios finales).
/// Escanea solo los últimos segundos por bloques con seek: rápido
/// incluso en archivos largos. Función pura de análisis, sin UI.
/// </summary>
public static class RecorteSilencio
{
    /// <summary>Umbral normal: solo fondo bajísimo (-45 dB).</summary>
    public const double UmbralDb = -45;

    /// <summary>Silencio seguido para declarar el fin (segundos).</summary>
    public const double MinSilencioSeg = 1.0;

    /// <summary>Ventana analizada al final (segundos).</summary>
    public const double VentanaSeg = 30.0;

    /// <summary>Margen tras el último sonido (segundos).</summary>
    public const double MargenSeg = 0.25;

    /// <summary>Detecta el fin efectivo (sin silencio final).</summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="duracion">Duración total.</param>
    /// <returns>Fin efectivo o null si no hay silencio que recortar.</returns>
    public static TimeSpan? DetectarFin(string path, TimeSpan duracion)
    {
        try
        {
            using var lector = CrearLector(path);
            var muestras = lector.ToSampleProvider();
            var totalSeg = duracion.TotalSeconds;
            if (totalSeg <= 0)
            {
                return null;
            }

            var umbral = (float)Math.Pow(10, UmbralDb / 20);
            var desdeSeg = Math.Max(0, totalSeg - VentanaSeg);
            var formato = lector.WaveFormat;
            var bloqueSeg = 0.1;
            var muestrasBloque = Math.Max(256, (int)(formato.SampleRate * bloqueSeg) * formato.Channels);
            var buffer = new float[muestrasBloque];
            var bloques = (int)((totalSeg - desdeSeg) / bloqueSeg);
            var ultimoSonidoSeg = -1.0;

            for (var i = 0; i < bloques; i++)
            {
                lector.CurrentTime = TimeSpan.FromSeconds(desdeSeg + i * bloqueSeg);
                var leidas = muestras.Read(buffer, 0, buffer.Length);
                if (leidas <= 0)
                {
                    break;
                }

                var pico = 0f;
                for (var j = 0; j < leidas; j++)
                {
                    var valor = Math.Abs(buffer[j]);
                    if (valor > pico)
                    {
                        pico = valor;
                    }
                }

                if (pico >= umbral)
                {
                    ultimoSonidoSeg = desdeSeg + (i + 1) * bloqueSeg;
                }
            }

            if (ultimoSonidoSeg < 0)
            {
                return TimeSpan.Zero;
            }

            var fin = ultimoSonidoSeg + MargenSeg;
            return fin < totalSeg - MinSilencioSeg ? TimeSpan.FromSeconds(fin) : null;
        }
        catch
        {
            return null;
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
}
