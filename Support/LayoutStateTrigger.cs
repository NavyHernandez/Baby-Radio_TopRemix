using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Trigger de estado de layout: activa un estado visual cuando el ancho
/// de la ventana coincide con el estado especificado. Se usa con
/// AdaptiveTriggerHelper para detectar cambios de tamaño.
/// </summary>
public sealed class LayoutStateTrigger : StateTriggerBase
{
    private AdaptiveTriggerHelper? _helper;

    /// <summary>
    /// Estado de layout que este trigger debe activar.
    /// </summary>
    public LayoutState State
    {
        get => (LayoutState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Propiedad de dependencia para el estado de layout.
    /// </summary>
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(LayoutState),
            typeof(LayoutStateTrigger),
            new PropertyMetadata(LayoutState.Medium, OnStateChanged));

    /// <summary>
    /// Se llama cuando cambia la propiedad State.
    /// </summary>
    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var trigger = (LayoutStateTrigger)d;
        trigger.UpdateIsActive();
    }

    /// <summary>
    /// Conecta el trigger con el helper de la ventana.
    /// </summary>
    /// <param name="helper">Helper de layout adaptativo.</param>
    public void Connect(AdaptiveTriggerHelper helper)
    {
        _helper = helper;
        _helper.StateChanged += OnHelperStateChanged;
        UpdateIsActive();
    }

    /// <summary>
    /// Desconecta el trigger del helper.
    /// </summary>
    public void Disconnect()
    {
        if (_helper is not null)
        {
            _helper.StateChanged -= OnHelperStateChanged;
            _helper = null;
        }
    }

    /// <summary>
    /// Actualiza IsActive cuando cambia el estado del helper.
    /// </summary>
    private void OnHelperStateChanged(object? sender, LayoutState state)
    {
        UpdateIsActive();
    }

    /// <summary>
    /// Evalúa si el trigger debe estar activo según el estado actual.
    /// </summary>
    private void UpdateIsActive()
    {
        if (_helper is null)
        {
            return;
        }

        SetActive(_helper.CurrentState == State);
    }
}
