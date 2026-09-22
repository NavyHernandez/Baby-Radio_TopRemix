using System.ComponentModel;
using BebeRadio.Support;
using BebeRadio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls;

/// <summary>
/// Display digital compacto 3×2 (título+reloj / artista+restante).
/// El code-behind solo pinta reloj, alias y marquee del título, y se
/// suscribe al reproductor; sin lógica de negocio.
/// </summary>
public sealed partial class MiniPantallaControl : UserControl
{
    /// <summary>Paso del marquee (30 ms, ~66 px/s).</summary>
    private static readonly TimeSpan IntervaloMarquee = TimeSpan.FromMilliseconds(30);

    /// <summary>Pausas del marquee en cada extremo (~0.9 s).</summary>
    private const int MarqueeEsperaTicks = 30;

    /// <summary>Píxeles por tick del marquee.</summary>
    private const double MarqueePasoPx = 2.0;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _marquee;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _reloj;
    private double _marqueeMax;
    private int _marqueeEspera;
    private int _marqueeFase;

    /// <summary>Propiedad de dependencia del reproductor (lo inyecta la shell).</summary>
    public static readonly DependencyProperty ReproductorProperty =
        DependencyProperty.Register(
            nameof(Reproductor),
            typeof(PlayerViewModel),
            typeof(MiniPantallaControl),
            new PropertyMetadata(null, OnReproductorChanged));

    /// <summary>Reproductor mostrado (nunca null en la shell).</summary>
    public PlayerViewModel? Reproductor
    {
        get => (PlayerViewModel?)GetValue(ReproductorProperty);
        set => SetValue(ReproductorProperty, value);
    }

    /// <summary>Inicializa el display y arranca reloj + marquee al cargar.</summary>
    public MiniPantallaControl()
    {
        InitializeComponent();
        TituloContenedor.SizeChanged += (_, _) => AjustarClipTitulo();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Re-suscribe al cambiar el reproductor inyectado.</summary>
    /// <param name="sender">Dependencia.</param>
    /// <param name="args">Valores viejo/nuevo.</param>
    private static void OnReproductorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is MiniPantallaControl pantalla)
        {
            if (args.OldValue is PlayerViewModel viejo)
            {
                viejo.PropertyChanged -= pantalla.OnReproductorCambiado;
            }

            if (args.NewValue is PlayerViewModel nuevo)
            {
                nuevo.PropertyChanged += pantalla.OnReproductorCambiado;
                pantalla.GestionarMarquee();
            }
        }
    }

    /// <summary>Al cargar: suscribe cambios, alias, reloj, onda y marquee.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Reproductor is not null)
        {
            Reproductor.PropertyChanged += OnReproductorCambiado;
        }

        TemaConsola.Cambio += RefrescarAlias;
        RefrescarAlias();
        SincronizarArtista();
        IniciarReloj();
        AjustarClipTitulo();
        GestionarMarquee();

        // Re-evalúa tras el layout: si la primera medida fue 0 el marquee
        // nunca arrancaría hasta el próximo cambio de título o estado.
        DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () =>
            {
                AjustarClipTitulo();
                GestionarMarquee();
            });
    }

    /// <summary>Desuscribe eventos y apaga timers al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Reproductor is not null)
        {
            Reproductor.PropertyChanged -= OnReproductorCambiado;
        }

        TemaConsola.Cambio -= RefrescarAlias;
        DetenerMarquee(false);
        _reloj?.Stop();
        _reloj = null;
    }

    /// <summary>Pinta el alias del operador (firma de la pantallita).</summary>
    /// <remarks>Vacío muestra "DJ"; se refresca al guardar en Mi cuenta.</remarks>
    private void RefrescarAlias()
    {
        var alias = TemaConsola.LeerPerfil().Alias;
        AliasTexto.Text = string.IsNullOrWhiteSpace(alias) ? "DJ" : alias.Trim();
    }

    /// <summary>Reinicia el marquee al cambiar título o estado.</summary>
    /// <param name="sender">Reproductor.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnReproductorCambiado(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.IsPlaying)
            or nameof(PlayerViewModel.IsPaused)
            or nameof(PlayerViewModel.Title))
        {
            if (e.PropertyName == nameof(PlayerViewModel.Title))
            {
                TituloDesplazamiento.X = 0;
            }

            GestionarMarquee();
        }
        else if (e.PropertyName == nameof(PlayerViewModel.Artist))
        {
            SincronizarArtista();
        }
    }

    /// <summary>Artista presente = texto; ausente = tiempo restante.</summary>
    /// <remarks>El restante ocupa el lugar de "Archivo local".</remarks>
    private void SincronizarArtista()
    {
        var artista = Reproductor?.Artist;
        var sinArtista = string.IsNullOrWhiteSpace(artista)
            || artista == AudioFileInspector.ArtistaDesconocido;
        ArtistaTexto.Visibility = sinArtista ? Visibility.Collapsed : Visibility.Visible;
        RestoTexto.Visibility = sinArtista ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Arranca el reloj digital (1 s, solo visible).</summary>
    private void IniciarReloj()
    {
        if (_reloj is not null)
        {
            return;
        }

        RefrescarReloj();
        _reloj = DispatcherQueue.CreateTimer();
        _reloj.Interval = TimeSpan.FromSeconds(1);
        _reloj.Tick += (_, _) => RefrescarReloj();
        _reloj.Start();
    }

    /// <summary>Pinta la hora actual.</summary>
    private void RefrescarReloj() => RelojTexto.Text = DateTime.Now.ToString("HH:mm:ss");

    /// <summary>Recorta el título al contenedor (llamado al redimensionar).</summary>
    private void AjustarClipTitulo()
    {
        var ancho = TituloContenedor.ActualWidth;
        var alto = TituloContenedor.ActualHeight;
        TituloContenedor.Clip = ancho > 0 && alto > 0
            ? new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, ancho, alto) }
            : null;
        GestionarMarquee();
    }

    /// <summary>Enciende el marquee si el título desborda, en cualquier estado.</summary>
    /// <remarks>
    /// Sonando, en pausa o detenido: el nombre completo siempre da la vuelta;
    /// si cabe, queda fijo. Así los títulos largos se leen sin dar play.
    /// </remarks>
    private void GestionarMarquee()
    {
        if (Reproductor is null)
        {
            DetenerMarquee(true);
            return;
        }

        EvaluarMarquee();
    }

    /// <summary>Mide desborde y arranca el scroll solo si no cabe.</summary>
    /// <remarks>Sin ellipsis al avanzar; con traza diagnóstica del cálculo.</remarks>
    private void EvaluarMarquee()
    {
        var visible = TituloContenedor.ActualWidth;
        if (visible <= 0)
        {
            return;
        }

        TituloTexto.Width = double.NaN;
        TituloTexto.TextTrimming = TextTrimming.None;
        TituloTexto.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var total = TituloTexto.DesiredSize.Width;
        if (total <= visible + 1)
        {
            DetenerMarquee(true);
            return;
        }

        TituloTexto.Width = total;
        _marqueeMax = total - visible;
        if (-TituloDesplazamiento.X > _marqueeMax)
        {
            TituloDesplazamiento.X = -_marqueeMax;
        }

        if (_marquee is null)
        {
            _marqueeFase = 0;
            _marqueeEspera = MarqueeEsperaTicks;
            _marquee = DispatcherQueue.CreateTimer();
            _marquee.Interval = IntervaloMarquee;
            _marquee.Tick += (_, _) => AvanzarMarquee();
            _marquee.Start();
        }
    }

    /// <summary>Avanza el scroll (pausas en extremos, loop hasta fin de pista).</summary>
    private void AvanzarMarquee()
    {
        switch (_marqueeFase)
        {
            case 0:
                if (--_marqueeEspera <= 0)
                {
                    _marqueeFase = 1;
                }

                break;
            case 1:
                TituloDesplazamiento.X -= MarqueePasoPx;
                if (-TituloDesplazamiento.X >= _marqueeMax)
                {
                    TituloDesplazamiento.X = -_marqueeMax;
                    _marqueeFase = 2;
                    _marqueeEspera = MarqueeEsperaTicks;
                }

                break;
            default:
                if (--_marqueeEspera <= 0)
                {
                    TituloDesplazamiento.X = 0;
                    _marqueeFase = 0;
                    _marqueeEspera = MarqueeEsperaTicks;
                }

                break;
        }
    }

    /// <summary>Apaga el scroll (opcionalmente vuelve al inicio con ellipsis).</summary>
    /// <param name="reiniciar">True para offset a cero y ancho auto.</param>
    private void DetenerMarquee(bool reiniciar)
    {
        _marquee?.Stop();
        _marquee = null;
        if (reiniciar)
        {
            TituloDesplazamiento.X = 0;
            TituloTexto.Width = double.NaN;
            TituloTexto.TextTrimming = TextTrimming.CharacterEllipsis;
        }
    }
}
