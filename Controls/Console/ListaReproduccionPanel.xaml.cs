using BebeRadio.Models;
using BebeRadio.Support;
using BebeRadio.ViewModels;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Panel izquierdo de cola. El code-behind solo enruta drag&drop, menús
/// y clics a <see cref="ListaReproduccionViewModel"/> y anexa sombras GPU.
/// </summary>
public sealed partial class ListaReproduccionPanel : UserControl
{
    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ListaReproduccionViewModel),
            typeof(ListaReproduccionPanel),
            new PropertyMetadata(null));

    /// <summary>ViewModel de la cola (lo inyecta la shell desde ConsolaViewModel).</summary>
    public ListaReproduccionViewModel? ViewModel
    {
        get => (ListaReproduccionViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Propiedad de dependencia del reproductor (mini-pantalla).</summary>
    public static readonly DependencyProperty ReproductorProperty =
        DependencyProperty.Register(
            nameof(Reproductor),
            typeof(PlayerViewModel),
            typeof(ListaReproduccionPanel),
            new PropertyMetadata(null));

    /// <summary>Reproductor de la mini-pantalla (lo inyecta la shell).</summary>
    public PlayerViewModel? Reproductor
    {
        get => (PlayerViewModel?)GetValue(ReproductorProperty);
        set => SetValue(ReproductorProperty, value);
    }

    /// <summary>Inicializa el panel y anexa sombras al cargar.</summary>
    public ListaReproduccionPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private bool _confirmando;

    /// <summary>Anexa sombras GPU y suscribe el refresco del alias visible.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        TemaConsola.Cambio += RefrescarArtistasVisibles;
        RefrescarArtistasVisibles();
    }

    /// <summary>Desuscribe el refresco del alias al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        TemaConsola.Cambio -= RefrescarArtistasVisibles;

    /// <summary>Refresca el artista visible de cada cajita (alias de la cuenta).</summary>
    private void RefrescarArtistasVisibles()
    {
        if (ViewModel is null)
        {
            return;
        }

        foreach (var entry in ViewModel.Queue)
        {
            entry.RefrescarArtista();
        }
    }

    /// <summary>Pone en vivo la entrada de cola clicada.</summary>
    /// <param name="sender">Lista de cola.</param>
    /// <param name="e">Item clicado.</param>
    private void OnQueueItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QueueEntry entry && ViewModel is not null)
        {
            ViewModel.PlayCommand.Execute(entry);
        }
    }

    /// <summary>Acepta arrastre solo si trae archivos (copia a la cola).</summary>
    /// <param name="sender">Lista de cola.</param>
    /// <param name="e">Datos del arrastre.</param>
    private void OnQueueDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Cargar en cola";
            e.DragUIOverride.IsGlyphVisible = true;
            e.Handled = true;
        }
    }

    /// <summary>Empaqueta la entrada arrastrada hacia la paleta (formato privado).</summary>
    /// <param name="sender">Lista de cola.</param>
    /// <param name="e">Items arrastrados.</param>
    /// <remarks>Solo entradas con audio real; los mocks cancelan el arrastre.</remarks>
    private void OnColaDragStarting(object sender, DragItemsStartingEventArgs e)
    {
        var entrada = e.Items.OfType<QueueEntry>()
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.FilePath));
        if (entrada?.FilePath is null)
        {
            e.Cancel = true;
            return;
        }

        e.Data.SetData(FormatosArrastre.EntradaCola, entrada.FilePath);
        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
    }

    /// <summary>Carga audios y carpetas soltados al final de la cola.</summary>
    /// <param name="sender">Lista de cola.</param>
    /// <param name="e">Archivos soltados.</param>
    /// <remarks>Ignora el formato privado: es reorden interno (ya lo mueve la lista).</remarks>
    private async void OnQueueDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.DataView.Contains(FormatosArrastre.EntradaCola))
        {
            return;
        }

        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        ViewModel.AddFiles(await ExploradorAudio.ExpandirAsync(items), IndiceInsercion(e.GetPosition(ColaList)));
    }

    /// <summary>Calcula dónde insertar (antes/después según la mitad del card).</summary>
    /// <param name="posicion">Punto relativo a la lista.</param>
    /// <returns>Índice de inserción (Count = al final).</returns>
    private int IndiceInsercion(Windows.Foundation.Point posicion)
    {
        for (var i = 0; i < ColaList.Items.Count; i++)
        {
            if (ColaList.ContainerFromIndex(i) is not ListViewItem contenedor)
            {
                continue;
            }

            var bounds = contenedor.TransformToVisual(ColaList).TransformBounds(
                new Windows.Foundation.Rect(0, 0, contenedor.ActualWidth, contenedor.ActualHeight));
            if (posicion.Y <= bounds.Bottom)
            {
                return posicion.Y <= bounds.Top + bounds.Height / 2 ? i : i + 1;
            }
        }

        return ColaList.Items.Count;
    }

    /// <summary>Vacía la lista (confirma solo si tiene items).</summary>
    /// <param name="sender">Icono limpiar.</param>
    /// <param name="e">Args del puntero.</param>
    /// <remarks>Blindado: un fallo no cierra la app.</remarks>
    private async void OnLimpiarTodoPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (_confirmando || ViewModel is null || ViewModel.Queue.Count == 0)
        {
            return;
        }

        _confirmando = true;
        try
        {
            var confirmar = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = "Limpiar lista",
                Content = $"Se quitarán {ViewModel.Queue.Count} pistas de la cola. ¿Continuar?",
                PrimaryButtonText = "Limpiar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirmar.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.LimpiarTodo();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Cola.LimpiarTodo");
        }
        finally
        {
            _confirmando = false;
        }
    }

    /// <summary>Carga manual con explorador (click derecho → Agregar).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    private async void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        foreach (var extension in AudioFileInspector.AudioExtensions)
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

        var archivos = await picker.PickMultipleFilesAsync();
        if (archivos.Count > 0)
        {
            ViewModel.AddFiles(await ExploradorAudio.ExpandirAsync(archivos));
        }
    }

    /// <summary>Fija el item en las opciones (el DataContext no siempre fluye al flyout).</summary>
    /// <param name="sender">Menú contextual.</param>
    /// <param name="args">Args (cancelable).</param>
    private void OnItemMenuOpening(object sender, object args)
    {
        if (sender is MenuFlyout menu && menu.Target is Border tarjeta
            && tarjeta.DataContext is QueueEntry entry)
        {
            foreach (var opcion in menu.Items.OfType<MenuFlyoutItem>())
            {
                opcion.CommandParameter = entry;
            }
        }
    }

    /// <summary>Pone en vivo el item (click derecho → Poner en vivo).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnPonerEnVivoClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null
            && (sender as MenuFlyoutItem)?.CommandParameter is QueueEntry entry)
        {
            ViewModel.PlayCommand.Execute(entry);
        }
    }

    /// <summary>Quita el item de la lista (click derecho → Quitar).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnQuitarClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null
            && (sender as MenuFlyoutItem)?.CommandParameter is QueueEntry entry)
        {
            ViewModel.QuitarCommand.Execute(entry);
        }
    }
}
