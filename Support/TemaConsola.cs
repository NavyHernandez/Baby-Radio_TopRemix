using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Support;

/// <summary>
/// Tema único de Baby Radio (consola oscura). Los colores salen de
/// ColorPalette.xaml; los controles del sistema siguen a RequestedTheme
/// sobre el contenido de la ventana. Sin modo claro.
/// </summary>
public static class TemaConsola
{
    /// <summary>Se eleva al aplicar (la VU recachea; la titlebar refresca el alias).</summary>
    public static event Action? Cambio;

    /// <summary>Último tema aplicado (para popups, que no heredan).</summary>
    public static ElementTheme TemaActual => ElementTheme.Dark;

    /// <summary>Lee el perfil guardado (o defecto sin alias).</summary>
    /// <returns>Perfil del JSON portable.</returns>
    public static Models.PerfilOperador LeerPerfil()
    {
        try
        {
            return ConsolaStore.Cargar().Perfil;
        }
        catch
        {
            return new Models.PerfilOperador();
        }
    }

    /// <summary>Guarda el alias en el JSON portable y avisa el cambio.</summary>
    /// <param name="alias">Nombre o alias.</param>
    public static void GuardarPerfil(string alias)
    {
        var config = ConsolaStore.Cargar();
        config.Perfil.Alias = alias.Trim();
        ConsolaStore.Guardar(config);
        Cambio?.Invoke();
    }

    /// <summary>Aplica el tema guardado a la ventana (arranque).</summary>
    /// <param name="ventana">Ventana principal.</param>
    /// <returns>Perfil aplicado.</returns>
    public static Models.PerfilOperador AplicarGuardado(Window ventana)
    {
        var perfil = LeerPerfil();
        Aplicar(ventana);
        return perfil;
    }

    /// <summary>Aplica el tema oscuro en vivo (tokens + sistema).</summary>
    /// <param name="ventana">Ventana principal.</param>
    public static void Aplicar(Window ventana)
    {
        if (ventana.Content is FrameworkElement raiz)
        {
            raiz.RequestedTheme = ElementTheme.Dark;
        }

        Cambio?.Invoke();
    }

    /// <summary>Fija el tema actual al popup (no heredan el de la ventana).</summary>
    /// <param name="dialogo">Diálogo a tematizar.</param>
    public static void AplicarADialogo(ContentDialog dialogo) =>
        dialogo.RequestedTheme = TemaActual;
}
