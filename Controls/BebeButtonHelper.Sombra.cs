using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace BebeRadio.Controls;

/// <summary>
/// Sombra GPU de los botones físico 3D (parcial de <see cref="BebeButtonHelper"/>).
/// Reposo: blur 10, offset Y 4, negro 30 %. Hover: blur 12. Pressed: offset Y 1,
/// blur 5, opacidad 12 %. El glow de actividad es XAML (grupo ActivityStates).
/// </summary>
public static partial class BebeButtonHelper
{
    private const float RestBlur = 10f;
    private const float HoverBlur = 12f;
    private const float PressedBlur = 5f;
    private const float RestOpacity = 0.30f;
    private const float PressedOpacity = 0.12f;

    private static readonly DependencyProperty ShadowStateProperty =
        DependencyProperty.RegisterAttached(
            "ShadowState",
            typeof(ShadowState),
            typeof(BebeButtonHelper),
            new PropertyMetadata(null));

    /// <summary>Marca si los manejadores de puntero ya están enganchados al botón.</summary>
    private static readonly DependencyProperty ShadowWiredProperty =
        DependencyProperty.RegisterAttached(
            "ShadowWired",
            typeof(bool),
            typeof(BebeButtonHelper),
            new PropertyMetadata(false));

    /// <summary>
    /// Crea y anexa la sombra de elevación GPU al borde del template.
    /// Debe llamarse desde <c>Loaded</c>; el borde se localiza por nombre
    /// (<c>SurfaceBorder</c>). Recorta con la geometría redondeada del borde.
    /// </summary>
    /// <param name="button">Botón al que anexar la sombra.</param>
    /// <remarks>
    /// Reemplaza un estado anterior si el template se recreó. Los manejadores de
    /// puntero se enganchan una sola vez por botón y consultan el estado vigente,
    /// así que reanexar no acumula suscripciones.
    /// </remarks>
    public static void AttachShadow(FrameworkElement button)
    {
        if (button is not Button target)
        {
            return;
        }

        if (FindTemplateChild(button, "SurfaceBorder") is not FrameworkElement face)
        {
            target.ClearValue(ShadowStateProperty);
            return;
        }

        var hostVisual = ElementCompositionPreview.GetElementVisual(face);
        var compositor = hostVisual.Compositor;

        var shadow = compositor.CreateDropShadow();
        shadow.BlurRadius = RestBlur;
        shadow.Offset = new Vector3(0, 4, 0);
        shadow.Color = Windows.UI.Color.FromArgb(255, 0, 0, 0);
        shadow.Opacity = target.IsEnabled ? RestOpacity : 0f;

        var geometry = compositor.CreateRoundedRectangleGeometry();
        var clip = compositor.CreateGeometricClip(geometry);

        var sprite = compositor.CreateSpriteVisual();
        sprite.Shadow = shadow;
        sprite.Clip = clip;
        ElementCompositionPreview.SetElementChildVisual(face, sprite);

        var state = new ShadowState(shadow, sprite, geometry, face);
        target.SetValue(ShadowStateProperty, state);
        ResizeShadow(face, state);
        face.SizeChanged += (_, _) => ResizeShadow(face, state);

        if (target.GetValue(ShadowWiredProperty) is not true)
        {
            target.SetValue(ShadowWiredProperty, true);
            target.PointerEntered += (_, _) => ConEstado(target, actual => actual.Shadow.BlurRadius = HoverBlur);
            target.PointerExited += (_, _) => ConEstado(target, actual => actual.Shadow.BlurRadius = RestBlur);
            target.PointerPressed += (_, _) => ConEstado(target, actual => ApplyPressed(actual, target.IsEnabled));
            target.PointerReleased += (_, _) => ConEstado(target, actual => ApplyRest(actual, target.IsEnabled));
            target.PointerCanceled += (_, _) => ConEstado(target, actual => ApplyRest(actual, target.IsEnabled));
            target.IsEnabledChanged += (_, _) => ConEstado(target, actual => ApplyRest(actual, target.IsEnabled));
        }
    }

    /// <summary>
    /// Reanexa la sombra solo si el template se recreó (borde distinto o ausente).
    /// </summary>
    /// <param name="button">Botón a verificar.</param>
    /// <remarks>Idempotente y barato: si la sombra sigue válida no hace nada.</remarks>
    public static void EnsureShadow(FrameworkElement button)
    {
        if (button.GetValue(ShadowStateProperty) is ShadowState state
            && FindTemplateChild(button, "SurfaceBorder") is FrameworkElement face
            && ReferenceEquals(face, state.Face))
        {
            return;
        }

        AttachShadow(button);
    }

    /// <summary>Ejecuta una acción sobre el estado de sombra vigente del botón.</summary>
    /// <param name="target">Botón.</param>
    /// <param name="accion">Acción a aplicar (si hay sombra).</param>
    private static void ConEstado(DependencyObject target, Action<ShadowState> accion)
    {
        if (target.GetValue(ShadowStateProperty) is ShadowState state)
        {
            accion(state);
        }
    }

    /// <summary>Aplica física de hundido: sombra baja y atenuada.</summary>
    /// <param name="state">Estado de sombra.</param>
    /// <param name="enabled">Si el botón está habilitado.</param>
    private static void ApplyPressed(ShadowState state, bool enabled)
    {
        state.Shadow.Offset = new Vector3(0, 1, 0);
        state.Shadow.BlurRadius = PressedBlur;
        state.Shadow.Opacity = enabled ? PressedOpacity : 0f;
    }

    /// <summary>Restaura física de reposo (o sin sombra si deshabilitado).</summary>
    /// <param name="state">Estado de sombra.</param>
    /// <param name="enabled">Si el botón está habilitado.</param>
    private static void ApplyRest(ShadowState state, bool enabled)
    {
        state.Shadow.Offset = new Vector3(0, 4, 0);
        state.Shadow.BlurRadius = RestBlur;
        state.Shadow.Opacity = enabled ? RestOpacity : 0f;
    }

    /// <summary>Sincroniza sprite y geometría con el tamaño real de la cara.</summary>
    /// <param name="face">Borde o elipse del template (aporta tamaño y radio).</param>
    /// <param name="state">Estado de sombra a actualizar.</param>
    /// <remarks>
    /// La geometría de recorte es de radio uniforme, así que se usa el MAYOR
    /// de los cuatro radios de la cara: con esquinas iguales (carts, transporte)
    /// es exactamente el de siempre; con las esquinas asimétricas de los tiles
    /// de la tira evita que el recorte se quede corto en el extremo redondeado.
    /// </remarks>
    private static void ResizeShadow(FrameworkElement face, ShadowState state)
    {
        var width = (float)face.ActualSize.X;
        var height = (float)face.ActualSize.Y;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        state.Sprite.Size = new Vector2(width, height);
        state.Geometry.Size = new Vector2(width, height);

        // Border: mayor radio de sus esquinas; Ellipse (pad circular): mitad del lado menor.
        var radius = face is Border border
            ? (float)Math.Max(
                Math.Max(border.CornerRadius.TopLeft, border.CornerRadius.TopRight),
                Math.Max(border.CornerRadius.BottomLeft, border.CornerRadius.BottomRight))
            : Math.Min(width, height) / 2f;
        state.Geometry.CornerRadius = new Vector2(radius, radius);
    }

    /// <summary>Busca un hijo nombrado dentro del template aplicado.</summary>
    /// <param name="parent">Elemento con template aplicado.</param>
    /// <param name="name">Nombre del hijo (p. ej. SurfaceBorder).</param>
    /// <returns>El hijo o null si no existe.</returns>
    private static DependencyObject? FindTemplateChild(DependencyObject parent, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement named && named.Name == name)
            {
                return named;
            }

            var nested = FindTemplateChild(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>Recursos Composition de la sombra de un botón (un estado por botón).</summary>
    /// <param name="Shadow">Sombra de elevación.</param>
    /// <param name="Sprite">Visual que porta la sombra.</param>
    /// <param name="Geometry">Geometría redondeada de recorte.</param>
    /// <param name="Face">Borde del template dueño de la sombra (detección de re-template).</param>
    private sealed record ShadowState(
        DropShadow Shadow, SpriteVisual Sprite, CompositionRoundedRectangleGeometry Geometry, FrameworkElement Face);
}
