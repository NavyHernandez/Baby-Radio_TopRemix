using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BebeRadio.Models;
using BebeRadio.Services;
using BebeRadio.Support;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Diálogo para configurar el mapeo de tecla global de un slot de paleta.
/// Click en área → titileo 10s máx (cian) + beep → presiona tecla → configura → Guardar.
/// Botón "Limpiar" compacto solo visible si hay mapeo.
/// </summary>
public sealed partial class PaletaMapeoDialog : ContentDialog, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly PaletteItem _item;
    private readonly string _propietariaId;
    private readonly KeyMappingService _keyMapping = KeyMappingService.Instancia;

    private VirtualKey _capturedKey = VirtualKey.None;
    private VirtualKeyModifiers _capturedModifiers = VirtualKeyModifiers.None;
    private bool _capturing;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _blinkTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()!;
    private const int CaptureTimeoutMs = 10_000; // 10 segundos
    private const int BlinkIntervalMs = 500;

    /// <summary>Título del slot para mostrar en el diálogo.</summary>
    public string TituloSlot => _item.Title;

    /// <summary>Descripción: categoría + índice.</summary>
    public string DescripcionSlot => $"Categoría: {_propietariaId} · Slot {_item.SlotIndex + 1}";

    /// <summary>True si el slot ya tiene un mapeo (para mostrar/ocultar botón Limpiar).</summary>
    public bool TieneMapeoActual => _item.MapeoTecla != VirtualKey.None;

    /// <summary>Bindable: Ctrl modifier.</summary>
    public bool ModCtrl
    {
        get => _capturedModifiers.HasFlag(VirtualKeyModifiers.Control);
        set => ActualizarModifier(VirtualKeyModifiers.Control, value);
    }

    /// <summary>Bindable: Alt modifier.</summary>
    public bool ModAlt
    {
        get => _capturedModifiers.HasFlag(VirtualKeyModifiers.Menu);
        set => ActualizarModifier(VirtualKeyModifiers.Menu, value);
    }

    /// <summary>Bindable: Shift modifier.</summary>
    public bool ModShift
    {
        get => _capturedModifiers.HasFlag(VirtualKeyModifiers.Shift);
        set => ActualizarModifier(VirtualKeyModifiers.Shift, value);
    }

    /// <summary>Bindable: Win modifier.</summary>
    public bool ModWin
    {
        get => _capturedModifiers.HasFlag(VirtualKeyModifiers.Windows);
        set => ActualizarModifier(VirtualKeyModifiers.Windows, value);
    }

    /// <summary>Bindable: índice del modo seleccionado (0=Toggle, 1=Retrigger, 2=Momentaneo).</summary>
    public int ModoIndex
    {
        get => (int)_item.MapeoModo;
        set => _item.MapeoModo = (ModoMapeo)value;
    }

    /// <summary>Crea el diálogo para un slot.</summary>
    /// <param name="item">Slot a configurar.</param>
    /// <param name="propietariaId">Dueña de la paleta.</param>
    public PaletaMapeoDialog(PaletteItem item, string propietariaId)
    {
        _item = item;
        _propietariaId = propietariaId;
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);

        // Cargar valores actuales del item
        _capturedKey = _item.MapeoTecla;
        _capturedModifiers = _item.MapeoModifiers;
        ModoCombo.SelectedIndex = (int)_item.MapeoModo;

        ActualizarCaptureText();
        ValidarConflictos();
        NotifyPropertyChanged(nameof(TieneMapeoActual));

        // Cleanup al cerrar
        Closing += (_, _) => DetenerCaptura();
    }

    private void ActualizarModifier(VirtualKeyModifiers flag, bool activo)
    {
        if (activo)
        {
            _capturedModifiers |= flag;
        }
        else
        {
            _capturedModifiers &= ~flag;
        }
        ActualizarCaptureText();
        ValidarConflictos();
    }

    private void ActualizarCaptureText()
    {
        var parts = new List<string>();

        if (_capturedModifiers.HasFlag(VirtualKeyModifiers.Control)) parts.Add("Ctrl");
        if (_capturedModifiers.HasFlag(VirtualKeyModifiers.Menu)) parts.Add("Alt");
        if (_capturedModifiers.HasFlag(VirtualKeyModifiers.Shift)) parts.Add("Shift");
        if (_capturedModifiers.HasFlag(VirtualKeyModifiers.Windows)) parts.Add("Win");

        if (_capturedKey != VirtualKey.None)
        {
            parts.Add(_capturedKey.ToString());
        }

        CaptureText.Text = parts.Count > 0 ? string.Join(" + ", parts) : "Haz clic y presiona una tecla...";
    }

    private void ValidarConflictos()
    {
        var conflicto = _keyMapping.HayConflicto(_propietariaId, _item.SlotIndex, _capturedKey, _capturedModifiers);
        ConflictWarning.Visibility = conflicto ? Visibility.Visible : Visibility.Collapsed;
        if (conflicto)
        {
            ConflictWarning.Text = "⚠ Esta combinación ya está en uso por el sistema, otra aplicación u otro slot. Elige otra tecla.";
        }
    }

    /// <summary>Click en área de captura: inicia modo captura con titileo y beep.</summary>
    private void OnCapturePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        CaptureBorder.Focus(FocusState.Programmatic);
        IniciarCaptura();
        e.Handled = true;
    }

    private void OnCaptureGotFocus(object sender, RoutedEventArgs e)
    {
        if (!_capturing)
        {
            IniciarCaptura();
        }
    }

    private void OnCaptureLostFocus(object sender, RoutedEventArgs e)
    {
        // No detener captura al perder foco; solo si timeout o tecla válida
    }

    /// <summary>Inicia captura: titileo border cian + beep + timer 10s.</summary>
    private void IniciarCaptura()
    {
        if (_capturing) return;

        // Validar que el slot tenga audio
        if (!_item.TieneAudio)
        {
            _ = MostrarAvisoSlotVacioAsync();
            return;
        }

        _capturing = true;

        // Beep de confirmación (.wav)
        try { SonidoService.Instancia.Reproducir(); } catch { /* ignore */ }

        // Titileo visual: alterna border cian (CategoryMusic) cada 500ms
        var isHighlighted = false;
        _blinkTimer = _uiQueue.CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(BlinkIntervalMs);
        _blinkTimer.Tick += (_, _) =>
        {
            if (!_capturing)
            {
                _blinkTimer?.Stop();
                _blinkTimer = null;
                return;
            }

            isHighlighted = !isHighlighted;
            var cianBrush = Application.Current.Resources["CategoryMusic"] as Brush;
            var subtleBrush = Application.Current.Resources["BorderSubtle"] as Brush;

            _uiQueue.TryEnqueue(() =>
            {
                CaptureBorder.BorderBrush = isHighlighted ? cianBrush : subtleBrush;
                CaptureBorder.BorderThickness = new Thickness(isHighlighted ? 3 : 1);
            });
        };
        _blinkTimer.Start();

        // Auto-timeout 10s
        _ = Task.Delay(CaptureTimeoutMs).ContinueWith(_ =>
        {
            if (_capturing)
            {
                _uiQueue.TryEnqueue(DetenerCaptura);
            }
        });
    }

    /// <summary>Muestra aviso de slot vacío y cancela captura.</summary>
    private async Task MostrarAvisoSlotVacioAsync()
    {
        try
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = "Slot vacío",
                Content = "Este slot no tiene audio cargado.\nArrastre un archivo o use «Cargar audio…» en el menú contextual antes de asignar un mapeo.",
                CloseButtonText = "Entendido",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "PaletaMapeo.SlotVacio");
        }
    }

    /// <summary>Detiene captura: restaura border normal, para titileo.</summary>
    private void DetenerCaptura()
    {
        if (!_capturing) return;

        _capturing = false;
        _blinkTimer?.Stop();
        _blinkTimer = null;

        var subtleBrush = Application.Current.Resources["BorderSubtle"] as Brush;
        _uiQueue.TryEnqueue(() =>
        {
            CaptureBorder.BorderBrush = subtleBrush;
            CaptureBorder.BorderThickness = new Thickness(1);
        });
    }

    /// <summary>Tecla presionada durante captura.</summary>
    private void OnCaptureKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_capturing) return;

        // Escape limpia el mapeo completamente
        if (e.Key == VirtualKey.Escape)
        {
            LimpiarMapeoCompleto();
            DetenerCaptura();
            e.Handled = true;
            return;
        }

        // Ignorar modificadores solos
        if (e.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift
            or VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            e.Handled = true;
            return;
        }

        // Tecla válida capturada
        _capturedKey = e.Key;
        ActualizarCaptureText();
        ValidarConflictos();
        DetenerCaptura(); // Para titileo, restaura border normal
        e.Handled = true;
    }

    private void OnModifierChanged(object sender, RoutedEventArgs e)
    {
        ActualizarCaptureText();
        ValidarConflictos();
    }

    private void OnModoChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModoCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _item.MapeoModo = Enum.Parse<ModoMapeo>(tag);
        }
    }

    /// <summary>Botón "Limpiar" compacto: borra mapeo del item y UI.</summary>
    private void OnLimpiarClick(object sender, RoutedEventArgs e)
    {
        LimpiarMapeoCompleto();
    }

    private void LimpiarMapeoCompleto()
    {
        // Desregistrar el hotkey del sistema ANTES de limpiar las propiedades
        if (_item.TieneMapeo)
        {
            _keyMapping.Desregistrar(_propietariaId, _item.SlotIndex);
        }

        _capturedKey = VirtualKey.None;
        _capturedModifiers = VirtualKeyModifiers.None;
        _item.MapeoTecla = VirtualKey.None;
        _item.MapeoModifiers = VirtualKeyModifiers.None;
        _item.MapeoModo = ModoMapeo.Toggle;

        ChkCtrl.IsChecked = false;
        ChkAlt.IsChecked = false;
        ChkShift.IsChecked = false;
        ChkWin.IsChecked = false;
        ModoCombo.SelectedIndex = 0;

        ActualizarCaptureText();
        ValidarConflictos();
        NotifyPropertyChanged(nameof(TieneMapeoActual));
    }

    /// <summary>
    /// Valida antes de cerrar con Guardar.
    /// Si solo cambió el modo (misma tecla), no re-registra en el SO: actualiza el modo
    /// en el lugar para no chocar con su propio hotkey (falso conflicto 1409).
    /// Si cambió la tecla, valida con <see cref="KeyMappingService.HayConflicto"/> (self-aware)
    /// y deja el registro final al ViewModel (evita doble registro).
    /// </summary>
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_capturedKey == VirtualKey.None)
        {
            // Sin tecla = limpiar mapeo (válido): desregistrar hotkey del sistema
            if (_item.TieneMapeo)
            {
                _keyMapping.Desregistrar(_propietariaId, _item.SlotIndex);
            }
            _item.MapeoTecla = VirtualKey.None;
            _item.MapeoModifiers = VirtualKeyModifiers.None;
            return;
        }

        var mismaCombinacion =
            _capturedKey == _item.MapeoTecla
            && _capturedModifiers == _item.MapeoModifiers;

        if (mismaCombinacion)
        {
            // Solo cambió el modo (o nada): MapeoModo ya quedó en _item vía ModoCombo.
            // Propagar al servicio sin tocar RegisterHotKey.
            _keyMapping.ActualizarModo(_propietariaId, _item.SlotIndex, _item.MapeoModo);
            return;
        }

        // Cambió la tecla: validar sin registrar (self-aware: ignora el propio slot,
        // detecta duplicados de otros slots y colisiones del SO).
        if (_keyMapping.HayConflicto(_propietariaId, _item.SlotIndex, _capturedKey, _capturedModifiers))
        {
            ConflictWarning.Text = "⚠ Esta combinación ya está en uso por el sistema, otra aplicación u otro slot. Elige otra tecla.";
            ConflictWarning.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        // Aplicar al item; el ViewModel persiste y registra el hotkey final.
        // MapeoModo ya está en _item vía ModoCombo.
        _item.MapeoTecla = _capturedKey;
        _item.MapeoModifiers = _capturedModifiers;
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}