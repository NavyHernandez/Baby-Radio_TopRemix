using BebeRadio.Models;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels;

/// <summary>
/// Categoría seleccionable: ficha + estado + comando de selección.
/// La misma instancia se muestra en la tira superior y la lateral derecha,
/// por lo que ambas quedan sincronizadas sin lógica extra.
/// </summary>
public sealed partial class SelectableCategory : ObservableObject
{
    private readonly Action<SelectableCategory> _onSelect;

    /// <summary>Ficha visual (label, token o #hex, icono, ejemplo).</summary>
    public CategorySwatch Swatch { get; private set; }

    /// <summary>Dueña de paleta (nombre de enum o custom:id).</summary>
    public string PropietariaId { get; }

    /// <summary>True si es personalizada (se puede eliminar).</summary>
    public bool EsPersonalizada => PropietariaId.StartsWith("custom:", StringComparison.Ordinal);

    /// <summary>True si es la categoría desplegada actualmente.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>True si su paleta tiene al menos un audio (riel lleno; si no, fantasma).</summary>
    [ObservableProperty]
    public partial bool TieneAudio { get; set; }

    /// <summary>Color del riel: transparente si vacío (esqueleto al tono del tema).</summary>
    public string ColorFantasma => TieneAudio ? Swatch.BrushKey : "#00000000";

    /// <summary>Icono ya parseado (el bindeo string→enum es frágil: casa por defecto).</summary>
    public FluentIcons.Common.Symbol Icono { get; private set; } = FluentIcons.Common.Symbol.MusicNote2;

    /// <summary>Crea el wrapper seleccionable.</summary>
    /// <param name="swatch">Ficha del registro central o personalizada.</param>
    /// <param name="onSelect">Callback al pulsar (despliega su paleta).</param>
    /// <param name="propietariaId">Dueña (null = nombre del enum).</param>
    public SelectableCategory(CategorySwatch swatch, Action<SelectableCategory> onSelect, string? propietariaId = null)
    {
        Swatch = swatch;
        _onSelect = onSelect;
        PropietariaId = propietariaId ?? swatch.Category.ToString();
        Icono = ParsearIcono(swatch.Symbol);
    }

    /// <summary>Aplica una ficha editada sin perder selección.</summary>
    /// <param name="nueva">Ficha nueva (mismo enum o placeholder).</param>
    public void RefrescarFicha(CategorySwatch nueva)
    {
        Swatch = nueva;
        Icono = ParsearIcono(nueva.Symbol);
        OnPropertyChanged(nameof(Swatch));
        OnPropertyChanged(nameof(ColorFantasma));
    }

    /// <summary>Parsea el símbolo (fallback musical, nunca la casa por defecto).</summary>
    /// <param name="simbolo">Nombre del símbolo.</param>
    /// <returns>Icono válido.</returns>
    private static FluentIcons.Common.Symbol ParsearIcono(string simbolo) =>
        Enum.TryParse<FluentIcons.Common.Symbol>(simbolo, out var icono)
            ? icono
            : FluentIcons.Common.Symbol.MusicNote2;

    /// <summary>Refresca el esqueleto al cambiar el conteo de audios.</summary>
    /// <param name="value">Nuevo valor.</param>
    partial void OnTieneAudioChanged(bool value) => OnPropertyChanged(nameof(ColorFantasma));

    /// <summary>Despliega la paleta de esta categoría.</summary>
    [RelayCommand]
    private void Select() => _onSelect(this);
}
