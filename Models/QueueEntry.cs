using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.Models;

/// <summary>
/// Entrada de la cola de reproducción (estilo Jazler). El item con
/// <see cref="IsCurrent"/> en true es el que suena (badge EN VIVO).
/// </summary>
public sealed partial class QueueEntry : ObservableObject
{
    /// <summary>Título de la entrada.</summary>
    public string Title { get; }

    /// <summary>Segunda línea (artista, campaña o detalle).</summary>
    public string Line2 { get; }

    /// <summary>
    /// Segunda línea visible: si el archivo no trae artista (sentinela
    /// "Archivo local"), muestra el alias de la cuenta cuando existe.
    /// </summary>
    /// <remarks>Dinámico: lee el alias guardado en cada acceso.</remarks>
    public string ArtistaVisible => ResolverArtistaVisible(Line2);

    /// <summary>
    /// Resuelve la segunda línea visible para una línea cruda y un alias.
    /// </summary>
    /// <param name="line2">Línea cruda de la entrada.</param>
    /// <param name="alias">Alias de la cuenta (puede ir vacío).</param>
    /// <returns>Artista real, alias o "Archivo local" como último fallback.</returns>
    public static string ResolverArtistaVisible(string line2, string? alias) =>
        line2 != AudioFileInspector.ArtistaDesconocido
            ? line2
            : string.IsNullOrWhiteSpace(alias)
                ? AudioFileInspector.ArtistaDesconocido
                : alias.Trim();

    /// <summary>Resuelve con el alias guardado actualmente.</summary>
    /// <param name="line2">Línea cruda de la entrada.</param>
    /// <returns>Artista real, alias vigente o "Archivo local".</returns>
    public static string ResolverArtistaVisible(string line2)
    {
        try
        {
            return ResolverArtistaVisible(line2, TemaConsola.LeerPerfil().Alias);
        }
        catch
        {
            return line2;
        }
    }

    /// <summary>Refresca el artista visible (p. ej. al cambiar el alias en Mi cuenta).</summary>
    public void RefrescarArtista() => OnPropertyChanged(nameof(ArtistaVisible));

    /// <summary>Duración (para acumulado y contadores).</summary>
    public TimeSpan Duration { get; }

    /// <summary>Categoría (define el dot de color).</summary>
    public TrackCategory Category { get; }

    /// <summary>
    /// Ruta del audio real (arrastrado a la cola). Null en entradas mock.
    /// En Fase 2 el motor reproduce esta ruta al llegar su turno.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>True si es la entrada en reproducción.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Fin efectivo (sin silencio final) o null (total).</summary>
    [ObservableProperty]
    public partial TimeSpan? FinEfectivo { get; set; }

    /// <summary>Crea una entrada de cola.</summary>
    /// <param name="title">Título.</param>
    /// <param name="line2">Segunda línea.</param>
    /// <param name="duration">Duración.</param>
    /// <param name="category">Categoría.</param>
    /// <param name="filePath">Ruta del audio real o null (mock).</param>
    public QueueEntry(
        string title, string line2, TimeSpan duration, TrackCategory category, string? filePath = null)
    {
        Title = title;
        Line2 = line2;
        Duration = duration;
        Category = category;
        FilePath = filePath;
    }

    /// <summary>Refresca el display al fijar el recorte.</summary>
    /// <param name="value">Nuevo fin efectivo.</param>
    partial void OnFinEfectivoChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(DuracionEfectiva));
        OnPropertyChanged(nameof(DisplayDuration));
    }

    /// <summary>Duración que sonará (recortada).</summary>
    public TimeSpan DuracionEfectiva => FinEfectivo ?? Duration;

    /// <summary>Duración efectiva formateada "m:ss".</summary>
    public string DisplayDuration => TimeFormatter.ToMinuteSecond(DuracionEfectiva);

    /// <summary>Override de color (token base al azar) o null.</summary>
    public string? ColorKey { get; set; }

    /// <summary>Clave del brush (override o color de su categoría).</summary>
    public string BrushKey => ColorKey ?? CategoryPalette.FromCategory(Category).BrushKey;
}
