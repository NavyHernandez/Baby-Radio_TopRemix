namespace BebeRadio.Models;

/// <summary>
/// Modo de interacción del mapeo de tecla para un slot de paleta.
/// </summary>
public enum ModoMapeo
{
    /// <summary>Pulsar una vez dispara; pulsar de nuevo detiene (toggle).</summary>
    Toggle,

    /// <summary>Cada pulsación dispara (retrigger), no detiene lo anterior.</summary>
    Retrigger,

    /// <summary>Mantenido = sonando; soltar = detiene (push-to-talk). Requiere hook de bajo nivel.</summary>
    Momentaneo
}