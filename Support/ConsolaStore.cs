using System.Text.Json;
using BebeRadio.Models;

namespace BebeRadio.Support;

/// <summary>
/// Persistencia portable de la consola Baby Radio (categorías + paletas).
/// Un solo JSON en la carpeta local; Exportar/Importar lo lleva a otra PC.
/// Tras importar un archivo portable (p. ej. de un disco extraíble) se entra
/// en sesión portable: la caché trabaja con esa copia y los guardados van a
/// ella, sin tocar el JSON local. Síncrono y tolerante: si el archivo falta o
/// está corrupto, devuelve configuración vacía (la vista cae al mock).
/// Sin I/O en ViewModels: ellos llaman aquí y este helper aísla el disco.
/// </summary>
public static class ConsolaStore
{
    /// <summary>Nombre del archivo local de configuración.</summary>
    public const string NombreArchivo = "baby-radio-consola.json";

    /// <summary>Versión de esquema que escribe y acepta este build.</summary>
    public const int VersionEsquema = 1;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly object CandadoConfig = new();
    private static ConsolaConfiguracion? _cache;

    /// <summary>
    /// Ruta del archivo portable en sesión (null = consola local).
    /// Se fija al activar una importación confirmada y muere con la app:
    /// el próximo arranque vuelve a la configuración local.
    /// </summary>
    private static string? _rutaSesion;

    /// <summary>Sesión portable activa (los guardados van al archivo cargado).</summary>
    /// <returns>True si se importó un portable y aún no se reinició.</returns>
    public static bool SesionPortableActiva
    {
        get
        {
            lock (CandadoConfig)
            {
                return !string.IsNullOrWhiteSpace(_rutaSesion);
            }
        }
    }

    /// <summary>Carpeta de datos roaming (sobrevive updates y reinstalaciones).</summary>
    /// <returns>Ruta en %APPDATA%\BabyRadio (la crea si falta).</returns>
    /// <remarks>
    /// Unpackaged (Velopack) no tiene ApplicationData: se usa System directo.
    /// %LOCALAPPDATA%\BabyRadio es la raíz de instalación de Velopack (se borra
    /// al desinstalar): los datos viven en roaming + migración única desde ahí.
    /// </remarks>
    public static string CarpetaDatos()
    {
        var carpeta = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BabyRadio");
        System.IO.Directory.CreateDirectory(carpeta);
        MigrarDesdeInstalacion(carpeta);
        return carpeta;
    }

    /// <summary>Nombres de datos propios a migrar desde la carpeta vieja.</summary>
    private static readonly string[] ArchivosMigrables =
    [
        NombreArchivo,
        LoudnessCache.NombreArchivo,
        RegistroErrores.NombreArchivo,
    ];

    /// <summary>
    /// Mueve por única vez los datos de %LOCALAPPDATA%\BabyRadio (raíz Velopack).
    /// </summary>
    /// <param name="destino">Carpeta roaming ya creada.</param>
    /// <remarks>Best-effort: solo mueve lo que existe y falta en destino.</remarks>
    private static void MigrarDesdeInstalacion(string destino)
    {
        try
        {
            var origen = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BabyRadio");
            if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase)
                || !System.IO.Directory.Exists(origen))
            {
                return;
            }

            foreach (var nombre in ArchivosMigrables)
            {
                var desde = System.IO.Path.Combine(origen, nombre);
                var hacia = System.IO.Path.Combine(destino, nombre);
                if (System.IO.File.Exists(desde) && !System.IO.File.Exists(hacia))
                {
                    System.IO.File.Move(desde, hacia);
                }
            }
        }
        catch
        {
            // La app sigue con datos frescos en roaming.
        }
    }

    /// <summary>Ruta completa del archivo local.</summary>
    /// <returns>Ruta en la carpeta de datos.</returns>
    public static string RutaArchivo() =>
        System.IO.Path.Combine(CarpetaDatos(), NombreArchivo);

    /// <summary>
    /// Carga la configuración (cacheada en memoria tras la primera lectura).
    /// Evita reparsear el JSON en cada cambio de categoría o refresco del riel.
    /// </summary>
    /// <returns>Configuración (nunca null).</returns>
    /// <remarks>Devuelve la misma instancia; mutarla sin <see cref="Guardar"/> afecta la caché.</remarks>
    public static ConsolaConfiguracion Cargar()
    {
        lock (CandadoConfig)
        {
            _cache ??= LeerDelDisco();
            return _cache;
        }
    }

    /// <summary>Lee la configuración del disco (sin caché).</summary>
    /// <returns>Configuración leída o vacía si falta/corrupta.</returns>
    private static ConsolaConfiguracion LeerDelDisco()
    {
        try
        {
            var ruta = RutaArchivo();
            if (!System.IO.File.Exists(ruta))
            {
                return new ConsolaConfiguracion();
            }

            var json = System.IO.File.ReadAllText(ruta);
            return JsonSerializer.Deserialize<ConsolaConfiguracion>(json, Opciones)
                ?? new ConsolaConfiguracion();
        }
        catch
        {
            return new ConsolaConfiguracion();
        }
    }

    /// <summary>Destino real de escritura (sesión portable o JSON local).</summary>
    /// <returns>Ruta del archivo portable en sesión, o la local.</returns>
    private static string RutaEscritura()
    {
        if (!string.IsNullOrWhiteSpace(_rutaSesion))
        {
            return _rutaSesion;
        }

        return RutaArchivo();
    }

    /// <summary>Guarda la configuración completa y actualiza la caché.</summary>
    /// <param name="config">Configuración a persistir.</param>
    /// <remarks>Sella la versión del esquema y escribe en la ruta de sesión
    /// (el archivo portable cargado, o el JSON local si no hay sesión).</remarks>
    public static void Guardar(ConsolaConfiguracion config)
    {
        lock (CandadoConfig)
        {
            config.Version = VersionEsquema;
            _cache = config;
            try
            {
                var destino = RutaEscritura();
                var carpeta = System.IO.Path.GetDirectoryName(destino);
                if (!string.IsNullOrEmpty(carpeta))
                {
                    System.IO.Directory.CreateDirectory(carpeta);
                }

                var json = JsonSerializer.Serialize(config, Opciones);
                System.IO.File.WriteAllText(destino, json);
            }
            catch
            {
                // Fase 1: la consola sigue funcionando en memoria.
            }
        }
    }

    /// <summary>Guarda los slots editados de una paleta.</summary>
    /// <param name="propietariaId">Dueña (enum o custom:id).</param>
    /// <param name="items">Items actuales (solo se persisten con audio o editados).</param>
    public static void GuardarSlots(string propietariaId, IEnumerable<PaletteItem> items)
    {
        var config = Cargar();
        config.Paletas[propietariaId] = items
            .Where(SlotEsPersistible)
            .Select(item => item.Guardar())
            .ToList();
        Guardar(config);
    }

    /// <summary>Lee los slots guardados de una paleta.</summary>
    /// <param name="propietariaId">Dueña (enum o custom:id).</param>
    /// <returns>Slots por índice (vacío si no hay guardado).</returns>
    public static Dictionary<int, SlotEfectoGuardado> LeerSlots(string propietariaId)
    {
        var config = Cargar();
        var resultado = new Dictionary<int, SlotEfectoGuardado>();
        if (!config.Paletas.TryGetValue(propietariaId, out var slots))
        {
            return resultado;
        }

        foreach (var slot in slots)
        {
            resultado[slot.Indice] = slot;
        }

        return resultado;
    }

    /// <summary>Cuenta los slots con audio de una paleta (para confirmar borrados).</summary>
    /// <param name="propietariaId">Dueña (enum o custom:id).</param>
    /// <returns>Número de slots con archivo.</returns>
    public static int ContarSlotsConAudio(string propietariaId) =>
        LeerSlots(propietariaId).Values.Count(slot => !string.IsNullOrWhiteSpace(slot.FilePath));

    /// <summary>Elimina todo lo guardado de una propietaria (categoría + paleta).</summary>
    /// <param name="propietariaId">Dueña a olvidar.</param>
    public static void OlvidarPropietaria(string propietariaId)
    {
        var config = Cargar();
        config.Paletas.Remove(propietariaId);
        config.CategoriasPersonalizadas.RemoveAll(categoria => categoria.Id == propietariaId);
        Guardar(config);
    }

    /// <summary>Exporta la configuración a una ruta portable.</summary>
    /// <param name="destinoPath">Archivo destino (*.baby-consola.json).</param>
    public static void Exportar(string destinoPath)
    {
        var json = JsonSerializer.Serialize(Cargar(), Opciones);
        System.IO.File.WriteAllText(destinoPath, json);
    }

    /// <summary>Importa y activa una configuración portable.</summary>
    /// <param name="origenPath">Archivo a importar.</param>
    /// <returns>True si se importó correctamente.</returns>
    public static bool Importar(string origenPath)
    {
        try
        {
            var json = System.IO.File.ReadAllText(origenPath);
            var config = JsonSerializer.Deserialize<ConsolaConfiguracion>(json, Opciones);
            if (config is null)
            {
                return false;
            }

            Guardar(config);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Exporta una copia portable con rutas relativas a la carpeta destino.</summary>
    /// <param name="destinoPath">Archivo destino (*.baby-consola.json).</param>
    /// <returns>Total de audios y cuántos quedaron fuera del disco destino (no sonarán en otra PC).</returns>
    /// <remarks>
    /// Clona la configuración en memoria (la caché local queda intacta) y
    /// relativiza cada FilePath que comparta unidad con el destino; el resto
    /// queda absoluto. Pensado para discos extraíbles que cambian de letra.
    /// </remarks>
    public static (int TotalAudios, int FueraDelDisco) ExportarPortable(string destinoPath)
    {
        var carpeta = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(destinoPath))
            ?? string.Empty;
        var copia = JsonSerializer.Deserialize<ConsolaConfiguracion>(
            JsonSerializer.Serialize(Cargar(), Opciones), Opciones)
            ?? new ConsolaConfiguracion();

        var total = 0;
        var fuera = 0;
        foreach (var lista in copia.Paletas.Values)
        {
            for (var i = 0; i < lista.Count; i++)
            {
                var slot = lista[i];
                if (string.IsNullOrWhiteSpace(slot.FilePath))
                {
                    continue;
                }

                total++;
                var portable = RutaPortable.RelativizarSiMismaUnidad(slot.FilePath, carpeta);
                if (System.IO.Path.IsPathRooted(portable))
                {
                    fuera++;
                }
                else
                {
                    lista[i] = slot with { FilePath = portable };
                }
            }
        }

        copia.Version = VersionEsquema;
        System.IO.File.WriteAllText(destinoPath, JsonSerializer.Serialize(copia, Opciones));
        return (total, fuera);
    }

    /// <summary>Lee un archivo portable sin aplicarlo (pre-escaneo previo al popup).</summary>
    /// <param name="origenPath">Archivo a leer.</param>
    /// <param name="versionFutura">True si es de una versión más nueva (se rechaza).</param>
    /// <returns>Configuración leída o null si falta, está corrupto o es futuro.</returns>
    public static ConsolaConfiguracion? LeerPortable(string origenPath, out bool versionFutura)
    {
        versionFutura = false;
        try
        {
            var json = System.IO.File.ReadAllText(origenPath);
            var config = JsonSerializer.Deserialize<ConsolaConfiguracion>(json, Opciones);
            if (config is null)
            {
                return null;
            }

            if (config.Version > VersionEsquema)
            {
                versionFutura = true;
                return null;
            }

            return config;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Resuelve las rutas relativas del portable contra su carpeta.</summary>
    /// <param name="config">Configuración recién leída (aún no activada).</param>
    /// <param name="carpetaOrigen">Carpeta del archivo portable.</param>
    /// <remarks>Deja cada FilePath en absoluto listo para el motor de audio.</remarks>
    public static void ResolverPortable(ConsolaConfiguracion config, string carpetaOrigen)
    {
        foreach (var lista in config.Paletas.Values)
        {
            for (var i = 0; i < lista.Count; i++)
            {
                var slot = lista[i];
                if (string.IsNullOrWhiteSpace(slot.FilePath))
                {
                    continue;
                }

                var resuelta = RutaPortable.ResolverContraCarpeta(slot.FilePath, carpetaOrigen);
                if (!string.Equals(resuelta, slot.FilePath, StringComparison.Ordinal))
                {
                    lista[i] = slot with { FilePath = resuelta };
                }
            }
        }
    }

    /// <summary>Cuenta audios y cuántos existen en disco (para el popup).</summary>
    /// <param name="config">Configuración ya resuelta contra su carpeta.</param>
    /// <returns>Total de slots con audio y cuántos se encontraron.</returns>
    public static (int TotalAudios, int Encontrados) ContarPortable(ConsolaConfiguracion config)
    {
        var total = 0;
        var encontrados = 0;
        foreach (var lista in config.Paletas.Values)
        {
            foreach (var slot in lista)
            {
                if (string.IsNullOrWhiteSpace(slot.FilePath))
                {
                    continue;
                }

                total++;
                try
                {
                    if (System.IO.File.Exists(slot.FilePath))
                    {
                        encontrados++;
                    }
                }
                catch
                {
                    // Ruta inválida: cuenta como no encontrado.
                }
            }
        }

        return (total, encontrados);
    }

    /// <summary>Activa un portable confirmado: entra en sesión portable.</summary>
    /// <param name="config">Configuración ya resuelta (la lee el pre-escaneo).</param>
    /// <param name="rutaOrigen">Archivo portable (destino de los guardados).</param>
    /// <remarks>
    /// Conserva el IdInstalacion local (la telemetría del anfitrión no se
    /// contamina) y fija la ruta de sesión: los próximos Guardar van al
    /// archivo cargado, sin tocar el JSON local. Muere con la app.
    /// </remarks>
    public static void ActivarPortable(ConsolaConfiguracion config, string rutaOrigen)
    {
        lock (CandadoConfig)
        {
            _cache ??= LeerDelDisco();
            if (!string.IsNullOrWhiteSpace(_cache.IdInstalacion))
            {
                config.IdInstalacion = _cache.IdInstalacion;
            }

            _cache = config;
            _rutaSesion = rutaOrigen;
        }
    }

    /// <summary>Indica si un slot merece persistirse (audio o edición real).</summary>
    /// <param name="item">Slot a evaluar.</param>
    /// <returns>True si tiene audio, cues, ganancia, fundidos o color.</returns>
    private static bool SlotEsPersistible(PaletteItem item) =>
        item.TieneAudio
        || item.CueInicio > TimeSpan.Zero
        || item.CueFin.HasValue
        || Math.Abs(item.GananciaDb) > 0.001
        || item.FundidoEntrada > TimeSpan.Zero
        || item.FundidoSalida > TimeSpan.Zero
        || item.ColorKey is not null;
}
