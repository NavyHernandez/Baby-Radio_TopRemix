namespace BebeRadio.Support;

/// <summary>
/// Dinámica del VU meter como funciones puras (AGENTS.md §3.4).
/// Escala dB (piso −60) + balística pro: ataque casi instantáneo,
/// release ~300 ms, todo con delta real (independiente del framerate).
/// Sin dependencias de UI: testeable y reutilizable con NAudio en Fase 2.
/// </summary>
public static class VuDynamics
{
    /// <summary>Piso de la escala en dB (silencio visual).</summary>
    public const double FloorDb = -60;

    /// <summary>Velocidad de ataque (1/s). 90 = pega con el oído.</summary>
    public const double AttackSpeed = 90;

    /// <summary>Velocidad de release (1/s). ~7 = caída suave broadcast.</summary>
    public const double ReleaseSpeed = 7;

    /// <summary>Tiempo que el pico se mantiene antes de caer, en segundos.</summary>
    public const double PeakHoldSeconds = 0.8;

    /// <summary>Velocidad de caída del pico retenido, en unidades/s.</summary>
    public const double PeakFallPerSecond = 0.6;

    /// <summary>
    /// Convierte nivel lineal 0–1 a posición 0–1 en escala dB.
    /// Los pianísimos se ven; el rojo solo llega cerca de 0 dB.
    /// </summary>
    /// <param name="linear">Nivel lineal 0–1.</param>
    /// <returns>Posición en escala, 0–1.</returns>
    public static double LinearToDbNormalized(double linear)
    {
        if (linear <= 0)
        {
            return 0;
        }

        var db = 20 * Math.Log10(Math.Min(1, linear));
        return Clamp01((db - FloorDb) / -FloorDb);
    }

    /// <summary>Nivel lineal correspondiente a un dB (para ticks de escala).</summary>
    /// <param name="db">Decibelios (negativo o cero).</param>
    /// <returns>Nivel lineal 0–1.</returns>
    public static double DbToLinear(double db) => Math.Pow(10, db / 20);

    /// <summary>
    /// Suaviza el nivel visible hacia el objetivo con balística asimétrica.
    /// </summary>
    /// <param name="display">Nivel visible actual (0–1, ya en escala dB).</param>
    /// <param name="target">Nivel objetivo (0–1, ya en escala dB).</param>
    /// <param name="deltaSeconds">Delta real del frame.</param>
    /// <returns>Nivel visible siguiente.</returns>
    public static double SmoothDisplay(double display, double target, double deltaSeconds)
    {
        var speed = target >= display ? AttackSpeed : ReleaseSpeed;
        var factor = 1 - Math.Exp(-speed * Math.Max(0, deltaSeconds));
        return display + ((target - display) * factor);
    }

    /// <summary>
    /// Interpola el nivel visible hacia el objetivo (evita saltos del VU).
    /// </summary>
    /// <param name="display">Nivel visible actual (0–1).</param>
    /// <param name="target">Nivel medido objetivo (0–1).</param>
    /// <returns>Nivel visible siguiente, limitado a 0–1.</returns>
    public static double ApplyInertia(double display, double target)
    {
        const double InertiaFactor = 0.25;
        var next = display + ((target - display) * InertiaFactor);
        return Clamp01(next);
    }

    /// <summary>
    /// Actualiza el pico retenido: sube al instante, mantiene y luego cae.
    /// </summary>
    /// <param name="peak">Pico actual (0–1).</param>
    /// <param name="peakAgeSeconds">Edad del pico en segundos.</param>
    /// <param name="current">Nivel visible actual (0–1).</param>
    /// <param name="deltaSeconds">Tiempo desde el último tick.</param>
    /// <returns>Tupla (nuevo pico, nueva edad).</returns>
    public static (double Peak, double Age) UpdatePeak(
        double peak, double peakAgeSeconds, double current, double deltaSeconds)
    {
        if (current >= peak)
        {
            return (Clamp01(current), 0);
        }

        var age = peakAgeSeconds + deltaSeconds;
        if (age < PeakHoldSeconds)
        {
            return (peak, age);
        }

        var fallen = peak - (PeakFallPerSecond * deltaSeconds);
        return (Clamp01(fallen), age);
    }

    /// <summary>Limita un valor al rango 0–1.</summary>
    /// <param name="value">Valor de entrada.</param>
    /// <returns>Valor recortado.</returns>
    private static double Clamp01(double value) =>
        value < 0 ? 0 : (value > 1 ? 1 : value);
}
