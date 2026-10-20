using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Wasapi;

namespace BebeRadio.Support;

/// <summary>
/// Crea salidas WasapiOut adaptadas al dispositivo elegido.
/// Los controladores DJ (Pioneer DDJ-FLX4/SZ, DDJ-1000SRT y afines) exponen
/// endpoints WASAPI multicanal (4–8 canales: master + booth + cue, 48/96 kHz);
/// un Init estéreo 44.1k a esos dispositivos puede fallar con
/// AUDCLNT_E_UNSUPPORTED_FORMAT o sonar en canales que no son el master.
/// Este adaptador multiplexa el estéreo a los canales 0/1 (master L/R)
/// usando el MixFormat del dispositivo tal cual (float32 con su channel
/// mask: un WAVEFORMATEX pelado de N canales lo rechaza
/// IAudioClient.Initialize con E_INVALIDARG —p. ej. DDJ-FLX4—), reintenta
/// con estéreo directo (el motor shared hace el upmix) y, si el dispositivo
/// pedido y el predeterminado fallan, prueba los demás endpoints activos
/// antes de rendirse. Siempre resamplea la fuente a la tasa del dispositivo
/// ANTES del multiplex (un mp3 44.1 kHz reléado 1:1 al mix 48 kHz del FLX4 se
/// reproduciría rápido y con alias = mala calidad). Si el fallo es
/// AUDCLNT_E_DEVICE_IN_USE (otra app —p. ej.
/// Serato DJ— retiene el endpoint en modo exclusivo) aborta al instante los
/// formatos restantes y avisa con <see cref="RespaldoPorOcupacion"/> para que
/// <see cref="RecuperacionSalidaAudio"/> vigile la liberación. Cada intento
/// fallido se registra con nombre, mix format y HRESULT para diagnosticar.
/// </summary>
internal static class AdaptadorSalidaAudio
{
    /// <summary>HRESULT AUDCLNT_E_DEVICE_IN_USE: otra sesión retiene el endpoint.</summary>
    private const int HResultDispositivoEnUso = unchecked((int)0x8889000A);

    /// <summary>
    /// Se dispara cuando el dispositivo pedido cae a respaldo por estar ocupado
    /// por otra aplicación en modo exclusivo. Argumentos: ID y nombre del
    /// dispositivo preferido (objetivo de la recuperación automática).
    /// </summary>
    public static event Action<string, string>? RespaldoPorOcupacion;

    /// <summary>Crea e inicializa una salida WasapiOut (shared mode) adaptada al dispositivo.</summary>
    /// <param name="dispositivo">Dispositivo de destino (MMDevice activo).</param>
    /// <param name="fuente">Cadena de audio estéreo a reproducir (ISampleProvider).</param>
    /// <param name="latenciaMs">Latencia del buffer en milisegundos.</param>
    /// <param name="contexto">Etiqueta para el log (p. ej. "MotorAudio.Abrir").</param>
    /// <param name="registrarFallos">False = no escribir en el log (sondeos periódicos; evita spam).</param>
    /// <returns>Salida inicializada lista para <c>Play()</c>.</returns>
    /// <remarks>
    /// Orden de intentos: multiplex extensible (event-sync, polling, resample)
    /// y después estéreo directo (event-sync, polling). Si todos fallan lanza
    /// <see cref="InvalidOperationException"/> con la última excepción como
    /// InnerException.
    /// </remarks>
    public static IWavePlayer CrearInicializada(
        MMDevice dispositivo, ISampleProvider fuente, int latenciaMs, string contexto,
        bool registrarFallos = true)
    {
        var nombre = SafeName(dispositivo);
        WaveFormat mixFormat;
        try
        {
            mixFormat = dispositivo.AudioClient.MixFormat;
        }
        catch (Exception ex)
        {
            if (registrarFallos)
            {
                RegistroErrores.Registrar(ex, $"{contexto}.MixFormat({nombre})");
            }

            mixFormat = fuente.WaveFormat;
        }

        var canalesMezcla = Math.Max(2, mixFormat.Channels);
        var descripcion = $"{nombre} mix={mixFormat.SampleRate}Hz/{mixFormat.Channels}ch";
        var formatoMultiplex = canalesMezcla > 2 ? FormatoMultiplex(mixFormat, canalesMezcla) : null;
        Exception? ultimo = null;

        // Aborta al instante si el endpoint está retenido por otra aplicación:
        // los formatos restantes fallarían igual con DEVICE_IN_USE.
        void AbortarSiOcupado(Exception? ex)
        {
            if (EsDispositivoOcupado(ex))
            {
                throw new InvalidOperationException(
                    $"Dispositivo '{nombre}' ocupado por otra aplicación en modo exclusivo " +
                    "(¿Serato DJ/ASIO?); no se prueban más formatos.", ex);
            }
        }

        // Intentos A-C: multiplex master L/R a canales 0/1 (formato extensible
        // del mix: el channel mask evita E_INVALIDARG en endpoints multicanal).
        var salida = TryIntento(dispositivo, fuente, formatoMultiplex, latenciaMs,
            useEventSync: true, resamplear: false, $"{contexto}[A event-sync {descripcion}]",
            ref ultimo, registrarFallos);
        if (salida is not null)
        {
            return salida;
        }

        AbortarSiOcupado(ultimo);

        salida = TryIntento(dispositivo, fuente, formatoMultiplex, latenciaMs,
            useEventSync: false, resamplear: false, $"{contexto}[B polling {descripcion}]",
            ref ultimo, registrarFallos);
        if (salida is not null)
        {
            return salida;
        }

        AbortarSiOcupado(ultimo);

        salida = TryIntento(dispositivo, fuente, formatoMultiplex, latenciaMs,
            useEventSync: false, resamplear: true, $"{contexto}[C resample {descripcion}]",
            ref ultimo, registrarFallos);
        if (salida is not null)
        {
            return salida;
        }

        AbortarSiOcupado(ultimo);

        // Intentos D-E: estéreo directo SIN multiplex. El motor shared
        // (AutoConvertPcm) upmixea 2ch al mix del dispositivo (canales front
        // = master en la mayoría de controladores DJ). Cobertura para
        // endpoints donde el Init multicanal sigue fallando.
        if (formatoMultiplex is not null)
        {
            salida = TryIntento(dispositivo, fuente, formatoMultiplex: null, latenciaMs,
                useEventSync: true, resamplear: false, $"{contexto}[D estereo event-sync {descripcion}]",
                ref ultimo, registrarFallos);
            if (salida is not null)
            {
                return salida;
            }

            AbortarSiOcupado(ultimo);

            salida = TryIntento(dispositivo, fuente, formatoMultiplex: null, latenciaMs,
                useEventSync: false, resamplear: false, $"{contexto}[E estereo polling {descripcion}]",
                ref ultimo, registrarFallos);
            if (salida is not null)
            {
                return salida;
            }

            AbortarSiOcupado(ultimo);
        }

        throw new InvalidOperationException(
            $"No se pudo inicializar la salida de audio en '{nombre}' " +
            $"(mix {mixFormat.SampleRate} Hz, {mixFormat.Channels} canales).", ultimo);
    }

    /// <summary>
    /// Crea la salida en el dispositivo pedido y, si el Init falla, reintenta
    /// con el predeterminado y, en último lugar, con los demás endpoints activos.
    /// </summary>
    /// <param name="dispositivoId">ID WASAPI del dispositivo (vacío = predeterminado).</param>
    /// <param name="fuente">Cadena de audio estéreo a reproducir (ISampleProvider).</param>
    /// <param name="latenciaMs">Latencia del buffer en milisegundos.</param>
    /// <param name="contexto">Etiqueta para el log.</param>
    /// <returns>Salida inicializada lista para <c>Play()</c>.</returns>
    /// <remarks>
    /// Centraliza el fallback de MotorAudio y MezcladorEfectos: cambiar de
    /// salida no debe dejar muda a la app ni requerir reinicio. Si el
    /// predeterminado es el mismo dispositivo fallido (p. ej. el controlador
    /// DJ es también el default de Windows) o también falla, se prueban los
    /// demás endpoints de render activos y se usa el primero que initialise.
    /// Si ninguno funciona, propaga la excepción del primer intento.
    /// </remarks>
    public static IWavePlayer CrearInicializadaConFallback(
        string dispositivoId, ISampleProvider fuente, int latenciaMs, string contexto)
    {
        MMDevice? preferido = string.IsNullOrEmpty(dispositivoId)
            ? null
            : EnumeradorDispositivosAudio.Instancia.ObtenerMMDevice(dispositivoId);

        if (!string.IsNullOrEmpty(dispositivoId) && preferido is null)
        {
            RegistroErrores.Traza(
                contexto, $"Dispositivo '{dispositivoId}' no disponible; se usa el predeterminado.");
        }

        var destino = preferido ?? EnumeradorDispositivosAudio.Instancia.ObtenerDispositivoPorDefecto();
        try
        {
            return CrearInicializada(destino, fuente, latenciaMs, contexto);
        }
        catch (Exception exPrincipal) when (preferido is not null)
        {
            if (EsDispositivoOcupado(exPrincipal))
            {
                RegistroErrores.Traza(contexto,
                    $"Dispositivo '{SafeName(preferido)}' ocupado por otra aplicación en modo exclusivo " +
                    "(¿Serato DJ/ASIO?); se usa el respaldo y se vigilará su liberación.");
                RespaldoPorOcupacion?.Invoke(dispositivoId, SafeName(preferido));
            }

            RegistroErrores.Registrar(exPrincipal, $"{contexto}.Fallback({SafeName(preferido)})");
            return CrearEnAlternativas(destino, preferido.ID, fuente, latenciaMs, contexto, exPrincipal);
        }
        catch (Exception exPrincipal) when (preferido is null)
        {
            // El predeterminado (único destino pedido) falló: probar el resto.
            if (EsDispositivoOcupado(exPrincipal))
            {
                RegistroErrores.Traza(contexto,
                    "El dispositivo predeterminado está ocupado por otra aplicación en modo exclusivo " +
                    "(¿Serato DJ/ASIO?); se prueban los demás endpoints.");
            }

            RegistroErrores.Registrar(exPrincipal, $"{contexto}.Fallback(default)");
            return CrearEnAlternativas(destino, destino.ID, fuente, latenciaMs, contexto, exPrincipal);
        }
    }

    /// <summary>Prueba el predeterminado (si no es el fallido) y luego los demás endpoints activos.</summary>
    /// <param name="fallido">Dispositivo cuyo Init acaba de fallar.</param>
    /// <param name="fallidoId">ID del dispositivo fallido (para no repetirlo).</param>
    /// <param name="fuente">Cadena estéreo de origen.</param>
    /// <param name="latenciaMs">Latencia del buffer.</param>
    /// <param name="contexto">Etiqueta del log.</param>
    /// <param name="exPrincipal">Excepción original del fallo preferido.</param>
    /// <returns>Salida inicializada en el primer alternativo que funcione.</returns>
    /// <remarks>Lanza <paramref name="exPrincipal"/> si ningún alternativo funciona.</remarks>
    private static IWavePlayer CrearEnAlternativas(
        MMDevice fallido, string fallidoId, ISampleProvider fuente, int latenciaMs,
        string contexto, Exception exPrincipal)
    {
        // 1) Predeterminado del sistema, si no es el mismo endpoint fallido.
        try
        {
            var defecto = EnumeradorDispositivosAudio.Instancia.ObtenerDispositivoPorDefecto();
            if (!string.Equals(defecto.ID, fallidoId, StringComparison.OrdinalIgnoreCase))
            {
                return CrearInicializada(defecto, fuente, latenciaMs, $"{contexto}[fallback default]");
            }
        }
        catch (Exception ex)
        {
            RegistroErrores.Registrar(ex, $"{contexto}.Fallback.Default");
        }

        // 2) Cualquier otro endpoint de render activo (p. ej. Altavoces Realtek
        //    cuando el default es el controlador DJ y ese falla).
        foreach (var candidato in EnumeradorDispositivosAudio.Instancia.ObtenerDispositivos())
        {
            if (string.Equals(candidato.Id, fallidoId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var mm = EnumeradorDispositivosAudio.Instancia.ObtenerMMDevice(candidato.Id);
            if (mm is null)
            {
                continue;
            }

            try
            {
                return CrearInicializada(mm, fuente, latenciaMs, $"{contexto}[fallback {candidato.Nombre}]");
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, $"{contexto}.Fallback({candidato.Nombre})");
            }
        }

        // Ninguno disponible: propagar el fallo original del dispositivo pedido.
        throw exPrincipal;
    }

    /// <summary>
    /// Detecta si una excepción (o alguna de sus inner) es
    /// AUDCLNT_E_DEVICE_IN_USE: el endpoint lo retiene otra aplicación en
    /// modo exclusivo (p. ej. Serato DJ sobre un controlador DJ).
    /// </summary>
    /// <param name="ex">Excepción a examinar (puede ser null).</param>
    /// <returns>True si el fallo es por dispositivo ocupado.</returns>
    public static bool EsDispositivoOcupado(Exception? ex)
    {
        for (var actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.HResult == HResultDispositivoEnUso)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sondea si el dispositivo admite abrir una salida ahora mismo: crea una
    /// WasapiOut descartable en modo shared (Init sin Play: no suena) y la
    /// libera. Si otra aplicación lo retiene en exclusivo, el Init falla con
    /// DEVICE_IN_USE y devuelve false sin interrumpir el audio en curso.
    /// </summary>
    /// <param name="dispositivoId">ID WASAPI del dispositivo a sondear.</param>
    /// <returns>True si el dispositivo está libre (una salida se pudo inicializar).</returns>
    public static bool EstaLibre(string dispositivoId)
    {
        if (string.IsNullOrWhiteSpace(dispositivoId))
        {
            return false;
        }

        var mm = EnumeradorDispositivosAudio.Instancia.ObtenerMMDevice(dispositivoId);
        if (mm is null)
        {
            return false;
        }

        try
        {
            using var salida = CrearInicializada(mm, new FuenteSilencio(), 100, "SondeoDispositivo",
                registrarFallos: false);
            return true;
        }
        catch
        {
            // Ocupado, desconectado o formato no admitido: para la sonda basta con "no".
            return false;
        }
    }

    /// <summary>Fuente silenciosa estéreo 48 kHz para sondas de dispositivo (sin Play no suena).</summary>
    private sealed class FuenteSilencio : ISampleProvider
    {
        /// <summary>Formato float32 estéreo 48 kHz.</summary>
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        /// <summary>rellena con silencio y devuelve el bloque pedido.</summary>
        /// <param name="buffer">Buffer de destino.</param>
        /// <param name="offset">Desplazamiento inicial.</param>
        /// <param name="count">Muestras a escribir.</param>
        /// <returns><paramref name="count"/> (siempre bloque completo).</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }
    }

    /// <summary>
    /// Elige el formato del multiplex. En WASAPI shared el MixFormat es float32
    /// con el channel mask del driver: si coincide en canales/tasa se usa TAL
    /// CUAL (copiarlo conserva la mask; un WAVEFORMATEX pelado sin mask lo
    /// rechaza IAudioClient.Initialize con E_INVALIDARG —p. ej. DDJ-FLX4—).
    /// Solo si no coincide se fabrica un float32 genérico.
    /// </summary>
    /// <param name="mixFormat">Mix format del dispositivo (o fallback).</param>
    /// <param name="canales">Canales de salida (mínimo 2).</param>
    /// <returns>Formato float32 de N canales.</returns>
    private static WaveFormat FormatoMultiplex(WaveFormat mixFormat, int canales)
    {
        var n = Math.Max(2, canales);
        var esFloat32 = mixFormat.Encoding == WaveFormatEncoding.IeeeFloat && mixFormat.BitsPerSample == 32;

        // Camino normal del driver: reutilizar el MixFormat (mask incluida).
        if (esFloat32 && mixFormat.Channels == n && mixFormat.SampleRate > 0)
        {
            return mixFormat;
        }

        var tasa = mixFormat.SampleRate > 0 ? mixFormat.SampleRate : 48000;
        return WaveFormat.CreateIeeeFloatWaveFormat(tasa, n);
    }

    /// <summary>Intenta crear e inicializar una salida; devuelve null si falla.</summary>
    /// <param name="dispositivo">Dispositivo de destino.</param>
    /// <param name="fuente">Cadena estéreo de origen.</param>
    /// <param name="formatoMultiplex">Formato multicanal del multiplex; null = estéreo directo.</param>
    /// <param name="latenciaMs">Latencia del buffer.</param>
    /// <param name="useEventSync">True = event-sync; false = polling.</param>
    /// <param name="resamplear">True = resamplear también sin multiplex (estéreo directo).</param>
    /// <param name="etiqueta">Contexto del log.</param>
    /// <param name="ultimo">Última excepción vista (se acumula).</param>
    /// <param name="registrarFallos">False = no escribir en el log (sondeo silencioso).</param>
    /// <returns>Salida inicializada o null si el intento falló.</returns>
    private static IWavePlayer? TryIntento(
        MMDevice dispositivo, ISampleProvider fuente, WaveFormat? formatoMultiplex, int latenciaMs,
        bool useEventSync, bool resamplear, string etiqueta, ref Exception? ultimo,
        bool registrarFallos = true)
    {
        WasapiOut? salida = null;
        try
        {
            var cadena = (ISampleProvider)fuente;

            // Resamplear la fuente a la tasa del dispositivo ANTES del multiplex
            // (y antes del Init si se pidió resamplear): el multiplex reléa tramas
            // 1:1 pero declara el MixFormat (p. ej. 48 kHz del FLX4); con una
            // fuente a 44.1 kHz se reproduciría ~8,8 % rápida y con alias.
            if (formatoMultiplex is not null || resamplear)
            {
                try
                {
                    var rate = dispositivo.AudioClient.MixFormat.SampleRate;
                    if (rate > 0 && rate != cadena.WaveFormat.SampleRate)
                    {
                        cadena = new WdlResamplingSampleProvider(cadena, rate);
                    }
                }
                catch (Exception ex)
                {
                    if (registrarFallos)
                    {
                        RegistroErrores.Registrar(ex, $"{etiqueta}.Resample");
                    }
                }
            }

            if (formatoMultiplex is not null)
            {
                cadena = new MultiplexMasterEstereo(cadena, formatoMultiplex);
            }

            salida = new WasapiOut(dispositivo, AudioClientShareMode.Shared, useEventSync, latenciaMs);
            salida.Init(cadena.ToWaveProvider());
            return salida;
        }
        catch (Exception ex)
        {
            ultimo = ex;
            if (registrarFallos)
            {
                RegistroErrores.Registrar(ex, etiqueta);
            }

            try
            {
                salida?.Dispose();
            }
            catch
            {
                // Ya falló: el Dispose best-effort no debe enmascarar el error real.
            }

            return null;
        }
    }

    /// <summary>Nombre amigable del dispositivo (best-effort, nunca lanza).</summary>
    /// <param name="dispositivo">Dispositivo a describir.</param>
    /// <returns>Nombre o "(desconocido)".</returns>
    private static string SafeName(MMDevice dispositivo)
    {
        try
        {
            return dispositivo.FriendlyName;
        }
        catch
        {
            return "(desconocido)";
        }
    }

    /// <summary>
    /// Multiplexa una fuente (mono o estéreo) a N canales: L→canal 0, R→canal 1,
    /// resto en silencio (los canales extra de un controlador DJ —booth, cue—
    /// no deben recibir la señal de la app). Una fuente mono se duplica a L y R.
    /// El formato de salida es el extensible pasado por el llamador (mask del mix).
    /// </summary>
    private sealed class MultiplexMasterEstereo(ISampleProvider fuente, WaveFormat formatoSalida) : ISampleProvider
    {
        /// <summary>Formato de salida: float32 al rate del mix, N canales con su mask.</summary>
        public WaveFormat WaveFormat { get; } = formatoSalida;

        /// <summary>Canales de la fuente (1 = mono, 2+ = estéreo/multicanal).</summary>
        private readonly int _canalesFuente = Math.Max(1, fuente.WaveFormat.Channels);

        private float[] _buffer = [];

        /// <summary>Lee tramas del origen y las reparte en los canales de salida (master L/R).</summary>
        /// <param name="buffer">Buffer de destino (intercalado, N canales).</param>
        /// <param name="offset">Desplazamiento inicial en el buffer.</param>
        /// <param name="count">Número de muestras a leer.</param>
        /// <returns>Muestras efectivamente escritas (múltiplo de canales).</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            var canales = WaveFormat.Channels;
            var tramas = count / canales;
            if (tramas <= 0)
            {
                return 0;
            }

            var necesarios = tramas * _canalesFuente;
            if (_buffer.Length < necesarios)
            {
                _buffer = new float[necesarios];
            }

            var leidos = fuente.Read(_buffer, 0, necesarios);
            var tramasLeidas = leidos / _canalesFuente;
            for (var t = 0; t < tramasLeidas; t++)
            {
                var src = t * _canalesFuente;
                var izq = _buffer[src];
                var der = _canalesFuente >= 2 ? _buffer[src + 1] : izq;
                var dest = offset + (t * canales);
                buffer[dest] = izq;
                buffer[dest + 1] = der;
                for (var c = 2; c < canales; c++)
                {
                    buffer[dest + c] = 0f;
                }
            }

            return tramasLeidas * canales;
        }
    }
}
