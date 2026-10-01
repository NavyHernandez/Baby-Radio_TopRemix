using Windows.System;

namespace BebeRadio.Models;

/// <summary>
/// Slot de efecto persistido en el JSON portable de la consola.
/// Solo se guardan los slots editados (índice + campos); el resto
/// se regenera desde el mock o como vacíos al desplegar.
/// </summary>
/// <param name="Indice">Posición 0-49 dentro de su paleta.</param>
/// <param name="Titulo">Título del efecto.</param>
/// <param name="DuracionTicks">Duración total del audio.</param>
/// <param name="FilePath">Ruta del audio o null (mock).</param>
/// <param name="CueInicioTicks">Desde dónde inicia el efecto.</param>
/// <param name="CueFinTicks">Dónde termina o null (hasta el final).</param>
/// <param name="GananciaDb">Ganancia en dB.</param>
/// <param name="FundidoEntradaTicks">Fundido de entrada.</param>
/// <param name="FundidoSalidaTicks">Fundido de salida.</param>
/// <param name="ColorKey">Override de color (#hex o token) o null.</param>
/// <param name="MapeoTecla">Tecla mapeada (VirtualKey; 0 = sin mapear).</param>
/// <param name="MapeoModifiers">Modificadores (Control, Alt, Shift, Win).</param>
/// <param name="MapeoModo">Modo de interacción (Toggle, Retrigger, Momentaneo).</param>
public sealed record SlotEfectoGuardado(
    int Indice,
    string Titulo,
    long DuracionTicks,
    string? FilePath,
    long CueInicioTicks,
    long? CueFinTicks,
    double GananciaDb,
    long FundidoEntradaTicks,
    long FundidoSalidaTicks,
    string? ColorKey,
    VirtualKey MapeoTecla = VirtualKey.None,
    VirtualKeyModifiers MapeoModifiers = VirtualKeyModifiers.None,
    ModoMapeo MapeoModo = ModoMapeo.Toggle);
