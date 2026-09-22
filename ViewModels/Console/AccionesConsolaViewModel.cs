using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Franja inferior de 7 acciones aluminio
/// (Config · Operador · Mix · Ganancia · Stop · Arriba · Abajo).
/// Los toggles viven aquí (la tira abre Config directo); detener y páginas
/// se inyectan como ganchos desde <see cref="ConsolaViewModel"/>.
/// </summary>
public sealed partial class AccionesConsolaViewModel : ObservableObject
{
    /// <summary>Modo Operador armado (glow persistente, Fase 2 lo usará).</summary>
    [ObservableProperty]
    public partial bool IsOperatorMode { get; set; }

    /// <summary>Mezcla armada (glow persistente, Fase 2 la usará).</summary>
    [ObservableProperty]
    public partial bool IsMixArmed { get; set; }

    /// <summary>Ganancia armada: prioriza efectos sobre la cola (titila).</summary>
    [ObservableProperty]
    public partial bool IsGananciaArmada { get; set; }

    /// <summary>Gancho: detener efectos de paleta (Stop solo corta la mezcla).</summary>
    public Action? AlPedirDetener { get; set; }

    /// <summary>Gancho: página anterior de la paleta (25+15).</summary>
    public Action? AlPedirSubir { get; set; }

    /// <summary>Gancho: página siguiente de la paleta (25+15).</summary>
    public Action? AlPedirBajar { get; set; }

    /// <summary>Alterna el Modo Operador (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleOperatorMode() => IsOperatorMode = !IsOperatorMode;

    /// <summary>Alterna la mezcla armada (toggle con glow).</summary>
    [RelayCommand]
    private void ToggleMix() => IsMixArmed = !IsMixArmed;

    /// <summary>Alterna la ganancia armada (toggle con titileo).</summary>
    [RelayCommand]
    private void ToggleGanancia() => IsGananciaArmada = !IsGananciaArmada;

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
