# AGENTS.md — Baby Radio (Radio Automation, WinUI 3)

> Fuente de verdad para agentes de código y contribuidores.
> Stack fijo: .NET 8 (`net8.0-windows10.0.19041.0`) · WinUI 3 · Windows App SDK 2.2.0 ·
> NAudio 2.2.1 · TagLibSharp 2.3.0 · FluentIcons.WinUI **2.2.339.1**
> (transitivo: FluentIcons.Common 2.1.339.1) · Win2D 1.4.0 ·
> CommunityToolkit.Mvvm 8.4.2 · Velopack 1.2.*. No cambiar versiones sin aprobar.
> Instalador: unpackaged permanente (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`),
> `publish.ps1` → Velopack → GitHub Releases `NavyHernandez/Baby-Radio_TopRemix` (v0.1.0).
> Sin push/commit/upload sin orden expresa del owner.
> `LangVersion latest` (partial properties del Toolkit; SDK 10 compilando `net8.0`).
>
> ⚠️ No usar FluentIcons.WinUI 2.2.339: su `FontManager`/`IconSizeValues` estático
> lanza `NullReferenceException` al arrancar sobre WinAppSDK 2.2 y la app muere con
> `XamlParseException: Cannot create instance of type 'SymbolIcon'`. El patch .1 lo corrige.

## 1. Mapa del proyecto (Fase 1)

```text
BebeRadio/ (app visible: Baby Radio; namespace raíz BebeRadio sin cambios)
├── AGENTS.md                 ← este archivo (leer antes de codificar)
├── App.xaml(.cs)              ← fusiona Themes/*, backdrop Mica
├── MainWindow.xaml(.cs)       ← Title fijo "Baby Radio" + fullscreen operador + RootFrame → Views/Phase1ShowcaseView
├── Themes/
│   ├── ColorPalette.xaml      ← tokens de color (Paleta A-Studio Console)
│   └── ButtonStyles.xaml      ← BebeButtonBase físico 3D + variantes
├── Models/Track.cs            ← DTO mock (sin persistencia en Fase 1)
├── Models/TrackCategory.cs      ← 10 categorías estilo Jazler
├── Models/CategorySwatch.cs     ← ficha (label, token, icono, ejemplo mock)
├── Models/CategoryPalette.cs    ← registro central (parrilla + futura librería)
├── Models/PaletteItem.cs        ← item de paleta + Vaciar (pool estable)
├── Models/QueueEntry.cs         ← entrada de cola (badge EN VIVO, FilePath real)
├── ViewModels/PlayerViewModel.cs ← cola real + Repeat + VU + ganancia normalizada cacheada
├── ViewModels/PlayerViewModel.Cue.cs ← parcial: cue/preview sin sonar + SolicitarReproduccion
├── ViewModels/ShowcaseViewModel.cs ← LEGADO (ver Console/ConsolaViewModel)
├── ViewModels/SelectableCategory.cs ← wrapper IsSelected + TieneAudio + comando Select
├── ViewModels/Console/
│   ├── ListaReproduccionViewModel.cs ← cola + EN VIVO + AddFiles con cue + ReproduccionPedida + análisis en background
│   ├── PaletaViewModel.cs             ← pool estable 40 + TamanoPagina (20/30) + mezcla + voces + Sonando
│   ├── CategoriasRailViewModel.cs     ← categorías + 7 fijas + conteos + riel vacío→null
│   ├── AccionesConsolaViewModel.cs    ← toggles + ganchos (Stop→efectos, ▲▼→páginas)
│   ├── OpcionCategoria.cs             ← opción de categoría por banco (operador)
│   ├── SelectorCategoriaViewModel.cs  ← selección independiente por banco (operador)
│   └── ConsolaViewModel.cs            ← orquestador + bancos operador (PaletaA/B, SelectorA/B)
├── Support/
│   ├── AudioFileInspector.cs    ← metadatos de arrastrados (TagLibSharp)
│   ├── AnalizadorLoudness.cs    ← LUFS BS.1770 (48k, filtros-K, gating)
│   ├── LoudnessCache.cs         ← caché baby-radio-loudness.json + ganancia ±12dB
│   ├── ServicioAnalisisAudio.cs ← worker único BelowNormal + Channel dedup
│   ├── ValidadorRecursos.cs     ← valida tokens de paleta al arrancar
│   ├── MockQueueBuilder.cs      ← cola inicial de 12 entradas
│   └── RegistroErrores.cs       ← log baby-radio-error.log (blindaje diálogos)
├── Controls/
│   ├── BebeButtonHelper.cs    ← attached props (CategoryColor, IsCircular) + sombra GPU + EnsureShadow
│   ├── UniformGridPanel.cs    ← grilla uniforme 5 col + filas 1fr con alto finito
│   ├── CategoryBrushConverter.cs ← clave de token → brush (para {Binding})
│   ├── BoolToOpacityConverter.cs ← bool → opacidad (selección, EN VIVO)
│   ├── VuMeterControl.xaml(.cs) ← Win2D estéreo, inercia + peak-hold
│   ├── PlayerBar.xaml(.cs)    ← transporte×6 + display 3×3 (reloj C1, título A2, artista B2) + VU
│   ├── ColorALetraConverter.cs ← color → tinta adaptativa (claro/oscuro)
│   ├── SlotAEstiloConverter.cs  ← TieneAudio → estilo macizo/claro
│   └── Console/
│       ├── ConsolaSombraHelper.cs       ← flash 300ms + sombras + FindDescendants
│       ├── ListaReproduccionPanel.xaml(.cs) ← izq: cola + drag&drop (inyecta su VM)
│       ├── PaletaPanel.xaml(.cs)            ← centro: header opcional + carts + guía + titileo
│       ├── CategoriasRailPanel.xaml(.cs)    ← der: 7 esqueleto/color 3D + IniciarCreacionAsync (inyecta su VM)
│       └── AccionesConsolaStrip.xaml(.cs)   ← abajo: 7 aluminio + Mix titila (TitileoArmado)
│       ├── TitileoArmado.cs                 ← titileo de armado reutilizable
│       ├── BancoOperadorPanel.xaml(.cs)     ← banco 5×N + selector + páginas propias
│       └── OperadorConsolaPanel.xaml(.cs)   ← modo operador: 2 bancos + tira + salir + Esc
└── Views/Phase1ShowcaseView.xaml(.cs) ← shell dual normal/operador (perezoso) vía ConsolaViewModel
```

Fases futuras (NO crear aún): `Views/{Playlist,Library,Carts,ClockScheduler,Settings}View`,
`Services/{AudioEngine,Library,Scheduler,VoiceTracking,Report}Service`, rotación, spots, voice tracking.

## 2. Paleta A — Studio Console (única autorizada en Fase 1)

| Token | Hex | Uso |
|---|---|---|
| `BackgroundBase` | `#121316` | Fondo ventana |
| `BackgroundPanel` | `#1A1C1F` | Fondo panel contenedor |
| `BackgroundLista` | `#1B1F22` | Fondo exacto del logo (zona de canciones, fusión con marca de agua) |
| `BackgroundCard` | `#23262B` | Tarjetas / superficie botón |
| `BackgroundCardPressed` | `#1D2025` | Superficie botón presionado |
| `BorderSubtle` | `#2E3238` | Bordes 1px |
| `HighlightTop` | `#FFFFFF` 8% | Highlight superior botón físico |
| `BottomEdge` | `#000000` 25% | Filo oscuro inferior del relieve |
| `TransportRimTop` | `#3A4049` | Aro del pad (luz) |
| `TransportRimBottom` | `#17191D` | Aro del pad (sombra) |
| `TransportDishCenter` | `#1B1E23` | Plato cóncavo (centro hundido) |
| `TransportDishEdge` | `#2B3038` | Plato cóncavo (borde) |
| `AluminumTop` | `#F2F4F6` | Botón claro (luz) |
| `AluminumMid` | `#D5DAE0` | Botón claro (base) |
| `AluminumBottom` | `#B9BFC7` | Botón claro (sombra) |
| `LightInk` | `#1A1C1F` | Tinta sobre botón claro |
| `LightDishCenter` | `#FBFCFD` | Plato aluminio (luz) |
| `LightDishEdge` | `#C2C7CD` | Plato aluminio (borde) |
| `LightRim` | `#71767E` | Aro del botón claro |
| `HoverOverlay` | `#FFFFFF` | Capa hover (opacidad animada 6–8%) |
| `PressedOverlay` | `#000000` 35% | Capa pressed (inner-shadow) |
| `CategoryMusic` | `#4CC2FF` | Música |
| `CategoryJingle` | `#C084FC` | Jingles |
| `CategoryCommercial` | `#FBBF24` | Comerciales |
| `CategoryId` | `#34D399` | IDs |
| `CategorySweeper` | `#F472B6` | Sweepers |
| `CategoryBed` | `#818CF8` | Beds |
| `CategoryNews` | `#FB923C` | Noticias |
| `CategoryPromo` | `#A3E635` | Promos |
| `CategoryProgram` | `#2DD4BF` | Programas |
| `CategoryFiller` | `#94A3B8` | Relleno |
| `StateOnAir` | `#FF453A` | On-air / reproduciendo |
| `DigitalAmber` | `#FFB000` | Display LED (título en player) |
| `VuTrack` | `#101214` | Fondo empotrado (VU, display) |
| `TextPrimary` | `#F2F3F5` | Texto principal |
| `TextSecondary` | `#A1A7B3` | Texto secundario |
| `TextDisabled` | `#5A606B` | Deshabilitado |

Reglas: categorías solo como acento (franja/dot), nunca fondo completo.
Al agregar una categoría: enum en `TrackCategory.cs` + token en paleta +
ficha en `CategoryPalette.All` (el showcase la muestra solo).
Tiempos/duración/BPM siempre en `Cascadia Mono`; resto `Segoe UI Variable`.
Excepción: duraciones de la Lista de Reproducción en `Segoe UI Variable`
semibold tabular 16 (números alineados, integrados a la UI).

## 3. Convenciones de código obligatorias

### 3.1 Modularidad y documentación (exigido por el owner)

- Un tipo por archivo. Archivos >250 líneas → dividir en `*.Logic.cs` parcial o extraer helper.
- Toda función pública/protegida lleva doc XML `/// <summary>` en español o inglés consistente
  (elegir uno por archivo), con `<param>`, `<returns>` y `<remarks>` si hay precondiciones.
- Funciones puras donde sea posible (p. ej. cálculo de inercia del VU, formato `mm:ss`).
  Sin I/O en ViewModels salvo `DispatcherTimer` mock (Fase 1) y futuro `IAudioEngine`.
- Nombres: `PascalCase` tipos/métodos, `camelCase` locales, `_camelCase` campos privados,
  `UPPER` solo constantes. Sin abreviaturas crípticas (`btn`, `tmp`, `mgr` prohibidos).
- MVVM estricto: la vista no contiene lógica de negocio; el code-behind solo enruta
  eventos a comandos o a helpers de composición (sombras). ViewModels con
  `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- Rendimiento: Win2D dibuja a ~30fps con `CanvasAnimatedControl` o `CanvasControl` +
  `GameLoop`-style timer; reutilizar `CanvasBitmap`/`brushes`, cero alocaciones en
  `Draw`. `DropShadow` se crea una vez en `Loaded`, nunca por frame.
- Accesibilidad: todo `Button` lleva `AutomationProperties.Name` en español.

### 3.2 XAML

- `x:Class` + `using:` namespaces explícitos. Estilos con `x:Key`, nunca implícitos globales
  que rompan páginas futuras. Colores siempre vía `{StaticResource Token}` — hex hardcodeado prohibido.
- `VisualStateManager` con transiciones 80–120ms (`Duration="0:0:0.1"`).
- Iconografía: solo `FluentIcons.WinUI` (`SymbolIcon` con `FluentSymbol`), mismo peso.
  Prohibido Segoe Fluent Icons del sistema en transporte/carts.

### 3.3 Botones físico 3D estilo consola (especificación cerrada)

- Capas (atrás→adelante): glow de actividad XAML + sombra GPU
  `Compositor.CreateDropShadow()` (blur 10, offset Y 4, negro 30 %) creada en
  `BebeButtonHelper.AttachShadow()` y recortada a la geometría del borde.
- Cuerpo: `Border` con gradiente cenital (claro arriba, base, oscuro abajo;
  transporte usa `TransportSurfaceGradient` de más contraste) + filo de luz
  superior 1px (`HighlightTop`) + filo oscuro inferior (`BottomEdge`).
- Contenido desplazado -1px en reposo (refuerza elevación).
- `PointerOver`: overlay blanco 3 % / 100ms + blur de sombra a 12.
- `Pressed`: sombra a offset Y 1 + blur 5 + 12 %, overlay de luz invertida
  (oscuro arriba) visible / 80ms, filo superior oculto, contenido a +2px.
  Sin easing lento: feedback instantáneo.
- `Disabled`: opacidad 0.4 sin sombra.
- `Active` (grupo `ActivityStates`, custom): glow de color persistente
  (borde 55 % + wash 14 % del `CategoryColor`); se activa con la attached
  property `BebeButtonHelper.IsActive`. En Fase 1 solo pulso de 300 ms al
  disparar paleta; en Fase 2 lo gobernará el motor de audio.
- Variantes: `BebeTransportButtonStyle` (pad DJ circular con **elipses**:
  aro `TransportRimGradient` + plato cóncavo radial `TransportDishBrush` +
  specular; escala a cualquier tamaño, iconos 20/22px, 52px (play 60)),
  `BebeCartButtonStyle` (CornerRadius 4, cuerpo teñido 18 % con `CategoryColor`
  vía attached property, sin franja).
- Consola inferior: fila de 7 botones aluminio (`BebeLightButtonStyle`, plato
  radial `LightDishBrush` + aro `LightRim`, tinta `LightInk`, iconos en color
  por función, toggles Operador/Mix con glow ámbar persistente).

### 3.4 VU meter Win2D (estilo consola real)

- `VuMeterControl` (200×32): `CanvasAnimatedControl` a 60 fps con vsync;
  barras segmentadas LED **verde→amarillo→rojo** (consola real), etiquetas L/R y escala dB (−40/−20/−12/−6/0).
- Escala dB con piso −60 (`VuDynamics.LinearToDbNormalized`); balística pro:
  ataque instantáneo, release suave, todo con delta real (framerate-free).
- Pico retenido 800ms + caída 0.6/s. Fuente en Fase 1: niveles CRUDOS a 20 Hz
  (mock seno + ruido); el suavizado vive SOLO en el control (sin doble inercia).
- En Fase 2 lo alimenta `NAudio` sin cambiar la firma `SetLevels(l, r)`.

## 4. Flujo de trabajo por fases

- Solo Fase 1: paleta + player mock + botones + showcase. No tocar BD/scheduler/playlist/rotación.
- Validar cada fase con `dotnet build` antes de avanzar.
- Commits en español, formato `fase1: descripción corta`.

## 5. Comandos

```powershell
dotnet restore
dotnet build -c Debug
dotnet run   # requiere identidad MSIX (VS o winapp CLI); si falla, ejecutar desde VS con F5
```

## 6. Definition of Done — Fase 1

- [ ] Compila sin warnings como errores en `net8.0-windows10.0.19041.0`.
- [ ] `Phase1ShowcaseView` muestra consola: cola izq (12 + EN VIVO, carga por
      arrastre de audios reales), `PlayerBar` + paleta desplegada de 20
      (clic solo dispara glow) y lateral der de categorías sincronizada.
- [ ] Play mueve timer monoespaciado + VU con inercia; Pressed desplaza 1–2px.
- [ ] Cero hex fuera de `ColorPalette.xaml`; cero lógica de audio real.

## 7. Documentación del código (exigido por el owner)

- **C#**: todo tipo/miembro público o protegido lleva doc XML `/// <summary>`
  (+`<param>`/`<returns>`, `<remarks>` si hay precondiciones). Un tipo por archivo.
- **XAML**: cada archivo lleva comentario de cabecera (qué es + reglas) y
  comentarios de sección (zonas, capas del template, grupos de estados).
- **Este archivo** es el índice vivo: el mapa de §1 describe cada archivo en
  una línea (qué contiene). Al crear un archivo, agregarlo a §1 + documentarlo.

### Inventario rápido (qué hace cada pieza)

| Archivo | Hace |
|---|---|
| `App.xaml(.cs)` | Recursos globales + apertura de `MainWindow` |
| `MainWindow.xaml(.cs)` | Title fijo + fullscreen operador + `RootFrame` → showcase |
| `Themes/ColorPalette.xaml` | Todos los colores/tokens + brush `AluminumTop` (único hex) |
| `Themes/ButtonStyles.xaml` | Templates 3D: transporte×6, carts, aluminio, categoría maciza, cart claro |
| `Controls/BebeButtonHelper.cs` | Attached props + sombra GPU + `IsActive` + `EnsureShadow` |
| `Controls/UniformGridPanel.cs` | Grilla uniforme (5 paleta / 7 acciones / 4 operador) + filas 1fr |
| `Controls/CategoryBrushConverter.cs` | Clave de token → brush |
| `Controls/BoolToOpacityConverter.cs` | Bool → opacidad (selección, EN VIVO) |
| `Controls/BoolAVisibilidadConverter.cs` | Bool → visibilidad (+Invertir: fantasmas) |
| `Controls/VuMeterControl.xaml(.cs)` | VU consola 60 fps, dB, picos + reposo (pausa canvas) |
| `Controls/PlayerBar.xaml(.cs)` | Transporte×6 + display 3×3 + reloj + VU combinado |
| `Controls/ColorALetraConverter.cs` | Color → tinta adaptativa (+EsColorClaro) |
| `Controls/SlotAEstiloConverter.cs` | TieneAudio → estilo macizo/claro |
| `Models/Track.cs` + `TrackCategory.cs` | DTO + 10 categorías |
| `Models/CategorySwatch.cs` + `CategoryPalette.cs` | Ficha + registro central |
| `Models/PaletteItem.cs` | Item de paleta (ColorFantasma, TintaCart, sin subtítulo) + `Vaciar` |
| `Models/QueueEntry.cs` | Entrada de lista (EN VIVO, `FilePath`) |
| `ViewModels/PlayerViewModel.cs` | Cola real + Repeat + VU combinado + pausa + ganancia normalizada |
| `ViewModels/PlayerViewModel.Cue.cs` | Parcial: cue/preview sin sonar + SolicitarReproduccion |
| `ViewModels/ShowcaseViewModel.cs` | LEGADO (ver Console/ConsolaViewModel) |
| `ViewModels/SelectableCategory.cs` | Wrapper IsSelected + TieneAudio + Icono enum + ColorFantasma |
| `ViewModels/Console/ListaReproduccionViewModel.cs` | Cola + EN VIVO + AddFiles con cue + ReproduccionPedida + análisis en background |
| `ViewModels/Console/PaletaViewModel.cs` | 40 slots + páginas (20/30 por `TamanoPagina`) + mezcla + voces + Sonando |
| `ViewModels/Console/CategoriasRailViewModel.cs` | Categorías + 7 fijas + conteos + vacío |
| `ViewModels/Console/AccionesConsolaViewModel.cs` | Toggles + ganchos paleta |
| `ViewModels/Console/ConsolaViewModel.cs` | Orquestador + bancos operador (PaletaA/B, SelectorA/B) + salir |
| `ViewModels/Console/OpcionCategoria.cs` | Opción de categoría por banco (operador) |
| `ViewModels/Console/SelectorCategoriaViewModel.cs` | Selección independiente por banco (operador) |
| `Support/TimeFormatter.cs` | `m:ss` puro |
| `Support/VuDynamics.cs` | Escala dB + balística + picos (puro) |
| `Support/MockLevelsProvider.cs` | Niveles mock (seno + ruido) |
| `Services/MezcladorEfectos.cs` | Singleton 6 voces (Mix), niveles 20 Hz, latencia 80 ms |
| `Services/MotorAudio.cs` | Salida única cola/tramo + ganancia normalización + anti-tardíos |
| `Support/MockQueueBuilder.cs` | Lista inicial de 12 |
| `Support/RegistroErrores.cs` | Log + trazas (blindaje diálogos y motor) |
| `Support/AudioFileInspector.cs` | Metadatos de arrastrados (TagLibSharp) |
| `Support/AnalizadorLoudness.cs` | LUFS BS.1770 (48k, filtros-K, gating) |
| `Support/LoudnessCache.cs` | Caché baby-radio-loudness.json + ganancia ±12dB |
| `Support/ServicioAnalisisAudio.cs` | Worker único BelowNormal + Channel dedup |
| `Support/ConsolaStore.cs` | JSON portable + caché en memoria |
| `Support/TemaConsola.cs` | Tema único oscuro + alias + `Cambio` |
| `Support/ValidadorRecursos.cs` | Valida tokens de paleta al arrancar |
| `Controls/Console/ConsolaSombraHelper.cs` | Flash 300ms + sombras + FindDescendants |
| `Controls/Console/ListaReproduccionPanel.xaml(.cs)` | Izq: cola + drag&drop (inyecta su VM) |
| `Controls/Console/PaletaPanel.xaml(.cs)` | Centro: header opcional + carts + guía + titileo + medición layout |
| `Controls/Console/OndaEfectoControl.xaml(.cs)` | Onda blindada + cues + pre-escucha (no pausa cola) |
| `Controls/Console/CategoriasRailPanel.xaml(.cs)` | Der: 7 esqueleto/color 3D + crear (inyecta su VM) |
| `Controls/Console/AccionesConsolaStrip.xaml(.cs)` | Abajo: 7 aluminio 112×103, iconos 26 relieve + Mix titila |
| `Controls/Console/TitileoArmado.cs` | Titileo de armado reutilizable |
| `Controls/Console/BancoOperadorPanel.xaml(.cs)` | Banco 5×N + selector + páginas propias |
| `Controls/Console/OperadorConsolaPanel.xaml(.cs)` | Modo operador: 2 bancos + tira + salir + Esc |
| `Views/Phase1ShowcaseView.xaml(.cs)` | Shell dual normal/operador (perezoso) + sombras |

## 8. Progreso por features (obligatorio)

- Una feature a la vez (`progress/feature_list.json`: `pending` → `in_progress` → `done`).
- Cerrar feature = `dotnet build` limpio + `status: done` + apéndice en `progress/history.md` (append-only, no editar entradas viejas).
- Lecturas >350 líneas van por `bulk_read` (plugin shunt en `.opencode/`); requiere `HF_API_KEY` del usuario y reiniciar opencode tras cambios de config.
