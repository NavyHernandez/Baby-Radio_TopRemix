using System.Text.Json.Nodes;

namespace BebeRadio.Support.Telemetria;

/// <summary>
/// Registro de una instalación para la telemetría (Firestore).
/// DTO puro: arma el payload REST sin I/O.
/// </summary>
/// <param name="Id">GUID estable de la instalación.</param>
/// <param name="Version">Versión de la app (p. ej. 0.1.3).</param>
/// <param name="SO">Descripción del sistema operativo.</param>
/// <param name="Arquitectura">Arquitectura del SO (X64, Arm64…).</param>
/// <param name="Ip">IP pública (vacía si no se pudo resolver).</param>
/// <param name="Pais">País de la IP (vacío si se desconoce).</param>
/// <param name="CodigoPais">Código ISO del país (vacío si se desconoce).</param>
/// <param name="PrimeraVezUtc">Primer reporte (solo se escribe al crear).</param>
/// <param name="UltimaVezUtc">Último reporte.</param>
public sealed record DatosInstalacion(
    string Id,
    string Version,
    string SO,
    string Arquitectura,
    string Ip,
    string Pais,
    string CodigoPais,
    DateTime PrimeraVezUtc,
    DateTime UltimaVezUtc)
{
    /// <summary>Campos que se actualizan en cada reporte (el resto se preserva).</summary>
    public static IReadOnlyList<string> CamposActualizables { get; } =
    [
        "version", "so", "arquitectura", "ip", "pais", "codigoPais", "ultimaVez",
    ];

    /// <summary>Arma los campos JSON en formato Firestore REST.</summary>
    /// <param name="incluirPrimeraVez">True solo al crear el documento.</param>
    /// <returns>Objeto fields (reutilizable en update y commit).</returns>
    public JsonObject ACamposFirestore(bool incluirPrimeraVez)
    {
        var campos = new JsonObject
        {
            ["version"] = Texto(Version),
            ["so"] = Texto(SO),
            ["arquitectura"] = Texto(Arquitectura),
            ["ip"] = Texto(Ip),
            ["pais"] = Texto(Pais),
            ["codigoPais"] = Texto(CodigoPais),
            ["ultimaVez"] = Marca(UltimaVezUtc),
        };
        if (incluirPrimeraVez)
        {
            campos["primeraVez"] = Marca(PrimeraVezUtc);
        }

        return campos;
    }

    /// <summary>Valor string de Firestore (nunca null).</summary>
    /// <param name="valor">Texto o null.</param>
    /// <returns>Nodo stringValue.</returns>
    private static JsonObject Texto(string? valor) =>
        new() { ["stringValue"] = valor ?? string.Empty };

    /// <summary>Valor timestamp de Firestore en RFC 3339.</summary>
    /// <param name="momento">Momento UTC.</param>
    /// <returns>Nodo timestampValue.</returns>
    private static JsonObject Marca(DateTime momento) =>
        new() { ["timestampValue"] = momento.ToUniversalTime().ToString("o") };
}
