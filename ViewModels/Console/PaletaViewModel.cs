using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Paleta de 50 slots por categoría (25+25, 5×5 por vista).
/// Sin mocks: todo slot arranca vacío y elegante; solo lo guardado en JSON
/// o arrastrado se ve lleno. Escucha a <see cref="CategoriasRailViewModel"/>.
/// El clic dispara por el <see cref="MezcladorEfectos"/> (polifonía con Mix,
/// exclusivo sin Mix) y <see cref="Sonando"/> guía el titileo de la vista.
/// </summary>
public sealed partial class PaletaViewModel : ObservableObject
{
    /// <summary>Slots por categoría (2 páginas de 25).</summary>
    public const int SlotsPorCategoria = 50;

    /// <summary>Carts visibles por página en modo normal (5 columnas × 5 filas).</summary>
    public const int ItemsPorPagina = 25;

    /// <summary>Carts por página de esta instancia (25 normal, 30 en operador).</summary>
    /// <remarks>Fijarlo antes del primer despliegue; la última página puede quedar parcial.</remarks>
    public int TamanoPagina { get; set; } = ItemsPorPagina;

    /// <summary>Carts de la página actual.</summary>
    public ObservableCollection<PaletteItem> Items { get; } = new();

    /// <summary>Slots que suenan ahora (la vista los titila).</summary>
    public ObservableCollection<PaletteItem> Sonando { get; } = new();

    /// <summary>Categoría desplegada actualmente (reflejo del riel).</summary>
    [ObservableProperty]
    public partial SelectableCategory? Selected { get; set; }

    /// <summary>True si no hay categoría (guía de creación en la vista).</summary>
    public bool SinCategoria => Selected is null;

    /// <summary>Página actual (0 o 1).</summary>
    [ObservableProperty]
    public partial int PaginaActual { get; set; }

    /// <summary>Indicador de página para el header (1/2).</summary>
    public string IndicadorPagina => $"{PaginaActual + 1}/{TotalPaginas}";

    /// <summary>Total de páginas (la última puede quedar parcial).</summary>
    public int TotalPaginas => (SlotsPorCategoria + TamanoPagina - 1) / TamanoPagina;

    /// <summary>Gancho: true si Mix está armado (mezcla) o false (exclusivo).</summary>
    public Func<bool>? MezclaActiva { get; set; }

    /// <summary>Atenuación de la cola en dB mientras suena un efecto priorizado.</summary>
    public const double AtenuacionColaDb = -6.0;

    /// <summary>Gancho del orquestador: Ganancia armada (priorizar efectos).</summary>
    public Func<bool>? GananciaActiva { get; set; }

    /// <summary>Gancho del orquestador: la cola está sonando.</summary>
    public Func<bool>? ColaSonando { get; set; }

    /// <summary>Gancho del orquestador: la paleta cambió (refrescar conteos).</summary>
    public event Action? PaletaCambio;

    /// <summary>Se invoca al pulsar hotkey en un slot sin audio (para que la vista muestre aviso).</summary>
    public event Action<PaletteItem>? SlotVacioAlDisparar;

    /// <summary>Dueña actual (enum o custom:id) para guardar cambios.</summary>
    public string PropietariaActual { get; private set; } = string.Empty;

    private readonly List<PaletteItem> _todos = new(SlotsPorCategoria);
    private readonly Dictionary<Guid, (string Duena, int Slot)> _voces = new();
    private readonly HashSet<PaletteItem> _pendientes = new();
    private readonly object _puerta = new();
    private readonly MezcladorEfectos _mezclador = MezcladorEfectos.Instancia;
    private readonly KeyMappingService _keyMapping = KeyMappingService.Instancia;
    private DispatcherQueue? _hiloUi;

    /// <summary>Crea la paleta vacía (el orquestador la despliega).</summary>
    public PaletaViewModel()
    {
        _mezclador.VozTerminada += OnVozTerminada;
        _keyMapping.OnHotkeyPressed += OnHotkeyPressed;
        _keyMapping.OnHotkeyReleased += OnHotkeyReleased;

        // Pool estable: los 50 slots y los visibles se crean una sola vez y se
        // mutan en el lugar. Así cambiar de categoría no recrea contenedores de UI.
        for (var i = 0; i < SlotsPorCategoria; i++)
        {
            _todos.Add(PaletteItem.SlotVacio(string.Empty, i, null));
        }

        for (var i = 0; i < ItemsPorPagina; i++)
        {
            Items.Add(_todos[i]);
        }
    }

    /// <summary>Fija el hilo UI para marshalar fines naturales del motor.</summary>
    /// <param name="cola">Cola del dispatcher de la vista.</param>
    public void UsarHiloUi(DispatcherQueue cola) => _hiloUi = cola;

    /// <summary>Reconstruye la paleta (50 slots mutados en el lugar).</summary>
    /// <param name="category">Categoría desplegada.</param>
    /// <remarks>
    /// Vuelve a la página 1 sin cortar efectos (siguen y retoman titileo).
    /// Reutiliza los mismos objetos para no regenerar contenedores de UI.
    /// </remarks>
    public void Desplegar(SelectableCategory category)
    {
        var cronometro = Stopwatch.StartNew();
        
        // Limpiar hotkeys de la categoría anterior antes de cambiar
        if (!string.IsNullOrEmpty(PropietariaActual))
        {
            _keyMapping.LimpiarPropietaria(PropietariaActual);
        }

        Selected = category;
        PropietariaActual = category.PropietariaId;

        var guardados = ConsolaStore.LeerSlots(PropietariaActual);
        var msJson = cronometro.ElapsedMilliseconds;

        for (var i = 0; i < SlotsPorCategoria; i++)
        {
            var item = _todos[i];
            item.Vaciar(PropietariaActual, i, category.Swatch.BrushKey);
            if (guardados.TryGetValue(i, out var slot))
            {
                AplicarGuardado(item, slot);
            }
        }

        var msItems = cronometro.ElapsedMilliseconds - msJson;
        PaginaActual = 0;
        MostrarPagina();
        SincronizarSonando();

        // Registrar hotkeys de la nueva categoría
        RegistrarHotkeysCategoria();

        RegistroErrores.Traza(
            "Paleta.Desplegar",
            $"json={msJson}ms slots={msItems}ms total={cronometro.ElapsedMilliseconds}ms");
    }

    /// <summary>Registra los hotkeys globales de los slots que tienen mapeo en la categoría actual.</summary>
    private void RegistrarHotkeysCategoria()
    {
        if (string.IsNullOrEmpty(PropietariaActual))
        {
            return;
        }

        foreach (var item in _todos)
        {
            if (item.TieneMapeo)
            {
                _keyMapping.Registrar(PropietariaActual, item.SlotIndex, item.MapeoTecla, item.MapeoModifiers, item.MapeoModo);
            }
        }
    }

    /// <summary>
    /// Registra el hotkey de un slot específico (tras editar mapeo en el diálogo).
    /// Idempotente: reemplaza el registro previo del slot (sin huérfanos) y
    /// libera el registro si el slot quedó sin mapeo.
    /// </summary>
    /// <param name="propietariaId">Dueña de la paleta.</param>
    /// <param name="slotIndex">Índice del slot (0-49).</param>
    public void RegistrarHotkeySlot(string propietariaId, int slotIndex)
    {
        if (string.IsNullOrEmpty(propietariaId) || slotIndex < 0 || slotIndex >= _todos.Count)
        {
            RegistroErrores.Traza("Paleta.RegistrarHotkeySlot", $"SKIP: propietariaId={propietariaId}, slotIndex={slotIndex}");
            return;
        }

        var item = _todos[slotIndex];
        RegistroErrores.Traza("Paleta.RegistrarHotkeySlot", $"slot={slotIndex}, TieneMapeo={item.TieneMapeo}, Tecla={item.MapeoTecla}, Modifiers={item.MapeoModifiers}, Modo={item.MapeoModo}, TieneAudio={item.TieneAudio}");
        if (!item.TieneMapeo)
        {
            _keyMapping.Desregistrar(propietariaId, slotIndex);
            return;
        }

        var ok = _keyMapping.Registrar(propietariaId, slotIndex, item.MapeoTecla, item.MapeoModifiers, item.MapeoModo);
        RegistroErrores.Traza("Paleta.RegistrarHotkeySlot", $"RegisterHotKey result={ok}");
    }

    /// <summary>Callback cuando se pulsa un hotkey global registrado.</summary>
    /// <param name="propietariaId">Dueña de la paleta.</param>
    /// <param name="slotIndex">Índice del slot.</param>
    /// <param name="modo">Modo de interacción.</param>
    private void OnHotkeyPressed(string propietariaId, int slotIndex, ModoMapeo modo)
    {
        RegistroErrores.Traza("Paleta.OnHotkeyPressed", $"RECEIVED: propietaria={propietariaId}, slot={slotIndex}, modo={modo}, PropietariaActual={PropietariaActual}");

        // Solo responder si es la categoría actualmente desplegada
        if (!string.Equals(propietariaId, PropietariaActual, StringComparison.Ordinal))
        {
            RegistroErrores.Traza("Paleta.OnHotkeyPressed", $"SKIP: category mismatch (actual={PropietariaActual})");
            return;
        }

        if (slotIndex < 0 || slotIndex >= _todos.Count)
        {
            RegistroErrores.Traza("Paleta.OnHotkeyPressed", $"SKIP: invalid slotIndex");
            return;
        }

        var item = _todos[slotIndex];
        RegistroErrores.Traza("Paleta.OnHotkeyPressed", $"DISPATCH: item={item.Title}, TieneAudio={item.TieneAudio}, EstaSonando={item.EstaSonando}");
        DispararPorMapeo(item, modo);
    }

    /// <summary>Callback cuando se suelta un hotkey global registrado (KeyUp para modo Momentaneo).</summary>
    /// <param name="propietariaId">Dueña de la paleta.</param>
    /// <param name="slotIndex">Índice del slot.</param>
    private void OnHotkeyReleased(string propietariaId, int slotIndex)
    {
        // Solo responder si es la categoría actualmente desplegada
        if (!string.Equals(propietariaId, PropietariaActual, StringComparison.Ordinal))
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= _todos.Count)
        {
            return;
        }

        var item = _todos[slotIndex];
        // Solo actuar si el slot está en modo Momentaneo y está sonando
        if (item.MapeoModo == ModoMapeo.Momentaneo && item.EstaSonando)
        {
            DetenerVozDe(item);
        }
    }

    /// <summary>
    /// Dispara un slot según su modo de mapeo.
    /// </summary>
    /// <param name="item">Slot a disparar.</param>
    /// <param name="modo">Modo de interacción (Toggle, Retrigger, Momentaneo).</param>
    /// <remarks>
    /// Toggle: si no suena -> dispara; si suena -> detiene.
    /// Retrigger: siempre dispara (nueva voz), no detiene lo anterior.
    /// Momentaneo: push-to-talk idempotente; suena mientras se mantiene la tecla,
    /// se detiene al soltar. Los KeyDown repetidos (auto-repeat) se ignoran.
    /// Si el slot no tiene audio, notifica via <see cref="SlotVacioAlDisparar"/> para que la vista muestre aviso.
    /// </remarks>
    private void DispararPorMapeo(PaletteItem item, ModoMapeo modo)
    {
        if (!item.TieneAudio)
        {
            SlotVacioAlDisparar?.Invoke(item);
            return;
        }

        switch (modo)
        {
            case ModoMapeo.Toggle:
                if (item.EstaSonando)
                {
                    DetenerVozDe(item);
                }
                else
                {
                    Disparar(item);
                }
                break;

            case ModoMapeo.Retrigger:
                // Siempre dispara una NUEVA voz superpuesta (layering), no detiene las anteriores.
                // El mixer permite hasta 6 voces globales; Stop global para todas.
                DispararRetrigger(item);
                break;

            case ModoMapeo.Momentaneo:
                // Momentaneo push-to-talk: idempotente ante el auto-repeat del teclado
                // (el servicio ya suprime repeats, esto cubre reentradas).
                // Si ya suena, no reiniciar: suena continuo hasta el KeyUp.
                // Si terminó solo con la tecla aún pulsada, queda parado hasta re-pulsar.
                if (!item.EstaSonando)
                {
                    Disparar(item);
                }
                break;
        }
    }

    /// <summary>Avanza a la página 2 (si no está).</summary>
    public void PaginaSiguiente()
    {
        if (PaginaActual >= TotalPaginas - 1)
        {
            return;
        }

        CambiarPagina(PaginaActual + 1);
    }

    /// <summary>Retrocede a la página 1 (si no está).</summary>
    public void PaginaAnterior()
    {
        if (PaginaActual <= 0)
        {
            return;
        }

        CambiarPagina(PaginaActual - 1);
    }

    /// <summary>Carga un audio real en un slot (arrastre o editor).</summary>
    /// <param name="item">Slot destino.</param>
    /// <param name="path">Ruta del archivo de audio.</param>
    /// <returns>True si el archivo es un audio legible.</returns>
    public bool SoltarAudio(PaletteItem item, string path)
    {
        var entrada = AudioFileInspector.TryInspect(path);
        if (entrada is null)
        {
            return false;
        }

        item.Title = entrada.Title;
        item.Duration = entrada.Duration;
        item.FilePath = path;
        item.CueInicio = TimeSpan.Zero;
        item.CueFin = null;
        Persistir();
        return true;
    }

    /// <summary>Restaura un slot a vacío (listo para llenar).</summary>
    /// <param name="item">Slot a limpiar.</param>
    /// <remarks>Si el slot sonaba, detiene su voz antes de reemplazarlo.</remarks>
    public void LimpiarSlot(PaletteItem item)
    {
        DetenerVozDe(item);
        if (item.TieneMapeo)
        {
            _keyMapping.Desregistrar(PropietariaActual, item.SlotIndex);
        }
        item.Vaciar(PropietariaActual, item.SlotIndex, Selected?.Swatch.BrushKey);
        Persistir();
    }

    /// <summary>Aplica el borrador del editor a un slot real.</summary>
    /// <param name="destino">Slot a actualizar.</param>
    /// <param name="borrador">Borrador validado del diálogo.</param>
    /// <remarks>Si el slot sonaba, detiene su voz antes de aplicar.</remarks>
    public void AplicarEdicion(PaletteItem destino, PaletteItem borrador)
    {
        DetenerVozDe(destino);
        destino.AplicarBorrador(borrador);
        Persistir();
    }

    /// <summary>Dispara un efecto por el mezclador.</summary>
    /// <param name="item">Slot pulsado.</param>
    /// <returns>True si quedó aceptado (titila; el audio engancha enseguida).</returns>
    /// <remarks>
    /// Re-pulsar reinicia: corta la voz o el arranque pendiente y empieza
    /// de cero (para detener, Stop). Feedback instantáneo en UI; la apertura
    /// pesada (lector + salida) corre en background y se marshala al hilo UI.
    /// Sin Mix el disparo reemplaza con fundido suave (la anterior cae en 120 ms
    /// mientras la nueva arranca); con Mix se suma hasta 6 voces. Los slots sin audio retornan false (la vista deja el pulso).
    /// Con Ganancia armada y la cola sonando, el efecto prioriza: la cola se
    /// atenúa (ducking) y retoma sola al terminar la última voz.
    /// Debe llamarse en el hilo UI.
    /// </remarks>
    public bool Disparar(PaletteItem item)
    {
        DetenerVozDe(item);
        if (MezclaActiva?.Invoke() != true)
        {
            DetenerEfectos();
        }

        if (!item.TieneAudio
            || string.IsNullOrWhiteSpace(item.FilePath)
            || !File.Exists(item.FilePath))
        {
            return false;
        }

        if (GananciaActiva?.Invoke() == true && ColaSonando?.Invoke() == true)
        {
            MotorAudio.Instancia.FijarMaestro(Math.Pow(10, AtenuacionColaDb / 20));
        }

        item.EstaSonando = true;
        Sonando.Add(item);
        lock (_puerta)
        {
            _pendientes.Add(item);
        }

        var ruta = item.FilePath;
        var inicio = item.CueInicio;
        var fin = item.CueFin;
        var ganancia = item.GananciaDb;
        var clave = (Duena: item.PropietariaId, Slot: item.SlotIndex);
        _ = Task.Run(() =>
        {
            var voz = _mezclador.Disparar(ruta, inicio, fin, ganancia);
            EnHiloUi(() =>
            {
                lock (_puerta)
                {
                    if (!_pendientes.Remove(item))
                    {
                        if (voz.HasValue)
                        {
                            _mezclador.Detener(voz.Value);
                        }

                        return;
                    }
                }

                if (voz is null)
                {
                    ApagarSlot(clave.Duena, clave.Slot);
                    RestaurarMaestroSiLibre();
                    return;
                }

                _voces[voz.Value] = (clave.Duena, clave.Slot);
                var actual = BuscarSlot(clave.Duena, clave.Slot);
                if (actual is not null && !Sonando.Contains(actual))
                {
                    actual.EstaSonando = true;
                    Sonando.Add(actual);
                }
            });
        });
        return true;
    }

    /// <summary>
    /// Dispara un efecto en modo Retrigger: crea NUEVA voz superpuesta (layering),
    /// sin detener las voces anteriores de este slot. El mixer permite hasta 6
    /// voces globales; si se satura, retorna false. El Stop global detiene todas.
    /// </summary>
    /// <param name="item">Slot a disparar.</param>
    /// <returns>True si se inició la voz; false si slot sin audio, archivo inválido o mixer saturado.</returns>
    public bool DispararRetrigger(PaletteItem item)
    {
        if (!item.TieneAudio
            || string.IsNullOrWhiteSpace(item.FilePath)
            || !File.Exists(item.FilePath))
        {
            return false;
        }

        if (GananciaActiva?.Invoke() == true && ColaSonando?.Invoke() == true)
        {
            MotorAudio.Instancia.FijarMaestro(Math.Pow(10, AtenuacionColaDb / 20));
        }

        var ruta = item.FilePath;
        var inicio = item.CueInicio;
        var fin = item.CueFin;
        var ganancia = item.GananciaDb;
        var clave = (Duena: item.PropietariaId, Slot: item.SlotIndex);

        var voz = _mezclador.Disparar(ruta, inicio, fin, ganancia);
        if (voz is null)
        {
            return false; // Mixer saturado (6 voces máx)
        }

        // Rastrear esta voz para poder detenerla con Stop global
        lock (_puerta)
        {
            _voces[voz.Value] = clave;
        }

        // UI: marcar como sonando y titilar
        item.EstaSonando = true;
        if (!Sonando.Contains(item))
        {
            Sonando.Add(item);
        }

        // Auto-limpieza al terminar natural
        _ = Task.Run(() =>
        {
            // Esperar a que la voz termine (evento VozTerminada ya maneja limpieza vía MarcarFinVoz)
            // Solo asegurar que si la voz se cancela antes, se limpie
        });

        return true;
    }

    /// <summary>Detiene solo las voces de este banco (Stop por pantalla) con fundido suave.</summary>
    /// <remarks>
    /// No toca la cola ni las voces de otros bancos (el mezclador es
    /// compartido: solo se detienen los Guid propios). El maestro se restaura
    /// solo si no queda ninguna voz sonando en toda la mezcla.
    /// Debe llamarse en el hilo UI.
    /// </remarks>
    [RelayCommand]
    public void DetenerBanco()
    {
        List<Guid> voces;
        lock (_puerta)
        {
            _pendientes.Clear();
            voces = _voces.Keys.ToList();
            _voces.Clear();
        }

        foreach (var voz in voces)
        {
            _mezclador.DetenerConFundido(voz);
        }

        foreach (var item in Sonando.ToList())
        {
            item.EstaSonando = false;
        }

        Sonando.Clear();
        RestaurarMaestroSiLibre();
    }

    /// <summary>
    /// Detiene todos los efectos con fundido suave (botón Stop y reemplazo sin Mix).
    /// </summary>
    /// <remarks>
    /// No toca la cola ni el reproductor (van por su lado). El audio cae en
    /// <see cref="MezcladorEfectos.FundidoSalidaMs"/> ms mientras la nueva voz ya
    /// arranca (solape breve y suave); la UI se apaga al instante.
    /// </remarks>
    public void DetenerEfectos()
    {
        _mezclador.DetenerTodosConFundido();
        LimpiarEstadoSonando();
    }

    /// <summary>Limpia voces rastreadas, pendientes y titileo (el audio fundido cae por su lado).</summary>
    private void LimpiarEstadoSonando()
    {
        MotorAudio.Instancia.FijarMaestro(1.0);
        _voces.Clear();
        lock (_puerta)
        {
            _pendientes.Clear();
        }

        foreach (var item in Sonando)
        {
            item.EstaSonando = false;
        }

        Sonando.Clear();
    }

    /// <summary>Apaga el estado de una voz terminada natural.</summary>
    /// <param name="voz">Id de voz.</param>
    /// <remarks>Llamar en el hilo UI (lo invoca la vista tras el evento).</remarks>
    public void MarcarFinVoz(Guid voz)
    {
        if (!_voces.Remove(voz, out var clave))
        {
            return;
        }

        _mezclador.Liberar(voz);
        ApagarSlot(clave.Duena, clave.Slot);
        RestaurarMaestroSiLibre();
    }

    /// <summary>Restaura el volumen de la cola si no queda voz sonando.</summary>
    /// <remarks>Solo restaura sin voces activas ni arranques pendientes.</remarks>
    private void RestaurarMaestroSiLibre()
    {
        lock (_puerta)
        {
            if (_mezclador.VocesActivas == 0 && _pendientes.Count == 0)
            {
                MotorAudio.Instancia.FijarMaestro(1.0);
            }
        }
    }

    /// <summary>Persiste los 50 slots de la paleta actual.</summary>
    public void Persistir()
    {
        if (!string.IsNullOrEmpty(PropietariaActual))
        {
            ConsolaStore.GuardarSlots(PropietariaActual, _todos);
        }

        PaletaCambio?.Invoke();
    }

    /// <summary>Refresca el indicador al cambiar de página.</summary>
    /// <param name="value">Página nueva.</param>
    partial void OnPaginaActualChanged(int value) => OnPropertyChanged(nameof(IndicadorPagina));

    /// <summary>Notifica la guía de creación al cambiar de categoría.</summary>
    /// <param name="value">Categoría nueva.</param>
    partial void OnSelectedChanged(SelectableCategory? value) => OnPropertyChanged(nameof(SinCategoria));

    /// <summary>Limpia la selección (riel vacío: guía "cree una categoría").</summary>
    public void LimpiarSeleccion()
    {
        DetenerEfectos();
        if (!string.IsNullOrEmpty(PropietariaActual))
        {
            _keyMapping.LimpiarPropietaria(PropietariaActual);
        }
        Selected = null;
        PropietariaActual = string.Empty;
        PaginaActual = 0;
        MostrarPagina();
        foreach (var item in _todos)
        {
            item.Vaciar(string.Empty, item.SlotIndex, null);
        }

        SincronizarSonando();
    }

    /// <summary>Cambia de página (lo que suena sigue sonando).</summary>
    /// <param name="pagina">Página destino.</param>
    private void CambiarPagina(int pagina)
    {
        PaginaActual = pagina;
        MostrarPagina();
    }

    /// <summary>Muestra la rebanada de la página actual.</summary>
    private void MostrarPagina()
    {
        var inicio = PaginaActual * TamanoPagina;
        var cantidad = Math.Min(TamanoPagina, Math.Max(0, _todos.Count - inicio));
        var igual = Items.Count == cantidad;
        for (var j = 0; igual && j < cantidad; j++)
        {
            igual = ReferenceEquals(Items[j], _todos[inicio + j]);
        }

        if (igual)
        {
            return;
        }

        Items.Clear();
        for (var j = 0; j < cantidad; j++)
        {
            Items.Add(_todos[inicio + j]);
        }
    }

    /// <summary>Detiene solo la voz de un slot (o su arranque pendiente) con fundido suave.</summary>
    /// <param name="item">Slot.</param>
    /// <remarks>La UI se apaga al instante; el audio cae en 120 ms.</remarks>
    private void DetenerVozDe(PaletteItem item)
    {
        lock (_puerta)
        {
            _pendientes.Remove(item);
        }

        var clave = (Duena: item.PropietariaId, Slot: item.SlotIndex);
        var voz = _voces.FirstOrDefault(par => par.Value == clave).Key;
        if (voz != Guid.Empty && _voces.Remove(voz))
        {
            _mezclador.DetenerConFundido(voz);
        }

        ApagarSlot(clave.Duena, clave.Slot);
    }

    /// <summary>Busca el objeto actual de un slot (tras redesplegar cambia).</summary>
    /// <param name="duena">Dueña.</param>
    /// <param name="slot">Índice 0-49.</param>
    /// <returns>Slot actual o null (otra categoría o fuera de rango).</returns>
    private PaletteItem? BuscarSlot(string duena, int slot)
    {
        if (!string.Equals(duena, PropietariaActual, StringComparison.Ordinal)
            || slot < 0 || slot >= _todos.Count)
        {
            return null;
        }

        return _todos[slot];
    }

    /// <summary>Apaga el flag visual de un slot (si está desplegado).</summary>
    /// <param name="duena">Dueña.</param>
    /// <param name="slot">Índice.</param>
    private void ApagarSlot(string duena, int slot)
    {
        var actual = BuscarSlot(duena, slot);
        if (actual is null)
        {
            return;
        }

        actual.EstaSonando = false;
        Sonando.Remove(actual);
    }

    /// <summary>Re-engancha el titileo a las voces vivas de la dueña actual.</summary>
    /// <remarks>Tras desplegar, los objetos son nuevos: se resuelven por clave.</remarks>
    private void SincronizarSonando()
    {
        foreach (var item in Sonando)
        {
            item.EstaSonando = false;
        }

        Sonando.Clear();
        foreach (var clave in _voces.Values.Distinct())
        {
            var actual = BuscarSlot(clave.Duena, clave.Slot);
            if (actual is not null)
            {
                actual.EstaSonando = true;
                Sonando.Add(actual);
            }
        }
    }

    /// <summary>Marshala el fin natural al hilo UI.</summary>
    /// <param name="voz">Voz terminada.</param>
    private void OnVozTerminada(Guid voz) => EnHiloUi(() => MarcarFinVoz(voz));

    /// <summary>Ejecuta en el hilo UI (directo si no hay cola fijada).</summary>
    /// <param name="accion">Acción.</param>
    private void EnHiloUi(Action accion)
    {
        if (_hiloUi is null)
        {
            accion();
            return;
        }

        _hiloUi.TryEnqueue(() => accion());
    }

    /// <summary>Aplica un guardado portable sobre un slot base.</summary>
    /// <param name="item">Slot base (vacío).</param>
    /// <param name="slot">Datos guardados.</param>
    private static void AplicarGuardado(PaletteItem item, SlotEfectoGuardado slot)
    {
        item.Title = slot.Titulo;
        item.Duration = TimeSpan.FromTicks(slot.DuracionTicks);
        item.FilePath = slot.FilePath;
        item.CueInicio = TimeSpan.FromTicks(slot.CueInicioTicks);
        item.CueFin = slot.CueFinTicks.HasValue ? TimeSpan.FromTicks(slot.CueFinTicks.Value) : null;
        item.GananciaDb = slot.GananciaDb;
        item.FundidoEntrada = TimeSpan.FromTicks(slot.FundidoEntradaTicks);
        item.FundidoSalida = TimeSpan.FromTicks(slot.FundidoSalidaTicks);
        item.ColorKey = slot.ColorKey;
        item.MapeoTecla = slot.MapeoTecla;
        item.MapeoModifiers = slot.MapeoModifiers;
        item.MapeoModo = slot.MapeoModo;
    }
}
