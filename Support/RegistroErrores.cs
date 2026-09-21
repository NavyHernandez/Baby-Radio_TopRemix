using System.Diagnostics;

namespace BebeRadio.Support;

/// <summary>
/// Registro de errores de la consola (diagnóstico en máquina del usuario).
/// Anexa cada excepción con contexto a <c>baby-radio-error.log</c> en la
/// carpeta local y la replica al debugger. Nunca lanza: si el disco falla,
/// solo escribe al debugger. Los flujos de diálogos lo usan para no cerrar
/// la app ante un fallo y dejar rastro de la causa real.
/// </summary>
public static class RegistroErrores
{
    /// <summary>Nombre del archivo de diagnóstico.</summary>
    public const string NombreArchivo = "baby-radio-error.log";

    private static readonly object _candado = new();

    /// <summary>Registra una traza informativa (quién paró qué).</summary>
    /// <param name="contexto">Flujo (p. ej. Cola.Stop).</param>
    /// <param name="detalle">Detalle breve.</param>
    /// <remarks>Diagnóstico best-effort: caza paradas inesperadas de la cola.</remarks>
    public static void Traza(string contexto, string detalle)
    {
        var linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {contexto}: {detalle}{Environment.NewLine}";
        Debug.WriteLine(linea);
        try
        {
            lock (_candado)
            {
                var ruta = System.IO.Path.Combine(
                    ConsolaStore.CarpetaDatos(), NombreArchivo);
                System.IO.File.AppendAllText(ruta, linea);
            }
        }
        catch
        {
            // Diagnóstico best-effort: ya quedó en el debugger.
        }
    }

    /// <summary>Registra una excepción con su contexto.</summary>
    /// <param name="excepcion">Excepción capturada.</param>
    /// <param name="contexto">Flujo donde ocurrió (p. ej. Paleta.Editar).</param>
    public static void Registrar(Exception excepcion, string contexto)
    {
        var linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {contexto}: {excepcion.GetType().Name}: {excepcion.Message}{Environment.NewLine}{excepcion.StackTrace}{Environment.NewLine}";
        Debug.WriteLine(linea);
        try
        {
            lock (_candado)
            {
                var ruta = System.IO.Path.Combine(
                    ConsolaStore.CarpetaDatos(), NombreArchivo);
                System.IO.File.AppendAllText(ruta, linea);
            }
        }
        catch
        {
            // Diagnóstico best-effort: ya quedó en el debugger.
        }
    }
}
