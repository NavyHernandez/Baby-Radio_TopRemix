using BebeRadio.Models;
using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Editor del slot de efecto (horizontal, letras chicas).
/// Izq: datos + archivo + onda con pre-escucha; der: color libre,
/// ganancia y fundidos. Trabaja sobre <see cref="Borrador"/>:
/// Cancelar no toca nada; Guardar valida y el llamador aplica.
/// </summary>
public sealed partial class PaletaEditorDialog : ContentDialog, System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Cambios de propiedades del diálogo (para x:Bind OneWay).</summary>
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private static string[] ExtensionesAudio => AudioFileInspector.AudioExtensions;

    /// <summary>Presets modernos del cart.</summary>
    public List<string> ColoresPresets { get; } = new()
    {
        "#22D3EE", "#A78BFA", "#FACC15", "#34D399", "#FB7185", "#818CF8",
        "#F97316", "#84CC16", "#2DD4BF", "#7DD3FC", "#F472B6", "#E879F9",
        "#4ADE80", "#38BDF6", "#FB923C", "#F87171", "#C084FC", "#94A3B8",
    };

    /// <summary>Borrador en edición (lo asigna el llamador antes de ShowAsync).</summary>
    /// <remarks>Notifica al reasignar para que los x:Bind TwoWay lo sigan.</remarks>
    public PaletteItem Borrador
    {
        get => _borrador;
        set
        {
            _borrador = value;
            Notificar(nameof(Borrador), nameof(FundidoEntradaSegundos), nameof(FundidoSalidaSegundos));
        }
    }

    private PaletteItem _borrador = new(TrackCategory.Music, string.Empty, TimeSpan.Zero);

    private bool _esColorAuto = true;
    private bool _sincronizandoColor;

    /// <summary>True si el cart usa el color de su categoría.</summary>
    public bool EsColorAuto
    {
        get => _esColorAuto;
        set
        {
            if (_esColorAuto == value)
            {
                return;
            }

            _esColorAuto = value;
            Borrador.ColorKey = value ? null : ColorHexLibre;
            Notificar(nameof(EsColorAuto), nameof(EsColorAutoNegado));
        }
    }

    /// <summary>Negado para habilitar los pickers.</summary>
    public bool EsColorAutoNegado => !EsColorAuto;

    private string _colorHexLibre = "#22D3EE";

    /// <summary>Color libre en edición (#RRGGBB).</summary>
    public string ColorHexLibre
    {
        get => _colorHexLibre;
        private set => _colorHexLibre = value;
    }

    /// <summary>Fundido de entrada en segundos.</summary>
    public double FundidoEntradaSegundos
    {
        get => Borrador.FundidoEntrada.TotalSeconds;
        set => Borrador.FundidoEntrada = TimeSpan.FromSeconds(Math.Max(0, value));
    }

    /// <summary>Fundido de salida en segundos.</summary>
    public double FundidoSalidaSegundos
    {
        get => Borrador.FundidoSalida.TotalSeconds;
        set => Borrador.FundidoSalida = TimeSpan.FromSeconds(Math.Max(0, value));
    }

    /// <summary>Inicializa el diálogo y suscribe color + cierre.</summary>
    public PaletaEditorDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        ColoresGrid.SelectionChanged += OnPresetElegido;
        LibrePicker.ColorChanged += OnColorLibreElegido;
        PrimaryButtonClick += OnGuardar;
        Closing += (_, _) => Onda.Liberar();
    }

    /// <summary>Prepara el diálogo con el borrador a editar.</summary>
    /// <param name="borrador">Clon del slot (no toca el real hasta Guardar).</param>
    /// <remarks>Llamar antes de ShowAsync. Nunca lanza (registra y sigue).</remarks>
    public void Preparar(PaletteItem borrador)
    {
        try
        {
            Borrador = borrador;
            _esColorAuto = borrador.ColorKey is null;
            if (!EsColorAuto && borrador.ColorKey is not null)
            {
                var indice = ColoresPresets.FindIndex(hex =>
                    string.Equals(hex, borrador.ColorKey, StringComparison.OrdinalIgnoreCase));
                if (indice >= 0)
                {
                    ColoresGrid.SelectedIndex = indice;
                }
                else
                {
                    LibrePicker.Color = HexAColor(borrador.ColorKey);
                }

                ColorHexLibre = borrador.ColorKey.StartsWith("#") ? borrador.ColorKey : "#22D3EE";
            }

            Notificar(nameof(EsColorAuto), nameof(EsColorAutoNegado));
            Onda.Vincular(borrador, BrochaDe(borrador.BrushKey));
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Paleta.PrepararEditor");
        }
    }

    /// <summary>Valida el borrador antes de cerrar con Guardar.</summary>
    /// <param name="sender">Este diálogo.</param>
    /// <param name="args">Args del botón (cancelable).</param>
    private void OnGuardar(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(Borrador.Title))
        {
            MostrarError("El título es obligatorio.");
            args.Cancel = true;
            return;
        }

        var fin = Borrador.CueFin ?? Borrador.Duration;
        if (fin <= Borrador.CueInicio)
        {
            MostrarError("El fin debe ser mayor que el inicio.");
            args.Cancel = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(Borrador.FilePath) && !System.IO.File.Exists(Borrador.FilePath))
        {
            MostrarError("El archivo de audio no existe.");
            args.Cancel = true;
        }
    }

    /// <summary>Preset elegido: sincroniza el libre en silencio.</summary>
    /// <param name="sender">Grilla de presets.</param>
    /// <param name="e">Args de selección.</param>
    private void OnPresetElegido(object sender, SelectionChangedEventArgs e)
    {
        if (_sincronizandoColor || ColoresGrid.SelectedItem is not string preset)
        {
            return;
        }

        _sincronizandoColor = true;
        try
        {
            ColorHexLibre = preset;
            LibrePicker.Color = HexAColor(preset);
            Borrador.ColorKey = preset;
        }
        finally
        {
            _sincronizandoColor = false;
        }
    }

    /// <summary>Color libre del usuario: suelta el preset.</summary>
    /// <param name="sender">ColorPicker.</param>
    /// <param name="args">Args del cambio.</param>
    private void OnColorLibreElegido(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_sincronizandoColor)
        {
            return;
        }

        _sincronizandoColor = true;
        try
        {
            ColoresGrid.SelectedIndex = -1;
            var color = args.NewColor;
            ColorHexLibre = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            Borrador.ColorKey = ColorHexLibre;
        }
        finally
        {
            _sincronizandoColor = false;
        }
    }

    /// <summary>Muestra un error de validación.</summary>
    /// <param name="mensaje">Texto del error.</param>
    private void MostrarError(string mensaje)
    {
        ErrorText.Text = mensaje;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>Abre el explorador para elegir el audio.</summary>
    /// <param name="sender">Botón Examinar.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnExaminarClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in ExtensionesAudio)
        {
            picker.FileTypeFilter.Add(extension);
        }

        try
        {
            var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(
                XamlRoot.ContentIslandEnvironment.AppWindowId);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
        catch
        {
            return;
        }

        var archivo = await picker.PickSingleFileAsync();
        if (archivo is not null)
        {
            CargarArchivo(archivo.Path);
        }
    }

    /// <summary>Acepta arrastre solo si trae archivos.</summary>
    /// <param name="sender">Zona de drop.</param>
    /// <param name="e">Datos del arrastre.</param>
    private void OnArchivoDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.Handled = true;
        }
    }

    /// <summary>Carga el primer audio soltado y recalcula la onda.</summary>
    /// <param name="sender">Zona de drop.</param>
    /// <param name="e">Archivos soltados.</param>
    private async void OnArchivoDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var archivo = items.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
        if (archivo is not null)
        {
            CargarArchivo(archivo.Path);
        }
    }

    /// <summary>Carga un audio en el borrador y refresca la onda.</summary>
    /// <param name="path">Ruta del archivo.</param>
    /// <remarks>Archivo distinto actualiza el título (mismo archivo lo respeta).</remarks>
    private void CargarArchivo(string path)
    {
        var entrada = AudioFileInspector.TryInspect(path);
        if (entrada is null)
        {
            MostrarError("Ese archivo no es un audio legible.");
            return;
        }

        var cambioArchivo = !string.Equals(Borrador.FilePath, path, StringComparison.OrdinalIgnoreCase);
        Borrador.FilePath = path;
        if (cambioArchivo)
        {
            Borrador.Title = entrada.Title;
        }

        Borrador.Duration = entrada.Duration;
        Borrador.CueInicio = TimeSpan.Zero;
        Borrador.CueFin = null;
        ErrorText.Visibility = Visibility.Collapsed;
        Onda.Vincular(Borrador, BrochaDe(Borrador.BrushKey));
    }

    /// <summary>Resuelve el brush de una clave (token o #hex).</summary>
    /// <param name="clave">Clave de color.</param>
    /// <returns>Brush o null.</returns>
    private static Brush? BrochaDe(string clave) =>
        new CategoryBrushConverter().Convert(clave, typeof(Brush), new object(), string.Empty) as Brush;

    /// <summary>Convierte un hex a Color (fallback cian).</summary>
    /// <param name="hex">Texto #RRGGBB o token.</param>
    /// <returns>Color.</returns>
    private static Windows.UI.Color HexAColor(string hex)
    {
        try
        {
            var limpio = hex.TrimStart('#');
            return Windows.UI.Color.FromArgb(
                255,
                System.Convert.ToByte(limpio.Substring(0, 2), 16),
                System.Convert.ToByte(limpio.Substring(2, 2), 16),
                System.Convert.ToByte(limpio.Substring(4, 2), 16));
        }
        catch
        {
            return Windows.UI.Color.FromArgb(255, 0x22, 0xD3, 0xEE);
        }
    }

    /// <summary>Notifica cambios del diálogo.</summary>
    /// <param name="nombres">Propiedades.</param>
    private void Notificar(params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nombre));
        }
    }
}
