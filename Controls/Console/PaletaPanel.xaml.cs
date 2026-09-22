using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using BebeRadio.Controls;
using BebeRadio.Models;
using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Parrilla de paleta (50 slots, 25+25). El code-behind dispara
/// el efecto por el mezclador, titila los carts que suenan (opacidad
/// 1 ↔ 0.35 cada 350 ms con un solo timer, como el Stop programado del
/// PlayerBar), abre el editor, enruta el arrastre por cart y exporta/
/// importa la configuración. La lógica vive en <see cref="PaletaViewModel"/>.
/// </summary>
public sealed partial class PaletaPanel : UserControl
{
    /// <summary>Intervalo del titileo de los carts que suenan.</summary>
    private static readonly TimeSpan IntervaloTitileo = TimeSpan.FromMilliseconds(350);

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _titileo;
    private readonly HashSet<Button> _botonesSonando = new();
    private bool _faseTitileo;
    private bool _editando;
    private PaletaViewModel? _suscrito;
    private Stopwatch? _medicionCambio;
    private int _pasadasSombras;

    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(PaletaViewModel),
            typeof(PaletaPanel),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    /// <summary>ViewModel de la paleta (lo inyecta la shell).</summary>
    public PaletaViewModel? ViewModel
    {
        get => (PaletaViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Se eleva al pedir crear categoría (guía sin categoría).</summary>
    public event Action? PideNuevaCategoria;

    /// <summary>Inicializa el panel y suscribe sombras y reproducción.</summary>
    public PaletaPanel()
    {
        InitializeComponent();
        PaletteList.LayoutUpdated += OnPanelReacomodado;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Re-suscribe al cambiar el ViewModel inyectado.</summary>
    /// <param name="sender">Dependencia.</param>
    /// <param name="args">Valores viejo/nuevo.</param>
    private static void OnViewModelPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is PaletaPanel panel)
        {
            panel.SuscribirReproduccion(
                args.OldValue as PaletaViewModel,
                args.NewValue as PaletaViewModel);
        }
    }

    /// <summary>Anexa sombras y suscribe el estado de reproducción al cargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        SuscribirReproduccion(null, ViewModel);
    }

    /// <summary>Desuscribe eventos y apaga el titileo al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SuscribirReproduccion(ViewModel, null);
        ApagarTitileo();
    }

    /// <summary>Suscribe la colección de slots que suenan y la página visible.</summary>
    /// <param name="viejo">ViewModel anterior (se desuscribe).</param>
    /// <param name="nuevo">ViewModel actual (se suscribe).</param>
    private void SuscribirReproduccion(PaletaViewModel? viejo, PaletaViewModel? nuevo)
    {
        if (viejo is not null)
        {
            viejo.Sonando.CollectionChanged -= OnSonandoCambiado;
            viejo.Items.CollectionChanged -= OnPaginaCambiada;
            viejo.PropertyChanged -= OnPaletaCambiada;
        }

        _suscrito = nuevo;

        if (nuevo is not null)
        {
            nuevo.UsarHiloUi(DispatcherQueue);
            nuevo.Sonando.CollectionChanged += OnSonandoCambiado;
            nuevo.Items.CollectionChanged += OnPaginaCambiada;
            nuevo.PropertyChanged += OnPaletaCambiada;
            SincronizarTitileo();
        }
        else
        {
            ApagarTitileo();
        }
    }

    /// <summary>Mide el cambio de categoría desde que se selecciona hasta el layout.</summary>
    /// <param name="sender">ViewModel de la paleta.</param>
    /// <param name="e">Propiedad cambiada.</param>
    /// <remarks>Diagnóstico temporal (Paleta.Layout en baby-radio-error.log).</remarks>
    private void OnPaletaCambiada(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaletaViewModel.Selected))
        {
            _medicionCambio = Stopwatch.StartNew();
            _pasadasSombras = 2;
        }
    }

    /// <summary>Re-sincroniza el titileo con los slots que suenan.</summary>
    /// <param name="sender">Colección.</param>
    /// <param name="e">Cambio.</param>
    private void OnSonandoCambiado(object? sender, NotifyCollectionChangedEventArgs e) =>
        SincronizarTitileo();

    /// <summary>Re-sincroniza tras paginar (los botones se recrean).</summary>
    /// <param name="sender">Colección.</param>
    /// <param name="e">Cambio.</param>
    private void OnPaginaCambiada(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _pasadasSombras = 2;
        if (_suscrito is not null && _suscrito.Sonando.Count > 0)
        {
            SincronizarTitileo();
        }
    }

    /// <summary>
    /// Rearma el re-anexado de sombras tras mutar un slot (cargar/limpiar).
    /// </summary>
    /// <remarks>
    /// Cambiar TieneAudio re-templa el cart y el nuevo SurfaceBorder nace sin
    /// sombra GPU (se vería más plano/brillante); las próximas pasadas de
    /// layout la reanexan (idempotente y barato).
    /// </remarks>
    private void RearmarSombras() => _pasadasSombras = 2;

    /// <summary>Re-engancha el titileo tras el pase de layout (botones recién creados).</summary>
    /// <param name="sender">Lista de carts.</param>
    /// <param name="e">Args.</param>
    /// <remarks>Solo dos comparaciones si no hay nada que enganchar.</remarks>
    private void OnPanelReacomodado(object? sender, object e)
    {
        if (_medicionCambio is not null)
        {
            RegistroErrores.Traza("Paleta.Layout", $"{_medicionCambio.ElapsedMilliseconds}ms");
            _medicionCambio = null;
        }

        // Tras un cambio de categoría o de página, los carts pueden re-templarse
        // (estilo macizo/esqueleto) y perder su sombra: se reanexa en las primeras
        // pasadas de layout (idempotente y barato).
        if (_pasadasSombras > 0)
        {
            _pasadasSombras--;
            ConsolaSombraHelper.AttachAllShadows(this);
        }

        if (_suscrito is not null
            && _suscrito.Sonando.Count > 0
            && _botonesSonando.Count == 0)
        {
            SincronizarTitileo();
        }
    }

    /// <summary>Enciende titileo en cada cart que suena (solo visibles).</summary>
    /// <remarks>Solo opacidad: sin glow para no agregar bordes.</remarks>
    private void SincronizarTitileo()
    {
        ApagarTitileo();
        if (_suscrito is null)
        {
            return;
        }

        foreach (var item in _suscrito.Sonando)
        {
            if (BuscarBoton(item) is Button boton)
            {
                boton.Opacity = 0.35;
                _botonesSonando.Add(boton);
            }
        }

        if (_botonesSonando.Count == 0)
        {
            return;
        }

        _faseTitileo = true;
        _titileo = DispatcherQueue.CreateTimer();
        _titileo.Interval = IntervaloTitileo;
        _titileo.Tick += (_, _) =>
        {
            _faseTitileo = !_faseTitileo;
            foreach (var boton in _botonesSonando)
            {
                boton.Opacity = _faseTitileo ? 0.35 : 1;
            }
        };
        _titileo.Start();
    }

    /// <summary>Dispara el efecto (re-pulsar reinicia) y deja pulso si no arrancó.</summary>
    /// <param name="sender">Botón pulsado.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Si el slot suena, el titileo lo gobierna Sonando.</remarks>
    private void OnPaletteFlashClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || ViewModel is null
            || button.DataContext is not PaletteItem item)
        {
            return;
        }

        if (!ViewModel.Disparar(item))
        {
            ConsolaSombraHelper.FlashActive(button, DispatcherQueue);
        }
    }

    /// <summary>Localiza el botón cuyo contenido es el slot pedido.</summary>
    /// <param name="item">Slot en reproducción.</param>
    /// <returns>El botón o null si no está en la página visible.</returns>
    private Button? BuscarBoton(PaletteItem item)
    {
        foreach (var boton in ConsolaSombraHelper.FindDescendants<Button>(this))
        {
            if (ReferenceEquals(boton.DataContext, item))
            {
                return boton;
            }
        }

        return null;
    }

    /// <summary>Apaga el titileo y restaura la opacidad enlazada.</summary>
    private void ApagarTitileo()
    {
        _titileo?.Stop();
        _titileo = null;
        foreach (var boton in _botonesSonando)
        {
            boton.ClearValue(OpacityProperty);
        }

        _botonesSonando.Clear();
    }

    /// <summary>Pide crear una categoría (guía sin categoría).</summary>
    /// <param name="sender">Botón de la guía.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnCrearCategoriaClick(object sender, RoutedEventArgs e) =>
        PideNuevaCategoria?.Invoke();

    /// <summary>Muestra un aviso breve (errores blindados de la paleta).</summary>
    /// <param name="mensaje">Texto del aviso.</param>
    private async Task MostrarAvisoAsync(string mensaje)
    {
        try
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = "Paleta",
                Content = mensaje,
                CloseButtonText = "Entendido",
                DefaultButton = ContentDialogButton.Close,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Paleta.Aviso");
        }
    }

    /// <summary>Fija el slot en las opciones: vacíos con carga+edición+guía.</summary>
    /// <param name="sender">Menú contextual.</param>
    /// <param name="args">Args (cancelable).</param>
    /// <remarks>Limpiar solo en llenos; la guía orienta sin arrastrar.</remarks>
    private void OnCartMenuOpening(object sender, object args)
    {
        if (sender is not MenuFlyout menu || menu.Target is not Button boton
            || boton.DataContext is not PaletteItem item)
        {
            return;
        }

        var vacio = !item.TieneAudio;
        foreach (var opcion in menu.Items.OfType<MenuFlyoutItem>())
        {
            opcion.CommandParameter = item;
            opcion.Visibility = opcion.Name switch
            {
                "GuiaItem" => vacio ? Visibility.Visible : Visibility.Collapsed,
                "LimpiarItem" => vacio ? Visibility.Collapsed : Visibility.Visible,
                _ => Visibility.Visible,
            };
        }
    }

    /// <summary>Carga un audio por explorador (sin arrastrar).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo abre aviso en vez de cerrar la app.</remarks>
    private async void OnCargarAudioClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not PaletteItem item)
        {
            return;
        }

        try
        {
            var picker = new FileOpenPicker();
            foreach (var extension in AudioFileInspector.AudioExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            InicializarPicker(picker);
            var archivo = await picker.PickSingleFileAsync();
            if (archivo is null)
            {
                return;
            }

            if (!ViewModel.SoltarAudio(item, archivo.Path))
            {
                await MostrarAvisoAsync("Ese archivo no es un audio legible.");
                return;
            }

            RearmarSombras();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Paleta.CargarAudio");
            await MostrarAvisoAsync("No se pudo cargar el audio. El error quedó registrado.");
        }
    }

    /// <summary>Abre el editor del slot (click derecho → Editar).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo abre aviso en vez de cerrar la app.</remarks>
    private async void OnEditarCartClick(object sender, RoutedEventArgs e)
    {
        if (_editando
            || ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not PaletteItem item)
        {
            return;
        }

        _editando = true;
        try
        {
            var dialogo = new PaletaEditorDialog { XamlRoot = XamlRoot };
            dialogo.Preparar(item.Clonar());
            if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.AplicarEdicion(item, dialogo.Borrador);
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Paleta.Editar");
            await MostrarAvisoAsync("No se pudo abrir el editor. El error quedó registrado.");
        }
        finally
        {
            _editando = false;
        }
    }

    /// <summary>Restaura el slot a vacío (click derecho → Limpiar).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLimpiarCartClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not PaletteItem item)
        {
            return;
        }

        ViewModel.LimpiarSlot(item);
        RearmarSombras();
    }

    /// <summary>Acepta arrastre de archivos o de la lista sobre un cart.</summary>
    /// <param name="sender">Botón del cart.</param>
    /// <param name="e">Datos del arrastre.</param>
    private void OnCartDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)
            || e.DataView.Contains(FormatosArrastre.EntradaCola))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Cargar efecto";
            e.DragUIOverride.IsGlyphVisible = true;
            e.Handled = true;
        }
    }

    /// <summary>Carga el primer audio soltado sobre el cart (persiste).</summary>
    /// <param name="sender">Botón del cart.</param>
    /// <param name="e">Archivos soltados o entrada de la lista.</param>
    /// <remarks>Ambas rutas terminan en SoltarAudio, que guarda en el JSON.</remarks>
    private async void OnCartDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || (sender as Button)?.DataContext is not PaletteItem item)
        {
            return;
        }

        if (e.DataView.Contains(FormatosArrastre.EntradaCola)
            && await e.DataView.GetDataAsync(FormatosArrastre.EntradaCola) is string rutaLista
            && !string.IsNullOrWhiteSpace(rutaLista))
        {
            ViewModel.SoltarAudio(item, rutaLista);
            RearmarSombras();
            return;
        }

        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var archivos = await e.DataView.GetStorageItemsAsync();
        var audio = archivos.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
        if (audio is not null)
        {
            ViewModel.SoltarAudio(item, audio.Path);
            RearmarSombras();
        }
    }

    /// <summary>Asocia el picker a la ventana (identidad de paquete).</summary>
    /// <param name="picker">Picker a inicializar (open o save).</param>
    private void InicializarPicker(object picker)
    {
        try
        {
            var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(
                XamlRoot.ContentIslandEnvironment.AppWindowId);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
        catch
        {
            // Sin HWND el picker no abre: se ignora en silencio.
        }
    }
}
