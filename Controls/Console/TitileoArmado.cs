using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Titileo de "armado" para botones de consola (p. ej. Mix): alterna la
/// opacidad del botón mientras está armado y la restaura al desarmar.
/// Un solo timer por botón, barato; reutilizable en cualquier tira.
/// </summary>
public sealed class TitileoArmado
{
    private readonly Button _boton;
    private readonly DispatcherQueue _cola;
    private readonly double _opacidadMinima;
    private readonly TimeSpan _intervalo;
    private DispatcherQueueTimer? _timer;
    private bool _fase;

    /// <summary>Crea el titileo para un botón.</summary>
    /// <param name="boton">Botón a titilar.</param>
    /// <param name="cola">Cola del dispatcher para el timer.</param>
    /// <param name="opacidadMinima">Opacidad del pulso bajo (0–1).</param>
    /// <param name="intervaloMs">Intervalo del parpadeo en milisegundos.</param>
    public TitileoArmado(Button boton, DispatcherQueue cola, double opacidadMinima = 0.45, int intervaloMs = 400)
    {
        _boton = boton;
        _cola = cola;
        _opacidadMinima = opacidadMinima;
        _intervalo = TimeSpan.FromMilliseconds(intervaloMs);
    }

    /// <summary>Enciende o apaga el titileo según el estado de armado.</summary>
    /// <param name="armado">True si el botón debe titilar.</param>
    public void Sincronizar(bool armado)
    {
        if (armado)
        {
            Encender();
        }
        else
        {
            Apagar();
        }
    }

    /// <summary>Detiene el titileo y restaura la opacidad.</summary>
    public void Apagar()
    {
        _timer?.Stop();
        _timer = null;
        _boton.Opacity = 1;
    }

    /// <summary>Inicia el titileo (sin duplicar timers).</summary>
    private void Encender()
    {
        if (_timer is not null)
        {
            return;
        }

        _fase = false;
        _timer = _cola.CreateTimer();
        _timer.Interval = _intervalo;
        _timer.Tick += (_, _) =>
        {
            _fase = !_fase;
            _boton.Opacity = _fase ? _opacidadMinima : 1;
        };
        _timer.Start();
    }
}
