using BebeRadio.Support;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BebeRadio.ViewModels.Console;

/// <summary>
/// Ajustes de la app (diálogo Config): transición suave al pasar a Siguiente.
/// Carga del JSON portable y persiste cada cambio al instante.
/// </summary>
public sealed partial class ConfiguracionViewModel : ObservableObject
{
    /// <summary>Duraciones de transición elegibles (segundos).</summary>
    public static IReadOnlyList<int> DuracionesTransicion { get; } = [3, 5, 7];

    /// <summary>Transición suave al pulsar Siguiente (default activada).</summary>
    [ObservableProperty]
    public partial bool TransicionActivada { get; set; } = true;

    /// <summary>Segundos de la transición (3, 5 o 7; default 5).</summary>
    [ObservableProperty]
    public partial int TransicionSegundos { get; set; } = 5;

    /// <summary>Índice 0-2 de la duración (puente TwoWay para RadioButtons).</summary>
    public int TransicionIndice
    {
        get => IndiceDe(TransicionSegundos);
        set
        {
            if (value >= 0 && value < DuracionesTransicion.Count)
            {
                TransicionSegundos = DuracionesTransicion[value];
            }
        }
    }

    /// <summary>Carga los ajustes guardados (o defaults si no hay).</summary>
    public ConfiguracionViewModel()
    {
        try
        {
            var config = ConsolaStore.Cargar();
            TransicionActivada = config.TransicionSiguienteActivada;
            TransicionSegundos = NormalizarSegundos(config.TransicionSiguienteSegundos);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Cargar");
        }
    }

    /// <summary>Persiste el toggle al cambiar.</summary>
    /// <param name="value">Nuevo estado.</param>
    partial void OnTransicionActivadaChanged(bool value) => Guardar();

    /// <summary>Valida y persiste los segundos al cambiar.</summary>
    /// <param name="value">Segundos elegidos.</param>
    partial void OnTransicionSegundosChanged(int value)
    {
        var normalizado = NormalizarSegundos(value);
        if (normalizado != value)
        {
            TransicionSegundos = normalizado;
            return;
        }

        OnPropertyChanged(nameof(TransicionIndice));
        Guardar();
    }

    /// <summary>Solo admite 3, 5 o 7 (el resto cae a 5).</summary>
    /// <param name="segundos">Valor a normalizar.</param>
    /// <returns>3, 5, 7 o 5 por defecto.</returns>
    public static int NormalizarSegundos(int segundos) =>
        segundos is 3 or 5 or 7 ? segundos : 5;

    /// <summary>Índice 0-2 de unos segundos normalizados.</summary>
    /// <param name="segundos">Duración (se normaliza primero).</param>
    /// <returns>0 para 3 s, 1 para 5 s, 2 para 7 s.</returns>
    public static int IndiceDe(int segundos) => NormalizarSegundos(segundos) switch
    {
        3 => 0,
        5 => 1,
        _ => 2,
    };

    /// <summary>Guarda los ajustes en el JSON portable (best-effort).</summary>
    private void Guardar()
    {
        try
        {
            var config = ConsolaStore.Cargar();
            config.TransicionSiguienteActivada = TransicionActivada;
            config.TransicionSiguienteSegundos = TransicionSegundos;
            ConsolaStore.Guardar(config);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Guardar");
        }
    }
}
