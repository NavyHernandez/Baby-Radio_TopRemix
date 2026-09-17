namespace BebeRadio.Models;

/// <summary>
/// Categoría de contenido de un track (codificación por color global, estilo Jazler).
/// Cada miembro tiene su token en <c>Themes/ColorPalette.xaml</c> y su ficha en
/// <see cref="CategoryPalette"/>. Al agregar una categoría, registrarla en ambos.
/// </summary>
public enum TrackCategory
{
    /// <summary>Música.</summary>
    Music,

    /// <summary>Jingles / cortinillas.</summary>
    Jingle,

    /// <summary>Comerciales / spots.</summary>
    Commercial,

    /// <summary>IDs de emisora.</summary>
    Id,

    /// <summary>Sweepers / barridas entre canciones.</summary>
    Sweeper,

    /// <summary>Beds / colchones musicales de fondo.</summary>
    Bed,

    /// <summary>Noticias / informativos.</summary>
    News,

    /// <summary>Promos / autopromoción.</summary>
    Promo,

    /// <summary>Programas / espacios.</summary>
    Program,

    /// <summary>Relleno / fillers.</summary>
    Filler,
}
