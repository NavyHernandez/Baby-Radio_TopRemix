using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.Models;

/// <summary>
/// Slot de efecto de una paleta (mock o audio real del usuario).
/// Observable para que el editor y el drop actualicen la vista al instante.
/// En Fase 2 el motor reproduce <see cref="FilePath"/> con
/// <see cref="CueInicio"/>, <see cref="CueFin"/>, <see cref="GananciaDb"/>
/// y fundidos.
/// </summary>
public sealed partial class PaletteItem : ObservableObject
{
    /// <summary>Categoría propietaria (fijas; en customs es solo referencia).</summary>
    [ObservableProperty]
    public partial TrackCategory Category { get; set; }

    /// <summary>Título del efecto.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Duración total del audio.</summary>
    [ObservableProperty]
    public partial TimeSpan Duration { get; set; }

    /// <summary>Ruta del audio real o null (mock sin audio).</summary>
    [ObservableProperty]
    public partial string? FilePath { get; set; }

    /// <summary>Desde dónde inicia el efecto.</summary>
    [ObservableProperty]
    public partial TimeSpan CueInicio { get; set; }

    /// <summary>Dónde termina el efecto o null (hasta el final).</summary>
    [ObservableProperty]
    public partial TimeSpan? CueFin { get; set; }

    /// <summary>Ganancia en dB (−20…+6).</summary>
    [ObservableProperty]
    public partial double GananciaDb { get; set; }

    /// <summary>Fundido de entrada.</summary>
    [ObservableProperty]
    public partial TimeSpan FundidoEntrada { get; set; }

    /// <summary>Fundido de salida.</summary>
    [ObservableProperty]
    public partial TimeSpan FundidoSalida { get; set; }

    /// <summary>Override de color (#hex o token) o null (color de su categoría).</summary>
    [ObservableProperty]
    public partial string? ColorKey { get; set; }

    /// <summary>Color actual de la dueña (#hex o token, lo fija Desplegar).</summary>
    [ObservableProperty]
    public partial string ColorCategoria { get; set; } = string.Empty;

    /// <summary>Dueña del slot (nombre de enum o custom:id).</summary>
    [ObservableProperty]
    public partial string PropietariaId { get; set; } = string.Empty;

    /// <summary>Posición 0-49 dentro de su paleta.</summary>
    [ObservableProperty]
    public partial int SlotIndex { get; set; }

    /// <summary>True mientras el motor reproduce este slot (la vista titila).</summary>
    [ObservableProperty]
    public partial bool EstaSonando { get; set; }

    /// <summary>Crea un slot de efecto (sin audio hasta arrastrar).</summary>
    /// <param name="category">Categoría propietaria.</param>
    /// <param name="title">Título.</param>
    /// <param name="duration">Duración.</param>
    public PaletteItem(TrackCategory category, string title, TimeSpan duration)
    {
        Category = category;
        Title = title;
        Duration = duration;
        PropietariaId = category.ToString();
    }

    /// <summary>Crea un slot vacío de categoría personalizada.</summary>
    /// <param name="propietariaId">Id custom:id de la dueña.</param>
    /// <param name="indice">Posición 0-49.</param>
    /// <param name="colorKey">Color de la dueña (#hex).</param>
    /// <returns>Slot listo para recibir un audio por arrastre.</returns>
    public static PaletteItem SlotVacio(string propietariaId, int indice, string? colorKey) =>
        new(TrackCategory.Filler, $"Efecto {indice + 1:00}", TimeSpan.Zero)
        {
            PropietariaId = propietariaId,
            SlotIndex = indice,
            ColorKey = colorKey,
        };

    /// <summary>True si el slot tiene un audio cargado.</summary>
    public bool TieneAudio => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>
    /// Deja el slot vacío (fantasma) reutilizando la misma instancia.
    /// Permite un pool estable de slots que no recrea contenedores de UI.
    /// </summary>
    /// <param name="propietariaId">Dueña actual.</param>
    /// <param name="indice">Posición 0-49.</param>
    /// <param name="colorKey">Color de la dueña (token o #hex) o null.</param>
    public void Vaciar(string propietariaId, int indice, string? colorKey)
    {
        PropietariaId = propietariaId;
        SlotIndex = indice;
        Category = TrackCategory.Filler;
        Title = $"Efecto {indice + 1:00}";
        Duration = TimeSpan.Zero;
        FilePath = null;
        CueInicio = TimeSpan.Zero;
        CueFin = null;
        GananciaDb = 0;
        FundidoEntrada = TimeSpan.Zero;
        FundidoSalida = TimeSpan.Zero;
        EstaSonando = false;
        ColorCategoria = string.Empty;
        ColorKey = colorKey;
        RefrescarDerivados();
    }

    /// <summary>Duración efectiva (fin − inicio, nunca negativa).</summary>
    public TimeSpan DuracionEfectiva
    {
        get
        {
            var fin = CueFin ?? Duration;
            var efectiva = fin - CueInicio;
            return efectiva < TimeSpan.Zero ? TimeSpan.Zero : efectiva;
        }
    }

    /// <summary>Línea de detalle: duración si tiene audio; guía si vacío.</summary>
    public string DisplayMeta =>
        TieneAudio
            ? TimeFormatter.ToMinuteSecond(DuracionEfectiva)
            : $"Vacío — arrastra un audio · {TimeFormatter.ToMinuteSecond(Duration)}";

    /// <summary>Clave del brush (override, dueña o token estático).</summary>
    public string BrushKey => ColorKey
        ?? (string.IsNullOrEmpty(ColorCategoria)
            ? CategoryPalette.FromCategory(Category).BrushKey
            : ColorCategoria);

    /// <summary>Color para el borde fantasma: transparente si vacío (solo bordes al tono del tema).</summary>
    public string ColorFantasma => TieneAudio ? BrushKey : "#00000000";

    /// <summary>Tinta del cart: oscura en aluminio/claros, blanca en macizos oscuros.</summary>
    public string TintaCart =>
        !TieneAudio || Controls.ColorALetraConverter.EsColorClaro(BrushKey) ? "#1A1C1F" : "#F2F3F5";

    /// <summary>Refresca la línea de detalle al editar título.</summary>
    /// <param name="value">Nuevo título.</param>
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayMeta));

    /// <summary>Refresca tiempos al cambiar duración o cues.</summary>
    /// <param name="value">Nuevo valor.</param>
    partial void OnDurationChanged(TimeSpan value) => RefrescarDerivados();

    /// <summary>Refresca marca de audio y tiempos.</summary>
    /// <param name="value">Nueva ruta.</param>
    partial void OnFilePathChanged(string? value) => RefrescarDerivados();

    /// <summary>Refresca tiempos al mover el inicio.</summary>
    /// <param name="value">Nuevo inicio.</param>
    partial void OnCueInicioChanged(TimeSpan value) => RefrescarDerivados();

    /// <summary>Refresca tiempos al mover el fin.</summary>
    /// <param name="value">Nuevo fin.</param>
    partial void OnCueFinChanged(TimeSpan? value) => RefrescarDerivados();

    /// <summary>Refresca el color al cambiar el override.</summary>
    /// <param name="value">Nueva clave.</param>
    partial void OnColorKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(BrushKey));
        OnPropertyChanged(nameof(ColorFantasma));
        OnPropertyChanged(nameof(TintaCart));
    }

    /// <summary>Refresca el color al cambiar la dueña.</summary>
    /// <param name="value">Nuevo color.</param>
    partial void OnColorCategoriaChanged(string value)
    {
        OnPropertyChanged(nameof(BrushKey));
        OnPropertyChanged(nameof(ColorFantasma));
        OnPropertyChanged(nameof(TintaCart));
    }

    /// <summary>Notifica todas las propiedades derivadas.</summary>
    private void RefrescarDerivados()
    {
        OnPropertyChanged(nameof(DisplayMeta));
        OnPropertyChanged(nameof(TieneAudio));
        OnPropertyChanged(nameof(DuracionEfectiva));
        OnPropertyChanged(nameof(BrushKey));
        OnPropertyChanged(nameof(ColorFantasma));
        OnPropertyChanged(nameof(TintaCart));
    }

    /// <summary>Clona el slot para editarlo como borrador.</summary>
    /// <returns>Copia independiente.</returns>
    public PaletteItem Clonar() => new(Category, Title, Duration)
    {
        FilePath = FilePath,
        CueInicio = CueInicio,
        CueFin = CueFin,
        GananciaDb = GananciaDb,
        FundidoEntrada = FundidoEntrada,
        FundidoSalida = FundidoSalida,
        ColorKey = ColorKey,
        PropietariaId = PropietariaId,
        SlotIndex = SlotIndex,
    };

    /// <summary>Copia los campos editables de un borrador.</summary>
    /// <param name="borrador">Borrador validado del diálogo.</param>
    public void AplicarBorrador(PaletteItem borrador)
    {
        Title = borrador.Title;
        FilePath = borrador.FilePath;
        Duration = borrador.Duration;
        CueInicio = borrador.CueInicio;
        CueFin = borrador.CueFin;
        GananciaDb = borrador.GananciaDb;
        FundidoEntrada = borrador.FundidoEntrada;
        FundidoSalida = borrador.FundidoSalida;
        ColorKey = borrador.ColorKey;
        OnPropertyChanged(nameof(DisplayMeta));
        OnPropertyChanged(nameof(BrushKey));
        OnPropertyChanged(nameof(TieneAudio));
        OnPropertyChanged(nameof(DuracionEfectiva));
    }

    /// <summary>Convierte el slot a guardado portable.</summary>
    /// <returns>DTO para el JSON.</returns>
    public SlotEfectoGuardado Guardar() => new(
        SlotIndex, Title, Duration.Ticks, FilePath,
        CueInicio.Ticks, CueFin?.Ticks, GananciaDb,
        FundidoEntrada.Ticks, FundidoSalida.Ticks, ColorKey);
}
