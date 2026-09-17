using BebeRadio.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Opción de categoría para el selector de un banco del modo operador.
/// Envuelve la ficha compartida con un estado de selección propio del banco.
/// </summary>
public sealed partial class OpcionCategoria : ObservableObject
{
    /// <summary>Categoría compartida (ficha, color, icono).</summary>
    public SelectableCategory Categoria { get; }

    /// <summary>True si este banco tiene desplegada esta categoría.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Crea la opción.</summary>
    /// <param name="categoria">Categoría compartida.</param>
    public OpcionCategoria(SelectableCategory categoria)
    {
        Categoria = categoria;
    }

    /// <summary>Refresca los bindings de la ficha (label, icono, color).</summary>
    /// <remarks>Se invoca cuando la categoría compartida cambia su ficha.</remarks>
    public void Refrescar() => OnPropertyChanged(nameof(Categoria));
}
