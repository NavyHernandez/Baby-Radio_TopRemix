namespace BebeRadio.Models;

/// <summary>
/// DTO inmutable de pista para el mock de Fase 1. Sin persistencia ni audio real.
/// </summary>
/// <param name="Title">Nombre de la pista.</param>
/// <param name="Artist">Artista o anunciante.</param>
/// <param name="Duration">Duración total del mock.</param>
/// <param name="Category">Categoría (define el color de acento).</param>
public sealed record Track(string Title, string Artist, TimeSpan Duration, TrackCategory Category);
