namespace BebeRadio.Models;

/// <summary>
/// Ficha visual de una categoría: etiqueta, token de color, icono y ejemplo.
/// DTO inmutable; el registro vivo está en <see cref="CategoryPalette"/>.
/// </summary>
/// <param name="Category">Categoría del dominio.</param>
/// <param name="Label">Etiqueta en español para botones y listas.</param>
/// <param name="BrushKey">Clave del brush en <c>Themes/ColorPalette.xaml</c> (p. ej. CategoryMusic).</param>
/// <param name="Symbol">Nombre del símbolo FluentIcons.WinUI (p. ej. MusicNote2).</param>
/// <param name="ExampleTitle">Título de ejemplo (mock) que se muestra en el botón.</param>
/// <param name="ExampleMeta">Detalle del ejemplo (artista/duración, mock).</param>
public sealed record CategorySwatch(
    TrackCategory Category,
    string Label,
    string BrushKey,
    string Symbol,
    string ExampleTitle,
    string ExampleMeta);
