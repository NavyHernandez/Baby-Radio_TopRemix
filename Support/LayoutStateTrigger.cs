using Microsoft.UI.Xaml;

namespace BebeRadio.Support;

/// <summary>
/// Trigger de estado de layout: activa un estado visual cuando el ancho
/// de la ventana coincide con el estado especificado (Compact, Medium,
/// Expanded). Se usa con <see cref="AdaptiveTriggerHelper"/> para detectar
/// cambios de tamaño.
/// </summary>
/// <remarks>
/// <para>
/// Uso típico en XAML (dentro de un <c>VisualState.StateTriggers</c>):
/// <c>&lt;local:LayoutStateTrigger State="Expanded" /&gt;</c>.
/// </para>
/// <para>
/// Precondición: este trigger NO escucha la ventana por sí mismo. Quien
/// posea el <see cref="AdaptiveTriggerHelper"/> (code-behind de la vista o
/// del control) debe llamar a <see cref="Connect"/> en cada trigger; mientras
/// no se conecte, <see cref="StateTriggerBase.IsActive"/> permanece en
/// <c>false</c> y el estado visual nunca se aplica. Al descargar la vista,
/// llamar a <see cref="Disconnect"/> para soltar el evento del helper.
/// </para>
/// <para>
/// Alcance: los estados se calculan por ANCHO. El alto de los controles
/// (p. ej. la tira de acciones inferior) NO depende de este trigger: se
/// reparte con filas proporcionales en <c>Phase1ShowcaseView.xaml</c>.
/// </para>
/// </remarks>
public sealed class LayoutStateTrigger : StateTriggerBase
{
    /// <summary>
    /// Helper de layout al que está conectado el trigger; <c>null</c> mientras
    /// no se haya llamado a <see cref="Connect"/> (o tras <see cref="Disconnect"/>).
    /// </summary>
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
    /// <remarks>
    /// Valor por defecto: <see cref="LayoutState.Medium"/> (tamaño intermedio,
    /// el de referencia del diseño).
    /// </remarks>
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(LayoutState),
            typeof(LayoutStateTrigger),
            new PropertyMetadata(LayoutState.Medium, OnStateChanged));

    /// <summary>
    /// Se llama cuando cambia la propiedad <see cref="State"/>: reevalúa si el
    /// trigger debe quedar activo con el nuevo estado objetivo.
    /// </summary>
    /// <param name="d">Trigger cuya propiedad cambió.</param>
    /// <param name="e">Datos del cambio (valor viejo y nuevo).</param>
    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var trigger = (LayoutStateTrigger)d;
        trigger.UpdateIsActive();
    }

    /// <summary>
    /// Conecta el trigger con el helper de la ventana y evalúa de inmediato el
    /// estado actual (por si el helper ya había calculado uno antes de conectar).
    /// </summary>
    /// <param name="helper">Helper de layout adaptativo.</param>
    /// <remarks>
    /// Si el trigger ya estaba conectado a otro helper, primero se desengancha
    /// del anterior para no duplicar suscripciones ni dejar fugas de memoria.
    /// </remarks>
    public void Connect(AdaptiveTriggerHelper helper)
    {
        // Evitar suscripciones duplicadas si se reconecta sin Disconnect previo.
        Disconnect();

        _helper = helper;
        _helper.StateChanged += OnHelperStateChanged;
        UpdateIsActive();
    }

    /// <summary>
    /// Desconecta el trigger del helper (idempotente: seguro de llamar varias veces).
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
    /// Actualiza <see cref="StateTriggerBase.IsActive"/> cuando el helper
    /// notifica un cambio de estado de layout.
    /// </summary>
    /// <param name="sender">Helper que originó el evento.</param>
    /// <param name="state">Nuevo estado de layout vigente.</param>
    private void OnHelperStateChanged(object? sender, LayoutState state)
    {
        UpdateIsActive();
    }

    /// <summary>
    /// Evalúa si el trigger debe estar activo: solo cuando el estado actual
    /// del helper coincide con <see cref="State"/>. Sin helper conectado no hace nada.
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
