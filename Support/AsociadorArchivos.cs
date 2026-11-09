using Microsoft.Win32;

namespace BebeRadio.Support;

/// <summary>
/// Registra a Baby Radio como candidato a reproductor de Windows (por usuario,
/// HKCU, sin admin): ProgID <c>BabyRadio.Audio</c>, "Abrir con" y la lista de
/// Apps predeterminadas (RegisteredApplications + Capabilities). Windows no
/// deja que la app se elija sola como predeterminada (UserChoice + UCPD);
/// el usuario la elige en Configuración con un clic (deep link
/// <c>ms-settings:defaultapps?registeredAppUser=Baby Radio</c>).
/// </summary>
/// <remarks>
/// Idempotente: registrar varias veces solo refresca las claves (se re-apunta
/// al exe vigente, útil tras actualizaciones). Las extensiones salen de
/// <see cref="AudioFileInspector.AudioExtensions"/> (una sola fuente de verdad).
/// </remarks>
public static class AsociadorArchivos
{
    /// <summary>Nombre de la app en RegisteredApplications (clave del deep link).</summary>
    public const string NombreApp = "Baby Radio";

    /// <summary>ProgID propio (no escribir el default de la extensión: lo decide el usuario).</summary>
    public const string ProgId = "BabyRadio.Audio";

    /// <summary>Ruta relativa de Capabilities bajo HKCU (misma raíz que RegisteredApplications).</summary>
    private const string RutaCapabilities =
        @"Software\Classes\Applications\BabyRadio.exe\Capabilities";

    /// <summary>Descripción mostrada en Apps predeterminadas.</summary>
    private const string Descripcion =
        "Baby Radio: consola de automatización musical y reproductor de audio.";

    /// <summary>
    /// Escribe (o refresca) el registro per-user para que Baby Radio aparezca
    /// en "Abrir con" y en Apps predeterminadas.
    /// </summary>
    /// <remarks>
    /// Lanzar solo desde ganchos de instalación de Velopack (OnFirstRun /
    /// OnRestarted): un build debug o portable no debe registrar su exe.
    /// Nunca lanza: los fallos quedan en <see cref="RegistroErrores"/>.
    /// </remarks>
    public static void Registrar()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            var comando = $"\"{exe}\" \"%1\"";

            // ProgID completo (icono + verb open): lo usa Windows si el usuario
            // nos elige como default; mantiene la info intacta al cambiar de app.
            using (var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                progId?.SetValue(string.Empty, "Archivo de audio Baby Radio");
                using var icono = progId?.CreateSubKey("DefaultIcon");
                icono?.SetValue(string.Empty, $"\"{exe}\",0");
                using var shell = progId?.CreateSubKey(@"shell\open");
                shell?.SetValue(string.Empty, "Reproducir con Baby Radio");
                using var comandoKey = shell?.CreateSubKey("command");
                comandoKey?.SetValue(string.Empty, comando);
            }

            // Candidato en "Abrir con" por extensión (OpenWithProgids + SupportedTypes).
            using (var aplicacion = Registry.CurrentUser.CreateSubKey(
                       $@"Software\Classes\Applications\{NombreExe()}\shell\open\command"))
            {
                aplicacion?.SetValue(string.Empty, comando);
            }

            using (var soportados = Registry.CurrentUser.CreateSubKey(
                       $@"Software\Classes\Applications\{NombreExe()}\SupportedTypes"))
            {
                foreach (var extension in AudioFileInspector.AudioExtensions)
                {
                    soportados?.SetValue(extension, string.Empty);
                }
            }

            // Apps predeterminadas (Settings): RegisteredApplications → Capabilities.
            using (var registradas = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                registradas?.SetValue(NombreApp, RutaCapabilities);
            }

            using (var capabilities = Registry.CurrentUser.CreateSubKey(RutaCapabilities))
            {
                capabilities?.SetValue("ApplicationName", NombreApp);
                capabilities?.SetValue("ApplicationDescription", Descripcion);
                using var asociaciones = capabilities?.CreateSubKey("FileAssociations");
                foreach (var extension in AudioFileInspector.AudioExtensions)
                {
                    asociaciones?.SetValue(extension, ProgId);
                }
            }

            // OpenWithProgids en la extensión (aparece en el submenú "Abrir con").
            foreach (var extension in AudioFileInspector.AudioExtensions)
            {
                using var openWith = Registry.CurrentUser.CreateSubKey(
                    $@"Software\Classes\{extension}\OpenWithProgids");
                openWith?.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Asociador.Registrar");
        }
    }

    /// <summary>
    /// Borra todas las claves escritas por <see cref="Registrar"/>.
    /// </summary>
    /// <remarks>Llamar en el gancho OnBeforeUninstallFastCallback de Velopack. Nunca lanza.</remarks>
    public static void Desregistrar()
    {
        try
        {
            BorrarSubKey($@"Software\Classes\{ProgId}");
            BorrarSubKey($@"Software\Classes\Applications\{NombreExe()}");
            BorrarValor(@"Software\RegisteredApplications", NombreApp);

            foreach (var extension in AudioFileInspector.AudioExtensions)
            {
                BorrarValor($@"Software\Classes\{extension}\OpenWithProgids", ProgId);
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Asociador.Desregistrar");
        }
    }

    /// <summary>
    /// Indica si el registro per-user está presente (para el estado en Config).
    /// </summary>
    /// <returns>True si RegisteredApplications y el comando del ProgID existen.</returns>
    public static bool EstaRegistrado()
    {
        try
        {
            using var registradas = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");
            if (registradas?.GetValue(NombreApp) is not string ruta || ruta != RutaCapabilities)
            {
                return false;
            }

            using var comando = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command");
            return comando?.GetValue(string.Empty) is string cmd && cmd.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Nombre del exe vigente (p. ej. <c>BabyRadio.exe</c>).</summary>
    /// <returns>Nombre de archivo del proceso actual.</returns>
    private static string NombreExe() =>
        Path.GetFileName(Environment.ProcessPath ?? "BabyRadio.exe");

    /// <summary>Borra un subárbol de HKCU si existe (silencioso si no).</summary>
    /// <param name="ruta">Ruta relativa a HKCU.</param>
    private static void BorrarSubKey(string ruta)
    {
        if (Registry.CurrentUser.OpenSubKey(ruta) is not null)
        {
            Registry.CurrentUser.DeleteSubKeyTree(ruta, throwOnMissingSubKey: false);
        }
    }

    /// <summary>Borra un valor si existe (silencioso si no).</summary>
    /// <param name="ruta">Ruta relativa a HKCU.</param>
    /// <param name="nombre">Nombre del valor.</param>
    private static void BorrarValor(string ruta, string nombre)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ruta, writable: true);
        if (key?.GetValue(nombre) is not null)
        {
            key.DeleteValue(nombre, throwOnMissingValue: false);
        }
    }
}
