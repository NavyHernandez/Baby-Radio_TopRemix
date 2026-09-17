namespace BebeRadio.Models;

/// <summary>
/// Perfil del operador (portable en el JSON de la consola).
/// Alias visible en la titlebar. Tema único oscuro.
/// </summary>
public sealed class PerfilOperador
{
    /// <summary>Nombre o alias del operador (vacío = sin alias).</summary>
    public string Alias { get; set; } = string.Empty;
}
