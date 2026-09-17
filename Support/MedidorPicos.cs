using System.Diagnostics;
using NAudio.Wave;

namespace BebeRadio.Support;

/// <summary>
/// Toma niveles estéreo de la cadena de audio para el VU.
/// Envuelve al proveedor real y reporta picos por bloque (tope 20 Hz).
/// Función de medición pura: no altera las muestras.
/// </summary>
public sealed class MedidorPicos : ISampleProvider
{
    /// <summary>Intervalo mínimo entre reportes en ticks del reloj de alta resolución.</summary>
    private static readonly long IntervaloTicks = Stopwatch.Frequency / 40;

    private readonly ISampleProvider _fuente;
    private long _ultimoReporte;

    /// <summary>Picos crudos L/R del último bloque (0-1).</summary>
    public event Action<float, float>? Niveles;

    /// <summary>Crea el medidor sobre la fuente real.</summary>
    /// <param name="fuente">Proveedor de muestras a medir.</param>
    public MedidorPicos(ISampleProvider fuente)
    {
        _fuente = fuente;
    }

    /// <summary>Formato de la fuente.</summary>
    public WaveFormat WaveFormat => _fuente.WaveFormat;

    /// <summary>Lee muestras midiendo el pico por canal.</summary>
    /// <param name="buffer">Destino.</param>
    /// <param name="offset">Desplazamiento.</param>
    /// <param name="count">Muestras a leer.</param>
    /// <returns>Muestras leídas.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        var leidas = _fuente.Read(buffer, offset, count);
        if (leidas <= 0)
        {
            return leidas;
        }

        var canales = Math.Max(1, WaveFormat.Channels);
        var picoIzq = 0f;
        var picoDer = 0f;
        for (var i = 0; i < leidas; i += canales)
        {
            var izq = Math.Abs(buffer[offset + i]);
            if (izq > picoIzq)
            {
                picoIzq = izq;
            }

            var der = canales > 1 && i + 1 < leidas ? Math.Abs(buffer[offset + i + 1]) : izq;
            if (der > picoDer)
            {
                picoDer = der;
            }
        }

        var ahora = Stopwatch.GetTimestamp();
        if (ahora - _ultimoReporte >= IntervaloTicks)
        {
            _ultimoReporte = ahora;
            Niveles?.Invoke(Math.Min(1, picoIzq), Math.Min(1, picoDer));
        }

        return leidas;
    }
}
