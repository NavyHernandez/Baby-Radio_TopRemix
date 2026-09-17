using BebeRadio.Models;
using BebeRadio.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Crear/editar categoría en un solo paso: nombre, color (preset o libre)
/// e icono (grilla curada) con vista previa en vivo. Valida al Guardar y
/// el llamador persiste con <c>CategoriasRailViewModel</c>.
/// </summary>
public sealed partial class CategoriaEditorDialog : ContentDialog
{
    /// <summary>Presets modernos (mismos que la consola).</summary>
    public List<string> Colores { get; } = new()
    {
        "#22D3EE", "#A78BFA", "#FACC15", "#34D399", "#FB7185", "#818CF8",
        "#F97316", "#84CC16", "#2DD4BF", "#7DD3FC", "#F472B6", "#E879F9",
        "#4ADE80", "#38BDF6", "#FB923C", "#F87171", "#C084FC", "#94A3B8",
    };

    /// <summary>Curada radial como valores (sin conversión en XAML).</summary>
    public List<FluentIcons.Common.Symbol> Iconos { get; } = new();

    /// <summary>Curada radial (verificada contra FluentIcons 2.2.339.1).</summary>
    private static readonly string[] IconosCurados =
    {
        "MusicNote1", "MusicNote2", "Mic", "MicOff", "Megaphone", "Speaker1",
        "Speaker2", "Headset", "Voicemail", "Star", "Sparkle",
        "Trophy", "Medal", "Crown", "Heart", "News", "Calendar", "Clock",
        "Bed", "Gift", "Globe", "Building", "Map", "Camera", "Video",
        "BookOpen", "Games", "Rss", "Chat", "Flag", "Play", "Record",
        "Rewind", "FastForward", "WeatherSunny", "WeatherMoon", "Lightbulb",
    };

    private readonly CategoryBrushConverter _brochas = new();
    private readonly ColorALetraConverter _letras = new();

    /// <summary>True mientras se sincroniza color (evita recursión y robos).</summary>
    private bool _sincronizandoColor;

    /// <summary>Nombre en edición.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Color en edición (#RRGGBB).</summary>
    public string ColorHex { get; private set; } = "#22D3EE";

    /// <summary>Icono en edición (nombre FluentIcons).</summary>
    public string Simbolo { get; private set; } = "MusicNote2";

    /// <summary>Inicializa el diálogo y filtra iconos válidos.</summary>
    public CategoriaEditorDialog()
    {
        InitializeComponent();
        TemaConsola.AplicarADialogo(this);
        foreach (var icono in IconosCurados)
        {
            if (Enum.TryParse<FluentIcons.Common.Symbol>(icono, out var simbolo)
                && !Iconos.Contains(simbolo))
            {
                Iconos.Add(simbolo);
            }
        }

        NombreBox.TextChanged += (_, _) => ActualizarPrevia();
        ColoresGrid.SelectionChanged += OnPresetElegido;
        LibrePicker.ColorChanged += OnColorLibreElegido;
        IconosGrid.SelectionChanged += (_, _) => ActualizarPrevia();
        PrimaryButtonClick += OnGuardar;
    }

    /// <summary>Precarga el diálogo (nueva o edición).</summary>
    /// <param name="titulo">Título del popup.</param>
    /// <param name="registro">Datos actuales o null (nueva).</param>
    public void Preparar(string titulo, CategoriaPersonalizada? registro)
    {
        Title = titulo;
        NombreBox.Text = registro?.Label ?? string.Empty;
        if (registro is not null)
        {
            SeleccionarColor(registro.ColorHex);
            SeleccionarIcono(registro.Symbol);
        }

        ActualizarPrevia();
    }

    /// <summary>Construye el registro validado (id lo pone el llamador).</summary>
    /// <returns>Registro listo para crear o aplicar.</returns>
    public CategoriaPersonalizada Resultado() =>
        new(string.Empty, Nombre.Trim(), ColorHex, Simbolo);

    /// <summary>True si el usuario pidió crear (cierra sin guardar).</summary>
    public bool PidioNueva { get; private set; }

    /// <summary>True si el usuario pidió gestionar (cierra sin guardar).</summary>
    public bool PidioGestionar { get; private set; }

    /// <summary>Pide crear una categoría nueva (la atiende el llamador).</summary>
    /// <param name="sender">Botón Nueva.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnNuevaClick(object sender, RoutedEventArgs e)
    {
        PidioNueva = true;
        Hide();
    }

    /// <summary>Pide abrir el gestor (la atiende el llamador).</summary>
    /// <param name="sender">Botón Gestionar.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnGestionarClick(object sender, RoutedEventArgs e)
    {
        PidioGestionar = true;
        Hide();
    }

    /// <summary>Valida antes de cerrar con Guardar.</summary>
    /// <param name="sender">Este diálogo.</param>
    /// <param name="args">Args del botón (cancelable).</param>
    private void OnGuardar(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ActualizarPrevia();
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MostrarError("El nombre es obligatorio.");
            args.Cancel = true;
            return;
        }

        if (!Enum.TryParse<FluentIcons.Common.Symbol>(Simbolo, out _))
        {
            MostrarError("Elige un icono válido.");
            args.Cancel = true;
        }
    }

    /// <summary>Muestra un error de validación.</summary>
    /// <param name="mensaje">Texto del error.</param>
    private void MostrarError(string mensaje)
    {
        ErrorText.Text = mensaje;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>Preset elegido: manda el preset y sincroniza el libre en silencio.</summary>
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
            ColorHex = preset;
            LibrePicker.Color = HexAColor(preset);
        }
        finally
        {
            _sincronizandoColor = false;
        }

        ActualizarPrevia();
    }

    /// <summary>Color libre del usuario: manda él y suelta el preset.</summary>
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
            ColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        finally
        {
            _sincronizandoColor = false;
        }

        ActualizarPrevia();
    }

    /// <summary>Sincroniza campos + vista previa con la selección.</summary>
    private void ActualizarPrevia()
    {
        Nombre = NombreBox.Text.Trim();

        if (IconosGrid.SelectedItem is FluentIcons.Common.Symbol icono)
        {
            Simbolo = icono.ToString();
        }

        PreviaNombre.Text = string.IsNullOrWhiteSpace(Nombre) ? "Nombre" : Nombre;
        if (Enum.TryParse<FluentIcons.Common.Symbol>(Simbolo, out var simbolo))
        {
            PreviaIcono.Symbol = simbolo;
        }
        else
        {
            MostrarError($"El icono “{Simbolo}” no existe en esta versión.");
            return;
        }

        if (_brochas.Convert(ColorHex, typeof(Brush), new object(), string.Empty) is Brush brocha)
        {
            BebeButtonHelper.SetCategoryColor(PreviaBoton, brocha);
            PreviaIcono.Foreground = (Brush)Application.Current.Resources["TextPrimary"];
            PreviaNombre.Foreground = _letras.Convert(ColorHex, typeof(Brush), new object(), string.Empty) as Brush;
        }

        ErrorText.Visibility = Visibility.Collapsed;
    }

    /// <summary>Preselecciona un color (preset o libre).</summary>
    /// <param name="hex">Color actual.</param>
    private void SeleccionarColor(string hex)
    {
        var normalizado = hex.StartsWith("#") ? hex : $"#{hex}";
        var indice = Colores.FindIndex(color =>
            string.Equals(color, normalizado, StringComparison.OrdinalIgnoreCase));
        if (indice >= 0)
        {
            ColoresGrid.SelectedIndex = indice;
        }
        else
        {
            ColoresGrid.SelectedIndex = -1;
            LibrePicker.Color = HexAColor(normalizado);
        }
    }

    /// <summary>Preselecciona un icono si está en la curada.</summary>
    /// <param name="simbolo">Nombre del símbolo.</param>
    private void SeleccionarIcono(string simbolo)
    {
        if (!Enum.TryParse<FluentIcons.Common.Symbol>(simbolo, out var valor))
        {
            return;
        }

        var indice = Iconos.IndexOf(valor);
        IconosGrid.SelectedIndex = indice;
        if (indice >= 0)
        {
            Simbolo = valor.ToString();
        }
    }

    /// <summary>Convierte un hex a Color (fallback cian).</summary>
    /// <param name="hex">Texto #RRGGBB.</param>
    /// <returns>Color.</returns>
    private static Windows.UI.Color HexAColor(string hex)
    {
        try
        {
            var limpio = hex.TrimStart('#');
            return Windows.UI.Color.FromArgb(
                255,
                Convert.ToByte(limpio.Substring(0, 2), 16),
                Convert.ToByte(limpio.Substring(2, 2), 16),
                Convert.ToByte(limpio.Substring(4, 2), 16));
        }
        catch
        {
            return Windows.UI.Color.FromArgb(255, 0x22, 0xD3, 0xEE);
        }
    }
}
