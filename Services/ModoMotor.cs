namespace BebeRadio.Services;

/// <summary>Modo de la única salida de audio.</summary>
public enum ModoMotor
{
    /// <summary>Sin audio abierto.</summary>
    Detenido,

    /// <summary>Entrada de la cola a volumen 1.</summary>
    Cola,

    /// <summary>Cue del editor con ganancia y auto-stop.</summary>
    Tramo,
}
