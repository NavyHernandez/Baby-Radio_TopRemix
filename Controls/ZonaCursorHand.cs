using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace BebeRadio.Controls;

/// <summary>
/// Grid con cursor de mano fijo (zona de hit del seek en la barra de progreso).
/// <see cref="UIElement.ProtectedCursor"/> solo puede asignarse desde un tipo
/// derivado del elemento, de ahí este helper de una línea.
/// </summary>
public sealed class ZonaCursorHand : Grid
{
    /// <summary>Inicializa la zona con cursor de mano.</summary>
    public ZonaCursorHand()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
