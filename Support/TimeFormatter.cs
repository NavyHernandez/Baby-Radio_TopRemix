namespace BebeRadio.Support;

/// <summary>Formato de tiempos para contadores (función pura).</summary>
public static class TimeFormatter
{
    /// <summary>
    /// Formatea un <see cref="TimeSpan"/> como minutaje radial <c>m:ss</c>.
    /// </summary>
    /// <param name="value">Tiempo a formatear (se trunca a segundos).</param>
    /// <returns>Cadena tipo "3:42". Nunca null.</returns>
    public static string ToMinuteSecond(TimeSpan value)
    {
        var totalSeconds = (int)value.TotalSeconds;
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        return $"{totalSeconds / 60}:{(totalSeconds % 60):D2}";
    }
}
