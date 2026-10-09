using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Composición pura de la consola Baby Radio: pulso de glow (300 ms),
/// anexo de sombras GPU y enumeración del árbol visual.
/// Sin lógica de negocio: solo efectos y recorrido visual.
/// </summary>
public static class ConsolaSombraHelper
{
    /// <summary>Pulsos de glow vivos (botón → timer del pulso).</summary>
    /// <remarks>
    /// El timer se ancla aquí: si viviera solo en la pila, el GC podría
    /// colectarlo antes del Tick y el glow quedaría encendido para siempre.
    /// Se vacía solo en cada Tick (≤300 ms), así que no retiene botones.
    /// </remarks>
    private static readonly Dictionary<Button, DispatcherQueueTimer> _pulsos = new();

    /// <summary>Protege <see cref="_pulsos"/> (clics y Ticks llegan al hilo UI).</summary>
    private static readonly object _pulsosCandado = new();

    /// <summary>Enciende el glow de actividad 300 ms (feedback de disparo).</summary>
    /// <param name="button">Botón a iluminar.</param>
    /// <param name="queue">Cola del dispatcher para el apagado.</param>
    /// <remarks>
    /// Un pulso nuevo cancela el pendiente del mismo botón (re-pulsar reinicia
    /// los 300 ms en vez de apagarse a mitad). El apagado solo ocurre si el
    /// Tick pertenece al pulso vigente.
    /// </remarks>
    public static void FlashActive(Button button, DispatcherQueue queue)
    {
        DispatcherQueueTimer? nuevo;
        lock (_pulsosCandado)
        {
            if (_pulsos.TryGetValue(button, out var anterior))
            {
                anterior.Stop();
                _pulsos.Remove(button);
            }

            nuevo = queue.CreateTimer();
            nuevo.Interval = TimeSpan.FromMilliseconds(300);
            nuevo.IsRepeating = false;
            nuevo.Tick += (_, _) => ApagarPulso(button, nuevo);
            _pulsos[button] = nuevo;
        }

        BebeButtonHelper.SetIsActive(button, true);
        nuevo.Start();
    }

    /// <summary>Apaga el pulso vigente de un botón cuando vence su timer.</summary>
    /// <param name="button">Botón pulsado.</param>
    /// <param name="timer">Timer que venció.</param>
    /// <remarks>Si el pulso ya fue reemplazado por uno nuevo, no toca el glow.</remarks>
    private static void ApagarPulso(Button button, DispatcherQueueTimer timer)
    {
        bool esVigente;
        lock (_pulsosCandado)
        {
            timer.Stop();
            esVigente = _pulsos.TryGetValue(button, out var vigente)
                && ReferenceEquals(vigente, timer);
            if (esVigente)
            {
                _pulsos.Remove(button);
            }
        }

        if (esVigente)
        {
            BebeButtonHelper.SetIsActive(button, false);
        }
    }

    /// <summary>Anexa (o reanexa si el template cambió) la sombra de cada botón bajo una raíz.</summary>
    /// <param name="root">Raíz visual (página o panel).</param>
    public static void AttachAllShadows(DependencyObject root)
    {
        foreach (var button in FindDescendants<Button>(root))
        {
            BebeButtonHelper.EnsureShadow(button);
        }
    }

    /// <summary>Enumera descendientes visuales de un tipo (solo composición).</summary>
    /// <param name="parent">Raíz de búsqueda.</param>
    /// <typeparam name="T">Tipo de descendiente.</typeparam>
    /// <returns>Secuencia de descendientes.</returns>
    public static IEnumerable<T> FindDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
