using System.Collections.ObjectModel;
using BebeRadio.Models;
using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Gestor de categorías: elige las 7 cargadas, ordena, edita, elimina
/// y crea sin salir del flujo. Borrador hasta Guardar; las acciones de
/// Editar/Eliminar/Nueva las encadena el llamador (sin diálogos anidados).
/// </summary>
public sealed partial class GestionCategoriasDialog : ContentDialog
{
    /// <summary>Máximo de cargadas en el riel.</summary>
    public const int MaxCargadas = 7;

    /// <summary>Filas en edición (borrador).</summary>
    public ObservableCollection<FilaGestionCategoria> Filas { get; } = new();

    /// <summary>True si pidió crear (cierra sin guardar).</summary>
    public bool PidioNueva { get; private set; }

    /// <summary>Id a editar o null.</summary>
    public string? PidioEditar { get; private set; }

    /// <summary>Id a eliminar o null.</summary>
    public string? PidioEliminar { get; private set; }

    private CategoriasRailViewModel? _origen;

    /// <summary>Inicializa el gestor.</summary>
    public GestionCategoriasDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
    }

    /// <summary>Carga el borrador desde el riel.</summary>
    /// <param name="origen">ViewModel del riel.</param>
    public void Cargar(CategoriasRailViewModel origen)
    {
        _origen = origen;
        Filas.Clear();
        foreach (var categoria in origen.Todas())
        {
            var registro = origen.ObtenerRegistro(categoria.PropietariaId);
            Filas.Add(new FilaGestionCategoria(
                categoria.PropietariaId,
                categoria.Swatch.Label,
                categoria.Swatch.BrushKey,
                categoria.Swatch.Symbol,
                categoria.EsPersonalizada,
                registro?.Cargada ?? true,
                registro?.Orden ?? 999));
        }

        OrdenarFilas();
    }

    /// <summary>Construye cargadas + orden para el ViewModel.</summary>
    /// <returns>Id → (cargada, orden).</returns>
    public Dictionary<string, (bool Cargada, int Orden)> Resultado()
    {
        var resultado = new Dictionary<string, (bool, int)>();
        var orden = 0;
        foreach (var fila in Filas.OrderBy(fila => fila.Orden))
        {
            resultado[fila.Id] = (fila.Cargada, orden);
            orden++;
        }

        return resultado;
    }

    /// <summary>Pide crear (la atiende el llamador).</summary>
    /// <param name="sender">Botón + Nueva.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnNuevaClick(object sender, RoutedEventArgs e)
    {
        PidioNueva = true;
        Hide();
    }

    /// <summary>Pide editar una fila (la atiende el llamador).</summary>
    /// <param name="sender">Botón Editar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnEditarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is FilaGestionCategoria fila)
        {
            PidioEditar = fila.Id;
            Hide();
        }
    }

    /// <summary>Pide eliminar una custom (la atiende el llamador con confirmación).</summary>
    /// <param name="sender">Botón Eliminar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnEliminarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not FilaGestionCategoria fila)
        {
            return;
        }

        if (!fila.EsPersonalizada)
        {
            MostrarAviso("Solo las personalizadas se eliminan.");
            return;
        }

        PidioEliminar = fila.Id;
        Hide();
    }

    /// <summary>Sube una fila en el orden.</summary>
    /// <param name="sender">Botón ▲.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnSubirClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is FilaGestionCategoria fila)
        {
            Mover(fila, -1);
        }
    }

    /// <summary>Baja una fila en el orden.</summary>
    /// <param name="sender">Botón ▼.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnBajarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is FilaGestionCategoria fila)
        {
            Mover(fila, 1);
        }
    }

    /// <summary>Hace cumplir el máximo de 7 cargadas.</summary>
    /// <param name="sender">CheckBox de la fila.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnCargadaChanged(object sender, RoutedEventArgs e)
    {
        var cargadas = Filas.Count(fila => fila.Cargada);
        if (cargadas <= MaxCargadas)
        {
            AvisoText.Visibility = Visibility.Collapsed;
            return;
        }

        if ((sender as CheckBox)?.DataContext is FilaGestionCategoria fila)
        {
            fila.Cargada = false;
        }

        MostrarAviso("Solo 7 pueden estar cargadas.");
    }

    /// <summary>Mueve una fila y reordena la vista.</summary>
    /// <param name="fila">Fila a mover.</param>
    /// <param name="paso">-1 arriba, +1 abajo.</param>
    private void Mover(FilaGestionCategoria fila, int paso)
    {
        var ordenadas = Filas.OrderBy(f => f.Orden).ToList();
        var indice = ordenadas.IndexOf(fila);
        var destino = indice + paso;
        if (indice < 0 || destino < 0 || destino >= ordenadas.Count)
        {
            return;
        }

        var otra = ordenadas[destino];
        (fila.Orden, otra.Orden) = (otra.Orden, fila.Orden);
        OrdenarFilas();
    }

    /// <summary>Reordena la vista por Orden.</summary>
    private void OrdenarFilas()
    {
        var ordenadas = Filas.OrderBy(fila => fila.Orden).ToList();
        Filas.Clear();
        foreach (var fila in ordenadas)
        {
            Filas.Add(fila);
        }
    }

    /// <summary>Muestra un aviso no bloqueante.</summary>
    /// <param name="mensaje">Texto del aviso.</param>
    private void MostrarAviso(string mensaje)
    {
        AvisoText.Text = mensaje;
        AvisoText.Visibility = Visibility.Visible;
    }
}
