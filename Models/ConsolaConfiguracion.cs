namespace BebeRadio.Models;

/// <summary>
/// Raíz del JSON portable de la consola Baby Radio.
/// Incluye categorías personalizadas, ajustes de fijas y slots editados.
/// El archivo puede copiarse a otra PC (Exportar/Importar).
/// </summary>
public sealed class ConsolaConfiguracion
{
    /// <summary>Categorías creadas por el usuario.</summary>
    public List<CategoriaPersonalizada> CategoriasPersonalizadas { get; set; } = new();

    /// <summary>Ajustes de nombre/color/icono sobre las 10 fijas.</summary>
    public List<CategoriaPersonalizada> AjustesFijas { get; set; } = new();

    /// <summary>Slots editados por propietaria (enum o custom:id).</summary>
    public Dictionary<string, List<SlotEfectoGuardado>> Paletas { get; set; } = new();

    /// <summary>Perfil del operador (alias + tema).</summary>
    public PerfilOperador Perfil { get; set; } = new();
}
