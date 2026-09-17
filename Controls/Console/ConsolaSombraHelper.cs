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
    /// <summary>Enciende el glow de actividad 300 ms (feedback de disparo).</summary>
    /// <param name="button">Botón a iluminar.</param>
    /// <param name="queue">Cola del dispatcher para el apagado.</param>
    public static void FlashActive(Button button, DispatcherQueue queue)
    {
        BebeButtonHelper.SetIsActive(button, true);
        var timer = queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(300);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => BebeButtonHelper.SetIsActive(button, false);
        timer.Start();
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
