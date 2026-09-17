namespace BebeRadio.Support;

/// <summary>
/// Datos de ayuda de Baby Radio (único punto a editar).
/// PENDIENTE del owner: número WhatsApp, PNGs QR en Assets/ y URL de GitHub.
/// </summary>
public static class ContactoInfo
{
    /// <summary>Número WhatsApp con código país (p. ej. 18095551234).</summary>
    public const string WhatsAppNumero = "";

    /// <summary>Mensaje precargado del chat.</summary>
    public const string WhatsAppMensaje = "Hola, necesito soporte con Baby Radio";

    /// <summary>Web de la emisora.</summary>
    public const string WebTopRemix = "https://www.top-remix.com";

    /// <summary>QR de la cuenta de usuario (Assets/usuario-qr.png).</summary>
    public const string QrUsuarioPath = "ms-appx:///Assets/usuario-qr.png";

    /// <summary>QR de soporte (Assets/soporte-qr.png).</summary>
    public const string QrSoportePath = "ms-appx:///Assets/soporte-qr.png";

    /// <summary>Releases de GitHub para auto-actualizar (vacío = pendiente).</summary>
    public const string GitHubReleasesUrl = "https://github.com/NavyHernandez/Baby-Radio_TopRemix";

    /// <summary>Link directo al chat (vacío si falta el número).</summary>
    public static string ChatUrl => string.IsNullOrWhiteSpace(WhatsAppNumero)
        ? string.Empty
        : $"https://wa.me/{WhatsAppNumero}?text={Uri.EscapeDataString(WhatsAppMensaje)}";

    /// <summary>Versión instalada (paquete o ensamblado).</summary>
    public static string VersionInstalada
    {
        get
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            }
        }
    }
}
