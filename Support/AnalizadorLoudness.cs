using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BebeRadio.Support;

/// <summary>
/// Medidor de loudness integrado según ITU-R BS.1770 (LUFS) sobre NAudio.
/// Decodifica en streaming y, si hace falta, remuestrea a 48 kHz (los
/// coeficientes K estándar son de 48 kHz), aplica los dos biquads de
/// ponderación K y promedia por bloques de 400 ms con solape del 75 %,
/// con gating absoluto (−70 LUFS) y relativo (−10 LU). Además reporta el
/// pico muestral. Función pura de análisis: sin UI, cancelable y con
/// memoria acotada (solo guarda la energía por bloque).
/// </summary>
public static class AnalizadorLoudness
{
    /// <summary>Frecuencia de análisis (coeficientes K publicados a 48 kHz).</summary>
    public const int FrecuenciaAnalisis = 48000;

    /// <summary>Duración del bloque de integración (ms).</summary>
    private const int BloqueMs = 400;

    /// <summary>Salto entre bloques (ms): 75 % de solape.</summary>
    private const int HopMs = 100;

    /// <summary>Hops que caben en un bloque.</summary>
    private const int HopsPorBloque = BloqueMs / HopMs;

    /// <summary>Offset de la fórmula de loudness BS.1770.</summary>
    private const double OffsetLufs = -0.691;

    /// <summary>Umbral del gate absoluto (LUFS).</summary>
    private const double GateAbsoluto = -70;

    /// <summary>Diferencia del gate relativo (LU).</summary>
    private const double GateRelativo = -10;

    // Filtro K — etapa 1 (high shelf) a 48 kHz.
    private static readonly double[] ShelfB = { 1.53512485958697, -2.69169618940638, 1.19839281085285 };
    private static readonly double[] ShelfA = { 1.0, -1.69065929318241, 0.73248077421585 };

    // Filtro K — etapa 2 (RLB high-pass) a 48 kHz.
    private static readonly double[] HighPassB = { 1.0, -2.0, 1.0 };
    private static readonly double[] HighPassA = { 1.0, -1.99004745483398, 0.99007225036621 };

    /// <summary>
    /// Analiza un archivo y devuelve su loudness integrado y su pico muestral.
    /// </summary>
    /// <param name="path">Ruta del audio.</param>
    /// <param name="ct">Cancelación cooperativa (se comprueba por lectura).</param>
    /// <returns>
    /// Tupla (LUFS integrado, pico 0..N). El loudness es <c>NaN</c> si no hay
    /// señal medible (silencio); el pico es 0 en ese caso.
    /// </returns>
    /// <remarks>Nunca lanza por audio inválido: propaga la excepción al llamador.</remarks>
    public static (double Lufs, double Pico) Analizar(string path, CancellationToken ct = default)
    {
        using var lector = CrearLector(path);
        var fuente = lector.ToSampleProvider();
        if (fuente.WaveFormat.SampleRate != FrecuenciaAnalisis)
        {
            fuente = new WdlResamplingSampleProvider(fuente, FrecuenciaAnalisis);
        }

        var canales = Math.Clamp(fuente.WaveFormat.Channels, 1, 2);
        var hopMuestrasPorCanal = FrecuenciaAnalisis * HopMs / 1000;
        var muestrasPorCanalBloque = FrecuenciaAnalisis * BloqueMs / 1000;

        var filtros = new FiltroK[canales];
        for (var c = 0; c < canales; c++)
        {
            filtros[c] = new FiltroK();
        }

        var buffer = new float[8192 * canales];
        var hopAcumulado = new double[canales];
        var ventana = new double[HopsPorBloque];
        var ventanaLlenos = 0;
        var ventanaIndice = 0;
        var hopContador = 0;
        var bloques = new List<double>();
        double pico = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var leidas = fuente.Read(buffer, 0, buffer.Length);
            if (leidas <= 0)
            {
                break;
            }

            var marcos = leidas / canales;
            for (var m = 0; m < marcos; m++)
            {
                var indiceBase = m * canales;
                for (var c = 0; c < canales; c++)
                {
                    var x = buffer[indiceBase + c];
                    var abs = x < 0 ? -x : x;
                    if (abs > pico)
                    {
                        pico = abs;
                    }

                    var y = filtros[c].Procesar(x);
                    hopAcumulado[c] += y * y;
                }

                hopContador++;
                if (hopContador < hopMuestrasPorCanal)
                {
                    continue;
                }

                var energiaHop = 0.0;
                for (var c = 0; c < canales; c++)
                {
                    energiaHop += hopAcumulado[c];
                    hopAcumulado[c] = 0;
                }

                ventana[ventanaIndice] = energiaHop;
                ventanaIndice = (ventanaIndice + 1) % HopsPorBloque;
                if (ventanaLlenos < HopsPorBloque)
                {
                    ventanaLlenos++;
                }

                if (ventanaLlenos == HopsPorBloque)
                {
                    var energiaBloque = 0.0;
                    for (var k = 0; k < HopsPorBloque; k++)
                    {
                        energiaBloque += ventana[k];
                    }

                    bloques.Add(energiaBloque / muestrasPorCanalBloque);
                }

                hopContador = 0;
            }
        }

        return (Integrar(bloques), pico);
    }

    /// <summary>
    /// Aplica el doble gating (absoluto + relativo) y devuelve el LUFS integrado.
    /// </summary>
    /// <param name="bloques">Energía media por bloque (ya ponderada por canal).</param>
    /// <returns>LUFS integrado o <c>NaN</c> si no hay bloques medibles.</returns>
    private static double Integrar(List<double> bloques)
    {
        var sumaAbsoluto = 0.0;
        var cuentaAbsoluto = 0;
        foreach (var energia in bloques)
        {
            if (energia > 0 && Loudness(energia) >= GateAbsoluto)
            {
                sumaAbsoluto += energia;
                cuentaAbsoluto++;
            }
        }

        if (cuentaAbsoluto == 0)
        {
            return double.NaN;
        }

        var preliminar = Loudness(sumaAbsoluto / cuentaAbsoluto);
        var umbral = preliminar + GateRelativo;

        var sumaFinal = 0.0;
        var cuentaFinal = 0;
        foreach (var energia in bloques)
        {
            if (energia <= 0)
            {
                continue;
            }

            var loudness = Loudness(energia);
            if (loudness >= GateAbsoluto && loudness >= umbral)
            {
                sumaFinal += energia;
                cuentaFinal++;
            }
        }

        return cuentaFinal > 0 ? Loudness(sumaFinal / cuentaFinal) : preliminar;
    }

    /// <summary>Convierte energía media de bloque a LUFS.</summary>
    /// <param name="energia">Energía media (suma de canales ponderados).</param>
    /// <returns>Loudness del bloque en LUFS.</returns>
    private static double Loudness(double energia) =>
        OffsetLufs + (10 * Math.Log10(energia));

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

    /// <summary>
    /// Biquad de ponderación K en serie (high shelf → RLB high-pass).
    /// Implementa la forma directa I con estado por canal.
    /// </summary>
    private sealed class FiltroK
    {
        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;
        private double _x1Hp;
        private double _x2Hp;
        private double _y1Hp;
        private double _y2Hp;

        /// <summary>Procesa una muestra y devuelve la salida K-ponderada.</summary>
        /// <param name="x">Muestra de entrada.</param>
        /// <returns>Muestra filtrada.</returns>
        public double Procesar(double x)
        {
            var shelf = (ShelfB[0] * x) + (ShelfB[1] * _x1) + (ShelfB[2] * _x2)
                - (ShelfA[1] * _y1) - (ShelfA[2] * _y2);
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = shelf;

            var highPass = (HighPassB[0] * shelf) + (HighPassB[1] * _x1Hp) + (HighPassB[2] * _x2Hp)
                - (HighPassA[1] * _y1Hp) - (HighPassA[2] * _y2Hp);
            _x2Hp = _x1Hp;
            _x1Hp = shelf;
            _y2Hp = _y1Hp;
            _y1Hp = highPass;
            return highPass;
        }
    }
}
