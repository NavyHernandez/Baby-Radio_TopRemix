using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi;

namespace BebeRadio.Support;

/// <summary>
/// Representa un dispositivo de salida de audio disponible en el sistema.
/// </summary>
public sealed class DispositivoAudio
{
    /// <summary>Identificador único del dispositivo (GUID de Windows).</summary>
    public required string Id { get; init; }

    /// <summary>Nombre amigable del dispositivo (ej: "Altavoces (Realtek Audio)").</summary>
    public required string Nombre { get; init; }

    /// <summary>True si es el dispositivo predeterminado del sistema.</summary>
    public bool EsPredeterminado { get; init; }

    /// <summary>Devuelve el nombre del dispositivo para mostrar en la UI.</summary>
    /// <returns>Nombre del dispositivo.</returns>
    public override string ToString() => Nombre;
}

/// <summary>
/// Cliente de notificación de cambios en dispositivos de audio.
/// Implementa IMMNotificationClient para recibir eventos del sistema.
/// </summary>
internal sealed class ClienteNotificacionAudio : IMMNotificationClient
{
    private readonly Action _onChanged;

    /// <summary>Crea el cliente de notificación.</summary>
    /// <param name="onChanged">Callback a ejecutar cuando cambia un dispositivo.</param>
    public ClienteNotificacionAudio(Action onChanged)
    {
        _onChanged = onChanged;
    }

    /// <summary>Se llama cuando cambia el estado de un dispositivo.</summary>
    /// <param name="deviceId">ID del dispositivo.</param>
    /// <param name="newState">Nuevo estado.</param>
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _onChanged();

    /// <summary>Se llama cuando se agrega un dispositivo.</summary>
    /// <param name="deviceId">ID del dispositivo agregado.</param>
    public void OnDeviceAdded(string deviceId) => _onChanged();

    /// <summary>Se llama cuando se remueve un dispositivo.</summary>
    /// <param name="deviceId">ID del dispositivo removado.</param>
    public void OnDeviceRemoved(string deviceId) => _onChanged();

    /// <summary>Se llama cuando cambia la propiedad de un dispositivo.</summary>
    /// <param name="deviceId">ID del dispositivo.</param>
    /// <param name="key">Clave de la propiedad.</param>
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }

    /// <summary>Se llama cuando cambia el dispositivo predeterminado.</summary>
    /// <param name="flow">Flujo de datos.</param>
    /// <param name="role">Rol del dispositivo.</param>
    /// <param name="defaultDeviceId">ID del nuevo dispositivo predeterminado.</param>
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => _onChanged();
}

/// <summary>
/// Enumerador de dispositivos de salida de audio con notificación de cambios.
/// Singleton que mantiene la lista actualizada y notifica cuando se conecta,
/// desconecta o cambia el estado de un dispositivo.
/// </summary>
public sealed class EnumeradorDispositivosAudio : IDisposable
{
    private static readonly Lazy<EnumeradorDispositivosAudio> _lazy = new(() => new());
    /// <summary>Instancia única del enumerador.</summary>
    public static EnumeradorDispositivosAudio Instancia => _lazy.Value;

    private readonly MMDeviceEnumerator _enumerator;
    private readonly ClienteNotificacionAudio _notificacion;
    private readonly object _candado = new();
    private List<DispositivoAudio> _dispositivos = [];
    private bool _disposed;

    /// <summary>Se dispara cuando cambia la lista de dispositivos (conexión/desconexión/estado).</summary>
    public event Action? DispositivosCambiaron;

    /// <summary>Crea el enumerador y registra la notificación de cambios.</summary>
    private EnumeradorDispositivosAudio()
    {
        _enumerator = new MMDeviceEnumerator();
        _notificacion = new ClienteNotificacionAudio(OnDispositivoCambiado);
        _enumerator.RegisterEndpointNotificationCallback(_notificacion);
        ActualizarLista();
    }

    /// <summary>Lista actual de dispositivos de salida activos.</summary>
    /// <returns>Lista de dispositivos (vacía si hay error).</returns>
    public IReadOnlyList<DispositivoAudio> ObtenerDispositivos()
    {
        lock (_candado)
        {
            return _dispositivos.ToList();
        }
    }

    /// <summary>Obtiene un dispositivo por su ID.</summary>
    /// <param name="id">Identificador del dispositivo.</param>
    /// <returns>El dispositivo o null si no existe.</returns>
    public MMDevice? ObtenerMMDevice(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return _enumerator.GetDevice(id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Obtiene el dispositivo predeterminado de multimedia.</summary>
    /// <returns>El dispositivo predeterminado.</returns>
    /// <exception cref="InvalidOperationException">Si no hay dispositivo predeterminado.</exception>
    public MMDevice ObtenerDispositivoPorDefecto()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("No se encontró un dispositivo de salida predeterminado.", ex);
        }
    }

    /// <summary>Actualiza la lista de dispositivos desde el enumerador.</summary>
    private void ActualizarLista()
    {
        List<DispositivoAudio> nuevaLista;
        try
        {
            var defaultId = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            nuevaLista = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(d => new DispositivoAudio
                {
                    Id = d.ID,
                    Nombre = d.FriendlyName,
                    EsPredeterminado = d.ID == defaultId,
                })
                .OrderBy(d => !d.EsPredeterminado)
                .ThenBy(d => d.Nombre)
                .ToList();
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, "EnumeradorDispositivosAudio.ActualizarLista");
            nuevaLista = [];
        }

        lock (_candado)
        {
            _dispositivos = nuevaLista;
        }
    }

    /// <summary>Callback cuando un dispositivo cambia (conexión/desconexión/estado).</summary>
    private void OnDispositivoCambiado()
    {
        ActualizarLista();
        DispositivosCambiaron?.Invoke();
    }

    /// <summary>Libera el enumerador y el cliente de notificación.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.UnregisterEndpointNotificationCallback(_notificacion);
        _enumerator.Dispose();
    }
}
