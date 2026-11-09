# Plan: Soporte de salida en controladores DJ (Pioneer SZ / DDJ-1000SRT / afines)

## Diagnóstico

**Síntoma reportado**: al configurar la salida de audio de Baby Radio hacia un controlador DJ (Pioneer SZ, DDJ-1000SRT o similar), no suena nada — ni la cola (reproductor) ni los efectos de paleta.

**Causa raíz** (auditoría de `Services/MotorAudio.cs`, `Services/MezcladorEfectos.cs`, `Support/EnumeradorDispositivosAudio.cs` + doc oficial WASAPI/NAudio):

1. **Formato multicanal del dispositivo**: los controladores DJ exponen endpoints WASAPI cuyo `MixFormat` tiene 4–8 canales (master + booth + cues) y sample rates 48/96 kHz. Nosotros `Init` con estéreo IEEE float 44.1k. En modo compartido el motor de Windows convierte sample rate (`AutoConvertPcm` va en NAudio 2.2.1), pero el mapeo de canales puede caer en salidas que no son el master físico del controlador → **silencio**; o el driver rechaza el `Initialize` con `AUDCLNT_E_UNSUPPORTED_FORMAT` (0x88890003).
2. **Errores tragados sin diagnóstico**: `MotorAudio.Abrir` (catch vacío → `return false`) y `MezcladorEfectos.Voz.Arrancar` (ídem) no registran la excepción. El resultado es "no suena nada" y el log en blanco. `SonarActual` de `PlayerViewModel` interpreta el `false` como "entrada sin audio" y salta toda la cola.
3. **Bug confirmado — cambio de dispositivo mata la reproducción (player)**: `MotorAudio.EstablecerDispositivo` (Services/MotorAudio.cs:140) llama `Cerrar()` **antes** de leer `_pathActual`/`_finEfectivoActual`; `Cerrar()` los limpia → `Abrir("", …)` lanza y el catch devuelve false → la música muere al cambiar de salida mientras suena. Además reabre con `gananciaDb = 0` (pierde la normalización). Y si estaba en **pausa** (`EnPausa` ⇒ `Reproduciendo == false`) solo actualiza `_dispositivoId`: el audio pausado sigue atado al dispositivo viejo.
4. **Bug confirmado — cambio de dispositivo con efectos activos (paletas)**: `MezcladorEfectos.EstablecerDispositivo` (Services/MezcladorEfectos.cs:323) hace `Detener(id)` (sin avisar a la UI) y re-dispara con **Guids nuevos**. `PaletaViewModel._voces` queda con los Guids viejos → `MarcarFinVoz` no libera, titileo eterno, `_mezclador._voces` fuga por fin natural sin `Liberar` → tras ~6 fugas `Disparar` rechaza (saturado) y las paletas dejan de sonar hasta reiniciar.
5. **Bug confirmado — hilo COM**: `EnumeradorDispositivosAudio.OnDispositivoCambiado` invoca `DispositivosCambiaron` desde el callback COM de Windows; `ConfiguracionViewModel.OnDispositivosCambiaron` muta `Dispositivos`/`DispositivoIndice` (bindeados) sin marshalar a UI → riesgo de crash con el diálogo Config abierto.
6. **Bug confirmado — fuga de ViewModel + reset silencioso a default**:
   - `ConfiguracionDialog` hace `new ConfiguracionViewModel()` en cada apertura (se abre desde `AccionesConsolaStrip` y `OperadorConsolaPanel`) y el VM se suscribe a `DispositivosCambiaron` en el constructor **sin desuscribirse nunca** → cada apertura deja un VM zombi que sigue aplicando cambios de índice a los motores y al JSON.
   - `OnDispositivosCambiaron` reemplaza la lista `Dispositivos` mientras el ComboBox está vivo; WinUI puede empujar `SelectedIndex = -1` por el TwoWay → `OnDispositivoIndiceChanged(-1)` → `EstablecerDispositivo("")` (vuelve al default) + `Guardar()` (persiste vacío). El texto del diálogo promete "si un dispositivo desaparece, se vuelve al predeterminado", pero ese fallback no está implementado: si el elegido desaparece, el índice viejo apunta a un dispositivo distinto.

Build verificado: `dotnet build -c Debug` → 0 warnings, 0 errores. `beep.wav` presente en `Assets/Sounds/` y en bin.

## Cambios propuestos

### 1. Nuevo `Support/AdaptadorSalidaAudio.cs` (un tipo, helper puro de composición)

`internal static class AdaptadorSalidaAudio` con un único punto de entrada:

```csharp
internal static WasapiOut CrearInicializada(
    MMDevice dispositivo, ISampleProvider fuente, int latenciaMs, out ISampleProvider cadena)
```

Estrategia (cada fallo se loguea con `RegistroErrores.Registrar` incluyendo nombre del dispositivo, mix format y HRESULT):

1. **Intento A** — si `MixFormat.Channels > 2`: encadenar `MultiplexingSampleProvider` (inputs 0,1 → outputs 0,1 = master L/R; resto de canales en silencio). `WasapiOut(Shared, eventSync: true, latencia)` + `Init`. (Con AutoConvertPcm el motor remuestrea solo.)
2. **Intento B** — si A falla: reintentar con `useEventSync: false` (polling). Algunos drivers USB de DJ fallan con `SetEventHandle`.
3. **Intento C** — si B falla: resamplear con `WdlResamplingSampleProvider` al `SampleRate` del mix + multiplex de canales; `Init` con el formato resultante.
4. Si todo falla: lanzar excepción con mensaje claro (nombre del dispositivo + `mixFormat`); el llamador decide fallback.

`MultiplexingSampleProvider` y `WdlResamplingSampleProvider` confirmados en NAudio.Core 2.2.1.

### 2. `Services/MotorAudio.cs`

- `Abrir`/`CrearSalida` usan el adaptador (misma cadena de etapas: lector → ganancia → maestro → medidor).
- El `catch` de `Abrir` registra la excepción (`RegistroErrores.Registrar(ex, "MotorAudio.Abrir")`) en vez de tragar; añadir Traza con dispositivo y modo.
- **Fix `EstablecerDispositivo`** (el bug del cambio de fuente):
  1. Capturar **antes** de `Cerrar()`: `_pathActual`, `_finEfectivoActual`, `Posicion`, `Modo`, `EnPausa` y un nuevo campo `_gananciaDbActual` (que `Abrir` guardará al abrir).
  2. Si había audio abierto (sonando **o en pausa**): `Cerrar()` → setear `_dispositivoId` → `Abrir(path, posicion, fin, _gananciaDbActual, modo)` → si estaba en pausa, `Pausar()` tras reabrir.
  3. **Fallback**: si el `Abrir` con el dispositivo elegido devuelve false y no era ya el predeterminado, reabrir una vez con el predeterminado y registrar aviso con nombre del dispositivo fallido.
- `ReproducirTramo` (preview editor) hereda el fix por `Abrir` (usa `_dispositivoId`).

### 3. `Services/MezcladorEfectos.cs`

- `Voz.Arrancar` usa el mismo adaptador (con `LatenciaVozMs = 30`); el `catch` registra la excepción.
- **Fix `EstablecerDispositivo`** (el bug de las paletas al cambiar de fuente): en lugar de `Detener(id)` + `Disparar(...)` (Guid nuevo), añadir método interno `Voz.ReabrirEnDispositivo(dispositivoId)` que **reutiliza el Guid existente**: captura path/posición/cue/ganancia antes de `Silenciar()`/`Soltar()` (sin quitar el Guid del diccionario), reabre la salida con el nuevo dispositivo y, si falla, se libera el Guid **avisando** vía `alTerminar(id)` para que `PaletaViewModel.MarcarFinVoz` limpie titileo y slot. Así el tracking de `_voces` en `PaletaViewModel` sigue siendo válido, no hay fuga de voces ni saturación del mezclador (máx. 6).
- `Detener(id)` durante el cambio no debe quedar el slot con `EstaSonando=true`: con reutilizar Guids y no notificar fin, el titileo continúa correctamente hasta que la voz termine naturalmente y llame `MarcarFinVoz`.

### 4. `ViewModels/Console/ConfiguracionViewModel.cs` + `Controls/Console/ConfiguracionDialog.xaml.cs`

- `OnDispositivosCambiaron` marshala a UI antes de mutar (el evento llega en hilo COM):
  - **Opción elegida**: el diálogo inyecta su `DispatcherQueue` al VM (nuevo parámetro o propiedad seteable en el constructor del diálogo tras `InitializeComponent`); el VM hace `TryEnqueue` para refrescar la lista.
  - Refrescar la lista con un flag `_refrescandoLista` (igual que `_cargando`) para que el empujón TwoWay `-1` del ComboBox no dispare `OnDispositivoIndiceChanged` ni reescriba el JSON; después de rebind, re-aplicar el índice guardado explícitamente.
- **Fallback a default**: si el dispositivo seleccionado desaparece de la lista (`FindIndex < 0`), poner índice -1 y aplicar motores + `Guardar()` (cumple el texto "se vuelve al predeterminado").
- **Fix fuga**: `IDisposable` en el VM (desuscribe `DispositivosCambiaron`); el diálogo se desuscribe en `Closed` (o el VM expone `Dispose` y el code-behind lo llama).
- `OnDispositivoIndiceChanged`: además de aplicar a ambos motores y guardar, pasar el nombre del dispositivo al log de errores si el cambio falla.
- Sin cambio de XAML (mismo `x:Bind`).

### 5. `AGENTS.md`

- Añadir `Support/AdaptadorSalidaAudio.cs` al mapa de §1 y al inventario de §7 (una línea).

## Fuera de cambio (explícito)

- No se migra a NAudio 3 `WasapiPlayer` (versión fija 2.2.1 por AGENTS).
- No se toca el modo exclusive (bloquearía al DJ software).
- No se cambia `SonidoService` (beep OK).
- Sin commit/push/release sin orden del owner.

## Verificación

1. `dotnet build -c Debug` → 0 warnings / 0 errores.
2. Con el controlador conectado (owner):
   - Elegir el controlador en Config **con música sonando** → la música continúa por el controlador (no muere, conserva ganancia de normalización).
   - Elegir el controlador **con la música en pausa** → al reanudar, suena por el controlador (no por el viejo).
   - Cola y efectos suenan por el controlador; VU se mueve.
   - Cambiar a default y volver → sigue sonando en cada salto.
3. Regression: default Realtek/auriculares igual que antes (intentos A–C no alteran el caso normal estéreo).
4. Efectos con el controlador: disparar carts, Stop, cambio de dispositivo con carts activos → titileo se apaga al terminar, sin slots fantasma; repetir cambio 6+ veces sin que las paletas dejen de responder (sin saturación del mezclador).
5. Desconectar el dispositivo seleccionado con Config abierto → vuelve al predeterminado automáticamente (y se persiste).
6. Abrir y cerrar Config varias veces mientras se conecta/desconecta el controlador → sin crash (hilo COM), sin reset silencioso a default, sin VMs zombi aplicando cambios.
7. Si el controlador rechaza el formato: log en `baby-radio-error.log` con nombre del dispositivo, mix format y HRESULT (no silencio ciego).
