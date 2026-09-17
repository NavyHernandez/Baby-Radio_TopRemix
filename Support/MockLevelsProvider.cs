namespace BebeRadio.Support;

/// <summary>
/// Generador mock de niveles estéreo para Fase 1 (seno + ruido suavizado).
/// En Fase 2 se sustituye por NAudio post-mezcla sin cambiar <c>SetLevels(l, r)</c>.
/// </summary>
public sealed class MockLevelsProvider
{
    private readonly Random _random = new();
    private double _phase;
    private double _smoothedLeft;
    private double _smoothedRight;

    /// <summary>
    /// Calcula el siguiente par de niveles (0–1). Cuando está en pausa devuelve ~0.
    /// </summary>
    /// <param name="isPlaying">Si el mock está reproduciendo.</param>
    /// <returns>Tupla (izquierdo, derecho).</returns>
    public (double Left, double Right) Next(bool isPlaying)
    {
        if (!isPlaying)
        {
            _smoothedLeft = Decay(_smoothedLeft);
            _smoothedRight = Decay(_smoothedRight);
            return (_smoothedLeft, _smoothedRight);
        }

        _phase += 0.09;
        var baseWave = 0.55 + (0.30 * Math.Sin(_phase)) + (0.10 * Math.Sin(_phase * 2.7));

        var rawLeft = Clamp01(baseWave + ((RandomUnit() - 0.5) * 0.22));
        var rawRight = Clamp01(baseWave + ((RandomUnit() - 0.5) * 0.22));

        // Suavizado extra en la fuente (la inercia final la aplica VuDynamics).
        _smoothedLeft = (_smoothedLeft * 0.6) + (rawLeft * 0.4);
        _smoothedRight = (_smoothedRight * 0.6) + (rawRight * 0.4);
        return (_smoothedLeft, _smoothedRight);
    }

    /// <summary>Decaimiento exponencial hacia cero al pausar.</summary>
    /// <param name="value">Valor actual.</param>
    /// <returns>Valor atenuado.</returns>
    private static double Decay(double value) => value * 0.85;

    /// <summary>Ruido uniforme 0–1.</summary>
    /// <returns>Muestra aleatoria.</returns>
    private double RandomUnit() => _random.NextDouble();

    /// <summary>Limita un valor al rango 0–1.</summary>
    /// <param name="value">Valor de entrada.</param>
    /// <returns>Valor recortado.</returns>
    private static double Clamp01(double value) =>
        value < 0 ? 0 : (value > 1 ? 1 : value);
}
