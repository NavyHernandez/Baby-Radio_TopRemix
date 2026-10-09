using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Estados de layout adaptativo según el ancho de la ventana.
/// </summary>
public enum LayoutState
{
    Compact,
    Medium,
    Expanded
}

/// <summary>
/// Helper de layout adaptativo: detecta el ancho de la ventana y expone
/// el estado actual (Compact/Medium/Expanded) para que el VisualStateManager
/// reajuste los tamaños de columnas, carts y botones.
/// </summary>
public sealed class AdaptiveTriggerHelper
{
    private readonly Window _window;
    private LayoutState _currentState;

    /// <summary>
    /// Ancho mínimo para el estado Expanded (por defecto 1600px).
    /// </summary>
    public double ExpandedMinWidth { get; set; } = 1600;

    /// <summary>
    /// Ancho mínimo para el estado Medium (por defecto 1200px).
    /// </summary>
    public double MediumMinWidth { get; set; } = 1200;

    /// <summary>
    /// Estado actual del layout.
    /// </summary>
    public LayoutState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Se dispara cuando cambia el estado del layout.
    /// </summary>
    public event EventHandler<LayoutState>? StateChanged;

    /// <summary>
    /// Inicializa el helper y se suscribe al SizeChanged de la ventana.
    /// </summary>
    /// <param name="window">Ventana a observar.</param>
    public AdaptiveTriggerHelper(Window window)
    {
        _window = window;
        _window.SizeChanged += OnSizeChanged;
        UpdateState();
    }

    /// <summary>
    /// Limpia la suscripción al SizeChanged.
    /// </summary>
    public void Dispose()
    {
        _window.SizeChanged -= OnSizeChanged;
    }

    /// <summary>
    /// Actualiza el estado según el ancho actual de la ventana.
    /// </summary>
    private void UpdateState()
    {
        var width = _window.Bounds.Width;
        if (width >= ExpandedMinWidth)
        {
            CurrentState = LayoutState.Expanded;
        }
        else if (width >= MediumMinWidth)
        {
            CurrentState = LayoutState.Medium;
        }
        else
        {
            CurrentState = LayoutState.Compact;
        }
    }

    /// <summary>
    /// Maneja el cambio de tamaño de la ventana.
    /// </summary>
    private void OnSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        UpdateState();
    }
}
