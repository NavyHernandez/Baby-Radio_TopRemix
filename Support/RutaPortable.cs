namespace BebeRadio.Support;

/// <summary>
/// Rutas portables del JSON de consola: convierte los FilePath absolutos de
/// los slots a relativos a la carpeta del JSON (para discos extraíbles que
/// cambian de letra entre PCs) y los resuelve de vuelta al cargar.
/// Funciones puras de texto, sin I/O: la existencia de archivos la comprueba
/// el store al pre-escanear la importación.
/// </summary>
public static class RutaPortable
{
    /// <summary>Relativiza una ruta si comparte unidad con la carpeta del JSON.</summary>
    /// <param name="rutaAbsoluta">Ruta absoluta del audio.</param>
    /// <param name="carpetaJson">Carpeta donde vive el JSON portable.</param>
    /// <returns>
    /// Ruta relativa a la carpeta del JSON, o la original si es de otra
    /// unidad o algo falla (esa queda marcada como no portable).
    /// </returns>
    public static string RelativizarSiMismaUnidad(string rutaAbsoluta, string carpetaJson)
    {
        try
        {
            var raizArchivo = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(rutaAbsoluta));
            var raizJson = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(carpetaJson));
            if (string.IsNullOrEmpty(raizArchivo)
                || string.IsNullOrEmpty(raizJson)
                || !raizArchivo.Equals(raizJson, StringComparison.OrdinalIgnoreCase))
            {
                return rutaAbsoluta;
            }

            return System.IO.Path.GetRelativePath(carpetaJson, rutaAbsoluta);
        }
        catch
        {
            return rutaAbsoluta;
        }
    }

    /// <summary>Resuelve una ruta guardada contra la carpeta del JSON.</summary>
    /// <param name="rutaGuardada">Absoluta o relativa, tal como quedó guardada.</param>
    /// <param name="carpetaJson">Carpeta del JSON que se está cargando.</param>
    /// <returns>Ruta absoluta lista para el motor de audio (o la original si falla).</returns>
    public static string ResolverContraCarpeta(string rutaGuardada, string carpetaJson)
    {
        try
        {
            if (System.IO.Path.IsPathRooted(rutaGuardada))
            {
                return System.IO.Path.GetFullPath(rutaGuardada);
            }

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(carpetaJson, rutaGuardada));
        }
        catch
        {
            return rutaGuardada;
        }
    }
}
