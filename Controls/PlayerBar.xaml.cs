using System.ComponentModel;
using BebeRadio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls;

/// <summary>
/// Barra de reproductor compacta (dirige la cola vía su ViewModel).
/// El code-behind solo enruta eventos, anexa sombras GPU, refleja niveles
/// al VU y parpadeos del transporte. La mini-pantalla vive en
/// <see cref="MiniPantallaControl"/> (sobre la lista).
/// </summary>
public sealed partial class PlayerBar : UserControl
{
    /// <summary>Intervalo del parpadeo del Stop programado.</summary>
    private static readonly TimeSpan IntervaloParpadeo = TimeSpan.FromMilliseconds(450);

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _parpadeo;
    private bool _faseParpadeo;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _parpadeoPlay;
    private bool _fasePlay;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _parpadeoRepeat;
    private bool _faseRepeat;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _parpadeoStop;
    private bool _faseStop;
    private int _pasosStop;

    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(PlayerViewModel),
            typeof(PlayerBar),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    private PlayerViewModel? _local;

    /// <summary>ViewModel del reproductor (lo inyecta la shell; defecto mock).</summary>
    public PlayerViewModel ViewModel
    {
        get
        {
            if (GetValue(ViewModelProperty) is PlayerViewModel inyectado)
            {
                return inyectado;
            }

            _local ??= new PlayerViewModel();
            return _local;
        }

        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Inicializa la barra y suscribe el reflejo de estado al VU.</summary>
    public PlayerBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Re-suscribe al cambiar el ViewModel inyectado.</summary>
    /// <param name="sender">Dependencia.</param>
    /// <param name="args">Valores viejo/nuevo.</param>
    private static void OnViewModelPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is PlayerBar barra)
        {
            if (args.OldValue is PlayerViewModel viejo)
            {
                viejo.PropertyChanged -= barra.OnViewModelChanged;
            }

            if (args.NewValue is PlayerViewModel nuevo)
            {
                nuevo.PropertyChanged += barra.OnViewModelChanged;
                barra.RefreshTransportVisual();
                barra.RefreshParpadeo();
            }
        }
    }

    /// <summary>
    /// Al cargar: anexa sombras GPU a los botones y suscribe cambios del VM.
    /// </summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BebeButtonHelper.AttachShadow(PlayButton);
        BebeButtonHelper.AttachShadow(PreviousButton);
        BebeButtonHelper.AttachShadow(StopButton);
        BebeButtonHelper.AttachShadow(NextButton);
        BebeButtonHelper.AttachShadow(RepeatButton);
        BebeButtonHelper.AttachShadow(StopAtEndButton);
        ViewModel.PropertyChanged += OnViewModelChanged;
        RefreshTransportVisual();
        RefreshParpadeo();
        RefreshRepeatBlink();
    }

    /// <summary>Desuscribe eventos y apaga parpadeos al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ApagarParpadeo();
        ApagarParpadeoPlay();
        ApagarParpadeoRepeat();
        ApagarParpadeoStop();
    }

    /// <summary>
    /// Refleja niveles al VU, iconos play/pausa y parpadeo del Stop programado.
    /// Lógica de vista pura (sin negocio): permitida en code-behind.
    /// </summary>
    /// <param name="sender">ViewModel.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.LevelLeft)
            or nameof(PlayerViewModel.LevelRight))
        {
            VuMeter.SetLevels(ViewModel.LevelLeft, ViewModel.LevelRight);
        }
        else if (e.PropertyName == nameof(PlayerViewModel.IsPlaying))
        {
            RefreshTransportVisual();
        }
        else if (e.PropertyName == nameof(PlayerViewModel.IsPaused))
        {
            RefreshTransportVisual();
        }
        else if (e.PropertyName == nameof(PlayerViewModel.RepeatArmed))
        {
            RefreshRepeatBlink();
        }
        else if (e.PropertyName == nameof(PlayerViewModel.StopAtEndArmed))
        {
            RefreshParpadeo();
        }
    }

    /// <summary>Alterna iconos play/pausa y titileo de pausa.</summary>
    /// <remarks>En pausa queda el icono pausa titilando (sin bordes).</remarks>
    private void RefreshTransportVisual()
    {
        var activo = ViewModel.IsPlaying || ViewModel.IsPaused;
        PlayIcon.Visibility = activo ? Visibility.Collapsed : Visibility.Visible;
        PauseIcon.Visibility = activo ? Visibility.Visible : Visibility.Collapsed;
        RefreshPlayBlink();
    }

    /// <summary>Enciende o apaga el titileo según la pausa.</summary>
    private void RefreshPlayBlink()
    {
        if (ViewModel.IsPaused)
        {
            EncenderParpadeoPlay();
        }
        else
        {
            ApagarParpadeoPlay();
        }
    }

    /// <summary>Parpadea el pad play/pausa (opacidad 1 ↔ 0.35 cada 450 ms).</summary>
    private void EncenderParpadeoPlay()
    {
        if (_parpadeoPlay is not null)
        {
            return;
        }

        _fasePlay = true;
        PlayButton.Opacity = 0.35;
        _parpadeoPlay = DispatcherQueue.CreateTimer();
        _parpadeoPlay.Interval = IntervaloParpadeo;
        _parpadeoPlay.Tick += (_, _) =>
        {
            _fasePlay = !_fasePlay;
            PlayButton.Opacity = _fasePlay ? 0.35 : 1;
        };
        _parpadeoPlay.Start();
    }

    /// <summary>Apaga el titileo del pad y restaura opacidad.</summary>
    private void ApagarParpadeoPlay()
    {
        _parpadeoPlay?.Stop();
        _parpadeoPlay = null;
        PlayButton.Opacity = 1;
    }

    /// <summary>Parpadea Stop al pulsar (2 pulsos, solo opacidad).</summary>
    /// <param name="sender">Botón Stop.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_parpadeoStop is not null)
        {
            return;
        }

        _faseStop = true;
        _pasosStop = 0;
        StopButton.Opacity = 0.35;
        _parpadeoStop = DispatcherQueue.CreateTimer();
        _parpadeoStop.Interval = TimeSpan.FromMilliseconds(150);
        _parpadeoStop.Tick += (_, _) =>
        {
            _faseStop = !_faseStop;
            StopButton.Opacity = _faseStop ? 0.35 : 1;
            if (++_pasosStop >= 4)
            {
                ApagarParpadeoStop();
            }
        };
        _parpadeoStop.Start();
    }

    /// <summary>Apaga el parpadeo de Stop y restaura opacidad.</summary>
    private void ApagarParpadeoStop()
    {
        _parpadeoStop?.Stop();
        _parpadeoStop = null;
        StopButton.Opacity = 1;
    }

    /// <summary>Enciende o apaga el titileo según Repeat armado.</summary>
    private void RefreshRepeatBlink()
    {
        if (ViewModel.RepeatArmed)
        {
            EncenderParpadeoRepeat();
        }
        else
        {
            ApagarParpadeoRepeat();
        }
    }

    /// <summary>Parpadea Repeat armado (opacidad 1 ↔ 0.35 cada 450 ms).</summary>
    private void EncenderParpadeoRepeat()
    {
        if (_parpadeoRepeat is not null)
        {
            return;
        }

        _faseRepeat = true;
        RepeatButton.Opacity = 0.35;
        _parpadeoRepeat = DispatcherQueue.CreateTimer();
        _parpadeoRepeat.Interval = IntervaloParpadeo;
        _parpadeoRepeat.Tick += (_, _) =>
        {
            _faseRepeat = !_faseRepeat;
            RepeatButton.Opacity = _faseRepeat ? 0.35 : 1;
        };
        _parpadeoRepeat.Start();
    }

    /// <summary>Apaga el titileo de Repeat y restaura opacidad.</summary>
    private void ApagarParpadeoRepeat()
    {
        _parpadeoRepeat?.Stop();
        _parpadeoRepeat = null;
        RepeatButton.Opacity = 1;
    }

    /// <summary>Enciende o apaga el parpadeo según el armado.</summary>
    private void RefreshParpadeo()
    {
        if (ViewModel.StopAtEndArmed)
        {
            EncenderParpadeo();
        }
        else
        {
            ApagarParpadeo();
        }
    }

    /// <summary>Parpadea el botón 🏁 (opacidad 1 ↔ 0.35 cada 450 ms).</summary>
    private void EncenderParpadeo()
    {
        if (_parpadeo is not null)
        {
            return;
        }

        _faseParpadeo = false;
        _parpadeo = DispatcherQueue.CreateTimer();
        _parpadeo.Interval = IntervaloParpadeo;
        _parpadeo.Tick += (_, _) =>
        {
            _faseParpadeo = !_faseParpadeo;
            StopAtEndButton.Opacity = _faseParpadeo ? 0.35 : 1;
        };
        _parpadeo.Start();
    }

    /// <summary>Apaga el parpadeo y restaura opacidad.</summary>
    private void ApagarParpadeo()
    {
        _parpadeo?.Stop();
        _parpadeo = null;
        StopAtEndButton.Opacity = 1;
    }

}
