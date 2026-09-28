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

    /// <summary>Modo operador en pantalla completa con 2 paletas (default).</summary>
    [ObservableProperty]
    public partial bool OperadorPantallaCompleta { get; set; } = true;

    /// <summary>Índice 0-1 del modo operador (puente TwoWay para RadioButtons).</summary>
    /// <remarks>Los índices negativos (sin selección al inicializar) se ignoran:
    /// antes convertían a completa y reescribían 2 pantallas en el JSON.</remarks>
    public int OperadorIndice
    {
        get => OperadorPantallaCompleta ? 0 : 1;
        set
        {
            if (value < 0)
            {
                return;
            }

            OperadorPantallaCompleta = value <= 0;
        }
    }

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

    /// <summary>Suprime el guardado durante la carga inicial.</summary>
    /// <remarks>Sin esto, cada setter del constructor persistía con los demás
    /// valores aún en default y pisaba el JSON (p. ej. 1 pantalla → 2).</remarks>
    private bool _cargando = true;

    /// <summary>Carga los ajustes guardados (o defaults si no hay).</summary>
    public ConfiguracionViewModel()
    {
        try
        {
            var config = ConsolaStore.Cargar();
            TransicionActivada = config.TransicionSiguienteActivada;
            TransicionSegundos = NormalizarSegundos(config.TransicionSiguienteSegundos);
            OperadorPantallaCompleta = config.OperadorPantallaCompleta;
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Cargar");
        }
        finally
        {
            _cargando = false;
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

    /// <summary>Persiste el modo operador al cambiar.</summary>
    /// <param name="value">Nuevo modo.</param>
    partial void OnOperadorPantallaCompletaChanged(bool value)
    {
        OnPropertyChanged(nameof(OperadorIndice));
        Guardar();
    }

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
    /// <remarks>No persiste durante la carga inicial (ver <see cref="_cargando"/>).</remarks>
    private void Guardar()
    {
        if (_cargando)
        {
            return;
        }

        try
        {
            var config = ConsolaStore.Cargar();
            config.TransicionSiguienteActivada = TransicionActivada;
            config.TransicionSiguienteSegundos = TransicionSegundos;
            config.OperadorPantallaCompleta = OperadorPantallaCompleta;
            ConsolaStore.Guardar(config);
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Config.Guardar");
        }
    }
}
