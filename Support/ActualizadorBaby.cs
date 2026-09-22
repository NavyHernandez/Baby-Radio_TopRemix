using System.Net.Http;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace BebeRadio.Support;

/// <summary>
/// Resultado de una comprobación de actualizaciones de Baby Radio.
/// </summary>
public sealed class ResultadoActualizacion
{
    /// <summary>Versión instalada en la máquina del usuario.</summary>
    public string VersionInstalada { get; init; } = string.Empty;

    /// <summary>Última versión disponible en GitHub Releases.</summary>
    public string VersionDisponible { get; init; } = string.Empty;

    /// <summary>True si la versión disponible es más nueva que la instalada.</summary>
    public bool HayActualizacion { get; init; }

    /// <summary>True si la consulta a GitHub se completó sin errores.</summary>
    public bool ConsultaOk { get; init; }

    /// <summary>Motivo legible del resultado (fallo real o confirmación de consulta).</summary>
    public string Detalle { get; init; } = string.Empty;

    /// <summary>Momento UTC de la comprobación.</summary>
    public DateTime ConsultadoUtc { get; init; }

    /// <summary>Información interna de Velopack para descargar y aplicar.</summary>
    internal UpdateInfo? InfoVelopack { get; init; }
}

/// <summary>
/// Servicio de auto-actualización de Baby Radio con Velopack + GitHub Releases.
/// Flujo: ComprobarAsync → DescargarAsync (progreso 0-100) → Aplicar (reinicia).
/// Los errores se registran en <see cref="RegistroErrores"/> y nunca se lanzan.
/// </summary>
public sealed class ActualizadorBaby
{
    /// <summary>Instancia única del servicio.</summary>
    public static ActualizadorBaby Instancia { get; } = new();

    /// <summary>Versión instalada (ensamblado en tiempo de ejecución).</summary>
    public static string VersionInstalada { get; } =
        typeof(ActualizadorBaby).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    private UpdateManager? _gestor;
    private UpdateInfo? _pendiente;

    private ActualizadorBaby()
    {
    }

    /// <summary>True si hay una actualización pendiente de descargar.</summary>
    public bool TienePendiente => _pendiente is not null;

    /// <summary>Versión pendiente de descargar (vacía si no hay ninguna).</summary>
    public string VersionPendiente =>
        _pendiente?.TargetFullRelease.Version.ToString() ?? string.Empty;

    /// <summary>
    /// Consulta GitHub Releases vía Velopack y devuelve el resultado.
    /// </summary>
    /// <returns>Resultado con versiones y disponibilidad.</returns>
    public async Task<ResultadoActualizacion> ComprobarAsync()
    {
        try
        {
            var fuente = new GithubSource(ContactoInfo.GitHubReleasesUrl, string.Empty, false, null);
            _gestor = new UpdateManager(fuente);

            var info = await _gestor.CheckForUpdatesAsync();
            if (info is null)
            {
                return new ResultadoActualizacion
                {
                    VersionInstalada = VersionInstalada,
                    VersionDisponible = VersionInstalada,
                    HayActualizacion = false,
                    ConsultaOk = true,
                    Detalle = $"Consultado a GitHub: estás al día (v{VersionInstalada}).",
                    ConsultadoUtc = DateTime.UtcNow,
                };
            }

            _pendiente = info;
            return new ResultadoActualizacion
            {
                VersionInstalada = VersionInstalada,
                VersionDisponible = info.TargetFullRelease.Version.ToString(),
                HayActualizacion = true,
                ConsultaOk = true,
                Detalle = $"Actualización disponible en GitHub: v{info.TargetFullRelease.Version}.",
                ConsultadoUtc = DateTime.UtcNow,
                InfoVelopack = info,
            };
        }
        catch (NotInstalledException ex)
        {
            RegistroErrores.Registrar(ex, "Actualizador.Comprobar.NoInstalada");
            return new ResultadoActualizacion
            {
                VersionInstalada = VersionInstalada,
                VersionDisponible = VersionInstalada,
                HayActualizacion = false,
                ConsultaOk = false,
                Detalle = "Esta copia no se instaló con el Setup de Baby Radio. Instala desde GitHub Releases para auto-actualizar.",
                ConsultadoUtc = DateTime.UtcNow,
            };
        }
        catch (HttpRequestException ex)
        {
            RegistroErrores.Registrar(ex, "Actualizador.Comprobar.Red");
            return new ResultadoActualizacion
            {
                VersionInstalada = VersionInstalada,
                VersionDisponible = VersionInstalada,
                HayActualizacion = false,
                ConsultaOk = false,
                Detalle = "Sin conexión con GitHub. Revisa tu internet e inténtalo de nuevo.",
                ConsultadoUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Actualizador.Comprobar");
            return new ResultadoActualizacion
            {
                VersionInstalada = VersionInstalada,
                VersionDisponible = VersionInstalada,
                HayActualizacion = false,
                ConsultaOk = false,
                Detalle = "No se pudo consultar GitHub. Revisa tu conexión e inténtalo de nuevo.",
                ConsultadoUtc = DateTime.UtcNow,
            };
        }
    }

    /// <summary>
    /// Descarga la actualización pendiente con reporte de progreso 0-100.
    /// </summary>
    /// <param name="progreso">Callback opcional de progreso.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <remarks>Requiere haber llamado antes a <see cref="ComprobarAsync"/> con update disponible.</remarks>
    public Task DescargarAsync(Action<int>? progreso = null, CancellationToken cancelacion = default)
    {
        if (_gestor is null || _pendiente is null)
        {
            throw new InvalidOperationException("Sin actualización pendiente. Llama a ComprobarAsync primero.");
        }

        return _gestor.DownloadUpdatesAsync(_pendiente, progreso, cancelacion);
    }

    /// <summary>
    /// Aplica la actualización descargada y reinicia la app.
    /// </summary>
    /// <remarks>Requiere descarga previa con <see cref="DescargarAsync"/>.</remarks>
    public void Aplicar()
    {
        if (_gestor is null || _pendiente is null)
        {
            throw new InvalidOperationException("Sin actualización pendiente.");
        }

        _gestor.ApplyUpdatesAndRestart(_pendiente);
    }
}
