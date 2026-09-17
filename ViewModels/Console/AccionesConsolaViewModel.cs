using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Franja inferior de 7 acciones aluminio
/// (Config · Operador · Mix · Ganancia · Stop · Arriba · Abajo).
/// Los toggles viven aquí; las acciones de mezcla (detener, mover,
/// configurar, ganancia) se inyectan como ganchos desde
/// <see cref="ConsolaViewModel"/> para cruzar partes sin acoplar vistas.
/// </summary>
public sealed partial class AccionesConsolaViewModel : ObservableObject
{
    /// <summary>Modo Operador armado (glow persistente, Fase 2 lo usará).</summary>
    [ObservableProperty]
    public partial bool IsOperatorMode { get; set; }

    /// <summary>Mezcla armada (glow persistente, Fase 2 la usará).</summary>
    [ObservableProperty]
    public partial bool IsMixArmed { get; set; }

    /// <summary>Gancho: abrir configuración (Fase 2: diálogo real).</summary>
    public Action? AlPedirConfiguracion { get; set; }

    /// <summary>Gancho: ajustar ganancia (Fase 2: fader real).</summary>
    public Action? AlPedirGanancia { get; set; }

    /// <summary>Gancho: detener efectos de paleta (Stop solo corta la mezcla).</summary>
    public Action? AlPedirDetener { get; set; }

    /// <summary>Gancho: página anterior de la paleta (20+20).</summary>
    public Action? AlPedirSubir { get; set; }

    /// <summary>Gancho: página siguiente de la paleta (20+20).</summary>
    public Action? AlPedirBajar { get; set; }

    /// <summary>Alterna el Modo Operador (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleOperatorMode() => IsOperatorMode = !IsOperatorMode;

    /// <summary>Alterna la mezcla armada (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleMix() => IsMixArmed = !IsMixArmed;

    /// <summary>Solicita abrir la configuración.</summary>
    /// <remarks>Fase 1: solo pulso visual; Fase 2: abre el diálogo.</remarks>
    [RelayCommand]
    private void AbrirConfiguracion() => AlPedirConfiguracion?.Invoke();

    /// <summary>Solicita ajustar la ganancia.</summary>
    /// <remarks>Fase 1: solo pulso visual; Fase 2: fader del motor.</remarks>
    [RelayCommand]
    private void AjustarGanancia() => AlPedirGanancia?.Invoke();

    /// <summary>Solicita detener los efectos que suenan.</summary>
    /// <remarks>El orquestador lo mezcla con la paleta (no toca la cola).</remarks>
    [RelayCommand]
    private void Detener() => AlPedirDetener?.Invoke();

    /// <summary>Solicita la página anterior de la paleta.</summary>
    [RelayCommand]
    private void Subir() => AlPedirSubir?.Invoke();

    /// <summary>Solicita la página siguiente de la paleta.</summary>
    [RelayCommand]
    private void Bajar() => AlPedirBajar?.Invoke();
}
