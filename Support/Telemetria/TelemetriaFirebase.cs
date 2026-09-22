using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace BebeRadio.Support.Telemetria;

/// <summary>
/// Telemetría ligera a Firestore (sin SDK): crea el ID único en la primera
/// ejecución y actualiza versión/SO/IP/país en cada arranque. Fire-and-forget,
/// best-effort: jamás bloquea ni tumba la app (todo cae a <see cref="RegistroErrores"/>).
/// </summary>
public static class TelemetriaFirebase
{
    /// <summary>Proyecto Firebase de Baby Radio.</summary>
    private const string ProjectId = "baby-radio-3bfbb";

    /// <summary>Clave web (pública por diseño Firebase; protegen las reglas).</summary>
    private const string ApiKey = "AIzaSyBAfxTg8NlxKqjpcT7F0jqlFtsmAzQDJ5c";

    /// <summary>Colección de instalaciones.</summary>
    private const string Coleccion = "instalaciones";

    /// <summary>IP + país en una sola llamada (HTTPS, plan gratuito).</summary>
    private const string UrlGeoIp = "https://ipapi.co/json/";

    /// <summary>Cliente compartido (un socket reutilizado, sin fugas).</summary>
    private static readonly HttpClient Http = new();

    /// <summary>Reporta la instalación (crea ID si es la primera vez).</summary>
    /// <remarks>Llamar sin await desde el arranque; nunca lanza.</remarks>
    public static async Task ReportarAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var token = cts.Token;

            var (esPrimeraVez, id) = AsegurarId();
            var ahora = DateTime.UtcNow;
            var (ip, pais, codigoPais) = await ResolverGeoIpAsync(token);

            // En updates la máscara excluye primeraVez: el servidor la preserva.
            var datos = new DatosInstalacion(
                id,
                ActualizadorBaby.VersionInstalada,
                RuntimeInformation.OSDescription.Trim(),
                RuntimeInformation.OSArchitecture.ToString(),
                ip,
                pais,
                codigoPais,
                ahora,
                ahora);

            await EnviarAsync(datos, esPrimeraVez, token);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Telemetria.Reportar");
        }
    }

    /// <summary>Devuelve el ID estable, generándolo y guardándolo si falta.</summary>
    /// <returns>(esPrimeraVez, id).</returns>
    private static (bool EsPrimeraVez, string Id) AsegurarId()
    {
        var config = ConsolaStore.Cargar();
        if (!string.IsNullOrWhiteSpace(config.IdInstalacion))
        {
            return (false, config.IdInstalacion);
        }

        config.IdInstalacion = Guid.NewGuid().ToString("N");
        ConsolaStore.Guardar(config);
        return (true, config.IdInstalacion);
    }

    /// <summary>Resuelve IP pública + país (vacío si no hay red).</summary>
    /// <param name="token">Cancelación.</param>
    /// <returns>(ip, país, código).</returns>
    private static async Task<(string Ip, string Pais, string Codigo)> ResolverGeoIpAsync(
        CancellationToken token)
    {
        try
        {
            using var respuesta = await Http.GetAsync(UrlGeoIp, token);
            if (!respuesta.IsSuccessStatusCode)
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            using var cuerpo = await respuesta.Content.ReadAsStreamAsync(token);
            using var json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: token);
            var raiz = json.RootElement;
            return (
                LeerTexto(raiz, "ip"),
                LeerTexto(raiz, "country_name"),
                LeerTexto(raiz, "country_code"));
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>Lee un string del JSON (vacío si falta o no es texto).</summary>
    /// <param name="raiz">Objeto raíz.</param>
    /// <param name="propiedad">Nombre de la propiedad.</param>
    /// <returns>Texto recortado o vacío.</returns>
    private static string LeerTexto(JsonElement raiz, string propiedad) =>
        raiz.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? (valor.GetString() ?? string.Empty).Trim()
            : string.Empty;

    /// <summary>Crea o actualiza el documento (PATCH = upsert con updateMask).</summary>
    /// <param name="datos">Registro a enviar.</param>
    /// <param name="esPrimeraVez">Incluye primeraVez en la máscara.</param>
    /// <param name="token">Cancelación.</param>
    private static async Task EnviarAsync(DatosInstalacion datos, bool esPrimeraVez, CancellationToken token)
    {
        var mascara = string.Join(
            "&",
            DatosInstalacion.CamposActualizables
                .Append(esPrimeraVez ? "primeraVez" : string.Empty)
                .Where(campo => !string.IsNullOrEmpty(campo))
                .Select(campo => $"updateMask.fieldPaths={campo}"));
        using var contenido = new StringContent(
            datos.AJsonFirestore(esPrimeraVez), Encoding.UTF8, "application/json");
        using var respuesta = await Http.PatchAsync($"{UrlDocumento(datos.Id)}&{mascara}", contenido, token);
        respuesta.EnsureSuccessStatusCode();
    }

    /// <summary>URL REST del documento (con API key).</summary>
    /// <param name="id">ID de instalación.</param>
    /// <returns>URL completa.</returns>
    private static string UrlDocumento(string id) =>
        $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/{Coleccion}/{Uri.EscapeDataString(id)}?key={ApiKey}";
}
