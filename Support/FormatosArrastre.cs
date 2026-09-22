namespace BebeRadio.Support;

/// <summary>
/// Formatos privados de arrastre dentro de la consola Baby Radio.
/// Distinguen los arrastres internos (lista ↔ paleta) de los archivos
/// que vienen del Explorador, para no duplicar ni mezclar flujos.
/// </summary>
public static class FormatosArrastre
{
    /// <summary>
    /// Entrada de cola arrastrada desde la lista (texto = FilePath del audio).
    /// Solo lo emiten entradas con audio real; los mocks no inician arrastre.
    /// </summary>
    public const string EntradaCola = "BabyRadio.QueueEntryPath";
}
