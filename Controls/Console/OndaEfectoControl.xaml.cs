using System.Numerics;
using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Onda del efecto con cues arrastrables y pre-escucha.
/// Se vincula al <see cref="PaletteItem"/> borrador y le escribe
/// <c>CueInicio/CueFin</c> directo (fin al final = null).
/// </summary>
public sealed partial class OndaEfectoControl : UserControl
{
    private const double MinRegionSeg = 0.1;
    private const double MargenMarcadorPx = 10;

    private PaletteItem? _borrador;
    private float[] _picos = Array.Empty<float>();
    private CancellationTokenSource? _carga;
    private readonly MotorAudio _motor = MotorAudio.Instancia;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _playhead;
    private TimeSpan? _posicion;
    private Brush _acento;
    private Arrastrando _arrastre;
    private bool _dibujoReportado;

    /// <summary>Interruptor diagnóstico: sin onda (aisla crash del editor).</summary>
    /// <remarks>En false la onda funciona normal (blindada con try/catch + log).</remarks>
    public static bool DesactivarOnda { get; set; }

    private enum Arrastrando
    {
        Nada,
        Inicio,
        Fin,
    }

    /// <summary>Crea el control y suscribe dibujo + audio.</summary>
    public OndaEfectoControl()
    {
        InitializeComponent();
        _acento = ObtenerBrocha("DigitalAmber", Colors.Orange);
        OndaCanvas.PointerPressed += OnPointerPressed;
        OndaCanvas.PointerMoved += OnPointerMoved;
        OndaCanvas.PointerReleased += OnPointerReleased;
        OndaCanvas.SizeChanged += (_, _) => OndaCanvas.Invalidate();
        _motor.TerminadoNatural += AlTerminarTramo;
        Unloaded += (_, _) => Liberar();
    }

    /// <summary>Vincula el borrador (calcula picos y refresca).</summary>
    /// <param name="borrador">Borrador del diálogo.</param>
    /// <param name="acento">Brush de la categoría (opcional).</param>
    public void Vincular(PaletteItem borrador, Brush? acento = null)
    {
        _carga?.Cancel();
        _borrador = borrador;
        _posicion = null;
        if (acento is not null)
        {
            _acento = acento;
        }

        if (DesactivarOnda)
        {
            _picos = Array.Empty<float>();
            OndaCanvas.Visibility = Visibility.Collapsed;
            MostrarEstado();
            return;
        }

        OndaCanvas.Visibility = Visibility.Visible;
        RefrescarLecturas();
        if (string.IsNullOrWhiteSpace(borrador.FilePath) || !System.IO.File.Exists(borrador.FilePath))
        {
            _picos = Array.Empty<float>();
            MostrarEstado();
            OndaCanvas.Invalidate();
            return;
        }

        Cargando.Visibility = Visibility.Visible;
        VacioText.Visibility = Visibility.Collapsed;
        PlayBoton.IsEnabled = false;
        var fuente = new CancellationTokenSource();
        _carga = fuente;
        var ruta = borrador.FilePath;
        _ = Task.Run(async () =>
        {
            float[] picos;
            try
            {
                picos = await OndaPicos.CalcularAsync(ruta, ct: fuente.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "Onda.Picos");
                picos = Array.Empty<float>();
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (_carga != fuente)
                {
                    return;
                }

                _picos = picos;
                MostrarEstado();
                OndaCanvas.Invalidate();
            });
        });
    }

    /// <summary>Corta el tramo y cancela carga (al cerrar el diálogo).</summary>
    public void Liberar()
    {
        _carga?.Cancel();
        _playhead?.Stop();
        if (_motor.Modo == ModoMotor.Tramo)
        {
            RegistroErrores.Traza("Onda.Liberar", "corta tramo al cerrar");
            _motor.Detener();
        }
    }

    /// <summary>Dibuja onda, región, marcadores y playhead (sin alocaciones).</summary>
    /// <param name="sender">Canvas Win2D.</param>
    /// <param name="args">Args de dibujo.</param>
    /// <remarks>Blindado: un fallo dibuja la base y se registra una vez.</remarks>
    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        try
        {
            Dibujar(sender, args);
        }
        catch (Exception ex)
        {
            if (!_dibujoReportado)
            {
                _dibujoReportado = true;
                RegistroErrores.Registrar(ex, "Onda.Dibujar");
            }
        }
    }

    /// <summary>Cuerpo del dibujo (ver <see cref="OnDraw"/>).</summary>
    /// <param name="sender">Canvas Win2D.</param>
    /// <param name="args">Args de dibujo.</param>
    private void Dibujar(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var sesion = args.DrawingSession;
        var ancho = (float)sender.ActualWidth;
        var alto = (float)sender.ActualHeight;
        if (ancho <= 0 || _borrador is null)
        {
            return;
        }

        var total = _borrador.Duration.TotalSeconds;
        var inicioX = (float)(InicioSeg() / Math.Max(total, 0.001) * ancho);
        var finX = (float)(FinSeg() / Math.Max(total, 0.001) * ancho);
        var centro = alto / 2;

        var recursos = Application.Current?.Resources;
        var atenuado = ObtenerColor(recursos, "TextDisabledColor", Colors.Gray);
        var linea = ObtenerColor(recursos, "BorderSubtleColor", Colors.Gray);
        var inicioColor = Colors.White;
        var finColor = ObtenerColor(recursos, "StateOnAirColor", Colors.Red);
        if (_acento is SolidColorBrush solido)
        {
            inicioColor = solido.Color;
        }

        // Barras: vivas en la región, tenues fuera.
        if (_picos.Length > 0 && total > 0)
        {
            var paso = ancho / _picos.Length;
            for (var i = 0; i < _picos.Length; i++)
            {
                var x = i * paso + paso / 2;
                var altura = Math.Max(1, _picos[i] * (alto - 8));
                var dentro = x >= inicioX && x <= finX;
                var color = dentro ? inicioColor : atenuado;
                sesion.DrawLine(
                    new Vector2(x, centro - altura / 2),
                    new Vector2(x, centro + altura / 2),
                    Color.FromArgb((byte)(dentro ? 230 : 110), color.R, color.G, color.B),
                    Math.Max(1, paso - 0.5f));
            }
        }
        else
        {
            sesion.DrawLine(new Vector2(0, centro), new Vector2(ancho, centro), linea, 1);
        }

        // Región sombreada inicio→fin.
        sesion.FillRectangle(
            new Windows.Foundation.Rect(inicioX, 0, Math.Max(0, finX - inicioX), alto),
            Color.FromArgb(28, inicioColor.R, inicioColor.G, inicioColor.B));

        // Marcadores.
        sesion.DrawLine(new Vector2(inicioX, 0), new Vector2(inicioX, alto), inicioColor, 2);
        sesion.DrawLine(new Vector2(finX, 0), new Vector2(finX, alto), finColor, 2);

        // Playhead.
        if (_posicion.HasValue && total > 0)
        {
            var x = (float)(_posicion.Value.TotalSeconds / total * ancho);
            sesion.DrawLine(new Vector2(x, 0), new Vector2(x, alto), Colors.White, 1.5f);
        }
    }

    /// <summary>Lee un Color de recursos con fallback (nunca lanza).</summary>
    /// <param name="recursos">Diccionario o null.</param>
    /// <param name="clave">Clave del color.</param>
    /// <param name="reserva">Color si falta.</param>
    /// <returns>Color.</returns>
    private static Color ObtenerColor(Microsoft.UI.Xaml.ResourceDictionary? recursos, string clave, Color reserva) =>
        recursos is not null
        && recursos.TryGetValue(clave, out var valor)
        && valor is Color color
            ? color
            : reserva;

    /// <summary>Lee un Brush de recursos con fallback (nunca lanza).</summary>
    /// <param name="clave">Clave del brush.</param>
    /// <param name="reserva">Color si falta.</param>
    /// <returns>Brush.</returns>
    private static Brush ObtenerBrocha(string clave, Color reserva)
    {
        if (Application.Current?.Resources.TryGetValue(clave, out var valor) == true
            && valor is Brush brocha)
        {
            return brocha;
        }

        return new SolidColorBrush(reserva);
    }

    /// <summary>Inicia arrastre (marcador cercano o salto de inicio).</summary>
    /// <param name="sender">Canvas.</param>
    /// <param name="e">Args del puntero.</param>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_borrador is null || _picos.Length == 0)
        {
            return;
        }

        var punto = e.GetCurrentPoint(OndaCanvas);
        var ancho = OndaCanvas.ActualWidth;
        var total = Math.Max(_borrador.Duration.TotalSeconds, 0.001);
        var inicioX = InicioSeg() / total * ancho;
        var finX = FinSeg() / total * ancho;

        if (Math.Abs(punto.Position.X - finX) <= MargenMarcadorPx)
        {
            _arrastre = Arrastrando.Fin;
        }
        else if (Math.Abs(punto.Position.X - inicioX) <= MargenMarcadorPx)
        {
            _arrastre = Arrastrando.Inicio;
        }
        else
        {
            _arrastre = Arrastrando.Inicio;
            FijarInicio(punto.Position.X / ancho * total);
        }

        OndaCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>Arrastra el marcador con constraints (fin mayor que inicio).</summary>
    /// <param name="sender">Canvas.</param>
    /// <param name="e">Args del puntero.</param>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_arrastre == Arrastrando.Nada || _borrador is null)
        {
            return;
        }

        var punto = e.GetCurrentPoint(OndaCanvas);
        var total = Math.Max(_borrador.Duration.TotalSeconds, 0.001);
        var tiempo = Math.Clamp(punto.Position.X / OndaCanvas.ActualWidth * total, 0, total);
        if (_arrastre == Arrastrando.Inicio)
        {
            FijarInicio(Math.Min(tiempo, FinSeg() - MinRegionSeg));
        }
        else
        {
            FijarFin(Math.Max(tiempo, InicioSeg() + MinRegionSeg));
        }

        e.Handled = true;
    }

    /// <summary>Suelta el marcador.</summary>
    /// <param name="sender">Canvas.</param>
    /// <param name="e">Args del puntero.</param>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _arrastre = Arrastrando.Nada;
        OndaCanvas.ReleasePointerCapture(e.Pointer);
    }

    /// <summary>Alterna pre-escucha desde el inicio.</summary>
    /// <param name="sender">Botón play.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (_borrador is null)
        {
            return;
        }

        if (_motor.Reproduciendo && _motor.Modo == ModoMotor.Tramo)
        {
            RegistroErrores.Traza("Onda.PreEscucha", "detiene tramo (toggle)");
            _motor.Detener();
            return;
        }

        if (string.IsNullOrWhiteSpace(_borrador.FilePath))
        {
            return;
        }

        RegistroErrores.Traza("Onda.PreEscucha", $"toma salida (modo={_motor.Modo})");
        if (!_motor.ReproducirTramo(_borrador.FilePath, _borrador.CueInicio, _borrador.CueFin, _borrador.GananciaDb))
        {
            return;
        }

        PlayIcono.Symbol = FluentIcons.Common.Symbol.Pause;
        if (_playhead is null)
        {
            _playhead = DispatcherQueue.CreateTimer();
            _playhead.Interval = TimeSpan.FromMilliseconds(100);
            _playhead.Tick += (_, _) =>
            {
                if (_motor.Modo == ModoMotor.Tramo)
                {
                    _posicion = _motor.Posicion;
                    OndaCanvas.Invalidate();
                }
            };
        }

        _playhead.Start();
    }

    /// <summary>Refleja el fin del tramo en el botón (hilo UI).</summary>
    /// <param name="modo">Modo que terminó (solo atiende tramo).</param>
    private void AlTerminarTramo(ModoMotor modo)
    {
        if (modo != ModoMotor.Tramo)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _playhead?.Stop();
            _posicion = null;
            PlayIcono.Symbol = FluentIcons.Common.Symbol.Play;
            OndaCanvas.Invalidate();
        });
    }

    /// <summary>Fija el inicio (segundos, clamp).</summary>
    /// <param name="segundos">Tiempo.</param>
    private void FijarInicio(double segundos)
    {
        if (_borrador is null)
        {
            return;
        }

        _borrador.CueInicio = TimeSpan.FromSeconds(Math.Max(0, segundos));
        RefrescarLecturas();
        OndaCanvas.Invalidate();
    }

    /// <summary>Fija el fin (al final = null, hasta el final).</summary>
    /// <param name="segundos">Tiempo.</param>
    private void FijarFin(double segundos)
    {
        if (_borrador is null)
        {
            return;
        }

        var total = _borrador.Duration.TotalSeconds;
        _borrador.CueFin = segundos >= total - 0.05 ? null : TimeSpan.FromSeconds(segundos);
        RefrescarLecturas();
        OndaCanvas.Invalidate();
    }

    /// <summary>Actualiza lecturas y estado del play.</summary>
    private void RefrescarLecturas()
    {
        if (_borrador is null)
        {
            return;
        }

        InicioText.Text = $"{Fmt(InicioSeg())} → {Fmt(FinSeg())}";
        TotalText.Text = $"total {Fmt(_borrador.Duration.TotalSeconds)}";
        PlayBoton.IsEnabled = _borrador.TieneAudio;
    }

    /// <summary>Muestra vacío/listo tras calcular.</summary>
    private void MostrarEstado()
    {
        Cargando.Visibility = Visibility.Collapsed;
        var vacio = _picos.Length == 0;
        VacioText.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
        PlayBoton.IsEnabled = !vacio && _borrador?.TieneAudio == true;
        RefrescarLecturas();
    }

    /// <summary>Inicio actual en segundos.</summary>
    /// <returns>Segundos.</returns>
    private double InicioSeg() => _borrador?.CueInicio.TotalSeconds ?? 0;

    /// <summary>Fin actual en segundos (null = total).</summary>
    /// <returns>Segundos.</returns>
    private double FinSeg() =>
        _borrador is null ? 0 : (_borrador.CueFin ?? _borrador.Duration).TotalSeconds;

    /// <summary>Formatea segundos como m:ss.d.</summary>
    /// <param name="segundos">Tiempo.</param>
    /// <returns>Cadena.</returns>
    private static string Fmt(double segundos)
    {
        var total = Math.Max(0, segundos);
        return $"{(int)(total / 60)}:{(int)(total % 60):D2}.{(int)((total % 1) * 10)}";
    }
}
