using System.Collections.ObjectModel;
using BebeRadio.Models;
using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Cola de reproducción (panel izquierdo estilo Jazler).
/// Dueña de <see cref="Queue"/>, entrada en vivo y resumen.
/// Fase 2: aquí se mezcla con Paleta (encolar) y Acciones
/// (detener, subir/bajar) sin tocar la vista.
/// </summary>
public sealed partial class ListaReproduccionViewModel : ObservableObject
{
    /// <summary>Entradas de la cola (izquierda, estilo Jazler).</summary>
    public ObservableCollection<QueueEntry> Queue { get; } = new();

    /// <summary>Entrada en reproducción (badge EN VIVO).</summary>
    [ObservableProperty]
    public partial QueueEntry? CurrentEntry { get; set; }

    /// <summary>
    /// Pedido explícito de reproducción (clic / "Poner en vivo").
    /// El reproductor lo atiende y suena desde cero; la selección por
    /// <see cref="AddFiles"/> (cue) no lo eleva y solo previsualiza.
    /// </summary>
    /// <remarks>Se eleva ANTES de mover <see cref="CurrentEntry"/> (un solo arranque).</remarks>
    public event EventHandler<QueueEntry>? ReproduccionPedida;

    /// <summary>Resumen "N pistas · total m:ss".</summary>
    [ObservableProperty]
    public partial string QueueSummary { get; set; } = string.Empty;

    /// <summary>True si la cola está vacía (muestra la guía de arrastre).</summary>
    [ObservableProperty]
    public partial bool ColaVacia { get; set; } = true;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _colaUi =
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    /// <summary>Inicializa la cola vacía (sin mocks: pruebas reales).</summary>
    public ListaReproduccionViewModel()
    {
        RefreshSummary();
        ServicioAnalisisAudio.Instancia.Preparado += AlPrepararEntrada;
    }

    /// <summary>Pone una entrada de la cola en reproducción (suena desde cero).</summary>
    /// <param name="entry">Entrada pulsada.</param>
    /// <remarks>
    /// Eleva <see cref="ReproduccionPedida"/> antes de mover
    /// <see cref="CurrentEntry"/> para que el reproductor arme la intención
    /// de sonar y haya un único arranque de motor.
    /// </remarks>
    [RelayCommand]
    private void Play(QueueEntry entry)
    {
        ReproduccionPedida?.Invoke(this, entry);
        if (!ReferenceEquals(CurrentEntry, entry))
        {
            CurrentEntry = entry;
        }
    }

    /// <summary>
    /// Carga archivos de audio reales en la posición indicada.
    /// La paleta NO encola: solo dispara (Fase 2 le dará sonido).
    /// Si la cola estaba vacía y sin actual, la primera insertada queda
    /// como actual (cue): la pantallita la muestra lista para Play, sin sonar.
    /// El recorte de silencio y el análisis de loudness corren en segundo plano.
    /// </summary>
    /// <param name="paths">Rutas soltadas sobre la cola.</param>
    /// <param name="indice">Posición de inserción (-1 = al final).</param>
    public void AddFiles(IEnumerable<string> paths, int indice = -1)
    {
        var eraVacia = CurrentEntry is null && Queue.Count == 0;
        var destino = indice < 0 || indice > Queue.Count ? Queue.Count : indice;
        var primeraInsertada = -1;
        foreach (var path in paths)
        {
            var entry = AudioFileInspector.TryInspect(path);
            if (entry is not null)
            {
                Queue.Insert(destino, entry);
                if (primeraInsertada < 0)
                {
                    primeraInsertada = destino;
                }

                ServicioAnalisisAudio.Instancia.Encolar(path, entry.Duration);
                destino++;
            }
        }

        if (eraVacia && primeraInsertada >= 0 && primeraInsertada < Queue.Count)
        {
            CurrentEntry = Queue[primeraInsertada];
        }

        RefreshSummary();
    }

    /// <summary>
    /// Aplica el resultado del análisis en segundo plano al hilo UI:
    /// fija el fin efectivo (recorte) de las entradas con esa ruta.
    /// </summary>
    /// <param name="path">Ruta analizada.</param>
    /// <param name="finEfectivo">Fin efectivo o null (sin recorte).</param>
    /// <param name="gananciaDb">Ganancia de normalización (la usa el reproductor vía caché).</param>
    /// <remarks>La ganancia queda cacheada; aquí solo se toca el fin efectivo.</remarks>
    private void AlPrepararEntrada(string path, TimeSpan? finEfectivo, double gananciaDb)
    {
        void Aplicar()
        {
            var cambio = false;
            foreach (var entry in Queue)
            {
                if (string.Equals(entry.FilePath, path, StringComparison.OrdinalIgnoreCase)
                    && entry.FinEfectivo != finEfectivo)
                {
                    entry.FinEfectivo = finEfectivo;
                    cambio = true;
                }
            }

            if (cambio)
            {
                RefreshSummary();
            }
        }

        if (_colaUi is null)
        {
            Aplicar();
            return;
        }

        _colaUi.TryEnqueue(Aplicar);
    }

    /// <summary>
    /// Detiene la reproducción lógica (suelta el EN VIVO).
    /// El motor de audio real lo ejecutará en Fase 2.
    /// </summary>
    public void Detener()
    {
        CurrentEntry = null;
    }

    /// <summary>
    /// Consume la entrada actual (sonó completa): la quita y pone
    /// la siguiente en vivo (o null si se vacía). El transporte reacciona solo.
    /// </summary>
    public void ConsumirActual()
    {
        if (CurrentEntry is null)
        {
            return;
        }

        var indice = Queue.IndexOf(CurrentEntry);
        if (indice >= 0)
        {
            Queue.RemoveAt(indice);
        }

        CurrentEntry = indice >= 0 && indice < Queue.Count ? Queue[indice] : null;
        RefreshSummary();
    }

    /// <summary>Vacía la cola (la vista confirma si no está vacía).</summary>
    public void LimpiarTodo()
    {
        if (Queue.Count == 0)
        {
            return;
        }

        CurrentEntry = null;
        Queue.Clear();
        RefreshSummary();
    }

    /// <summary>Saca una entrada de la cola.</summary>
    /// <param name="entry">Entrada a quitar.</param>
    /// <remarks>Si era la actual, el EN VIVO pasa a la siguiente.</remarks>
    [RelayCommand]
    private void Quitar(QueueEntry entry)
    {
        var indice = Queue.IndexOf(entry);
        if (indice < 0)
        {
            return;
        }

        var eraActual = ReferenceEquals(entry, CurrentEntry);
        Queue.RemoveAt(indice);
        if (eraActual)
        {
            CurrentEntry = indice < Queue.Count ? Queue[indice] : null;
        }

        RefreshSummary();
    }

    /// <summary>Mueve la entrada actual una posición hacia arriba.</summary>
    /// <remarks>Futuro: lo invocará AccionesConsolaStrip (flecha ▲).</remarks>
    public void MoverActualArriba()
    {
        MoveCurrent(-1);
    }

    /// <summary>Mueve la entrada actual una posición hacia abajo.</summary>
    /// <remarks>Futuro: lo invocará AccionesConsolaStrip (flecha ▼).</remarks>
    public void MoverActualAbajo()
    {
        MoveCurrent(1);
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

    /// <summary>Mueve la entrada actual un desplazamiento relativo.</summary>
    /// <param name="offset">-1 arriba, +1 abajo.</param>
    private void MoveCurrent(int offset)
    {
        if (CurrentEntry is null || Queue.Count < 2)
        {
            return;
        }

        var index = Queue.IndexOf(CurrentEntry);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= Queue.Count)
        {
            return;
        }

        Queue.Move(index, target);
        RefreshSummary();
    }

    /// <summary>Recalcula "N pistas · total m:ss" y el estado vacío.</summary>
    private void RefreshSummary()
    {
        var total = TimeSpan.Zero;
        foreach (var entry in Queue)
        {
            total += entry.DuracionEfectiva;
        }

        QueueSummary = $"{Queue.Count} pistas · total {TimeFormatter.ToMinuteSecond(total)}";
        ColaVacia = Queue.Count == 0;
    }
}
