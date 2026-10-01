using BebeRadio.Support;
using BebeRadio.ViewModels;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Riel derecho de 7 categorías. Click despliega; el menú edita, elimina
/// (customs con confirmación) o restaura (fijas). Crear y elegir las 7
/// vive en el editor y el gestor (flujos encadenados sin diálogos anidados).
/// </summary>
public sealed partial class CategoriasRailPanel : UserControl
{
    /// <summary>Propiedad de dependencia del ViewModel inyectado.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(CategoriasRailViewModel),
            typeof(CategoriasRailPanel),
            new PropertyMetadata(null));

    /// <summary>ViewModel del riel (lo inyecta la shell).</summary>
    public CategoriasRailViewModel? ViewModel
    {
        get => (CategoriasRailViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Inicializa el riel y anexa sombras al cargar.</summary>
    public CategoriasRailPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private bool _dialogoAbierto;

    /// <summary>Inicia el flujo de creación (lo pide la paleta sin categoría).</summary>
    /// <remarks>Blindado: un fallo abre aviso en vez de cerrar la app.</remarks>
    public async Task IniciarCreacionAsync()
    {
        if (_dialogoAbierto || ViewModel is null)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            await FlujoNuevaAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Crear");
            await MostrarAvisoAsync("No se pudo crear la categoría. El error quedó registrado.");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    private bool _avisoChequeado;

    /// <summary>Anexa sombras GPU y lanza el chequeo de actualización.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        _ = ComprobarAvisoActualizacionAsync();
    }

    /// <summary>
    /// Consulta GitHub en diferido y enciende el punto pulsante si hay versión nueva.
    /// </summary>
    /// <remarks>Silencioso: sin red o copia no instalada no muestra aviso ni falla.</remarks>
    private async Task ComprobarAvisoActualizacionAsync()
    {
        if (_avisoChequeado)
        {
            return;
        }

        _avisoChequeado = true;
        try
        {
            await Task.Delay(8000);
            if (ViewModel is not { } vm)
            {
                return;
            }

            await vm.ComprobarActualizacionAsync();
            if (vm.HayActualizacion)
            {
                ToolTipService.SetToolTip(
                    AcercaSlot,
                    $"Acerca de Baby Radio — ¡v{vm.VersionActualizacion} disponible!");
                (Resources["PulsoPunto"] as Storyboard)?.Begin();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.AvisoUpdate");
        }
    }

    /// <summary>Ajusta el menú (Eliminar solo customs, Restaurar solo fijas).</summary>
    /// <param name="sender">Menú contextual.</param>
    /// <param name="args">Args (cancelable).</param>
    private void OnMenuOpening(object sender, object args)
    {
        if (sender is not MenuFlyout menu || menu.Target is not Button boton)
        {
            return;
        }

        var categoria = boton.DataContext as SelectableCategory;
        foreach (var item in menu.Items.OfType<MenuFlyoutItem>())
        {
            item.CommandParameter = categoria;
            if (item.Text == "Eliminar")
            {
                item.Visibility = categoria?.EsPersonalizada == true ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (item.Text == "Restaurar valores")
            {
                item.Visibility = categoria?.EsPersonalizada == false ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>Abre el editor (click derecho → Editar) y encadena Nueva/Gestionar.</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo abre aviso en vez de cerrar la app.</remarks>
    private async void OnEditarCategoriaClick(object sender, RoutedEventArgs e)
    {
        if (_dialogoAbierto
            || ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not SelectableCategory categoria)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            var registro = ViewModel.ObtenerRegistro(categoria.PropietariaId);
            if (registro is null)
            {
                return;
            }

            var dialogo = new CategoriaEditorDialog { XamlRoot = XamlRoot };
            dialogo.Preparar($"Editar {registro.Label}", registro);
            var resultado = await dialogo.ShowAsync();
            if (resultado == ContentDialogResult.Primary)
            {
                ViewModel.AplicarEdicion(categoria.PropietariaId, dialogo.Resultado());
                ConsolaSombraHelper.AttachAllShadows(this);
            }
            else if (dialogo.PidioNueva)
            {
                await FlujoNuevaAsync();
            }
            else if (dialogo.PidioGestionar)
            {
                await FlujoGestionarAsync();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Editar");
            await MostrarAvisoAsync("No se pudo abrir el editor. El error quedó registrado.");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Elimina la personalizada con confirmación (cuenta audios).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo abre aviso en vez de cerrar la app.</remarks>
    private async void OnEliminarCategoriaClick(object sender, RoutedEventArgs e)
    {
        if (_dialogoAbierto
            || ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not SelectableCategory categoria
            || !categoria.EsPersonalizada)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            if (await ConfirmarEliminarAsync(categoria.Swatch.Label, categoria.PropietariaId))
            {
                ViewModel.Eliminar(categoria.PropietariaId);
                ConsolaSombraHelper.AttachAllShadows(this);
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Eliminar");
            await MostrarAvisoAsync("No se pudo eliminar la categoría. El error quedó registrado.");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Restaura una fija a sus valores originales.</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnRestaurarCategoriaClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null
            || (sender as MenuFlyoutItem)?.CommandParameter is not SelectableCategory categoria
            || categoria.EsPersonalizada)
        {
            return;
        }

        ViewModel.RestaurarFija(categoria.PropietariaId);
    }

    /// <summary>Flujo de creación (desde editor o gestor).</summary>
    private async Task FlujoNuevaAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialogo = new CategoriaEditorDialog { XamlRoot = XamlRoot };
        dialogo.Preparar("Nueva categoría", null);
        var resultado = await dialogo.ShowAsync();
        if (resultado == ContentDialogResult.Primary)
        {
            ViewModel.Crear(dialogo.Resultado());
            ConsolaSombraHelper.AttachAllShadows(this);
        }
        else if (dialogo.PidioGestionar)
        {
            await FlujoGestionarAsync();
        }
    }

    /// <summary>Flujo del gestor (toggles, orden, editar, eliminar, nueva).</summary>
    private async Task FlujoGestionarAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        while (true)
        {
            var gestor = new GestionCategoriasDialog { XamlRoot = XamlRoot };
            gestor.Cargar(ViewModel);
            var resultado = await gestor.ShowAsync();
            if (resultado == ContentDialogResult.Primary)
            {
                ViewModel.AplicarGestion(gestor.Resultado());
                ConsolaSombraHelper.AttachAllShadows(this);
                return;
            }

            if (gestor.PidioNueva)
            {
                await FlujoNuevaAsync();
                continue;
            }

            if (gestor.PidioEditar is not null)
            {
                await FlujoEditarIdAsync(gestor.PidioEditar);
                continue;
            }

            if (gestor.PidioEliminar is not null)
            {
                var fila = ViewModel.Todas().FirstOrDefault(c => c.PropietariaId == gestor.PidioEliminar);
                if (fila is not null && await ConfirmarEliminarAsync(fila.Swatch.Label, fila.PropietariaId))
                {
                    ViewModel.Eliminar(fila.PropietariaId);
                }

                continue;
            }

            return;
        }
    }

    /// <summary>Edita una categoría por id y vuelve (usado por el gestor).</summary>
    /// <param name="propietariaId">Dueña a editar.</param>
    private async Task FlujoEditarIdAsync(string propietariaId)
    {
        if (ViewModel is null)
        {
            return;
        }

        var registro = ViewModel.ObtenerRegistro(propietariaId);
        if (registro is null)
        {
            return;
        }

        var dialogo = new CategoriaEditorDialog { XamlRoot = XamlRoot };
        dialogo.Preparar($"Editar {registro.Label}", registro);
        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.AplicarEdicion(propietariaId, dialogo.Resultado());
            ConsolaSombraHelper.AttachAllShadows(this);
        }
    }

    /// <summary>Abre el popup de la cuenta (un solo vuelo).</summary>
    /// <param name="sender">Icono usuario.</param>
    /// <param name="e">Args del puntero.</param>
    private async void OnUsuarioClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (_dialogoAbierto)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            await new UsuarioDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Cuenta");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Abre el popup de soporte (un solo vuelo).</summary>
    /// <param name="sender">Icono soporte.</param>
    /// <param name="e">Args del puntero.</param>
    private async void OnSoporteClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (_dialogoAbierto)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            await new SoporteDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Soporte");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Abre el popup acerca de (un solo vuelo).</summary>
    /// <param name="sender">Icono info.</param>
    /// <param name="e">Args del puntero.</param>
    private async void OnAcercaClick(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (_dialogoAbierto)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            await new AcercaDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Acerca");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Se eleva al importar o volver a local: el orquestador redespliega todo.</summary>
    public event Action? ConfiguracionImportada;

    /// <summary>
    /// Refresca el menú portable leyendo el estado actual de sesión.
    /// "Configuración local" (y su separador) solo aparece en sesión portable.
    /// </summary>
    /// <remarks>Leer aquí (no una sola vez al cargar) para que cada despliegue refleje lo vigente.</remarks>
    private void ActualizarMenuPortable()
    {
        var enSesion = ConsolaStore.SesionPortableActiva;
        var visibilidad = enSesion
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
        ConfiguracionLocalItem.Visibility = visibilidad;
        PortableSeparador.Visibility = visibilidad;
    }

    /// <summary>Muestra "Configuración local" solo en sesión portable.</summary>
    /// <param name="sender">Menú portable.</param>
    /// <param name="e">Args de apertura.</param>
    private void OnPortableMenuOpening(object? sender, object e)
    {
        if (sender is MenuFlyout)
        {
            ActualizarMenuPortable();
        }
    }

    /// <summary>Sale de la sesión portable y vuelve a la configuración local.</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>
    /// Lo guardado en el portable ya quedó en su archivo; el JSON local está
    /// intacto. Blindado: un fallo avisa en vez de cerrar la app.
    /// </remarks>
    private async void OnConfiguracionLocalClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ConsolaStore.SalirSesionPortable();
            ConfiguracionImportada?.Invoke();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.ConfiguracionLocal");
            await MostrarAvisoAsync("No se pudo volver a la configuración local.", "Consola portable");
        }
    }

    /// <summary>Abre el menú portable con click izquierdo en el iconito.</summary>
    /// <param name="sender">Iconito portable.</param>
    /// <param name="e">Args del puntero.</param>
    /// <remarks>Refresca el menú antes de desplegarlo (lee la sesión vigente).</remarks>
    private void OnPortablePressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement icono && icono.ContextFlyout is MenuFlyout menu)
        {
            ActualizarMenuPortable();
            menu.ShowAt(icono);
        }
    }

    /// <summary>Nombre sugerido para la copia portable (con alias o neutro).</summary>
    /// <returns>alias_baby-radio-consola, o baby-radio-consola sin alias.</returns>
    /// <remarks>El alias se sanea de caracteres inválidos y se recorta a 40.</remarks>
    private static string NombrePortableSugerido()
    {
        try
        {
            var alias = ConsolaStore.Cargar().Perfil.Alias.Trim();
            foreach (var invalido in System.IO.Path.GetInvalidFileNameChars())
            {
                alias = alias.Replace(invalido.ToString(), string.Empty);
            }

            alias = alias.Trim();
            if (alias.Length > 40)
            {
                alias = alias[..40].Trim();
            }

            return string.IsNullOrWhiteSpace(alias)
                ? "baby-radio-consola"
                : $"{alias}_baby-radio-consola";
        }
        catch
        {
            return "baby-radio-consola";
        }
    }

    /// <summary>Exporta la copia portable (rutas relativas a la carpeta destino).</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>
    /// El nombre sugerido lleva el alias del operador
    /// (alias_baby-radio-consola) o el neutro sin alias. Filtro .json simple:
    /// Windows rechaza extensiones de doble punto. Blindado: un fallo avisa
    /// en vez de cerrar la app.
    /// </remarks>
    private async void OnExportarClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedFileName = NombrePortableSugerido(),
            };
            picker.FileTypeChoices.Add("Configuración Baby Radio", new List<string> { ".json" });
            InicializarPicker(picker);
            var archivo = await picker.PickSaveFileAsync();
            if (archivo is null)
            {
                return;
            }

            var (total, fuera) = ConsolaStore.ExportarPortable(archivo.Path);
            if (fuera > 0)
            {
                await MostrarAvisoAsync(
                    $"Copia guardada. {fuera} de {total} efectos están fuera del disco destino y no sonarán en otra PC.",
                    "Consola portable");
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Exportar");
            await MostrarAvisoAsync("No se pudo guardar la copia portable.", "Consola portable");
        }
    }

    /// <summary>Importa un JSON portable con confirmación y pide redesplegar.</summary>
    /// <param name="sender">Opción del menú.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>
    /// Pre-escanea sin efectos, muestra el resumen para confirmar y solo al
    /// aceptar activa la sesión portable (los guardados van al archivo
    /// cargado, sin tocar el JSON local). Blindado: un fallo no cierra la app.
    /// </remarks>
    private async void OnImportarClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            InicializarPicker(picker);
            var archivo = await picker.PickSingleFileAsync();
            if (archivo is null)
            {
                return;
            }

            var config = ConsolaStore.LeerPortable(archivo.Path, out var versionFutura);
            if (config is null)
            {
                await MostrarAvisoAsync(
                    versionFutura
                        ? "Ese archivo es de una versión más nueva de Baby Radio. Actualiza la app para cargarlo."
                        : "No se pudo leer ese archivo de configuración.",
                    "Consola portable");
                return;
            }

            var carpeta = System.IO.Path.GetDirectoryName(archivo.Path) ?? string.Empty;
            ConsolaStore.ResolverPortable(config, carpeta);
            var (total, encontrados) = ConsolaStore.ContarPortable(config);
            if (!await ConfirmarPortableAsync(archivo.Path, config, total, encontrados))
            {
                return;
            }

            ConsolaStore.ActivarPortable(config, archivo.Path);
            ConfiguracionImportada?.Invoke();

            var faltan = total - encontrados;
            if (faltan > 0)
            {
                await MostrarAvisoAsync(
                    $"{faltan} de {total} efectos no se encontraron en el disco. Esos carts quedan cargados pero no sonarán.",
                    "Consola portable");
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Importar");
            await MostrarAvisoAsync("No se pudo abrir el selector de archivos.", "Consola portable");
        }
    }

    /// <summary>Pide confirmación para cargar una consola portable.</summary>
    /// <param name="ruta">Archivo portable elegido.</param>
    /// <param name="config">Configuración pre-escaneada.</param>
    /// <param name="total">Efectos con audio.</param>
    /// <param name="encontrados">Audios existentes en disco.</param>
    /// <returns>True si confirma la carga (false si falla).</returns>
    private async Task<bool> ConfirmarPortableAsync(
        string ruta, BebeRadio.Models.ConsolaConfiguracion config, int total, int encontrados)
    {
        try
        {
            var confirmar = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = "Cargar consola portable",
                Content = $"Archivo: {ruta}\n"
                    + $"Categorías propias: {config.CategoriasPersonalizadas.Count}\n"
                    + $"Efectos con audio: {total} ({encontrados} encontrados)\n\n"
                    + "No se modificará la configuración local de esta PC: "
                    + "los cambios de esta sesión se guardan solo en el archivo cargado.",
                PrimaryButtonText = "Cargar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
            };
            return await confirmar.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.ConfirmarPortable");
            return false;
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

    /// <summary>Pide confirmación de borrado (cuenta efectos con audio).</summary>
    /// <param name="label">Nombre de la categoría.</param>
    /// <param name="propietariaId">Dueña a eliminar.</param>
    /// <returns>True si confirma (false si falla).</returns>
    private async Task<bool> ConfirmarEliminarAsync(string label, string propietariaId)
    {
        try
        {
            var conAudio = ConsolaStore.ContarSlotsConAudio(propietariaId);
            var confirmar = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = $"Eliminar {label}",
                Content = conAudio > 0
                    ? $"Se perderán {conAudio} efectos con audio. ¿Continuar?"
                    : "Se eliminará la categoría y su paleta. ¿Continuar?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
            };
            return await confirmar.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.ConfirmarEliminar");
            return false;
        }
    }

    /// <summary>Muestra un aviso breve (errores blindados del riel).</summary>
    /// <param name="mensaje">Texto del aviso.</param>
    /// <param name="titulo">Título del diálogo (default Categorías).</param>
    private async Task MostrarAvisoAsync(string mensaje, string titulo = "Categorías")
    {
        try
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["BebeDialogStyle"],
                RequestedTheme = TemaConsola.TemaActual,
                Title = titulo,
                Content = mensaje,
                CloseButtonText = "Entendido",
                DefaultButton = ContentDialogButton.Close,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Riel.Aviso");
        }
    }
}
