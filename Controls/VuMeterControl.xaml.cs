using BebeRadio.Support;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace BebeRadio.Controls;

/// <summary>
/// VU meter estéreo estilo consola: barras segmentadas, escala dB (−60…0),
/// balística pro (ataque instantáneo, release suave) y pico retenido.
/// Render a 60 fps con vsync vía <c>CanvasAnimatedControl</c>.
/// Fuente: <c>SetLevels(l, r)</c> con niveles CRUDOS (el suavizado vive aquí).
/// </summary>
public sealed partial class VuMeterControl : UserControl
{
    private const double BarHeight = 6;
    private const double BarGap = 4;
    private const double BarCornerRadius = 2;
    private const double LabelWidth = 14;
    private const double SegmentStep = 7;
    private const float BarsTop = 2;

    /// <summary>Umbral de silencio para reposar el canvas (0–1).</summary>
    private const double Epsilon = 0.0005;

    private static readonly double[] ScaleMarksDb = { -40, -20, -12, -6, 0 };

    private double _targetLeft;
    private double _targetRight;
    private double _displayLeft;
    private double _displayRight;
    private double _peakLeft;
    private double _peakRight;
    private double _peakAgeLeft;
    private double _peakAgeRight;
    private bool _reposando;

    private CanvasLinearGradientBrush? _gradient;
    private float _gradientWidth;
    private readonly CanvasTextFormat _scaleText = new()
    {
        FontSize = 7,
        HorizontalAlignment = CanvasHorizontalAlignment.Center,
    };

    private Color _trackColor;
    private Color _peakColor;
    private Color _textColor;
    private Color _tickColor;
    private Color _green;
    private Color _yellow;
    private Color _red;

    /// <summary>Inicializa el control; el loop vsync lo maneja Win2D.</summary>
    public VuMeterControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshPalette();
            TemaConsola.Cambio += OnTemaCambiado;
            _reposando = false;
            VuCanvas.Paused = false;
        };
        Unloaded += (_, _) =>
        {
            TemaConsola.Cambio -= OnTemaCambiado;
            _reposando = true;
            VuCanvas.Paused = true;
        };
    }

    /// <summary>
    /// Fija los niveles objetivo CRUDOS (0–1). Sin inercia aquí: cada frame
    /// suaviza con delta real. En Fase 2 la llamará el motor NAudio.
    /// Reanuda el render si estaba en reposo (ahorro de CPU en silencio).
    /// </summary>
    /// <param name="left">Nivel izquierdo 0–1.</param>
    /// <param name="right">Nivel derecho 0–1.</param>
    public void SetLevels(double left, double right)
    {
        _targetLeft = Clamp01(left);
        _targetRight = Clamp01(right);
        if (_reposando && (_targetLeft > Epsilon || _targetRight > Epsilon))
        {
            _reposando = false;
            VuCanvas.Paused = false;
        }
    }

    /// <summary>Avanza display y picos con el delta real del vsync.</summary>
    /// <param name="sender">Canvas animado.</param>
    /// <param name="args">Timing con ElapsedTime.</param>
    private void OnUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
    {
        var delta = Math.Min(0.25, args.Timing.ElapsedTime.TotalSeconds);
        var goalLeft = VuDynamics.LinearToDbNormalized(_targetLeft);
        var goalRight = VuDynamics.LinearToDbNormalized(_targetRight);

        _displayLeft = VuDynamics.SmoothDisplay(_displayLeft, goalLeft, delta);
        _displayRight = VuDynamics.SmoothDisplay(_displayRight, goalRight, delta);
        (_peakLeft, _peakAgeLeft) = VuDynamics.UpdatePeak(_peakLeft, _peakAgeLeft, _displayLeft, delta);
        (_peakRight, _peakAgeRight) = VuDynamics.UpdatePeak(_peakRight, _peakAgeRight, _displayRight, delta);

        // Reposo: sin señal objetivo y todo asentado en cero, se pausa el canvas
        // (deja de dibujar a 60 fps y libera GPU/CPU hasta la próxima actividad).
        var quiescente = _targetLeft <= Epsilon && _targetRight <= Epsilon
            && _displayLeft <= Epsilon && _displayRight <= Epsilon
            && _peakLeft <= Epsilon && _peakRight <= Epsilon;
        if (quiescente && !_reposando)
        {
            _reposando = true;
            sender.Paused = true;
        }
    }

    /// <summary>Recachea paleta al cargar o cambiar tamaño.</summary>
    /// <param name="sender">Origen.</param>
    /// <param name="e">Args.</param>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => RefreshPalette();

    /// <summary>Recachea el gradiente al aplicar el tema.</summary>
    private void OnTemaCambiado() => RefreshPalette();

    /// <summary>Dibuja etiquetas L/R, barras segmentadas, picos y escala dB.</summary>
    /// <param name="sender">Canvas animado.</param>
    /// <param name="args">Sesión de dibujo.</param>
    private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        var session = args.DrawingSession;
        var width = (float)sender.Size.Width;
        if (width <= LabelWidth + 10)
        {
            return;
        }

        EnsureGradient(session, width);
        var barX = (float)LabelWidth;
        var barW = width - barX;

        session.DrawText("L", new Windows.Foundation.Rect(0, (float)BarsTop - 2, (float)LabelWidth, 12), _textColor, _scaleText);
        session.DrawText("R", new Windows.Foundation.Rect(0, (float)(BarsTop + BarHeight + BarGap) - 2, (float)LabelWidth, 12), _textColor, _scaleText);

        DrawChannel(session, barX, barW, (float)BarsTop, _displayLeft, _peakLeft);
        DrawChannel(session, barX, barW, (float)(BarsTop + BarHeight + BarGap), _displayRight, _peakRight);
        DrawScale(session, barX, barW, (float)(BarsTop + (BarHeight * 2) + BarGap + 2));
    }

    /// <summary>Dibuja un canal: pista, relleno segmentado y línea de pico.</summary>
    /// <param name="session">Sesión.</param>
    /// <param name="x">Origen horizontal de barras.</param>
    /// <param name="width">Ancho de barras.</param>
    /// <param name="top">Offset vertical.</param>
    /// <param name="level">Nivel en escala dB 0–1.</param>
    /// <param name="peak">Pico retenido 0–1.</param>
    private void DrawChannel(
        CanvasDrawingSession session, float x, float width, float top, double level, double peak)
    {
        var height = (float)BarHeight;
        var radius = (float)BarCornerRadius;
        session.FillRoundedRectangle(x, top, width, height, radius, radius, _trackColor);

        var fillWidth = (float)(width * Clamp01(level));
        if (fillWidth > 0 && _gradient is not null)
        {
            session.FillRectangle(x, top, fillWidth, height, _gradient);

            // Segmentos LED: ranuras del color de pista sobre el relleno.
            for (var cut = x + (float)SegmentStep; cut < x + fillWidth; cut += (float)SegmentStep)
            {
                session.FillRectangle(cut, top, 1.5f, height, _trackColor);
            }
        }

        var peakX = x + (float)(width * Clamp01(peak));
        if (peakX > x + 1)
        {
            session.FillRectangle(peakX - 1, top, 2, height, _peakColor);
        }
    }

    /// <summary>Dibuja ticks y etiquetas de la escala dB.</summary>
    /// <param name="session">Sesión.</param>
    /// <param name="x">Origen horizontal de barras.</param>
    /// <param name="width">Ancho de barras.</param>
    /// <param name="top">Offset vertical de la escala.</param>
    private void DrawScale(CanvasDrawingSession session, float x, float width, float top)
    {
        foreach (var mark in ScaleMarksDb)
        {
            var norm = VuDynamics.LinearToDbNormalized(VuDynamics.DbToLinear(mark));
            var tickX = x + (float)(width * norm);
            var isZero = mark == 0;
            session.FillRectangle(tickX, top, 1, 3, isZero ? _red : _tickColor);
            if (mark is 0 or -20 or -40)
            {
                // El 0 cae en el borde derecho: su caja se recorre a la
                // izquierda para no dibujarse fuera del canvas (recorte).
                var labelX = isZero ? tickX - 25 : tickX - 12;
                session.DrawText(
                    mark.ToString(),
                    new Windows.Foundation.Rect(labelX, top + 3, 24, 8),
                    _textColor,
                    _scaleText);
            }
        }
    }

    /// <summary>Lee la paleta global a campos (evita lookups por frame).</summary>
    private void RefreshPalette()
    {
        _trackColor = ResourceColor("VuTrackColor", Color.FromArgb(255, 0x10, 0x12, 0x14));
        _peakColor = ResourceColor("VuPeakColor", Color.FromArgb(255, 255, 255, 255));
        // Etiquetas fijas claras: el VU es display empotrado oscuro en ambos temas.
        _textColor = Color.FromArgb(255, 0xA1, 0xA7, 0xB3);
        _tickColor = Color.FromArgb(255, 0x5A, 0x60, 0x6B);
        _green = ResourceColor("VuGreenColor", Color.FromArgb(255, 0x30, 0xD1, 0x58));
        _yellow = ResourceColor("VuYellowColor", Color.FromArgb(255, 0xFF, 0xD6, 0x0A));
        _red = ResourceColor("VuRedColor", Color.FromArgb(255, 0xFF, 0x45, 0x3A));
        _gradient?.Dispose();
        _gradient = null;
    }

    /// <summary>Crea (una vez por tamaño) el gradiente verde→amarillo→rojo.</summary>
    /// <param name="session">Sesión para crear recursos.</param>
    /// <param name="width">Ancho total del canvas.</param>
    private void EnsureGradient(ICanvasResourceCreator session, float width)
    {
        if (_gradient is not null && Math.Abs(_gradientWidth - width) < 1)
        {
            return;
        }

        var barX = (float)LabelWidth;
        var barW = width - barX;
        _gradient?.Dispose();
        _gradient = new CanvasLinearGradientBrush(
            session,
            new[]
            {
                new CanvasGradientStop { Position = 0f, Color = _green },
                new CanvasGradientStop { Position = 0.72f, Color = _green },
                new CanvasGradientStop { Position = 0.85f, Color = _yellow },
                new CanvasGradientStop { Position = 0.96f, Color = _red },
                new CanvasGradientStop { Position = 1f, Color = _red },
            });
        _gradient.StartPoint = new System.Numerics.Vector2(barX, 0);
        _gradient.EndPoint = new System.Numerics.Vector2(barX + barW, 0);
        _gradientWidth = width;
    }

    /// <summary>Lee un color de la paleta global.</summary>
    /// <param name="key">Clave del recurso.</param>
    /// <param name="fallback">Valor si no existe.</param>
    /// <returns>Color de paleta o fallback.</returns>
    private static Color ResourceColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    /// <summary>Limita un valor al rango 0–1.</summary>
    /// <param name="value">Valor de entrada.</param>
    /// <returns>Valor recortado.</returns>
    private static double Clamp01(double value) =>
        value < 0 ? 0 : (value > 1 ? 1 : value);
}
