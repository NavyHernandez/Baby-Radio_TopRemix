using System.ComponentModel;
using BebeRadio.Models;
using BebeRadio.Support;
using BebeRadio.ViewModels.Console;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace BebeRadio.Controls.Console;

/// <summary>
/// Pantalla del modo operador: dos bancos independientes + tira compacta.
/// El code-behind enruta salir (botón y Esc) y los flujos de categorías
/// (crear/editar/gestionar) a <see cref="ConsolaViewModel"/>; la reproducción
/// la gobiernan los bancos y la mezcla global.
/// </summary>
public sealed partial class OperadorConsolaPanel : UserControl
{
    /// <summary>Propiedad de dependencia del orquestador.</summary>
    public static readonly DependencyProperty ConsolaProperty =
        DependencyProperty.Register(
            nameof(Consola),
            typeof(ConsolaViewModel),
            typeof(OperadorConsolaPanel),
            new PropertyMetadata(null));

    /// <summary>Orquestador (lo inyecta la shell).</summary>
    public ConsolaViewModel? Consola
    {
        get => (ConsolaViewModel?)GetValue(ConsolaProperty);
        set => SetValue(ConsolaProperty, value);
    }

    private bool _dialogoAbierto;
    private TitileoArmado? _titileoMix;

    /// <summary>Inicializa el panel, anexa sombras y reenvía eventos de los bancos.</summary>
    public OperadorConsolaPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnDescargado;
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => SincronizarTitileoMix());
        BancoA.PideGestionarCategorias += FlujoGestionarDisparado;
        BancoB.PideGestionarCategorias += FlujoGestionarDisparado;
        BancoA.PideNuevaCategoria += FlujoNuevaDisparado;
        BancoB.PideNuevaCategoria += FlujoNuevaDisparado;
    }

    /// <summary>Anexa sombras GPU y engancha el titileo de Mix al cargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsolaSombraHelper.AttachAllShadows(this);
        _titileoMix ??= new TitileoArmado(MixButton, DispatcherQueue);
        if (Consola?.Acciones is not null)
        {
            Consola.Acciones.PropertyChanged -= OnAccionesCambiadas;
            Consola.Acciones.PropertyChanged += OnAccionesCambiadas;
        }

        SincronizarTitileoMix();
    }

    /// <summary>Desengancha el titileo de Mix al descargar.</summary>
    /// <param name="sender">Este control.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnDescargado(object sender, RoutedEventArgs e)
    {
        if (Consola?.Acciones is not null)
        {
            Consola.Acciones.PropertyChanged -= OnAccionesCambiadas;
        }

        _titileoMix?.Apagar();
    }

    /// <summary>Sincroniza el titileo de Mix con su armado.</summary>
    /// <param name="sender">Acciones.</param>
    /// <param name="e">Propiedad cambiada.</param>
    private void OnAccionesCambiadas(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccionesConsolaViewModel.IsMixArmed))
        {
            SincronizarTitileoMix();
        }
    }

    /// <summary>Titila Mix si está armado y el panel es visible.</summary>
    private void SincronizarTitileoMix()
    {
        if (Visibility == Visibility.Visible
            && Consola is not null
            && Consola.Acciones.IsMixArmed)
        {
            _titileoMix?.Sincronizar(true);
        }
        else
        {
            _titileoMix?.Apagar();
        }
    }

    /// <summary>Sale del modo operador (botón).</summary>
    /// <param name="sender">Botón salir.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnSalirClick(object sender, RoutedEventArgs e) => Consola?.SalirOperador();

    /// <summary>Sale del modo operador con Escape.</summary>
    /// <param name="sender">Origen del teclado.</param>
    /// <param name="e">Tecla pulsada.</param>
    private void OnTecla(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            Consola?.SalirOperador();
        }
    }

    /// <summary>Pulso de glow en botones de consola (feedback).</summary>
    /// <param name="sender">Botón pulsado.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnConsolaFlashClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            ConsolaSombraHelper.FlashActive(button, DispatcherQueue);
        }
    }

    /// <summary>Abre la ventana de ajustes (sin reentrancia).</summary>
    /// <param name="sender">Botón Config.</param>
    /// <param name="e">Args de enrutado.</param>
    /// <remarks>Blindado: un fallo no cierra la app.</remarks>
    private async void OnConfigClick(object sender, RoutedEventArgs e)
    {
        if (_dialogoAbierto)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            if (sender is Button boton)
            {
                ConsolaSombraHelper.FlashActive(boton, DispatcherQueue);
            }

            await new ConfiguracionDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Operador.Config");
        }
        finally
        {
            _dialogoAbierto = false;
        }
    }

    /// <summary>Abre el gestor desde la tira (sin reentrancia).</summary>
    /// <param name="sender">Botón categorías.</param>
    /// <param name="e">Args de enrutado.</param>
    private void OnGestionarClick(object sender, RoutedEventArgs e) => FlujoGestionarDisparado();

    /// <summary>Abre el gestor (protegido contra diálogos dobles).</summary>
    private void FlujoGestionarDisparado()
    {
        if (!_dialogoAbierto)
        {
            _ = FlujoGestionarAsync();
        }
    }

    /// <summary>Abre la creación (protegida contra diálogos dobles).</summary>
    private void FlujoNuevaDisparado()
    {
        if (!_dialogoAbierto)
        {
            _ = FlujoNuevaAsync();
        }
    }

    /// <summary>Flujo del gestor (toggles, orden, editar, eliminar, nueva).</summary>
    private async Task FlujoGestionarAsync()
    {
        if (Consola is null)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            while (true)
            {
                var gestor = new GestionCategoriasDialog { XamlRoot = XamlRoot };
                gestor.Cargar(Consola.Categorias);
                var resultado = await gestor.ShowAsync();
                if (resultado == ContentDialogResult.Primary)
                {
                    Consola.Categorias.AplicarGestion(gestor.Resultado());
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
                    var fila = Consola.Categorias.Todas().FirstOrDefault(c => c.PropietariaId == gestor.PidioEliminar);
                    if (fila is not null && await ConfirmarEliminarAsync(fila.Swatch.Label, fila.PropietariaId))
                    {
                        Consola.Categorias.Eliminar(fila.PropietariaId);
                    }

                    continue;
                }

                return;
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Operador.Gestionar");
            await MostrarAvisoAsync("No se pudo abrir el gestor. El error quedó registrado.");
        }
        finally
        {
            _dialogoAbierto = false;
            ResincronizarBancos();
            ConsolaSombraHelper.AttachAllShadows(this);
        }
    }

    /// <summary>Flujo de creación de categoría.</summary>
    private async Task FlujoNuevaAsync()
    {
        if (Consola is null)
        {
            return;
        }

        _dialogoAbierto = true;
        try
        {
            var dialogo = new CategoriaEditorDialog { XamlRoot = XamlRoot };
            dialogo.Preparar("Nueva categoría", null);
            if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
            {
                Consola.Categorias.Crear(dialogo.Resultado());
                ConsolaSombraHelper.AttachAllShadows(this);
            }
            else if (dialogo.PidioGestionar)
            {
                await FlujoGestionarAsync();
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Operador.Crear");
            await MostrarAvisoAsync("No se pudo crear la categoría. El error quedó registrado.");
        }
        finally
        {
            _dialogoAbierto = false;
            ResincronizarBancos();
            ConsolaSombraHelper.AttachAllShadows(this);
        }
    }

    /// <summary>Edita una categoría por id y vuelve.</summary>
    /// <param name="propietariaId">Dueña a editar.</param>
    private async Task FlujoEditarIdAsync(string propietariaId)
    {
        if (Consola is null)
        {
            return;
        }

        var registro = Consola.Categorias.ObtenerRegistro(propietariaId);
        if (registro is null)
        {
            return;
        }

        var dialogo = new CategoriaEditorDialog { XamlRoot = XamlRoot };
        dialogo.Preparar($"Editar {registro.Label}", registro);
        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
        {
            Consola.Categorias.AplicarEdicion(propietariaId, dialogo.Resultado());
            ConsolaSombraHelper.AttachAllShadows(this);
        }
    }

    /// <summary>
    /// Re-despliega ambos bancos desde sus selectores (tras gestionar/crear/
    /// eliminar/importar, el riel pudo redeplegar la paleta central).
    /// </summary>
    private void ResincronizarBancos()
    {
        if (Consola is null)
        {
            return;
        }

        Consola.SelectorA.AsegurarSeleccion();
        Consola.SelectorB.AsegurarSeleccion();
        if (Consola.SelectorA.Selected is not null)
        {
            Consola.PaletaA.Desplegar(Consola.SelectorA.Selected);
        }
        else
        {
            Consola.PaletaA.LimpiarSeleccion();
        }

        if (Consola.SelectorB.Selected is not null)
        {
            Consola.PaletaB.Desplegar(Consola.SelectorB.Selected);
        }
        else
        {
            Consola.PaletaB.LimpiarSeleccion();
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
            RegistroErrores.Registrar(ex, "Operador.ConfirmarEliminar");
            return false;
        }
    }

    /// <summary>Muestra un aviso breve (errores blindados).</summary>
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
                Title = "Operador",
                Content = mensaje,
                CloseButtonText = "Entendido",
                DefaultButton = ContentDialogButton.Close,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "Operador.Aviso");
        }
    }
}
