using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace BebeRadio.Services;

/// <summary>
/// Reproductor simple de sonidos .wav embebidos como recurso.
/// Carga el archivo una vez y lo reutiliza (bajo overhead).
/// </summary>
public sealed class SonidoService
{
    private static readonly Lazy<SonidoService> _instancia = new(() => new SonidoService());
    public static SonidoService Instancia => _instancia.Value;

    private MediaPlayer? _player;
    private bool _inicializado;

    private SonidoService() { }

    /// <summary>Inicializa el reproductor con un .wav embebido.</summary>
    /// <param name="recursoUri">URI del recurso (ej. "ms-appx:///Assets/Sounds/beep.wav").</param>
    public void Inicializar(string recursoUri)
    {
        if (_inicializado) return;

        try
        {
            _player = new MediaPlayer();
            _player.Source = MediaSource.CreateFromUri(new Uri(recursoUri));
            _player.Volume = 0.5; // Volumen moderado
            _player.AutoPlay = false;
            _inicializado = true;
        }
        catch
        {
            // Fallback silencioso
        }
    }

    /// <summary>Reproduce el sonido (fire-and-forget).</summary>
    public void Reproducir()
    {
        if (_player is null) return;

        try
        {
            _player.Play();
        }
        catch
        {
            // Ignorar errores de reproducción
        }
    }

    /// <summary>Libera recursos.</summary>
    public void Dispose()
    {
        _player?.Dispose();
        _player = null;
        _inicializado = false;
    }
}