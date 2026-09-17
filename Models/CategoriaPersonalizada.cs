namespace BebeRadio.Models;

/// <summary>
/// Categoría definida por el usuario (o ajuste de una fija).
/// Las fijas viven en <see cref="CategoryPalette"/>; sus ajustes usan
/// esta misma forma con <see cref="Id"/> = nombre del enum.
/// Las nuevas usan <c>custom:guid</c>. Se persiste en JSON portable.
/// </summary>
/// <param name="Id">Identificador (nombre de enum o custom:guid).</param>
/// <param name="Label">Etiqueta en español.</param>
/// <param name="ColorHex">Color en formato #RRGGBB.</param>
/// <param name="Symbol">Nombre del símbolo FluentIcons (p. ej. MusicNote2).</param>
/// <param name="Cargada">True si ocupa un slot del riel (máx 7).</param>
/// <param name="Orden">Posición en el riel y el gestor.</param>
public sealed record CategoriaPersonalizada(
    string Id,
    string Label,
    string ColorHex,
    string Symbol,
    bool Cargada = true,
    int Orden = 999);
