namespace BebeRadio.Support;

/// <summary>
/// Lee metadatos de archivos de audio reales (arrastrados a la cola) con
/// TagLibSharp, sin reproducir. Puente de Fase 1 hasta el LibraryService.
/// </summary>
public static class AudioFileInspector
{
    /// <summary>Solo tipos que NAudio reproduce seguro (directo o MediaFoundation).</summary>
    public static readonly string[] AudioExtensions =
        { ".mp3", ".wav", ".m4a", ".wma", ".aiff", ".aif" };

    /// <summary>Indica si la ruta es un audio reproducible.</summary>
    /// <param name="path">Ruta del archivo.</param>
    /// <returns>True si la extensión está en la whitelist.</returns>
    public static bool IsAudioFile(string path) =>
        AudioExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Crea una entrada de cola desde un archivo real (título/artista/duración).
    /// </summary>
    /// <param name="path">Ruta del archivo de audio.</param>
    /// <returns>Entrada lista para encolar, o null si ilegible.</returns>
    /// <remarks>Nunca lanza: archivos corruptos devuelven null.</remarks>
    public static Models.QueueEntry? TryInspect(string path)
    {
        if (!IsAudioFile(path))
        {
            return null;
        }

        try
        {
            using var file = TagLib.File.Create(path);
            var title = string.IsNullOrWhiteSpace(file.Tag.Title)
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : file.Tag.Title;
            var artist = file.Tag.FirstPerformer ?? "Archivo local";
            var duration = file.Properties.Duration;
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromMinutes(3);
            }

            var bases = Models.CategoryPalette.All;
            return new Models.QueueEntry(
                title, artist, duration, Models.TrackCategory.Music, path)
            {
                ColorKey = bases[Random.Shared.Next(bases.Count)].BrushKey,
            };
        }
        catch
        {
            return null;
        }
    }
}
