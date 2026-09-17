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
