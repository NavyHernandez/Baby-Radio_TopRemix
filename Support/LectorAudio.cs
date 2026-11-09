using NAudio.Vorbis;
using NAudio.Wave;

namespace BebeRadio.Support;

/// <summary>
/// Fábrica compartida de lectores de audio (motor de cola + mezclador de
/// efectos). Cadena: .ogg → <see cref="VorbisWaveReader"/> (Media Foundation
/// no trae decodificador Vorbis); el resto → <see cref="AudioFileReader"/> con
/// fallback a <see cref="MediaFoundationReader"/> (flac/aac nativos en
/// Windows 10 19041+).
/// </summary>
public static class LectorAudio
{
    /// <summary>
    /// Abre un lector posicionable para la ruta dada.
    /// </summary>
    /// <param name="path">Ruta del archivo de audio.</param>
    /// <returns>Lector posicionable (<c>WaveStream</c>).</returns>
    /// <remarks>Lanza si el archivo es ilegible o el formato no está soportado.</remarks>
    public static WaveStream Crear(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return new VorbisWaveReader(path);
        }

        try
        {
            return new AudioFileReader(path);
        }
        catch
        {
            return new MediaFoundationReader(path);
        }
    }
}
