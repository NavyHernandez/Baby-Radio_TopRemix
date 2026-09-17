using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.Models;

/// <summary>
/// Fila editable del gestor de categorías (borrador: no toca el
/// riel hasta Guardar). Lleva estado de carga + orden.
/// </summary>
public sealed partial class FilaGestionCategoria : ObservableObject
{
    /// <summary>Dueña (nombre de enum o custom:id).</summary>
    public string Id { get; }

    /// <summary>Etiqueta visible.</summary>
    public string Label { get; }

    /// <summary>Clave de color (token o #hex).</summary>
    public string BrushKey { get; }

    /// <summary>Símbolo FluentIcons.</summary>
    public string Symbol { get; }

    /// <summary>True si es personalizada (se puede eliminar).</summary>
    public bool EsPersonalizada { get; }

    /// <summary>True si ocupa un slot del riel (máx 7).</summary>
    [ObservableProperty]
    public partial bool Cargada { get; set; }

    /// <summary>Posición en el riel y el gestor.</summary>
    [ObservableProperty]
    public partial int Orden { get; set; }

    /// <summary>Crea la fila.</summary>
    /// <param name="id">Dueña.</param>
    /// <param name="label">Etiqueta.</param>
    /// <param name="brushKey">Clave de color.</param>
    /// <param name="symbol">Símbolo.</param>
    /// <param name="esPersonalizada">True si es custom.</param>
    /// <param name="cargada">Cargada en el riel.</param>
    /// <param name="orden">Posición.</param>
    public FilaGestionCategoria(
        string id, string label, string brushKey, string symbol,
        bool esPersonalizada, bool cargada, int orden)
    {
        Id = id;
        Label = label;
        BrushKey = brushKey;
        Symbol = symbol;
        EsPersonalizada = esPersonalizada;
        Cargada = cargada;
        Orden = orden;
    }
}
