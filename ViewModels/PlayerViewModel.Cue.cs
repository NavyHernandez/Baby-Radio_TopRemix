using BebeRadio.Models;

namespace BebeRadio.ViewModels;

/// <summary>
/// Parcial de <see cref="PlayerViewModel"/> con la lógica de cue/preview:
/// mostrar la primera canción en la pantallita sin arrancar el motor.
/// Separar "previsualizar" de "sonar" permite que <c>AddFiles</c> deje la
/// pista lista para Play mientras el clic / Next / fin natural sí suenan.
/// </summary>
public sealed partial class PlayerViewModel
{
    /// <summary>Intención explícita de sonar (clic, Next, fin natural).</summary>
    /// <remarks>Se consume en <c>AlCambiarActual</c>; el cue no lo arma.</remarks>
    private bool _sonidoSolicitado;

    /// <summary>
    /// Solicita reproducción explícita de una entrada (suena desde cero).
    /// </summary>
    /// <param name="entry">Entrada a sonar (debe pertenecer a la cola conectada).</param>
    /// <remarks>
    /// Si la entrada ya es la actual, suena de inmediato; si no, arma
    /// <c>_sonidoSolicitado</c> y mueve <c>CurrentEntry</c> (el cambio
    /// dispara <c>AlCambiarActual</c>, que consume el flag y suena).
    /// </remarks>
    public void SolicitarReproduccion(QueueEntry entry)
    {
        if (_cola is null)
        {
            return;
        }

        _sonidoSolicitado = true;
        if (!ReferenceEquals(_cola.CurrentEntry, entry))
        {
            _cola.CurrentEntry = entry;
        }
        else
        {
            _sonidoSolicitado = false;
            SonarActual(entry);
        }
    }

    /// <summary>
    /// Atiende el pedido explícito de reproducción de la lista (clic / menú).
    /// </summary>
    /// <param name="sender">Lista que pide (se ignora si no es la conectada).</param>
    /// <param name="entry">Entrada pedida.</param>
    /// <remarks>
    /// La lista invoca este evento ANTES de mover <c>CurrentEntry</c>, así el
    /// handler deja un solo cambio con flag y hay un único arranque de motor.
    /// </remarks>
    private void AlPedirReproduccion(object? sender, QueueEntry entry)
    {
        if (!ReferenceEquals(sender, _cola))
        {
            return;
        }

        SolicitarReproduccion(entry);
    }

    /// <summary>
    /// Previsualiza una entrada: publica Título/Artista/tiempos sin tocar el motor.
    /// </summary>
    /// <param name="entry">Entrada a mostrar (queda lista para Play).</param>
    /// <remarks>Función pura de display: no abre audio ni cambia <c>IsPlaying</c> a true.</remarks>
    private void Previsualizar(QueueEntry entry)
    {
        _sonandoA = null;
        Title = entry.Title;
        Artist = entry.Line2;
        Aviso = string.Empty;
        IsPlaying = false;
        IsPaused = false;
        RefreshTimeTexts(TimeSpan.Zero, entry.DuracionEfectiva);
    }

    /// <summary>Limpia el display al placeholder "Sin pista en cola".</summary>
    /// <remarks>Consume <c>_sonidoSolicitado</c> para no arrastrar autoplay al vaciar.</remarks>
    private void LimpiarDisplay()
    {
        _sonandoA = null;
        _sonidoSolicitado = false;
        IsPlaying = false;
        IsPaused = false;
        Title = "Sin pista en cola";
        Artist = "Agrega audios a la lista";
        Aviso = string.Empty;
        RefreshTimeTexts(TimeSpan.Zero, TimeSpan.Zero);
    }
}
