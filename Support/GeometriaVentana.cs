namespace BebeRadio.Support;

/// <summary>
/// Geometría de ventana (funciones puras, sin I/O): conversión DIPs → píxeles
/// físicos y límite al área de trabajo de un display. Necesario porque
/// <c>AppWindow</c> (Resize/Move) trabaja en PÍXELES FÍSICOS mientras XAML
/// (Window.Bounds, AdaptiveTriggerHelper) trabaja en DIPs: con monitores de
/// DPI distinto (p. ej. 150 % + 100 %) un "1366x768" pasado a secas a
/// <c>AppWindow.Resize</c> no es un 1366x768 lógico.
/// </summary>
public static class GeometriaVentana
{
    /// <summary>Convierte un tamaño lógico (DIP) a píxeles físicos.</summary>
    /// <param name="anchoDip">Ancho en DIPs (p. ej. 1366).</param>
    /// <param name="altoDip">Alto en DIPs (p. ej. 768).</param>
    /// <param name="escala">Escala de rasterización del display (0 o negativa = 1).</param>
    /// <returns>Tamaño físico redondeado, mínimo 1x1.</returns>
    public static Windows.Graphics.SizeInt32 DipsAFisicos(double anchoDip, double altoDip, double escala)
    {
        var factor = escala > 0 ? escala : 1.0;
        return new Windows.Graphics.SizeInt32(
            Math.Max(1, (int)Math.Round(anchoDip * factor)),
            Math.Max(1, (int)Math.Round(altoDip * factor)));
    }

    /// <summary>Limita un tamaño físico para que quepa en el área de trabajo de un display.</summary>
    /// <param name="tamano">Tamaño físico deseado.</param>
    /// <param name="areaTrabajo"><c>DisplayArea.WorkArea</c> (físicos) del display destino.</param>
    /// <returns>Tamaño clampeado al área de trabajo, mínimo 1x1.</returns>
    public static Windows.Graphics.SizeInt32 LimitarAlWorkArea(
        Windows.Graphics.SizeInt32 tamano,
        Windows.Graphics.RectInt32 areaTrabajo)
    {
        return new Windows.Graphics.SizeInt32(
            Math.Max(1, Math.Min(tamano.Width, areaTrabajo.Width)),
            Math.Max(1, Math.Min(tamano.Height, areaTrabajo.Height)));
    }
}
