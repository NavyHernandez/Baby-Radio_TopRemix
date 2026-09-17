using System.Collections.ObjectModel;
using BebeRadio.Models;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels;

/// <summary>
/// Estado de la consola Fase 1: categorías seleccionables, paleta desplegada
/// (20 items fijos) y cola de reproducción. Todo mock y determinista.
/// La cola y el PlayerBar se cablearán al motor de audio en Fase 2.
/// </summary>
public sealed partial class ShowcaseViewModel : ObservableObject
{
    /// <summary>Categorías fijas visibles en la tira (temporal hasta modo dinámico).</summary>
    public const int MaxSideCategories = 7;

    /// <summary>Categorías (misma instancia en tira superior y lateral).</summary>
    public ObservableCollection<SelectableCategory> Categories { get; } = new();

    /// <summary>Primeras 7 para la tira derecha (mismas instancias, sincronizadas).</summary>
    public IReadOnlyList<SelectableCategory> SideCategories { get; private set; } =
        Array.Empty<SelectableCategory>();

    /// <summary>Items de la paleta desplegada (24).</summary>
    public ObservableCollection<PaletteItem> Items { get; } = new();

    /// <summary>Cola de reproducción (izquierda, estilo Jazler).</summary>
    public ObservableCollection<QueueEntry> Queue { get; } = new();

    /// <summary>Categoría desplegada actualmente.</summary>
    [ObservableProperty]
    public partial SelectableCategory? Selected { get; set; }

    /// <summary>Entrada en reproducción (badge EN VIVO).</summary>
    [ObservableProperty]
    public partial QueueEntry? CurrentEntry { get; set; }

    /// <summary>Resumen "N pistas · total m:ss".</summary>
    [ObservableProperty]
    public partial string QueueSummary { get; set; } = string.Empty;

    /// <summary>Modo Operador armado (glow persistente, Fase 2 lo usará).</summary>
    [ObservableProperty]
    public partial bool IsOperatorMode { get; set; }

    /// <summary>Mezcla armada (glow persistente, Fase 2 la usará).</summary>
    [ObservableProperty]
    public partial bool IsMixArmed { get; set; }

    /// <summary>Inicializa categorías, paleta de Música y cola mock.</summary>
    public ShowcaseViewModel()
    {
        foreach (var swatch in CategoryPalette.All)
        {
            Categories.Add(new SelectableCategory(swatch, Select));
        }

        SideCategories = Categories.Take(MaxSideCategories).ToList();

        foreach (var entry in MockQueueBuilder.BuildInitial())
        {
            Queue.Add(entry);
        }

        if (Categories.Count > 0)
        {
            Select(Categories[0]);
        }

        if (Queue.Count > 0)
        {
            CurrentEntry = Queue[0];
        }

        RefreshSummary();
    }

    /// <summary>Despliega la paleta de una categoría (tira sup. y lateral).</summary>
    /// <param name="category">Categoría pulsada.</param>
    private void Select(SelectableCategory category)
    {
        Selected = category;
        foreach (var candidate in Categories)
        {
            candidate.IsSelected = ReferenceEquals(candidate, category);
        }

        Items.Clear();
        for (var i = 0; i < 20; i++)
        {
            Items.Add(PaletteItem.SlotVacio(category.PropietariaId, i, category.Swatch.BrushKey));
        }
    }

    /// <summary>Pone una entrada de la cola en reproducción.</summary>
    /// <param name="entry">Entrada pulsada.</param>
    [RelayCommand]
    private void Play(QueueEntry entry) => CurrentEntry = entry;

    /// <summary>Alterna el Modo Operador (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleOperatorMode() => IsOperatorMode = !IsOperatorMode;

    /// <summary>Alterna la mezcla armada (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleMix() => IsMixArmed = !IsMixArmed;

    /// <summary>
    /// Carga archivos de audio reales (arrastrados) al final de la cola.
    /// La paleta NO encola: solo dispara (Fase 2 le dará sonido).
    /// </summary>
    /// <param name="paths">Rutas soltadas sobre la cola.</param>
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var entry = AudioFileInspector.TryInspect(path);
            if (entry is not null)
            {
                Queue.Add(entry);
            }
        }

        RefreshSummary();
    }

    /// <summary>Al cambiar la entrada actual, mueve el badge EN VIVO.</summary>
    /// <param name="value">Nueva entrada actual.</param>
    partial void OnCurrentEntryChanged(QueueEntry? value)
    {
        foreach (var entry in Queue)
        {
            entry.IsCurrent = ReferenceEquals(entry, value);
        }
    }

    /// <summary>Recalcula "N pistas · total m:ss".</summary>
    private void RefreshSummary()
    {
        var total = TimeSpan.Zero;
        foreach (var entry in Queue)
        {
            total += entry.Duration;
        }

        QueueSummary = $"{Queue.Count} pistas · total {TimeFormatter.ToMinuteSecond(total)}";
    }
}
