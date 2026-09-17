using System.Collections.ObjectModel;
using BebeRadio.Models;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Orquestador de la consola Baby Radio (Fase 1).
/// Compone las 4 partes (lista, paleta, categorías, acciones) y las mezcla:
/// selección de categoría → redespliegue de paleta; acciones → paleta
/// (Stop corta efectos, Arriba/Abajo cambian de página); paleta → riel
/// (conteos lleno/fantasma). La cola va por su lado con el reproductor.
/// Mantiene fachada compatible con la vista monolítica anterior
/// para migrar por partes sin romper bindings.
/// </summary>
public sealed partial class ConsolaViewModel : ObservableObject
{
    /// <summary>Parte: cola de reproducción (izquierda).</summary>
    public ListaReproduccionViewModel Lista { get; } = new();

    /// <summary>Parte: paleta desplegada (centro, modo normal).</summary>
    public PaletaViewModel Paleta { get; } = new();

    /// <summary>Parte: paleta del banco A (modo operador, 30 por página).</summary>
    public PaletaViewModel PaletaA { get; } = new() { TamanoPagina = 30 };

    /// <summary>Parte: paleta del banco B (modo operador, 30 por página).</summary>
    public PaletaViewModel PaletaB { get; } = new() { TamanoPagina = 30 };

    /// <summary>Selector de categoría del banco A (modo operador).</summary>
    public SelectorCategoriaViewModel SelectorA { get; } = new();

    /// <summary>Selector de categoría del banco B (modo operador).</summary>
    public SelectorCategoriaViewModel SelectorB { get; } = new();

    /// <summary>Parte: riel de categorías (derecha).</summary>
    public CategoriasRailViewModel Categorias { get; } = new();

    /// <summary>Parte: franja inferior de acciones aluminio.</summary>
    public AccionesConsolaViewModel Acciones { get; } = new();

    /// <summary>Parte: reproductor (dirige la cola).</summary>
    public PlayerViewModel Reproductor { get; } = new();

    /// <summary>Categorías (fachada: misma instancia del riel).</summary>
    public ObservableCollection<SelectableCategory> Categories => Categorias.Categories;

    /// <summary>Primeras 7 (fachada del riel).</summary>
    public IReadOnlyList<SelectableCategory> SideCategories => Categorias.SideCategories;

    /// <summary>Items de la paleta (fachada).</summary>
    public ObservableCollection<PaletteItem> Items => Paleta.Items;

    /// <summary>Cola de reproducción (fachada).</summary>
    public ObservableCollection<QueueEntry> Queue => Lista.Queue;

    /// <summary>Categoría desplegada (fachada del riel).</summary>
    public SelectableCategory? Selected => Categorias.Selected;

    /// <summary>Entrada en reproducción (fachada de la lista).</summary>
    public QueueEntry? CurrentEntry => Lista.CurrentEntry;

    /// <summary>Resumen de cola (fachada de la lista).</summary>
    public string QueueSummary => Lista.QueueSummary;

    /// <summary>Modo Operador (fachada de acciones).</summary>
    public bool IsOperatorMode => Acciones.IsOperatorMode;

    /// <summary>Mezcla armada (fachada de acciones).</summary>
    public bool IsMixArmed => Acciones.IsMixArmed;

    /// <summary>Título del modo operador (OPERADOR o OPERADOR "alias").</summary>
    [ObservableProperty]
    public partial string TituloOperador { get; set; } = "OPERADOR";

    /// <summary>
    /// Crea el orquestador y cablea la mezcla entre partes.
    /// </summary>
    public ConsolaViewModel()
    {
        // Mezcla 1: categoría seleccionada → paleta redesplegada.
        Categorias.SeleccionCambiada += Paleta.Desplegar;
        if (Categorias.Selected is not null)
        {
            Paleta.Desplegar(Categorias.Selected);
        }

        // Mezcla 1b: riel vacío → paletas en guía de creación.
        Categorias.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CategoriasRailViewModel.Selected)
                && Categorias.Selected is null)
            {
                Paleta.LimpiarSeleccion();
                PaletaA.LimpiarSeleccion();
                PaletaB.LimpiarSeleccion();
            }
        };

        // Mezcla 2: franja de acciones → paleta (la cola va por su lado).
        // Stop corta solo efectos; Arriba/Abajo cambian de página (20+20).
        // En operador, el Stop corta los efectos de AMBOS bancos (+ central).
        Acciones.AlPedirDetener = () =>
        {
            Paleta.DetenerEfectos();
            PaletaA.DetenerEfectos();
            PaletaB.DetenerEfectos();
        };
        Acciones.AlPedirSubir = Paleta.PaginaAnterior;
        Acciones.AlPedirBajar = Paleta.PaginaSiguiente;

        // Mezcla 3: la paleta pregunta si Mix está armado (mezcla o exclusivo).
        Paleta.MezclaActiva = () => Acciones.IsMixArmed;

        // Mezcla 4: la paleta avisa cambios (el riel refresca lleno/fantasma).
        Paleta.PaletaCambio += Categorias.RefrescarConteos;

        // Mezcla 5: reproductor dirige la cola (el cue previsualiza sin sonar;
        // el pedido explícito —clic, Next, fin natural— sí suena desde cero).
        Reproductor.ConectarCola(Lista);

        // Mezcla 6: bancos del modo operador (30 por página). A y B usan sus
        // paletas propias; ambas toman la mezcla (Mix) y refrescan el riel al
        // cambiar; las mezclas 1–4 del modo normal siguen intactas.
        SelectorA.Conectar(Categorias);
        SelectorB.Conectar(Categorias);
        SelectorA.SeleccionCambiada += PaletaA.Desplegar;
        SelectorB.SeleccionCambiada += PaletaB.Desplegar;
        PaletaA.MezclaActiva = () => Acciones.IsMixArmed;
        PaletaA.PaletaCambio += Categorias.RefrescarConteos;
        PaletaB.MezclaActiva = () => Acciones.IsMixArmed;
        PaletaB.PaletaCambio += Categorias.RefrescarConteos;

        // Propaga cambios de fachada a la vista (shell aún bindea al orquestador).
        Lista.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        Paleta.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        Categorias.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        Acciones.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
    }

    /// <summary>Pone una entrada de la cola en reproducción (mezcla lista).</summary>
    /// <param name="entry">Entrada pulsada.</param>
    [RelayCommand]
    private void Play(QueueEntry entry) => Lista.PlayCommand.Execute(entry);

    /// <summary>Alterna el Modo Operador (delega a acciones).</summary>
    [RelayCommand]
    private void ToggleOperatorMode() => Acciones.ToggleOperatorModeCommand.Execute(null);

    /// <summary>Alterna la mezcla armada (delega a acciones).</summary>
    [RelayCommand]
    private void ToggleMix() => Acciones.ToggleMixCommand.Execute(null);

    /// <summary>Carga archivos arrastrados al final de la cola.</summary>
    /// <param name="paths">Rutas soltadas sobre la cola.</param>
    public void AddFiles(IEnumerable<string> paths) => Lista.AddFiles(paths);

    /// <summary>
    /// Prepara los bancos del modo operador (selectores + despliegue inicial).
    /// Conserva la selección de cada banco durante la sesión.
    /// </summary>
    /// <remarks>Llamar al primer ingreso al modo operador.</remarks>
    public void PrepararOperador()
    {
        RefrescarTituloOperador();
        var actual = Categorias.Selected ?? (Categorias.Categories.Count > 0 ? Categorias.Categories[0] : null);
        if (actual is null)
        {
            return;
        }

        if (SelectorA.Selected is null)
        {
            SelectorA.Seleccionar(actual);
        }

        if (SelectorB.Selected is null)
        {
            SelectorB.Seleccionar(actual);
        }
    }

    /// <summary>
    /// Recalcula el título del modo operador desde el alias guardado
    /// (OPERADOR o OPERADOR + nombre, sin comillas). Se llama en cada ingreso
    /// al modo. No toca la titlebar principal (sigue "Baby Radio — alias").
    /// </summary>
    public void RefrescarTituloOperador()
    {
        var alias = TemaConsola.LeerPerfil().Alias;
        TituloOperador = string.IsNullOrWhiteSpace(alias)
            ? "OPERADOR"
            : $"OPERADOR {alias.Trim()}";
    }

    /// <summary>
    /// Sale del modo operador y sincroniza el riel con el banco A.
    /// </summary>
    public void SalirOperador()
    {
        if (SelectorA.Selected is not null && !ReferenceEquals(Categorias.Selected, SelectorA.Selected))
        {
            Categorias.Select(SelectorA.Selected);
        }

        Acciones.IsOperatorMode = false;
    }

    /// <summary>Recarga todo tras importar (categorías + paleta actual).</summary>
    /// <remarks>La selección se conserva si la dueña sigue existiendo.</remarks>
    public void RecargarTodo()
    {
        Categorias.Recargar();
        if (Categorias.Selected is not null)
        {
            Paleta.Desplegar(Categorias.Selected);
        }

        Categorias.RefrescarConteos();
    }
}
