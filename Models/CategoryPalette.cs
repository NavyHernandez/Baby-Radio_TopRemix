namespace BebeRadio.Models;

/// <summary>
/// Registro central de categorías estilo Jazler: una ficha por botón de la parrilla.
/// Fuente única para la paleta del showcase, la futura librería y la playlist.
/// Los ejemplos son mocks de Fase 1 (sin persistencia ni audio real).
/// </summary>
public static class CategoryPalette
{
    /// <summary>Parrilla completa en orden de presentación (10 categorías).</summary>
    public static IReadOnlyList<CategorySwatch> All { get; } = new List<CategorySwatch>
    {
        new(TrackCategory.Music, "Música", "CategoryMusic", "MusicNote2",
            "Neon Skyline", "Midnight Drive · 3:42"),
        new(TrackCategory.Jingle, "Jingles", "CategoryJingle", "Sparkle",
            "Top Radio ID 07", "Cortinilla · 0:08"),
        new(TrackCategory.Commercial, "Comerciales", "CategoryCommercial", "Megaphone",
            "Spot Café Central", "Campaña · 0:30"),
        new(TrackCategory.Id, "IDs", "CategoryId", "Mic",
            "Hora en punto", "Señal · 0:05"),
        new(TrackCategory.Sweeper, "Sweepers", "CategorySweeper", "ArrowShuffle",
            "Barrida Verano", "Transición · 0:06"),
        new(TrackCategory.Bed, "Beds", "CategoryBed", "Bed",
            "Bed Noticias", "Fondo · loop"),
        new(TrackCategory.News, "Noticias", "CategoryNews", "News",
            "Flash 12:00", "Informativo · 2:15"),
        new(TrackCategory.Promo, "Promos", "CategoryPromo", "Star",
            "Promo Concierto", "Evento · 0:25"),
        new(TrackCategory.Program, "Programas", "CategoryProgram", "Calendar",
            "El Despertador", "Magazine · 58:00"),
        new(TrackCategory.Filler, "Relleno", "CategoryFiller", "Clock",
            "Instrumental 12", "Fondo · 1:30"),
    }.AsReadOnly();

    /// <summary>Busca la ficha de una categoría.</summary>
    /// <param name="category">Categoría a buscar.</param>
    /// <returns>Su ficha.</returns>
    /// <exception cref="KeyNotFoundException">Si la categoría no está registrada.</exception>
    public static CategorySwatch FromCategory(TrackCategory category)
    {
        foreach (var swatch in All)
        {
            if (swatch.Category == category)
            {
                return swatch;
            }
        }

        throw new KeyNotFoundException($"Categoría sin registrar: {category}.");
    }
}
