using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Selector de categoría de un banco del modo operador. Reusa la lista
/// compartida del riel pero con selección propia e independiente por banco.
/// Recuerda la última selección durante la sesión y se reconstruye si la
/// lista de categorías cambia (crear, eliminar, gestionar).
/// </summary>
public sealed partial class SelectorCategoriaViewModel : ObservableObject
{
    /// <summary>Opciones de la tira (una por categoría cargada).</summary>
    public ObservableCollection<OpcionCategoria> Opciones { get; } = new();

    /// <summary>Categoría desplegada por este banco.</summary>
    [ObservableProperty]
    public partial SelectableCategory? Selected { get; set; }

    /// <summary>Se eleva al desplegar una categoría de este banco.</summary>
    public event Action<SelectableCategory>? SeleccionCambiada;

    private CategoriasRailViewModel? _fuente;

    /// <summary>Conecta la lista compartida del riel (llamar una vez).</summary>
    /// <param name="fuente">Riel dueño de la lista de categorías.</param>
    public void Conectar(CategoriasRailViewModel fuente)
    {
        if (_fuente is not null)
        {
            _fuente.Categories.CollectionChanged -= AlCambiarLista;
            foreach (var opcion in Opciones)
            {
                opcion.Categoria.PropertyChanged -= AlCambiarFicha;
            }
        }

        _fuente = fuente;
        Reconstruir();
        _fuente.Categories.CollectionChanged += AlCambiarLista;
    }

    /// <summary>Despliega una categoría en este banco.</summary>
    /// <param name="categoria">Categoría a desplegar.</param>
    public void Seleccionar(SelectableCategory categoria)
    {
        if (ReferenceEquals(Selected, categoria))
        {
            return;
        }

        Selected = categoria;
        foreach (var opcion in Opciones)
        {
            opcion.IsSelected = ReferenceEquals(opcion.Categoria, categoria);
        }

        SeleccionCambiada?.Invoke(categoria);
    }

    /// <summary>Despliega la primera opción si no hay selección.</summary>
    public void AsegurarSeleccion()
    {
        if (Selected is null && Opciones.Count > 0)
        {
            Seleccionar(Opciones[0].Categoria);
        }
    }

    /// <summary>
    /// Reconstruye las opciones preservando la selección si sigue existiendo.
    /// </summary>
    private void Reconstruir()
    {
        var previa = Selected;
        Opciones.Clear();
        if (_fuente is null)
        {
            return;
        }

        foreach (var categoria in _fuente.Categories)
        {
            categoria.PropertyChanged += AlCambiarFicha;
            Opciones.Add(new OpcionCategoria(categoria)
            {
                IsSelected = ReferenceEquals(categoria, previa),
            });
        }

        if (previa is not null && !_fuente.Categories.Contains(previa))
        {
            Selected = null;
        }
    }

    /// <summary>Reconstruye al cambiar la lista del riel.</summary>
    /// <param name="sender">Colección.</param>
    /// <param name="e">Cambio.</param>
    private void AlCambiarLista(object? sender, NotifyCollectionChangedEventArgs e) => Reconstruir();

    /// <summary>Refresca la opción si cambia la ficha compartida.</summary>
    /// <param name="sender">Categoría cambiada.</param>
    /// <param name="e">Propiedad.</param>
    private void AlCambiarFicha(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is null)
        {
            return;
        }

        foreach (var opcion in Opciones)
        {
            if (ReferenceEquals(opcion.Categoria, sender))
            {
                opcion.Refrescar();
                return;
            }
        }
    }
}
