using System.Text;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace BebeRadio.Support;

/// <summary>
/// Extrae las rutas de audio de una activación de Windows (doble clic en
/// canciones del Explorador): tipo File (registro unpackaged) o Launch con
/// la línea de comandos del ProgID (<c>"BabyRadio.exe" "cancion.mp3"</c>).
/// </summary>
/// <remarks>Filtra solo archivos de audio existentes (whitelist del inspector).</remarks>
public static class ActivacionArchivos
{
    /// <summary>
    /// Devuelve las rutas de audio de la activación (vacío si no las trae).
    /// </summary>
    /// <param name="activacion">Args de activación (puede ser null).</param>
    /// <returns>Rutas de audio existentes.</returns>
    public static List<string> ExtraerRutas(AppActivationArguments? activacion)
    {
        if (activacion is null)
        {
            return [];
        }

        try
        {
            switch (activacion.Kind)
            {
                case ExtendedActivationKind.File:
                    if (activacion.Data is IFileActivatedEventArgs archivos)
                    {
                        return archivos.Files
                            .Select(f => f.Path)
                            .Where(p => !string.IsNullOrEmpty(p)
                                && AudioFileInspector.IsAudioFile(p))
                            .ToList();
                    }

                    break;

                case ExtendedActivationKind.Launch:
                    if (activacion.Data is ILaunchActivatedEventArgs lanzamiento
                        && !string.IsNullOrWhiteSpace(lanzamiento.Arguments))
                    {
                        return DividirArgumentos(lanzamiento.Arguments)
                            .Where(p => File.Exists(p) && AudioFileInspector.IsAudioFile(p))
                            .ToList();
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Activacion.ExtraerRutas");
        }

        return [];
    }

    /// <summary>
    /// Extrae las rutas de audio de la línea de comandos cruda del proceso
    /// (fallback del arranque: el ProgID lanza <c>"exe" "cancion.mp3"</c>).
    /// </summary>
    /// <returns>Rutas de audio existentes (sin el exe).</returns>
    public static List<string> ExtraerDeCommandLine()
    {
        try
        {
            return Environment.GetCommandLineArgs()
                .Skip(1)
                .Where(p => File.Exists(p) && AudioFileInspector.IsAudioFile(p))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Divide una línea de comandos en rutas respetando comillas
    /// (<c>"ruta con espacios.mp3" otra.mp3</c> → 2 rutas).
    /// </summary>
    /// <param name="argumentos">Línea de comandos sin el exe.</param>
    /// <returns>Tokens de la línea.</returns>
    public static List<string> DividirArgumentos(string argumentos)
    {
        var tokens = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        foreach (var c in argumentos)
        {
            if (c == '"')
            {
                entreComillas = !entreComillas;
            }
            else if (c == ' ' && !entreComillas)
            {
                if (actual.Length > 0)
                {
                    tokens.Add(actual.ToString());
                    actual.Clear();
                }
            }
            else
            {
                actual.Append(c);
            }
        }

        if (actual.Length > 0)
        {
            tokens.Add(actual.ToString());
        }

        return tokens;
    }
}
