namespace BebeRadio.Support;

/// <summary>Cola inicial mock estilo Jazler (mezcla de categorías, 12 entradas).</summary>
public static class MockQueueBuilder
{
    /// <summary>Construye la cola inicial de reproducción.</summary>
    /// <returns>Entradas con la primera marcada como actual.</returns>
    public static List<Models.QueueEntry> BuildInitial() => new()
    {
        new("Neon Skyline", "Midnight Drive", TimeSpan.FromSeconds(222), Models.TrackCategory.Music),
        new("Top Radio ID 07", "Cortinilla", TimeSpan.FromSeconds(8), Models.TrackCategory.Jingle),
        new("Fiebre de Sábado", "Los Cometas", TimeSpan.FromSeconds(204), Models.TrackCategory.Music),
        new("Spot Café Central", "Campaña A", TimeSpan.FromSeconds(30), Models.TrackCategory.Commercial),
        new("Barrida Verano", "Transición", TimeSpan.FromSeconds(6), Models.TrackCategory.Sweeper),
        new("Ciudad Eléctrica", "Voltaje", TimeSpan.FromSeconds(187), Models.TrackCategory.Music),
        new("Flash 12:00", "Informativo", TimeSpan.FromSeconds(135), Models.TrackCategory.News),
        new("Bed Noticias", "Fondo", TimeSpan.FromSeconds(60), Models.TrackCategory.Bed),
        new("Promo Concierto", "Evento", TimeSpan.FromSeconds(25), Models.TrackCategory.Promo),
        new("Ruta 66", "Los Viajeros", TimeSpan.FromSeconds(243), Models.TrackCategory.Music),
        new("Hora en punto", "Señal", TimeSpan.FromSeconds(5), Models.TrackCategory.Id),
        new("El Despertador", "Magazine", TimeSpan.FromSeconds(3480), Models.TrackCategory.Program),
    };
}
