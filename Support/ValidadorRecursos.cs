namespace BebeRadio.Support;

/// <summary>
/// Valida al arrancar que los tokens críticos de la paleta existan.
/// Un {StaticResource} inexistente hace que la pantalla que lo usa muera con
/// XamlParseException al cargarse (p. ej. el modo operador); esta validación
/// lo detecta en el arranque y lo deja registrado en el log. Nunca lanza.
/// </summary>
public static class ValidadorRecursos
{
    /// <summary>Tokens críticos (brushes y estilos de XAML cargado perezoso).</summary>
    private static readonly string[] ClavesEsenciales =
    {
        "BackgroundBase", "BackgroundPanel", "BorderSubtle",
        "TextPrimary", "TextSecondary", "TextDisabled",
        "CategoryMusic", "CategoryJingle", "CategoryCommercial", "CategoryId",
        "CategorySweeper", "CategoryBed", "CategoryNews", "CategoryPromo",
        "CategoryProgram", "CategoryFiller",
        "StateOnAir", "DigitalAmber", "VuTrack",
        "LightInk", "LightRim", "AluminumTop", "AluminumEdge",
        "HighlightTop", "BottomEdge", "HoverOverlay", "PressedOverlay",
        "BebeLightButtonStyle", "BebeCategoryButtonStyle", "BebeCartButtonStyle",
        "BebeDialogStyle",
    };

    /// <summary>Comprueba las claves y registra las faltantes (sin lanzar).</summary>
    /// <returns>Claves faltantes (vacío si todo está).</returns>
    /// <remarks>Llamar una vez al arrancar, tras fusionar los recursos.</remarks>
    public static IReadOnlyList<string> ValidarEsenciales()
    {
        var faltantes = new List<string>();
        try
        {
            var recursos = Microsoft.UI.Xaml.Application.Current?.Resources;
            if (recursos is null)
            {
                return faltantes;
            }

            foreach (var clave in ClavesEsenciales)
            {
                if (!recursos.TryGetValue(clave, out _))
                {
                    faltantes.Add(clave);
                }
            }
        }
        catch
        {
            return faltantes;
        }

        if (faltantes.Count > 0)
        {
            RegistroErrores.Registrar(
                new KeyNotFoundException($"Tokens faltantes: {string.Join(", ", faltantes)}"),
                "Recursos.Validacion");
        }

        return faltantes;
    }
}
