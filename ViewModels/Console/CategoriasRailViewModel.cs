using System.Collections.ObjectModel;
using BebeRadio.Models;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Riel de categorías: siempre 7 cargadas (fijas + personalizadas).
/// El gestor elige cuáles 7 y su orden; el riel solo muestra.
/// Notifica <see cref="SeleccionCambiada"/> para redesplegar la paleta y
/// <see cref="CategoriaEliminada"/> para que el orquestador reubique.
/// Todo persiste en el JSON portable.
/// </summary>
public sealed partial class CategoriasRailViewModel : ObservableObject
{
    /// <summary>Categorías cargadas en el riel (siempre 7 como máximo).</summary>
    public const int MaxSideCategories = 7;

    /// <summary>Las 7 cargadas (mismas instancias que la paleta).</summary>
    public ObservableCollection<SelectableCategory> Categories { get; } = new();

    /// <summary>Reflejo del riel para la vista (siempre ≤ 7).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SelectableCategory> SideCategories { get; set; } =
        Array.Empty<SelectableCategory>();

    /// <summary>Categoría desplegada actualmente.</summary>
    [ObservableProperty]
    public partial SelectableCategory? Selected { get; set; }

    /// <summary>True si hay una actualización disponible en GitHub (aviso "Acerca de").</summary>
    [ObservableProperty]
    public partial bool HayActualizacion { get; set; }

    /// <summary>Versión disponible en GitHub (vacía si no hay aviso).</summary>
    [ObservableProperty]
    public partial string VersionActualizacion { get; set; } = string.Empty;

    /// <summary>Se eleva al desplegar una categoría (la paleta se suscribe).</summary>
    public event Action<SelectableCategory>? SeleccionCambiada;

    /// <summary>Se eleva al eliminar una categoría (dueña a olvidar).</summary>
    public event Action<string>? CategoriaEliminada;

    /// <summary>Inicializa el riel (registro + JSON portable).</summary>
    public CategoriasRailViewModel()
    {
        Recargar();
        if (Categories.Count > 0)
        {
            Select(Categories[0]);
        }
    }

    /// <summary>
    /// Consulta GitHub en segundo plano y enciende el aviso si hay versión nueva.
    /// </summary>
    /// <remarks>
    /// Blindado: sin red o copia no instalada no muestra aviso ni lanza
    /// (<see cref="ActualizadorBaby.ComprobarAsync"/> devuelve ConsultaOk=false).
    /// </remarks>
    public async Task ComprobarActualizacionAsync()
    {
        try
        {
            var resultado = await ActualizadorBaby.Instancia.ComprobarAsync();
            if (resultado.ConsultaOk && resultado.HayActualizacion)
            {
                VersionActualizacion = resultado.VersionDisponible;
                HayActualizacion = true;
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.AvisoUpdate");
        }
    }

    /// <summary>Despliega una categoría y avisa a la paleta.</summary>
    /// <param name="category">Categoría pulsada.</param>
    public void Select(SelectableCategory category)
    {
        Selected = category;
        foreach (var candidate in Categories)
        {
            candidate.IsSelected = ReferenceEquals(candidate, category);
        }

        SeleccionCambiada?.Invoke(category);
    }

    /// <summary>Crea una personalizada (entra descargada si el riel está lleno).</summary>
    /// <param name="registro">Datos validados del diálogo.</param>
    /// <returns>La categoría creada.</returns>
    public SelectableCategory Crear(CategoriaPersonalizada registro)
    {
        var config = ConsolaStore.Cargar();
        var normalizado = Normalizar(registro with
        {
            Id = $"custom:{Guid.NewGuid():N}",
            Cargada = Categories.Count < MaxSideCategories,
            Orden = config.CategoriasPersonalizadas.Count + CategoryPalette.All.Count,
        });
        config.CategoriasPersonalizadas.Add(normalizado);
        ConsolaStore.Guardar(config);
        Recargar();
        var creada = Todas().First(categoria => categoria.PropietariaId == normalizado.Id);
        var enRiel = Categories.FirstOrDefault(categoria => categoria.PropietariaId == normalizado.Id);
        if (enRiel is not null)
        {
            Select(enRiel);
        }

        return creada;
    }

    /// <summary>Aplica la edición (fija como ajuste, custom como dato).</summary>
    /// <param name="propietariaId">Dueña a actualizar.</param>
    /// <param name="registro">Datos validados del diálogo.</param>
    public void AplicarEdicion(string propietariaId, CategoriaPersonalizada registro)
    {
        var config = ConsolaStore.Cargar();
        var actual = EstadoDe(propietariaId, config);
        var normalizado = Normalizar(registro with
        {
            Id = propietariaId,
            Cargada = actual.Cargada,
            Orden = actual.Orden,
        });
        var existente = Todas().FirstOrDefault(categoria => categoria.PropietariaId == propietariaId);
        if (existente is null)
        {
            return;
        }

        if (existente.EsPersonalizada)
        {
            config.CategoriasPersonalizadas.RemoveAll(categoria => categoria.Id == propietariaId);
            config.CategoriasPersonalizadas.Add(normalizado);
        }
        else
        {
            config.AjustesFijas.RemoveAll(categoria => categoria.Id == propietariaId);
            config.AjustesFijas.Add(normalizado);
        }

        ConsolaStore.Guardar(config);
        Recargar();
    }

    /// <summary>Aplica el gestor (cargadas + orden de todas).</summary>
    /// <param name="estados">Id → (cargada, orden) de cada categoría.</param>
    public void AplicarGestion(IReadOnlyDictionary<string, (bool Cargada, int Orden)> estados)
    {
        var config = ConsolaStore.Cargar();
        foreach (var custom in config.CategoriasPersonalizadas)
        {
            if (estados.TryGetValue(custom.Id, out var estado))
            {
                config.CategoriasPersonalizadas[config.CategoriasPersonalizadas.IndexOf(custom)] =
                    custom with { Cargada = estado.Cargada, Orden = estado.Orden };
            }
        }

        foreach (var (id, estado) in estados)
        {
            if (id.StartsWith("custom:", StringComparison.Ordinal))
            {
                continue;
            }

            var ajuste = config.AjustesFijas.FirstOrDefault(a => a.Id == id);
            if (ajuste is not null)
            {
                config.AjustesFijas[config.AjustesFijas.IndexOf(ajuste)] =
                    ajuste with { Cargada = estado.Cargada, Orden = estado.Orden };
            }
            else if (!estado.Cargada || estado.Orden != OrdenOriginal(id))
            {
                var swatch = CategoryPalette.All.First(s => s.Category.ToString() == id);
                config.AjustesFijas.Add(new CategoriaPersonalizada(
                    id, swatch.Label, HexDe(swatch.BrushKey), swatch.Symbol,
                    estado.Cargada, estado.Orden));
            }
        }

        ConsolaStore.Guardar(config);
        Recargar();
    }

    /// <summary>Elimina una personalizada (previa confirmación en la vista).</summary>
    /// <param name="propietariaId">Id custom:id a eliminar.</param>
    /// <remarks>Las fijas no se eliminan: se restauran con <see cref="RestaurarFija"/>.</remarks>
    public void Eliminar(string propietariaId)
    {
        var existente = Todas().FirstOrDefault(categoria => categoria.PropietariaId == propietariaId);
        if (existente is null || !existente.EsPersonalizada)
        {
            return;
        }

        ConsolaStore.OlvidarPropietaria(propietariaId);
        var eraSeleccion = ReferenceEquals(existente, Selected);
        Recargar();
        CategoriaEliminada?.Invoke(propietariaId);
        if (eraSeleccion && Categories.Count > 0)
        {
            Select(Categories[0]);
        }
    }

    /// <summary>Restaura una fija a su ficha original.</summary>
    /// <param name="propietariaId">Nombre del enum a restaurar.</param>
    public void RestaurarFija(string propietariaId)
    {
        var config = ConsolaStore.Cargar();
        config.AjustesFijas.RemoveAll(categoria => categoria.Id == propietariaId);
        ConsolaStore.Guardar(config);
        Recargar();
    }

    /// <summary>Obtiene los datos actuales para precargar el editor.</summary>
    /// <param name="propietariaId">Dueña a editar.</param>
    /// <returns>Registro editable o null si no existe.</returns>
    public CategoriaPersonalizada? ObtenerRegistro(string propietariaId)
    {
        var existente = Todas().FirstOrDefault(categoria => categoria.PropietariaId == propietariaId);
        if (existente is null)
        {
            return null;
        }

        var estado = EstadoDe(propietariaId, ConsolaStore.Cargar());
        return new CategoriaPersonalizada(
            propietariaId,
            existente.Swatch.Label,
            HexDe(existente.Swatch.BrushKey),
            existente.Swatch.Symbol,
            estado.Cargada,
            estado.Orden);
    }

    /// <summary>Todas las categorías (cargadas + descargadas) para el gestor.</summary>
    /// <returns>Lista ordenada por Orden.</returns>
    public IReadOnlyList<SelectableCategory> Todas() => _todas.ToList();

    /// <summary>Reconstruye el riel (registro + JSON, máx 7 cargadas).</summary>
    public void Recargar()
    {
        var seleccion = Selected?.PropietariaId;
        var config = ConsolaStore.Cargar();
        _todas.Clear();
        foreach (var swatch in CategoryPalette.All)
        {
            var id = swatch.Category.ToString();
            var ajuste = config.AjustesFijas.FirstOrDefault(a => a.Id == id);
            var ficha = ajuste is null ? swatch : FichaDe(id, ajuste, swatch);
            _todas.Add(new SelectableCategory(ficha, Select, id));
        }

        foreach (var custom in config.CategoriasPersonalizadas.OrderBy(c => c.Orden))
        {
            _todas.Add(new SelectableCategory(FichaDe(custom.Id, custom), Select, custom.Id));
        }

        var cargadas = _todas
            .Where(categoria => EstadoDe(categoria.PropietariaId, config).Cargada)
            .OrderBy(categoria => EstadoDe(categoria.PropietariaId, config).Orden)
            .Take(MaxSideCategories)
            .ToList();

        Categories.Clear();
        foreach (var categoria in cargadas)
        {
            Categories.Add(categoria);
        }

        RefrescarRiel();
        RefrescarConteos();
        var retomar = Categories.FirstOrDefault(categoria => categoria.PropietariaId == seleccion)
            ?? Categories.FirstOrDefault();
        if (retomar is not null)
        {
            Select(retomar);
        }
        else
        {
            Selected = null;
        }
    }

    /// <summary>Recalcula qué categorías tienen audio (riel lleno o fantasma).</summary>
    /// <remarks>Una sola lectura del JSON para todo el riel.</remarks>
    public void RefrescarConteos()
    {
        var config = ConsolaStore.Cargar();
        foreach (var categoria in _todas)
        {
            categoria.TieneAudio = config.Paletas.TryGetValue(categoria.PropietariaId, out var slots)
                && slots.Any(slot => !string.IsNullOrWhiteSpace(slot.FilePath));
        }
    }

    private readonly List<SelectableCategory> _todas = new();

    /// <summary>Recalcula las visibles del riel.</summary>
    private void RefrescarRiel() => SideCategories = Categories.ToList();

    /// <summary>Lee el estado guardado (cargada + orden) de una dueña.</summary>
    /// <param name="id">Dueña.</param>
    /// <param name="config">Configuración cargada.</param>
    /// <returns>Registro de estado (defecto: cargada, orden original).</returns>
    private static CategoriaPersonalizada EstadoDe(string id, ConsolaConfiguracion config)
    {
        var custom = config.CategoriasPersonalizadas.FirstOrDefault(c => c.Id == id);
        if (custom is not null)
        {
            return custom;
        }

        var ajuste = config.AjustesFijas.FirstOrDefault(a => a.Id == id);
        if (ajuste is not null)
        {
            return ajuste;
        }

        var swatch = CategoryPalette.All.FirstOrDefault(s => s.Category.ToString() == id);
        return swatch is null
            ? new CategoriaPersonalizada(id, id, "#22D3EE", "MusicNote2")
            : new CategoriaPersonalizada(id, swatch.Label, HexDe(swatch.BrushKey), swatch.Symbol, true, OrdenOriginal(id));
    }

    /// <summary>Orden original de una fija (posición en el registro).</summary>
    /// <param name="id">Nombre del enum.</param>
    /// <returns>Índice 0-9.</returns>
    private static int OrdenOriginal(string id)
    {
        var lista = CategoryPalette.All.ToList();
        for (var i = 0; i < lista.Count; i++)
        {
            if (lista[i].Category.ToString() == id)
            {
                return i;
            }
        }

        return 999;
    }

    /// <summary>Construye la ficha visible (ajuste o registro).</summary>
    /// <param name="id">Dueña.</param>
    /// <param name="registro">Ajuste/custom o null (ficha original).</param>
    /// <param name="original">Ficha original de fija (null en customs).</param>
    /// <returns>Ficha lista para el riel.</returns>
    private static CategorySwatch FichaDe(string id, CategoriaPersonalizada? registro, CategorySwatch? original = null)
    {
        if (registro is not null
            && !Enum.TryParse<FluentIcons.Common.Symbol>(registro.Symbol, out _))
        {
            RegistroErrores.Registrar(
                new FormatException($"Símbolo inválido: {registro.Symbol}"), "Riel.Ficha");
            registro = registro with { Symbol = "MusicNote2" };
        }

        if (registro is null && original is not null)
        {
            return original;
        }

        if (original is not null && registro is not null)
        {
            return new CategorySwatch(
                original.Category, registro.Label, registro.ColorHex,
                registro.Symbol, original.ExampleTitle, original.ExampleMeta);
        }

        return new CategorySwatch(
            TrackCategory.Filler, registro!.Label, registro.ColorHex,
            registro.Symbol, "Efectos de " + registro.Label, "Personalizada");
    }

    /// <summary>Normaliza un registro (label recortado, hex con #).</summary>
    /// <param name="registro">Registro del diálogo.</param>
    /// <returns>Registro normalizado.</returns>
    private static CategoriaPersonalizada Normalizar(CategoriaPersonalizada registro)
    {
        var hex = registro.ColorHex.StartsWith("#") ? registro.ColorHex : $"#{registro.ColorHex}";
        return registro with { Label = registro.Label.Trim(), ColorHex = hex };
    }

    /// <summary>Resuelve el hex actual de una clave (token o #hex).</summary>
    /// <param name="brushKey">Clave del brush.</param>
    /// <returns>Hex para precargar el editor.</returns>
    private static string HexDe(string brushKey)
    {
        if (brushKey.StartsWith("#"))
        {
            return brushKey;
        }

        if (Microsoft.UI.Xaml.Application.Current?.Resources.TryGetValue(brushKey, out var resource) == true
            && resource is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            var color = brush.Color;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return "#22D3EE";
    }
}
