using System.Text.Json;
using BebeRadio.Models;

namespace BebeRadio.Support;

/// <summary>
/// Persistencia portable de la consola Baby Radio (categorías + paletas).
/// Un solo JSON en la carpeta local; Exportar/Importar lo lleva a otra PC.
/// Síncrono y tolerante: si el archivo falta o está corrupto, devuelve
/// configuración vacía (la vista cae al mock). Sin I/O en ViewModels:
/// ellos llaman aquí y este helper aísla el disco.
/// </summary>
public static class ConsolaStore
{
    /// <summary>Nombre del archivo local de configuración.</summary>
    public const string NombreArchivo = "baby-radio-consola.json";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly object CandadoConfig = new();
    private static ConsolaConfiguracion? _cache;

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

    /// <summary>Guarda la configuración completa y actualiza la caché.</summary>
    /// <param name="config">Configuración a persistir.</param>
    public static void Guardar(ConsolaConfiguracion config)
    {
        lock (CandadoConfig)
        {
            _cache = config;
            try
            {
                var json = JsonSerializer.Serialize(config, Opciones);
                System.IO.File.WriteAllText(RutaArchivo(), json);
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
