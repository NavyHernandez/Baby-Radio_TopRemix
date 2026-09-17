using Windows.Storage;

namespace BebeRadio.Support;

/// <summary>
/// Expande lo soltado/elegido a rutas de audio: archivos directos y
/// carpetas (recursivo, orden alfabético). Aísla el I/O WinRT para que
/// los ViewModels sigan recibiendo simples rutas.
/// </summary>
public static class ExploradorAudio
{
    /// <summary>Expande items (archivos y carpetas) a rutas de audio.</summary>
    /// <param name="items">Items de arrastre o picker.</param>
    /// <returns>Rutas de audio ordenadas.</returns>
    public static async Task<List<string>> ExpandirAsync(IEnumerable<IStorageItem> items)
    {
        var rutas = new List<string>();
        foreach (var item in items)
        {
            if (item is StorageFile archivo)
            {
                if (AudioFileInspector.IsAudioFile(archivo.Path))
                {
                    rutas.Add(archivo.Path);
                }
            }
            else if (item is StorageFolder carpeta)
            {
                rutas.AddRange(await AudiosDeCarpetaAsync(carpeta));
            }
        }

        return rutas;
    }

    /// <summary>Reúne los audios de una carpeta y subcarpetas.</summary>
    /// <param name="carpeta">Carpeta raíz.</param>
    /// <returns>Rutas ordenadas alfabéticamente.</returns>
    private static async Task<List<string>> AudiosDeCarpetaAsync(StorageFolder carpeta)
    {
        var rutas = new List<string>();
        try
        {
            var archivos = await carpeta.GetFilesAsync();
            foreach (var archivo in archivos.OrderBy(a => a.Name))
            {
                if (AudioFileInspector.IsAudioFile(archivo.Path))
                {
                    rutas.Add(archivo.Path);
                }
            }

            var subcarpetas = await carpeta.GetFoldersAsync();
            foreach (var sub in subcarpetas.OrderBy(c => c.Name))
            {
                rutas.AddRange(await AudiosDeCarpetaAsync(sub));
            }
        }
        catch
        {
            // Carpeta ilegible: se omite en silencio.
        }

        return rutas;
    }
}
