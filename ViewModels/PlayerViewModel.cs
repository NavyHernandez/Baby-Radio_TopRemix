using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace BebeRadio.ViewModels;

/// <summary>
/// Reproductor con audio real: dirige la cola vía <see cref="MotorAudio"/>.
/// Regla: el cue (selección con motor detenido) solo previsualiza Título/Artista
/// y queda listo para Play; el pedido explícito (clic, Next, fin natural) o un
/// cambio con el motor sonando sí suena desde cero.
/// Tiempos desde la posición real y VU desde los samples (nada mock).
/// La vista se enlaza por binding; el code-behind no contiene lógica de negocio.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject
{
    /// <summary>Segundos para que Previous reinicie en vez de retroceder.</summary>
    private const double UmbralPreviousSeg = 3;

    /// <summary>Anti-rebote de avance (doble clic no consume dos pistas).</summary>
    private static readonly TimeSpan UmbralNext = TimeSpan.FromMilliseconds(600);

    private DateTime _ultimoNext = DateTime.MinValue;
    private CancellationTokenSource? _transicionCts;

    private readonly DispatcherTimer _ticker;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _colaUi;
    private readonly MotorAudio _motor = MotorAudio.Instancia;
    private readonly MezcladorEfectos _mezcla = MezcladorEfectos.Instancia;
    private (float Izq, float Der, long Ticks) _nivelCola;
    private (float Izq, float Der, long Ticks) _nivelEfecto;
    private ListaReproduccionViewModel? _cola;
    private QueueEntry? _sonandoA;

    [ObservableProperty]
    public partial string Title { get; set; } = "Sin pista en cola";

    [ObservableProperty]
    public partial string Artist { get; set; } = "Agrega audios a la lista";

    /// <summary>Aviso breve (p. ej. entradas sin audio saltadas).</summary>
    [ObservableProperty]
    public partial string Aviso { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>Pausa activa (el pad muestra pausa y titila).</summary>
    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    /// <summary>Stop programado armado (detiene al terminar la pista, estilo Jazler).</summary>
    [ObservableProperty]
    public partial bool StopAtEndArmed { get; set; }

    /// <summary>Repetición armada (la pista actual re-empieza al terminar).</summary>
    [ObservableProperty]
    public partial bool RepeatArmed { get; set; }

    /// <summary>True mientras baja el audio antes de avanzar (el Next debe titilar).</summary>
    [ObservableProperty]
    public partial bool TransicionEnCurso { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string RemainingText { get; set; } = "-0:00";

    [ObservableProperty]
    public partial double LevelLeft { get; set; }

    [ObservableProperty]
    public partial double LevelRight { get; set; }

    /// <summary>Inicializa el reproductor (ticker UI + eventos de motores).</summary>
    public PlayerViewModel()
    {
        _colaUi = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _motor.TerminadoNatural += AlTerminarNatural;
        _motor.Niveles += AlRecibirNiveles;
        _mezcla.Niveles += AlRecibirNivelesEfectos;
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _ticker.Tick += (_, _) => RefrescarReloj();
        _ticker.Start();
    }

    /// <summary>Conecta la cola: el transporte la dirige desde entonces.</summary>
    /// <param name="cola">ViewModel de la lista de reproducción.</param>
    public void ConectarCola(ListaReproduccionViewModel cola)
    {
        if (_cola is not null)
        {
            _cola.PropertyChanged -= OnColaChanged;
            _cola.ReproduccionPedida -= AlPedirReproduccion;
        }

        _cola = cola;
        _cola.PropertyChanged += OnColaChanged;
        _cola.ReproduccionPedida += AlPedirReproduccion;
        AlCambiarActual();
    }

    /// <summary>Alterna reproducción/pausa/reanuda (sin actual toma la primera).</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (_motor.Modo == ModoMotor.Tramo)
        {
            return;
        }

        if (_motor.Reproduciendo)
        {
            Support.RegistroErrores.Traza("Cola.Pausa", "toggle del transporte");
            _motor.Pausar();
            IsPlaying = false;
            IsPaused = true;
            return;
        }

        if (_motor.EnPausa)
        {
            _motor.Reanudar();
            IsPlaying = true;
            IsPaused = false;
            return;
        }

        SonarActual(Current());
    }

    /// <summary>Detiene y rebobina a cero (conserva la cola).</summary>
    [RelayCommand]
    private void Stop()
    {
        Support.RegistroErrores.Traza("Cola.Stop", "transporte detener");
        CancelarTransicion();
        _motor.Detener();
        IsPlaying = false;
        IsPaused = false;
        RefreshTimeTexts(TimeSpan.Zero, DuracionActual());
    }

    /// <summary>Salta a la siguiente (la actual se consume y sale).</summary>
    /// <remarks>
    /// Anti-rebote 600 ms + una sola transición a la vez. Con la transición de
    /// Config activada y el motor sonando, baja el audio en N segundos antes de avanzar.
    /// </remarks>
    [RelayCommand]
    private void Next()
    {
        var ahora = DateTime.UtcNow;
        if (ahora - _ultimoNext < UmbralNext || _transicionCts is not null)
        {
            return;
        }

        _ultimoNext = ahora;
        if (_cola is null)
        {
            Stop();
            return;
        }

        var (transicionOn, segundos) = LeerTransicion();
        if (transicionOn && _motor.Reproduciendo)
        {
            AvanzarConTransicion(segundos);
            return;
        }

        _sonidoSolicitado = true;
        _cola.ConsumirActual();
        if (Current() is null)
        {
            Stop();
        }
    }

    /// <summary>Baja el audio en N segundos y luego avanza (fire-and-forget).</summary>
    /// <param name="segundos">Duración de la transición (3, 5 o 7).</param>
    /// <remarks>Stop/Previous/fin natural cancelan (sin doble avance).</remarks>
    private async void AvanzarConTransicion(int segundos)
    {
        CancelarTransicion();
        var cts = new CancellationTokenSource();
        _transicionCts = cts;
        TransicionEnCurso = true;
        try
        {
            await _motor.TransicionAsync(0, TimeSpan.FromSeconds(segundos), cts.Token);
            if (cts.IsCancellationRequested || _cola is null)
            {
                return;
            }

            _sonidoSolicitado = true;
            _cola.ConsumirActual();
            if (Current() is null)
            {
                Stop();
            }
        }
        finally
        {
            if (ReferenceEquals(_transicionCts, cts))
            {
                _transicionCts = null;
            }

            cts.Dispose();
            _colaUi.TryEnqueue(() => TransicionEnCurso = false);
        }
    }

    /// <summary>Cancela la transición en curso (conserva el nivel alcanzado).</summary>
    private void CancelarTransicion()
    {
        _transicionCts?.Cancel();
        _transicionCts?.Dispose();
        _transicionCts = null;
        TransicionEnCurso = false;
    }

    /// <summary>Lee el ajuste de transición (defaults si el JSON falla).</summary>
    /// <returns>(activado, segundos normalizados).</returns>
    private static (bool Activado, int Segundos) LeerTransicion()
    {
        try
        {
            var config = ConsolaStore.Cargar();
            return (config.TransicionSiguienteActivada,
                ConfiguracionViewModel.NormalizarSegundos(config.TransicionSiguienteSegundos));
        }
        catch
        {
            return (true, 5);
        }
    }

    /// <summary>Reinicia si lleva +3s; si no, va a la anterior.</summary>
    [RelayCommand]
    private void Previous()
    {
        CancelarTransicion();
        if (_cola is null)
        {
            return;
        }

        if (_motor.Posicion.TotalSeconds > UmbralPreviousSeg || Vecina(-1) is null)
        {
            var actual = Current();
            if (actual is not null)
            {
                SonarActual(actual);
            }

            return;
        }

        _cola.PlayCommand.Execute(Vecina(-1)!);
    }

    /// <summary>Arma/desarma el stop (detenido: además continúa la lista).</summary>
    /// <remarks>Excluyente con repetir: armar uno desarma el otro.</remarks>
    [RelayCommand]
    private void ToggleStopAtEnd()
    {
        StopAtEndArmed = !StopAtEndArmed;
        if (StopAtEndArmed)
        {
            RepeatArmed = false;
        }

        if (_motor.Modo == ModoMotor.Tramo)
        {
            return;
        }

        if (!_motor.Reproduciendo && !_motor.EnPausa)
        {
            SonarActual(Current());
        }
    }

    /// <summary>Arma/desarma la repetición de la pista actual.</summary>
    /// <remarks>Excluyente con stop-al-fin: armar uno desarma el otro.</remarks>
    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatArmed = !RepeatArmed;
        if (RepeatArmed)
        {
            StopAtEndArmed = false;
        }
    }

    /// <summary>Refresca reloj, estado y silencio del VU (hilo UI, 20 Hz).</summary>
    private void RefrescarReloj()
    {
        if (_motor.Modo == ModoMotor.Tramo)
        {
            return;
        }

        IsPlaying = _motor.Reproduciendo;
        IsPaused = _motor.EnPausa;
        PublicarCombinado();

        RefreshTimeTexts(_motor.Posicion, DuracionActual());
    }

    /// <summary>Fin natural de cola: consume la sonada y sigue (o para si armado).</summary>
    /// <param name="modo">Modo que terminó (tramos se ignoran).</param>
    private void AlTerminarNatural(ModoMotor modo)
    {
        if (modo != ModoMotor.Cola)
        {
            return;
        }

        _colaUi.TryEnqueue(() =>
        {
            CancelarTransicion();
            if (RepeatArmed)
            {
                var actual = Current();
                if (actual is not null)
                {
                    SonarActual(actual);
                }

                return;
            }

            var armado = StopAtEndArmed;
            StopAtEndArmed = false;
            _sonidoSolicitado = true;
            _cola?.ConsumirActual();
            if (armado)
            {
                Stop();
            }
        });
    }

    /// <summary>Lleva niveles crudos de la cola al VU (el control suaviza).</summary>
    /// <param name="izq">Pico izquierdo 0-1.</param>
    /// <param name="der">Pico derecho 0-1.</param>
    private void AlRecibirNiveles(float izq, float der) =>
        _colaUi.TryEnqueue(() =>
        {
            _nivelCola = (izq, der, DateTime.UtcNow.Ticks);
            PublicarCombinado();
        });

    /// <summary>Lleva niveles crudos de los efectos al VU (el control suaviza).</summary>
    /// <param name="izq">Pico izquierdo 0-1.</param>
    /// <param name="der">Pico derecho 0-1.</param>
    private void AlRecibirNivelesEfectos(float izq, float der) =>
        _colaUi.TryEnqueue(() =>
        {
            _nivelEfecto = (izq, der, DateTime.UtcNow.Ticks);
            PublicarCombinado();
        });

    /// <summary>Publica el máximo de las fuentes frescas (cola o efectos).</summary>
    /// <remarks>Sin fuentes frescas cae a 0 (silencio del VU).</remarks>
    private void PublicarCombinado()
    {
        var ahora = DateTime.UtcNow.Ticks;
        var colaFresca = new TimeSpan(ahora - _nivelCola.Ticks) < TimeSpan.FromMilliseconds(150);
        var efectoFresco = new TimeSpan(ahora - _nivelEfecto.Ticks) < TimeSpan.FromMilliseconds(150);
        LevelLeft = Math.Clamp(
            Math.Max(colaFresca ? _nivelCola.Izq : 0, efectoFresco ? _nivelEfecto.Izq : 0), 0, 1);
        LevelRight = Math.Clamp(
            Math.Max(colaFresca ? _nivelCola.Der : 0, efectoFresco ? _nivelEfecto.Der : 0), 0, 1);
    }

    /// <summary>Reacciona a cambios de la cola (solo la entrada actual).</summary>
    /// <param name="sender">Cola.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnColaChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ListaReproduccionViewModel.CurrentEntry))
        {
            AlCambiarActual();
        }
    }

    /// <summary>
    /// Reacciona al cambio de entrada actual: previsualiza si el motor está
    /// detenido (cue listo para Play) o suena si hay pedido explícito o el
    /// motor venía sonando (continuidad de radio).
    /// </summary>
    private void AlCambiarActual()
    {
        var actual = Current();
        if (actual is null)
        {
            LimpiarDisplay();
            return;
        }

        if (_sonidoSolicitado)
        {
            _sonidoSolicitado = false;
            SonarActual(actual);
            return;
        }

        if (ReferenceEquals(actual, _sonandoA))
        {
            return;
        }

        if (_motor.Reproduciendo || _motor.EnPausa)
        {
            SonarActual(actual);
            return;
        }

        Previsualizar(actual);
    }

    /// <summary>Suena una entrada (null = toma la primera; salta sin audio).</summary>
    /// <param name="entry">Entrada a sonar o null (primera).</param>
    private void SonarActual(QueueEntry? entry)
    {
        var candidata = entry
            ?? (_cola is not null && _cola.Queue.Count > 0 ? _cola.Queue[0] : null);
        if (_cola is null || candidata is null)
        {
            _sonandoA = null;
            IsPlaying = false;
            Title = "Sin pista en cola";
            Artist = "Agrega audios a la lista";
            RefreshTimeTexts(TimeSpan.Zero, TimeSpan.Zero);
            return;
        }
        while (candidata is not null && !_motor.ReproducirCola(candidata, GananciaNormalizada(candidata)))
        {
            Aviso = $"Sin audio: {candidata.Title}";
            candidata = SiguienteDe(candidata);
        }

        if (candidata is null)
        {
            _sonandoA = null;
            IsPlaying = false;
            Title = "Sin pista en cola";
            Artist = "Agrega audios a la lista";
            RefreshTimeTexts(TimeSpan.Zero, TimeSpan.Zero);
            return;
        }

        _sonandoA = candidata;
        Title = candidata.Title;
        Artist = candidata.Line2;
        Aviso = string.Empty;
        IsPlaying = true;
        if (!ReferenceEquals(_cola.CurrentEntry, candidata))
        {
            _cola.CurrentEntry = candidata;
        }

        RefreshTimeTexts(TimeSpan.Zero, DuracionActual());
    }

    /// <summary>Entrada actual de la cola (o null).</summary>
    /// <returns>Entrada en vivo.</returns>
    private QueueEntry? Current() => _cola?.CurrentEntry;

    /// <summary>
    /// Ganancia de normalización cacheada de una entrada (0 si aún no está lista).
    /// </summary>
    /// <param name="entry">Entrada a reproducir.</param>
    /// <returns>Ganancia en dB; 0 cuando no hay análisis previo.</returns>
    /// <remarks>
    /// Solo lee la caché (disco/memoria): no reanaliza en el hilo de reproducción.
    /// Si la pista se agrega y suena antes de terminar el análisis en segundo
    /// plano, esta primera reproducción suena sin ajuste; las siguientes ya lo usan.
    /// </remarks>
    private static double GananciaNormalizada(QueueEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.FilePath)
            && LoudnessCache.TryObtenerGanancia(entry.FilePath, out var ganancia)
                ? ganancia
                : 0;

    /// <summary>Duración efectiva (recorte) para display y progreso.</summary>
    /// <returns>Duración.</returns>
    private TimeSpan DuracionActual()
    {
        var efectiva = Current()?.DuracionEfectiva ?? TimeSpan.Zero;
        if (_motor.Duracion > TimeSpan.Zero && efectiva > TimeSpan.Zero)
        {
            return _motor.Duracion < efectiva ? _motor.Duracion : efectiva;
        }

        return efectiva;
    }

    /// <summary>Vecina de la actual en la cola.</summary>
    /// <param name="offset">-1 anterior, +1 siguiente.</param>
    /// <returns>Vecina o null.</returns>
    private QueueEntry? Vecina(int offset)
    {
        var actual = Current();
        if (_cola is null || actual is null)
        {
            return null;
        }

        var indice = _cola.Queue.IndexOf(actual) + offset;
        return indice >= 0 && indice < _cola.Queue.Count ? _cola.Queue[indice] : null;
    }

    /// <summary>Siguiente de una entrada dada.</summary>
    /// <param name="entry">Entrada base.</param>
    /// <returns>Siguiente o null.</returns>
    private QueueEntry? SiguienteDe(QueueEntry entry)
    {
        if (_cola is null)
        {
            return null;
        }

        var indice = _cola.Queue.IndexOf(entry) + 1;
        return indice >= 0 && indice < _cola.Queue.Count ? _cola.Queue[indice] : null;
    }

    /// <summary>Recalcula textos de tiempo y progreso 0–1.</summary>
    /// <param name="posicion">Posición real.</param>
    /// <param name="duracion">Duración real.</param>
    private void RefreshTimeTexts(TimeSpan posicion, TimeSpan duracion)
    {
        ElapsedText = TimeFormatter.ToMinuteSecond(posicion);
        var restante = duracion - posicion;
        if (restante < TimeSpan.Zero)
        {
            restante = TimeSpan.Zero;
        }

        RemainingText = $"-{TimeFormatter.ToMinuteSecond(restante)}";
        Progress = duracion.TotalSeconds <= 0
            ? 0
            : Math.Clamp(posicion.TotalSeconds / duracion.TotalSeconds, 0, 1);
    }
}
