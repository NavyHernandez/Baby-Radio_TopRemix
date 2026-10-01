# Documentación: Sistema de Mapeo de Teclas Globales (Hotkeys)

## Resumen
Sistema completo de mapeo de teclas globales para slots de paleta en Baby Radio. Permite asignar combinaciones de teclas (con modificadores Ctrl/Alt/Shift/Win) a slots de efectos, con 3 modos de interacción y persistencia en JSON portable.

---

## Archivos Creados

| Archivo | Descripción |
|---------|-------------|
| `Models/ModoMapeo.cs` | Enum: `Toggle`, `Retrigger`, `Momentaneo` |
| `Services/KeyMappingService.cs` | Singleton: `RegisterHotKey` + hook `WH_KEYBOARD_LL` para KeyUp global |
| `Services/WindowMessageHook.cs` | Hook `SetWindowSubclass` para recibir `WM_HOTKEY` en WinUI 3 |
| `Services/SonidoService.cs` | Reproductor .wav embebido (`ms-appx:///Assets/Sounds/beep.wav`) |
| `Controls/Console/PaletaMapeoDialog.xaml` | Diálogo UI: captura tecla, modificadores, modo, botón Limpiar |
| `Controls/Console/PaletaMapeoDialog.xaml.cs` | Lógica: titileo 10s, validación conflictos, desregistro hotkeys |

---

## Archivos Modificados

| Archivo | Cambios Clave |
|---------|--------------|
| `Models/PaletteItem.cs` | + `MapeoTecla`, `MapeoModifiers`, `MapeoModo`, `TieneMapeo` (ObservableProperty) |
| `Models/SlotEfectoGuardado.cs` | + campos mapeo para persistencia JSON |
| `Support/ConsolaStore.cs` | `SlotEsPersistible` incluye `|| item.TieneMapeo` |
| `ViewModels/Console/PaletaViewModel.cs` | + `RegistrarHotkeysCategoria()`, `OnHotkeyPressed`, `OnHotkeyReleased`, `DispararRetrigger()` |
| `Controls/Console/PaletaPanel.xaml` | + MenuFlyoutItem "Mapeo…" en menú contextual |
| `Controls/Console/PaletaPanel.xaml.cs` | Handler `OnMapeoClick` → abre diálogo, persiste, registra hotkey |
| `MainWindow.xaml.cs` | Inicializa `KeyMappingService` con `hWnd` real + `SonidoService` |
| `App.xaml.cs` | Dispose `KeyMappingService` al cerrar |

---

## Funcionalidades

### 1. Modos de Interacción

| Modo | Comportamiento |
|------|----------------|
| **Toggle** | 1ª pulsación = Play, 2ª = Stop (alterna) |
| **Retrigger** | Cada pulsación = nueva voz superpuesta (layering, máx 6 global) |
| **Momentáneo** | Push-to-talk idempotente: suena mientras se mantiene la tecla, para al soltar (hook `WH_KEYBOARD_LL`). El auto-repeat del teclado se suprime (servicio + ViewModel); si termina solo con la tecla aún pulsada, queda parado hasta soltar + re-pulsar |

### 2. Captura de Tecla (Diálogo)
- Click en recuadro → **Titileo cian 10s** + **sonido .wav**
- Usuario presiona tecla → Se detiene titileo, muestra combinación (ej. `Ctrl + F5`)
- Checkboxes modificadores: Ctrl, Alt, Shift, Win
- ComboBox modo: Toggle / Retrigger / Momentáneo
- Botón "Limpiar" compacto (solo visible si hay mapeo)
- Escape durante captura = limpiar
- Validación conflictos en tiempo real (RegisterHotKey test)

### 3. Persistencia
- Guarda en `baby-radio-consola.json` via `SlotEfectoGuardado`
- Al arrancar app / cambiar categoría: `RegistrarHotkeysCategoria()` restaura hotkeys
- Al limpiar slot / mapeo: `Desregistrar()` libera hotkey del sistema

### 4. Feedback Visual
- **Titileo** (opacidad 35% ↔ 100% cada 350ms) mientras slot suena
- **Flash 300ms** (`BebeButtonHelper.SetIsActive`) al pulsar slot vacío
- **Diálogo aviso** si slot sin audio al pulsar hotkey o abrir mapeo

---

## Flujo Técnico

```
MainWindow constructor:
  1. hWnd = WindowNative.GetWindowHandle(this)
  2. KeyMappingService.Instancia.Inicializar(hWnd)
  3. SonidoService.Instancia.Inicializar("ms-appx:///Assets/Sounds/beep.wav")
  4. _messageHook = new WindowMessageHook(this)
  5. _messageHook.HotkeyReceived += id => KeyMappingService.ProcesarHotkey(id)

KeyMappingService.Registrar():
  RegisterHotKey(_hWnd, id, mod, vk)  → WM_HOTKEY al WndProc de la ventana
  → WindowMessageHook (SetWindowSubclass) LO VE
  → KeyMappingService.ProcesarHotkey(id)
  → OnHotkeyPressed(propietariaId, slotIndex, modo)
  → PaletaViewModel.DispararPorMapeo()

Momentáneo (KeyUp):
  Hook WH_KEYBOARD_LL intercepta WM_KEYUP/WM_SYSKEYUP global
  → Filtra slots con modo Momentaneo + misma tecla
  → OnHotkeyReleased(propietariaId, slotIndex)
  → PaletaViewModel.DetenerVozDe()
```

---

## Assets

| Archivo | Especificación |
|---------|----------------|
| `Assets/Sounds/beep.wav` | 8864 bytes, 100ms, 800Hz, mono 16-bit 44.1kHz, fade in/out 5ms |

---

## Logs de Diagnóstico (`%APPDATA%\BabyRadio\baby-radio-error.log`)

| Trace | Significado |
|-------|-------------|
| `KeyMapping.Inicializar: hWnd=0x...` | Handle ventana recibido OK |
| `KeyMapping.Registrar: OK: id=..., vk=77 (0x4D)...` | Hotkey registrado (M = 0x4D) |
| `KeyMapping.Registrar: FAIL: error=1409` | Tecla ya en uso (HOTKEY_ALREADY_REGISTERED) |
| `KeyMapping.ProcesarHotkey: RECEIVED: id=..., slot=...` | WM_HOTKEY recibido por hook |
| `Paleta.OnHotkeyPressed: RECEIVED: ... DISPATCH: item=...` | ViewModel procesa y dispara |
| `Paleta.OnHotkeyReleased: ...` | KeyUp para Momentáneo |

---

## Problemas Conocidos / Limitaciones

1. **Modificadores en KeyUp (Momentáneo)**: resuelto — el `KeyUp` libera por tecla principal sin exigir estado de modificadores (el orden al soltar puede variar).
2. **Límite 6 voces globales**: `MezcladorEfectos.MaxVoces = 6`. Retrigger satura → retorna false.
3. **App no instalada (debug)**: `RegisterHotKey` con `hWnd` de ventana debug funciona, pero Velopack update check falla (normal).
4. **Beep .wav**: Requiere `Assets/Sounds/beep.wav` en proyecto (Build Action: Content).

---

## Pruebas Recomendadas

1. **Toggle**: Mapear `F5` → pulsar `F5` = play, volver a pulsar = stop
2. **Retrigger**: Mapear `F6` → pulsar 3 veces = 3 capas sonando, Stop = para todas
3. **Momentáneo**: Mapear `F7` (modo Momentáneo) → mantener `F7` = suena, soltar = para
4. **Persistencia**: Cerrar app, reabrir → hotkeys activos
4. **Conflicto**: Intentar mapear `F5` (usado por navegador) → debe avisar conflicto
5. **Limpiar**: Botón "Limpiar" / Escape / Guardar vacío → tecla ya no dispara
6. **Cambio categoría**: Hotkeys categoría anterior se limpian, nuevos se registran