# Bitácora histórica (append-only)

Cada vez que se cierra una feature, su resumen se añade aquí. No edites entradas anteriores. Solo añades al final.

---

## 2026-09-15 — Sesión 1: Consola Baby Radio funcional con audio real

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Shell modular**: `Phase1ShowcaseView` adelgazada; `ConsolaViewModel` orquesta Lista, Paleta, Categorías, Acciones y Reproductor (`Controls/Console`, `ViewModels/Console`).
2. **App visible Baby Radio** (namespace `BebeRadio` intacto).
3. **Colores vivos**: 7 tokens modernos + franja superior Jazler en carts + tinte 30%.
4. **Lista de reproducción**: arranca vacía; drop de audios/carpetas, carga manual, reorden, inserción en posición, menús, Limpiar todo, dots aleatorios, recorte de silencios (-45 dB), solo tipos reproducibles.
5. **Motor NAudio real**: `Services/MotorAudio` único (cola/tramo, 150ms), `MedidorPicos` 25 Hz, VU real, Stop-al-fin con parpadeo, VU a cero al parar, ataque VU 90.
6. **Paleta y editor**: 20 carts, Editar/Limpiar/drop, onda Win2D con cues + pre-escucha, color libre, ganancia/fundidos, JSON portable + export/import, popup 780px.
7. **Categorías**: riel fijo 7, editor (nombre/color/iconos verificados), gestor (toggles, orden, nueva, confirmación), fijas editables.
8. **Temas**: claro/oscuro en vivo sin perder detalles, alias en titlebar, perfil portable; popups y reproductor legibles en claro.
9. **Ayuda**: cuenta, soporte y acerca de con QRs, chat, web y versión (`ContactoInfo` pendiente: número + PNGs + GitHub).
10. **Estilos**: botones 3D + `BebeDialogStyle` + `ContentDialogMaxWidth` 920.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `npm install` en `.opencode/`: 27 paquetes.

---

## 2026-09-15 — Sesión 2: Tooling opencode + progress

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Plugin shunt** copiado de Top-Remix-App (`bulk_read` + hook >350 líneas), `opencode.json` con env, `.gitignore` solo `node_modules` (el origen lo ignoraba todo: aquí el plugin sí se versiona).
2. **`progress/feature_list.json`**: 13 features Baby Radio (9 done, 3 pending, 1 in_progress→done) con acceptance.
3. **`progress/history.md`**: Sesión 1 con el estado real.
4. **`AGENTS.md §8`**: regla de progreso por features.

### Verificación
- `node`: ambos JSON válidos; `npm install` OK.
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente por el usuario: `HF_API_KEY` en su `opencode.jsonc` + reiniciar opencode.

---

## 2026-09-15 — Sesión 3: Paletas 40 sin mocks + páginas + Mix polifónico

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Mocks fuera**: `Support/MockPaletteBuilder.cs` eliminado (legado usa `SlotVacio`); `Desplegar`/`LimpiarSlot` trabajan con 40 vacíos elegantes.
2. **Paginación**: `PaletaViewModel` con `_todos` (40) + `Items` (rebanada 20) + `PaginaActual/IndicadorPagina`; Arriba/Abajo del orquestador → páginas (la cola va por su lado).
3. **Mezcla**: nuevo `Services/MezcladorEfectos.cs` (6 voces, cue+ganancia, auto-stop, fin natural con id); `MotorAudio` intacto. Mix OFF exclusivo, ON polifónico; `Sonando` guía el titileo multi-cart.
4. **Acciones**: Stop corta solo efectos (`DetenerEfectos`); Mix titila 3×150ms y fija glow ámbar; tooltips y nombres de página.
5. **Fantasmas**: slots vacíos tenues + icono +; riel con `TieneAudio` por conteo (`RefrescarConteos`, clic derecho intacto); nuevo `Controls/BoolAVisibilidadConverter.cs`.
6. **Persistencia**: `Persistir` guarda los 40; `PaletaCambio` refresca el riel.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 14 `paletas-40-mix` en done.

---

## 2026-09-15 — Sesión 4: Esqueletos + blindaje + guía crear

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Esqueleto sin color**: `ColorFantasma` en `PaletteItem`/`SelectableCategory` (`#00000000` si vacío, con notificaciones); paleta y riel al tono del tema con solo bordes; riel con doble icono (color/tenue) + aviso Vacío.
2. **Blindaje anti-cierre**: `Support/RegistroErrores.cs` (log local); guard de reentrancia + try/catch en Editar paleta, editar/eliminar/gestionar/popups del riel y limpiar cola; `PaletaEditorDialog.Borrador` con INPC (los TwoWay ya siguen al clon).
3. **Guía crear**: `SinCategoria` + `LimpiarSeleccion` en paleta; riel vacío → `Selected=null`; overlay "Cree una categoría primero" con botón → `Riel.IniciarCreacionAsync()` desde la shell.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 15 `esqueletos-blindaje-guia` en done.

---

## 2026-09-15 — Sesión 5: Guía en clic derecho de vacíos

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Clic derecho en slot vacío**: ya no abre Editar/Limpiar; muestra guía deshabilitada "Arrastre un audio para cargar" (los llenos conservan sus acciones).
2. Blindaje previo intacto (reentrancia + try/catch + log) para el Editar de slots con audio.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

---

## 2026-09-16 — Sesión 6: Transporte homogéneo + display 3×3 (estado actual)

**Agente:** humano + opencode (muse-spark)

### Transporte (6 pads, lenguaje único: titileo = estado, sin bordes)
1. **Repetir** (`ArrowRepeatAll` verificado en FluentIcons 2.1.339.1): `RepeatArmed` + replay sin consumir en `AlTerminarNatural`; excluyente con StopAlFin; titila armado (se quitó su glow).
2. **Pausa**: nuevo `IsPaused` (ticker+toggle+stop); pad con icono pausa titilando (opacidad, sin bordes).
3. **Stop**: doble pulso al pulsar; **🏁** conserva titileo + glow ámbar.
4. Sombras GPU en los 6 (`AttachShadow`).

### Display en grilla 3×3 (280 px, mapa en comentario XAML)
- C1 = reloj `HH:mm:ss` + fecha `dd/MM` (`Consolas`, ámbar/gris, tick 1 s propio); A2 = título ámbar 13; B2 = artista gris 11; R3 = avisos (span 3).
- Libres para futuro: A1, B1, C2, A3, B3, C3 (+ R3 multi-uso). Marquee retirado (título a ancho completo con ellipsis).
- VU combinado: `MezcladorEfectos.Instancia` publica máximos a 20 Hz (`MedidorPicos` por voz, latencia 80 ms); `PlayerViewModel` publica el máximo cola/efectos con frescura 150 ms.

### Paleta/riel/cola (estado vigente, sin documentar antes)
- 40 slots, páginas 1/2 por ▲▼ (Stop = solo efectos); Mix ON polifonía 6 / OFF exclusivo; re-pulsar reinicia; arranque en background con feedback instantáneo + cancelación; navegar no corta (voces por clave dueña/slot, re-titileo por `LayoutUpdated`).
- Carts pegados 88 px (estilo por `TieneAudio`: macizo 3D lleno / aluminio esqueleto `*Prueba` en prueba visual); `TintaCart` adaptativa; nombre 12 + duración (sin subtítulo en modelo ni editor); menú vacíos con Cargar audio…/Editar…; `Duration` se copia al Guardar; título se actualiza si cambia el archivo.
- Riel 3D macizo a color si tiene audio (logo blanco, `ColorALetraConverter`), esqueleto si vacío (68 px); icono enum-typed (`Icono`, fallback musical); guía "Cree una categoría primero" si riel vacío.
- Tira 7 en 112×103, iconos 26 con relieve, Mix titila continuo sin glow; pickers sin RGB; `Vincular` ya no pausa la cola; trazas de motor en log.
- Blindaje: `App.UnhandledException` + tareas + reentrancia + try/catch en diálogos → `baby-radio-error.log`; `OnDraw` blindado; `Borrador` con INPC.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `AGENTS.md` §1 e inventario: PlayerBar 3×3 + 2 converters nuevos.

---

## 2026-09-16 — Sesión 7: Transporte + display + refinamientos (estado actual)

**Agente:** humano + opencode (muse-spark)

### Transporte homogéneo (lenguaje único: titileo = estado, sin bordes)
1. **Repetir** (`ArrowRepeatAll` verificado en DLL 2.1.339.1): `RepeatArmed`, replay sin consumir en fin natural, excluyente con `StopAtEndArmed`; titila armado (sin glow).
2. **Pausa**: `IsPaused` (ticker + toggle + stop); pad con icono pausa titilando 450 ms.
3. **Stop**: doble pulso al pulsar; **🏁** titileo + glow ámbar. Sombras en los 6 pads.
4. **Display 3×3** (280 px, mapa en comentario XAML): C1 reloj `HH:mm:ss` + fecha `dd/MM` (`Consolas`, tick 1 s); A2 título span (marquee retirado: ancho completo + ellipsis); B2 artista; R3 avisos. Libres: A1, B1, C2, A3, B3, C3.
5. **VU combinado**: `MezcladorEfectos.Instancia` (`MedidorPicos` por voz, agregado máximo 20 Hz); `PlayerViewModel` publica máximo cola/efectos con frescura 150 ms.

### Paleta, riel y cola (refinamientos)
1. **Riel**: `BebeCategoryButtonStyle` 3D macizo (gloss/shade nuevos en paleta) si `TieneAudio`; esqueleto si vacío; logo blanco + `ColorALetraConverter` (+`EsColorClaro`); icono enum-typed (`Icono`, validación en `FichaDe` + log); 68 px; previa del editor igual.
2. **Carts**: pegados (`Margin 0`, 88 px), estilo por contenido (`SlotAEstiloConverter`: macizo/claro `*Prueba` en prueba visual) + `TintaCart`; nombre 12 + duración (sin subtítulo en UI, `PaletteItem`, `SlotEfectoGuardado` y `QueueEntry.FromPalette` eliminado; JSON viejo compatible).
3. **Mezcla**: arranque en background (feedback instantáneo + cancelación), latencia 80 ms, re-pulsar reinicia, navegar no corta (voces por clave dueña/slot + re-titileo por `LayoutUpdated`), Mix titila continuo sin glow, Stop = solo efectos.
4. **Editor**: título/duración se actualizan si cambia el archivo; `Duration` se copia al Guardar; menú vacíos con Cargar audio…/Editar…/guía; `Vincular` ya no pausa la cola; `OnDraw` blindado + `TryGetValue`; red global `App.UnhandledException` + trazas de motor en log.
5. **Cola**: `MotorAudio` con generación anti eventos tardíos; `Next` anti-rebote 600 ms (escala a N pistas: una por clic, consumo al terminar).
6. **Tira 7**: 112×103, iconos 26 con relieve, pickers sin RGB; categorías: guía crear si riel vacío.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 16 `transporte-display` y 17 `paleta-riel-cola-refino` en done.
- `AGENTS.md` §1 e inventario sincronizados (PlayerBar, converters, motor, mezclador, riel, paleta, onda).

---

## 2026-09-16 — Sesión 8: Pantallita con primera canción en cue (sin autoplay)

**Agente:** humano + opencode (muse-spark)

### Cambios realizados
1. **Cue sin sonar**: `ListaReproduccionViewModel.AddFiles` deja la primera insertada como `CurrentEntry` si la cola estaba vacía; el reproductor la previsualiza (Título/Artista/tiempos) sin abrir el motor. Play suena desde 0.
2. **Separación previsualizar vs sonar**: nuevo parcial `ViewModels/PlayerViewModel.Cue.cs` (`SolicitarReproduccion`, `Previsualizar`, `LimpiarDisplay`, flag `_sonidoSolicitado`); `AlCambiarActual` previsualiza con motor detenido y suena con pedido explícito o motor ya sonando (continuidad en Next/Quitar).
3. **Pedido explícito preservado**: evento `ReproduccionPedida` en la lista (elevado antes de mover `CurrentEntry`, un solo arranque); clic/Poner en vivo, `Next` y fin natural siguen sonando; re-clic re-suena desde cero.
4. **`AGENTS.md`**: mapa §1 + inventario con `PlayerViewModel.Cue.cs` y `ReproduccionPedida`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 18 `pantallita-primer-cue` en done.
- Manual pendiente: arrastrar 1ª canción → nombre sin audio; Play → suena; vaciar → placeholder.

---

## 2026-09-16 — Sesión 9: Normalización LUFS + rendimiento + modo operador + tema único

**Agente:** humano + opencode (muse-spark)

### Normalización de volumen a −14 LUFS (solo reproducción)
1. **Análisis** (`Support/AnalizadorLoudness.cs`): BS.1770 a mano sobre NAudio — remuestreo a 48 kHz, filtros-K, bloques 400 ms/hop 100 ms, gating −70/−10 LU; devuelve `{Lufs, Pico}` en streaming con buffers reutilizados.
2. **Caché** (`Support/LoudnessCache.cs`): `baby-radio-loudness.json` local (separado del portable) con `mtime+length` para invalidar; `gain = −14 − lufs` recortado a ±12 dB y por techo de pico (−0.5 dB headroom); guardado con debounce 2 s.
3. **Worker** (`Support/ServicioAnalisisAudio.cs`): un solo hilo `BelowNormal` + `Channel` acotado y deduplicado; calcula loudness + recorte y avisa; si ya está cacheado no reanaliza.
4. **Reproducción**: `MotorAudio.ReproducirCola(entry, gananciaDb)` con `VolumeSampleProvider` universal (arregla el fallback MediaFoundation, clamp 0..8); `PlayerViewModel.SonarActual` usa la ganancia cacheada (0 si aún no lista); `ListaReproduccionViewModel` mueve el análisis al worker y aplica `FinEfectivo` en UI. Archivos originales intactos.
5. **NOTA**: se probó un `MezcladorEfectos` de salida única y se **revirtió** por regresión de calidad; quedan N salidas por voz en formato nativo (diseño vigente: 1 salida cola + N salidas efectos).

### Rendimiento del cambio de categoría (~1 s → ~15–25 ms)
1. **Pool estable** (`Models/PaletteItem.Vaciar`): los 40 slots y los visibles se crean una vez y se mutan; `MostrarPagina` no toca la colección si la página no cambió (cero regeneración de contenedores).
2. **Caché** (`Support/ConsolaStore.cs`): `ConsolaConfiguracion` en memoria con candado; `Guardar`/`Importar` la actualizan.
3. **Sombras** (`BebeButtonHelper.EnsureShadow` + `ShadowWiredProperty` + `ConEstado`): reanexo idempotente al re-templarse, sin acumular handlers; `ConsolaSombraHelper.AttachAllShadows` usa `EnsureShadow`; `PaletaPanel` reanexa en 2 pasadas de layout (`_pasadasSombras`).
4. **Medición**: trazas `Paleta.Desplegar` (json/slots/total) y `Paleta.Layout` en el log. Medido: Desplegar 13–26 ms, layout 23–61 ms.
5. **`UniformGridPanel`**: con alto finito reparte filas 1fr y devuelve el tamaño exacto; con alto infinito mantiene el alto natural (modo normal intacto).

### Modo operador (2 bancos, fullscreen, sin reproductor)
1. **Bancos**: `SelectorCategoriaViewModel` + `OpcionCategoria` (selección independiente por banco, recuerda en sesión, se reconstruye al gestionar); `ConsolaViewModel` suma `PaletaA`/`PaletaB` (+`TamanoPagina` 30 → 5×6, páginas 30+10), Stop corta todos los bancos, `PrepararOperador`/`SalirOperador` (sincroniza riel con banco A).
2. **Vista**: `BancoOperadorPanel` (tira de iconos + 1/2 + ◀▶ + gestionar + `PaletaPanel` con `MostrarEncabezado=False`) y `OperadorConsolaPanel` (2 bancos + tira `Config·Categorías·Stop·Mix` en Grid 4×* + salir + Esc + flujos crear/gestionar).
3. **Shell/ventana**: `Phase1ShowcaseView` dual (panel operador perezoso al primer ingreso); `MainWindow.EntrarOperador/SalirOperador` (fullscreen + oculta TitleBar, restaura tamaño).
4. **Tira**: `TitileoArmado` compartido (tira normal refactorizada + operador con ciclo de vida por visibilidad); Mix titila sin glow; bevel en iconos + relieve grabado en textos; tira al 100 % sin huecos.
5. **Blindaje**: brush `AluminumTop` agregado (rompía el panel con `XamlParseException` silencioso); `Support/ValidadorRecursos.cs` valida tokens al arrancar (`Recursos.Validacion`); `RefrescarModo` con try/catch que registra `Shell.Operador` y restaura el modo.

### Tema único oscuro + título del operador
1. **Claro eliminado**: `UsuarioDialog` solo alias + Guardar (placeholder `Navy Mix Dj`); `TemaConsola` sin tablas ni params claro (`TemaActual` siempre Dark, `Cambio` como `Action`); `PerfilOperador` sin `TemaClaro` (JSON viejos compatibles); titlebar fija en `Baby Radio`. Se conservan: tokens oscuros, carts por contenido (`BebeCartButtonStyleClaro`), `ColorALetraConverter`, `RequestedTheme` de popups.
2. **Título operador**: `ConsolaViewModel.TituloOperador` refrescado en cada ingreso (`OPERADOR nombre` sin comillas, o `OPERADOR`); alias persiste en JSON (`perfil.alias`).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores (múltiples builds limpios en la sesión).
- `progress/feature_list.json`: features 13 y 19–23 en done (11 QR y 12 Velopack siguen pending del owner).
- `AGENTS.md` §1 e inventario sincronizados (9 archivos nuevos + filas actualizadas).
- Manual: normalización (2 pistas de loudness opuesto suenan parejas), operador (2 bancos, fullscreen, salir/Esc, reingreso con memoria), alias → título operador.

---
## 2026-09-17 — Instalador v0.1.0 Velopack + Buscar actualizaciones (feature 12)

1. **csproj**: `Version 0.1.0`, `AssemblyName BabyRadio`, `RuntimeIdentifiers win-x86/x64/arm64`, `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true` (unpackaged permanente, Runtime incluido: el cliente no instala nada aparte), Velopack `0.0.1298` → `1.2.*`, `Content` de `Assets/release_notes.txt`.
2. **App.xaml.cs**: `VelopackApp.Build().SetAutoApplyOnStartup(true).Run()` con try-catch al inicio de `OnLaunched` (debug/primera ejecución continúan normal).
3. **ActualizadorBaby.cs** (nuevo, `Support/`): `ComprobarAsync` (GithubSource explícito a `NavyHernandez/Baby-Radio_TopRemix` + log en `RegistroErrores`), `DescargarAsync` con progreso 0-100, `Aplicar` con restart. Errores nunca se lanzan.
4. **AcercaDialog**: botón "Buscar actualizaciones" cableado — busca → si hay update descarga con % en vivo en `EstadoText` (vía `DispatcherQueue`) → aplica y reinicia; sin update informa versión; sin red, texto simple + log.
5. **publish.ps1** (nuevo): publish self-contained + `vpk pack` a `releases/` + upload idempotente (limpia release/tag `v0.1.0` huérfanos, captura `LASTEXITCODE` sin `Tee-Object`). Lecciones de Remove_Top aplicadas (Runtime MSIX, drafts huérfanos, exit code).
6. **Incidente `vpk pack`**: primer intento falló con `FileNotFoundException` de un `.mui` satelital en el zip temporal (carrera transitoria, archivo intacto en `publish/`); reintento directo OK.
7. **Artefactos locales** (sin upload, sin push por orden del owner): `releases/BabyRadio-win-Setup.exe` (146 MB), `BabyRadio-win-Portable.zip`, `BabyRadio-0.1.0-full.nupkg` + `RELEASES`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 12 en done.
- `AGENTS.md`: stack Velopack 1.2.* + nota instalador unpackaged + restricción sin push sin orden.

---
## 2026-09-17 — v0.1.1: fix Assets fuera del instalador + icono ventana

1. **Falla detectada en instalada**: sin logo/marca de agua ni icono. Causa 1: `publish/` no contenía `Assets/` (los `Content` del csproj no tenían `CopyToOutputDirectory`). Causa 2: `ImageIconSource` con `.ico` (formato no soportado) + `SetIcon` con ruta relativa.
2. **Fix**: `CopyToOutputDirectory=PreserveNewest` en todos los Assets; `TitleBar.IconSource` → PNG `Square44x44Logo.scale-200.png`; `Support/IconoVentana.cs` nuevo (P/Invoke `WM_SETICON` con ruta absoluta, nunca lanza); `SetIcon` con ruta absoluta.
3. **Ejecutable versionado**: `Version 0.1.1` (FileVersion 0.1.1.0 verificada) + copia `releases/BabyRadio-0.1.1-Setup.exe`.
4. **Verificado**: `Assets/` completo en `publish/` (watermark, ico, tiles, release_notes); `dotnet build` 0/0; `vpk pack` OK.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Instalador local v0.1.1 pendiente de prueba en instalada + upload a Releases con orden del owner.

---
## 2026-09-21 — Ayuda: Mi cuenta sin QR/chat + Soporte con WhatsApp real (feature 11)

1. **ContactoInfo**: `WhatsAppNumero = "593982311600"` (internacional, sin '+' ni espacios); `QrSoportePath` → `Assets/SupportQr.jpeg`; se elimina `QrUsuarioPath` (sin uso). `ChatUrl` = `https://wa.me/593982311600?text=...`.
2. **csproj**: `Assets/SupportQr.jpeg` como `Content` con `CopyToOutputDirectory=PreserveNewest` (viaja al instalador Velopack).
3. **UsuarioDialog**: sin marco de QR ni botón de chat; solo alias + Guardar + web; pie `© Derechos reservados por GH Dev Company` (`TextSecondary` 11, `AutomationProperties.Name` en español).
4. **SoporteDialog**: se elimina la rama "WhatsApp pendiente (sin número)" (el número ya existe); se conserva el fallback de QR como red de seguridad.
5. **Updater/CI (misma sesión)**: `publish.ps1` con `--publish` (vpk dejaba la release como borrador → la app nunca veía el update) y `exit 1` si falta token; secret `GH_TOKEN` verificado; release **v0.1.2** publicada en GitHub con `--merge` idempotente.
6. **VS DEP1560**: causa = perfil `Baby Radio (Package)` (deploy MSIX sobre app unpackaged). `Properties/launchSettings.json` deja solo `Baby Radio (Unpackaged)`; `EnableWinAppRunSupport=false` en el csproj.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `Assets/SupportQr.jpeg` copiado a `bin/.../Assets/`.
- `progress/feature_list.json`: feature 11 en done.

---
## 2026-09-21 — Buscar actualizaciones honesto + puntito en Acerca de (features 24–25)

1. **Consulta verificable**: `ResultadoActualizacion` suma `ConsultaOk` + `Detalle`; `ComprobarAsync` distingue `NotInstalledException` (copia no instalada), `HttpRequestException` (sin red) y genérica, cada una con log propio. `AcercaDialog` muestra 3 estados: motivo real, al día con hora de consulta a GitHub, o descarga-aplica.
2. **Aviso**: `CategoriasRailViewModel` suma `HayActualizacion` + `VersionActualizacion` y `ComprobarActualizacionAsync` blindado; icono Info en `Grid` 20×20 con `Ellipse` 8 px `StateOnAir` + `Storyboard` de pulso + tooltip con versión; chequeo diferido 8 s en `OnLoaded`.
3. Verificado: `dotnet build` 0/0; desde `bin/` informa copia no instalada (antes mentía "al día"); sin red no hay punto ni fallos.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 24 y 25 en done.

---
## 2026-09-21 — Arrastrar Lista → Paleta con guardado + fix de brillo (features 26–27)

1. **Drag**: `Support/FormatosArrastre.EntradaCola` (formato privado con `FilePath`); `ColaList` con `CanDragItems` + `OnColaDragStarting` (solo audio real); `OnQueueDrop` ignora el formato propio (sin duplicados al reordenar); `OnCartDragOver/Drop` aceptan el formato y encaminan por `SoltarAudio` (persiste en JSON).
2. **Brillo**: al cargar, el cart re-templaba y nacía sin sombra GPU (más plano/brillante); el re-anexado solo se armaba al cambiar categoría/página (confirmado: al volver se normalizaba). Nuevo `RearmarSombras()` en drop (2 rutas), `OnCargarAudioClick` y `OnLimpiarCartClick`.
3. Verificado: `dotnet build` 0/0; cart cargado persiste al reabrir e iguala brillo sin cambiar de categoría.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 26 y 27 en done.

---
## 2026-09-21 — Ganancia con ducking (feature 28)

1. **Toggle**: `IsGananciaArmada` + `ToggleGanancia` + `TitileoArmado` propio (espejo de Mix); hooks `GananciaActiva`/`ColaSonando` en `Paleta` (+A/B) cableados en `ConsolaViewModel`; eliminado el gancho muerto `AlPedirGanancia`.
2. **Ducking**: con armada + cola sonando, `Disparar` atenúa la música −6 dB (`AtenuacionColaDb`) con etapa maestra en vivo en `MotorAudio` (`FijarMaestro`); restaura a 1.0 al terminar la última voz, en `Stop` o si el arranque falla. Hallazgo: subir dB al efecto no servía (voces recortadas a 1.0, slots a 0 dB).
3. Verificado: `dotnet build` 0/0. Pendiente prueba manual con audio real (música baja, efecto domina, retoma sola).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 28 en done.

---
## 2026-09-21 — Config + transición en Siguiente (feature 29)

1. **Ajustes**: `ConsolaConfiguracion` suma `TransicionSiguienteActivada` (default true) + `TransicionSiguienteSegundos` (default 5); `ConfiguracionViewModel` (carga/guarda inmediato, valida 3/5/7, puente `TransicionIndice`); `ConfiguracionDialog` (`ToggleSwitch` + `RadioButtons` 3/5/7); tira y panel operador abren el diálogo (eliminado el gancho muerto `AlPedirConfiguracion`).
2. **Transición**: `MotorAudio` suma etapa compuesta (maestro × transición) + `TransicionAsync` (~20 Hz, cancelable); `Next` rampa-avanza con guard anti-doble y cancela en `Stop`/`Previous`/fin natural; `Abrir` restablece transición a 1.
3. **Incidente WMC9999**: borrar `AbrirConfiguracionCommand` rompió un `x:Bind` en `OperadorConsolaPanel.xaml:83` y el compilador XAML colapsó con error genérico (sus satélites de recursos faltan en esta máquina). Lección: ante WMC9999, listar TODOS los errores (suele haber `WMC0001/WMC0010/CS` ocultos) y `grep x:Bind` antes de borrar miembros del VM. Bisección documentada en sesión.
4. Verificado: `dotnet build` 0/0 tras el fix. Pendiente prueba manual (rampa 3/5/7 s, OFF = corte, Stop a mitad).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 29 en done.

---
## 2026-09-22 — Mini-pantalla 3×2 + PlayerBar compacto 60/40 (feature 30)

1. **Display trasplantado**: `MiniPantallaControl` nuevo (título+reloj / artista o restante + alias firma Segoe Script, marquee heredado, reloj 1 s, DP `Reproductor`) sobre la cabecera de la lista; el restante ocupa el lugar de artista ausente ("Archivo local" ya no se muestra).
2. **PlayerBar compacto**: fuera display, reloj y marquee (~120 líneas menos); una fila 60/40 con transporte repartido (pads 52/play 60, spacing 4 centrado) + progreso y VU estirado con fondo `BackgroundPanel` fusionado.
3. Nota: se probaron variantes de mini-onda en vivo y se revirtieron a petición (no constituyen feature).
4. Verificado: `dotnet build` 0/0; display y transporte vivos y sincronizados.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 30 en done.

---
## 2026-09-22 — Paleta 50 + guía vacía + portable al riel (feature 31)

1. **50 slots**: `SlotsPorCategoria` 40→50 (páginas 25+25; guardados 0-39 intactos, 40-49 nacen vacíos); carts `MinHeight` 81 con filas 1fr (se adaptan a pantalla).
2. **Guía vacía**: hint de arrastre al fondo de la caja solo con `ColaVacia` (nueva prop fijada en `RefreshSummary`).
3. **Sin header**: fuera "Paleta:" e icono portable del panel; limpiados DP `MostrarEncabezado`, evento y handlers muertos en paleta/banco/operador; exportar/importar junto a Acerca de en el riel con evento propio (la shell recarga igual).
4. Verificado: `dotnet build` 0/0; brillo parejo vía `RearmarSombras`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 31 en done.

---
## 2026-09-22 — Confirmar cierre con música + release CI (features 32–33)

1. **Cierre**: `MainWindow` intercepta `AppWindow.Closing` (WinAppSDK 2.2 no tiene deferral: cancela en síncrono y dialoga después, flag anti-loop); `ContentDialog` solo con `Reproductor.IsPlaying`; en silencio cierra directo; blindado con log. Funciona en operador.
2. **Release**: secret `GH_TOKEN` verificado; `publish.ps1` con `--publish` + `--merge` y `exit 1` sin token; **v0.1.2 pública** con assets Velopack; `launchSettings` solo Unpackaged + `EnableWinAppRunSupport=false` (fix DEP1560).
3. Verificado: `dotnet build` 0/0; release visible en GitHub; F5 sin DEP1560.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 32 y 33 en done.

---
## 2026-09-22 — Marquee siempre + datos en roaming (feature 34)

1. **Marquee**: corre con overflow en cualquier estado (sonando/pausa/detenido) + re-evaluación post-layout (si la primera medida fue 0 nunca arrancaba) + espera 0.9 s; tooltip con el título completo.
2. **Datos**: `%LOCALAPPDATA%\BabyRadio` es la raíz de instalación de Velopack (sobrevive updates en la práctica, pero se borra al desinstalar — docs oficiales). `ConsolaStore.CarpetaDatos()` ahora es `%APPDATA%\BabyRadio` con migración única best-effort de los 3 archivos desde la ubicación vieja.
3. Verificado: `dotnet build` 0/0. Release v0.1.3 en camino con estos cambios.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 34 en done.

---
## 2026-09-22 — Telemetría Firestore (feature 35)

1. **Modular sin SDK**: `Support/Telemetria/` con `DatosInstalacion` (DTO + JSON REST) + `TelemetriaFirebase` (`HttpClient` compartido con User-Agent —ipapi.co daba 429 sin él—, GUID en `ConsolaConfiguracion.IdInstalacion`, IP + país en 1 GET, `commit` con update + incremento atómico de `aperturas` que preserva `primeraVez`).
2. **Disparo**: fire-and-forget en `App.OnLaunched` tras activar la ventana; todo best-effort con log, jamás bloquea el arranque.
3. **Pendiente del owner**: pegar las reglas de `instalaciones` en consola Firebase (ya entregadas); sin reglas todo da `PERMISSION_DENIED` (registrado en log, sin afectar la app).
4. Verificado: `dotnet build` 0/0. Falta prueba manual contra el proyecto `baby-radio-3bfbb`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 35 en done.

---
## 2026-09-22 — Logo en ventana y taskbar (feature 36)

1. **Solo ventana + taskbar**: `TitleBar.IconSource` a `bbradioLogo.jpg` (1091×960, casi cuadrado) + `IconoVentana` vía `Bitmap.GetHicon()` (`LoadImage` no carga JPG), con fallback silencioso al `.ico` y respaldo anclado (el `Handle` muere si el GC recoge el `Icon`).
2. **Instalador intacto**: `.ico` de `ApplicationIcon` sin tocar; JPG declarado `Content` para que viaje en el publish.
3. Incidente menor: `??` sobre `nint` (CS0019 tras la cascada WMC9999 habitual) corregido con comparación explícita.
4. Verificado: `dotnet build` 0/0. Pendiente visual en ejecución.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 36 en done.

---

---
## 2026-09-22 — Icono PNG con transparencia en ventana y taskbar (feature 37)

1. **PNG nuevo**: `Assets/bbradioLogo-removebg-preview.png` (493×506 RGBA) como icono de `TitleBar.IconSource` + `IconoVentana` (recorte cuadrado centrado, HICON 16/32 px en alta calidad, fallback al JPG legado y al `.ico`).
2. **FIX taskbar**: mostraba el logo viejo porque `AppWindow.SetIcon` y el `.exe` usan el `.ico` embebido (`ApplicationIcon`); `Assets/AppIcon.ico` regenerado multi-tamaño (16–256 con alfa) desde el PNG. Release `BabyRadio.exe` v0.1.5 publicado en `bin/x64/Release/.../win-x64/publish/` (con el PNG en `Assets/`).
3. Lista de reproducción y marca de agua intactas. Si un anclado conserva el icono viejo: desanclar/reanclar (caché de Windows).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `dotnet publish -c Release -r win-x64`: exe + PNG presentes en `publish/`.
- `progress/feature_list.json`: feature 37 en done.

---
## 2026-09-23 — Titileo de Siguiente en transición + alias en cajitas (features 38 y 39)

1. **Titileo Next (feature 38)**: `PlayerViewModel` suma `TransicionEnCurso` observable (true al entrar a `AvanzarConTransicion`, false en `finally` vía `_colaUi` + en `CancelarTransicion` para Stop/Previous/fin natural); `PlayerBar` titila `NextButton` (opacidad 1 ↔ 0.35 cada 450 ms, igual que Repeat/Pausa) suscrito a la propiedad, con apagado en `Unloaded`/cambio de VM. Incidente: en `OnUnloaded` se reemplazó por error `ApagarParpadeoStop`, corregido (ambos apagados).
2. **Alias en cajitas (feature 39)**: `QueueEntry` suma `ArtistaVisible` computado (artista real intacto; sentinela "Archivo local" + alias → alias; sin alias → "Archivo local") + `ResolverArtistaVisible` puro y `RefrescarArtista`; `ListaReproduccionPanel` bindea `ArtistaVisible` y se suscribe a `TemaConsola.Cambio` (refresco al guardar en Mi cuenta, desuscribe en `Unloaded`). Mini-pantalla y reproductor intactos.
3. Release v0.1.6: `publish.ps1 -SkipUpload` + limpieza de `releases/` (solo vigente).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 38 y 39 en done.

## 2026-09-25 - Fader maestro horizontal (feature 40)
1. **Fader total de salida**: nuevo `Controls/MasterFaderControl.xaml(.cs)` (riel VuTrack empotrado, knob aluminio via override local de SliderThumbStyle, mute Speaker2/SpeakerMute + % en Cascadia Mono) montado sobre VU y www.top-remix.com en `PlayerBar` (columna der Spacing 2->6, mismo Stretch que el VU).
2. **Audio real**: `MotorAudio` suma _usuarioLineal + FijarMaestroUsuario (combinado usuario x ducking x transicion, ducking intacto); `MezcladorEfectos` suma maestro en vivo (etapa VolumeSampleProvider por voz + FijarMaestro a nuevas y activas; ganancia ahora uniforme tambien para MediaFoundation).
3. **VM + persistencia**: `PlayerViewModel` suma VolumenMaestro lineal, puente VolumenMaestroPorciento TwoWay con curva cuadratica, ToggleMute con previo, TextoVolumen; carga/aplica al arrancar y guarda en `ConsolaConfiguracion.VolumenMaestro`.
4. **Docs**: AGENTS.md mapa + inventario con MasterFaderControl.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 40 en done.

## 2026-09-25 - Fader channel-strip clasico (feature 40)
Restyle de `MasterFaderControl` a consola real: cap rectangular 18x30 (cuerpo BackgroundCard + filo HighlightTop, grip de 3 estrias, linea indicadora blanca, sombra BottomEdge, hundido Pressed 80ms) en override local de SliderThumbStyle; ranura oscura de 6px; escala dB serigrafiada 0/-6/-12/-20/-40 (posiciones de la curva cuadratica); pista del Slider invisible (manda el cap contra la escala, sin fill). Sin cambios de audio/VM/persistencia.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-09-25 - Crossfader Pioneer DJM (feature 40)
Template COMPLETO del Slider (DjCrossfaderStyle): adios cromo Windows (la pista nativa SliderTrackFill era lo que delataba el Slider estandar). Ranura fresada 6px + filo de luz, cap DJM 32x22 negro mate con grips laterales y linea blanca central, anillo de foco CategoryMusic, Disabled a 0.4. Partes HorizontalThumb/TrackRect/DecreaseRect + TickBars conservadas: arrastre, clic-en-pista, flechas y UIA intactos. Sin cambios de audio/VM.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-09-25 - Fader fusionado al panel (feature 40)
MasterFaderControl sin caja propia: fondo BackgroundPanel (igual que PlayerBar) y sin borde ni radio; solo quedan ranura + cap DJM + escala flotando sobre el panel.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-09-25 - Marca web sobre transporte (feature 40)
www.top-remix.com movido de la columna del VU a encima de los botones del reproductor: FontSize 9, LineHeight 10 compacto, Spacing 1 (casi sin padding vertical). Columna derecha solo fader + VU.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-09-25 - Tira inferior anclada al fondo (feature 41) + release v0.1.7
1. **Layout central**: `Views/Phase1ShowcaseView.xaml` deja el `StackPanel` (que no distribuia el alto y dejaba un hueco/recorte bajo la tira) por un `Grid` de filas `Auto / * / Auto`: `PlayerBar` (Auto), `PaletaPanel` (*, reparte el sobrante entre sus 5 filas) y `AccionesConsolaStrip` (Auto, alto de repo) anclada al borde inferior.
2. **Release v0.1.7**: bump en `BebeRadio.csproj`, `publish.ps1 -SkipUpload` y limpieza de `releases/` (solo la version vigente).
3. **Docs**: `Assets/release_notes.txt` con la entrada v0.1.7; `progress/feature_list.json` con la feature 41 en done.

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: features 40 y 41 en done.

## 2026-09-26 - Fader visible en todo el ancho + paleta sin costuras (features 42-43) + release v0.1.8
1. **Fader (feature 42)**: el cap DJM quedaba estático/centrado/anclado aunque el volumen cambiaba. Causa doble: el template `DjCrossfaderStyle` era un `Grid` de 1 celda (el motor del `Slider` posiciona el thumb con columnas y no tenía dónde aplicarlo) + `OnVolumenMaestroChanged` persistía el JSON completo en el hilo UI en cada tick y re-notificaba el porciento. Fix: `HorizontalTemplate` con 3 columnas nativas (`Auto/Auto/*`, thumb en la central, ranura/pista abarcando todo); sin re-notify de `VolumenMaestroPorciento` en el drag; guardado con debounce 400 ms en background (`ProgramarGuardadoVolumen`); `ToggleMute` cancela el debounce y persiste inmediato.
2. **Paleta (feature 43)**: costuras entre filas en pantallas grandes (muescas del radio 4 + filetes por alturas fraccionarias según DPI). Fix: `UseLayoutRounding="True"` + `BackgroundCard` en el `UniformGridPanel` de `PaletaPanel` (aplica a normal y operador por reutilización). Sin cambios de layout, botones ni lógica.
3. **Release v0.1.8**: bump en `BebeRadio.csproj`, `publish.ps1 -SkipUpload` (full + delta 0.1.7→0.1.8 + `BabyRadio-win-Setup.exe` + `RELEASES`) y limpieza de `releases/` (solo la versión vigente).
4. **Docs**: `Assets/release_notes.txt` con la entrada v0.1.8; `progress/feature_list.json` con las features 42 y 43 en done.
### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `publish.ps1 -SkipUpload`: paquete 0.1.8 OK (full + delta + Setup + RELEASES).

## 2026-09-28 - Paleta sin costuras: bordes enteros compartidos (feature 44)

1. **Causa raíz**: las líneas finas (1-2px) entre filas en pantallas grandes persistían tras la feature 43. El alto/filas daba fracciones (p. ej. 146.6px) y el redondeo de layout ajustaba el origen y el alto de cada cart por separado, abriendo filetes de `BackgroundCard` entre filas. En la laptop el alto salía divisible y no se notaba.
2. **Fix**: `UniformGridPanel.ArrangeOverride` calcula los bordes de celda de forma acumulativa con `Math.Floor` sobre el tamaño final real: celdas vecinas comparten exactamente el mismo borde y la última fila/columna absorbe el píxel sobrante. La grilla cubre el 100% del alto en cualquier pantalla/DPI. Se eliminó el campo cacheado `_rowHeight` (mediciones con variables locales).
3. **Docs**: comentarios XML del panel en español; `progress/feature_list.json` con la feature 44 en done. Sin cambios de XAML, estilos, ViewModels ni releases.

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 44 en done.
- Pendiente visual del owner en pantalla grande (normal 5x5 + operador 5x6).

## 2026-09-28 - Exportar/Importar portable para disco extraíble (feature 45)

1. **Exportar (riel, existente)**: `ConsolaStore.ExportarPortable` clona la config en memoria (caché intacta) y relativiza cada `FilePath` que comparta unidad con la carpeta destino (`RutaPortable`, funciones puras sin I/O); lo de otra unidad queda absoluto. Si hay no-portables, aviso "X de Y efectos están fuera del disco destino y no sonarán en otra PC". Local intacto, sin nada automático.
2. **Importar (riel, existente)**: pre-escaneo sin efectos (`LeerPortable` + validación `VersionEsquema` + `ResolverPortable` contra la carpeta del archivo + `ContarPortable`) y popup de confirmación "Cargar consola portable" con resumen (archivo, categorías propias, efectos X/Y encontrados) + aviso de que lo local no se modifica. Al confirmar, `ActivarPortable` entra en sesión portable (los `Guardar` van al archivo cargado, muere con la app) conservando el `IdInstalacion` local; al cancelar no pasa nada. Faltantes → aviso post-carga.
3. **Modelos/store**: `ConsolaConfiguracion` suma `Version` (viejos = 0); `Guardar` sella la versión y escribe en la ruta de sesión; `Importar` original intacto. Nuevo `Support/RutaPortable.cs`.
4. **Docs**: `AGENTS.md` mapa + inventario (`RutaPortable`, sesión en `ConsolaStore`); `progress/feature_list.json` con la feature 45 en done.

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 45 en done.
- Pendiente funcional del owner: exportar al USB → cargar en otra PC con distinta letra → todo suena; Cancelar no toca nada; JSON local del anfitrión intacto.

## 2026-09-28 - Pickers con filtro válido y export con nombre del alias (feature 46)

1. **Causa del reporte**: al pulsar Importar no se abría el explorador. El log (`Riel.Importar`) mostró `ArgumentException` en `FileTypeFilter.Add(".baby-consola.json")`: Windows solo acepta extensiones simples de un punto; el doble punto es inválido y el catch blindado lo tragaba en silencio. Bug preexistente, heredado por la feature 45.
2. **Fix**: filtros a `".json"` en Importar y `FileTypeChoices` en Exportar (los `*.baby-consola.json` existentes terminan en `.json` y siguen visibles). Exportar sugiere `<alias>_baby-radio-consola` (alias saneado de caracteres inválidos, recorte a 40) o `baby-radio-consola` sin alias. Si el picker falla, aviso al usuario además del log.
3. **Docs**: `progress/feature_list.json` con la feature 46 en done.

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 46 en done.
- Pendiente funcional del owner: Importar abre el explorador; Exportar con alias sugiere el nombre.

## 2026-09-28 - Modo operador configurable + Stop por banco (feature 47)

1. **Ajuste nuevo**: `ConsolaConfiguracion.OperadorPantallaCompleta` (default true); `ConfiguracionViewModel` con observable + puente `OperadorIndice` y guardado inmediato; `ConfiguracionDialog` suma RadioButtons "Modo operador" (completa·2 paletas / ventana·1 paleta) con nota de que se aplica al entrar.
2. **Ventana**: `MainWindow.EntrarOperador(bool)` — completa = FullScreen sin titlebar (igual que antes); ventana = no toca presenter ni titlebar (movible, redimensionable, convive con otras apps). `ConsolaViewModel.OperadorPantallaCompleta` se lee del JSON en `PrepararOperador`; la shell lo pasa al entrar.
3. **Panel**: `OperadorConsolaPanel` muestra 1 o 2 bancos según el ajuste (columna B a ancho 0 + colapso, reevaluado al entrar); solo categorías + paleta + tira en ambos modos. `SalirOperador` y Esc intactos.
4. **Stop por banco**: `PaletaViewModel.DetenerBanco` ([RelayCommand]) detiene solo sus Guid propios del mezclador compartido (el maestro se restaura solo sin voces vivas); botón Stop en el header de cada `BancoOperadorPanel`. El Stop global de la tira sigue cortando todo. Sin Ganancia en operador (pedido del owner).
5. **Docs**: `AGENTS.md` mapa + inventario; `progress/feature_list.json` con la feature 47 en done.

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `progress/feature_list.json`: feature 47 en done.
- Pendiente funcional del owner: elegir modo en Config → 2 paletas fullscreen idéntico; 1 paleta en ventana redimensionable; Stop por banco solo corta ese banco.

## 2026-09-28 - Release v0.1.9 (features 44-47) + ejecutable

1. **Versionado**: `<Version>` 0.1.8 → 0.1.9 en `BebeRadio.csproj`.
2. **Release**: `publish.ps1 -SkipUpload` (dotnet publish Release win-x64 self-contained + `vpk pack`: full + delta 0.1.8→0.1.9 + `BabyRadio-win-Setup.exe` + `RELEASES`). Sin subida a GitHub (sin orden del owner).
3. **Limpieza de `releases/`**: borrados `BabyRadio-0.1.8-*.nupkg`; solo queda la versión vigente.
4. **Docs**: entrada v0.1.9 en `Assets/release_notes.txt`; features 44-47 en done.
5. Incluye: paleta sin costuras definitivo (44), portable USB con sesión (45), fix pickers + export con alias (46), modo operador configurable + Stop por banco (47).

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores (build Release del publish OK).
- `releases/`: 0.1.9-full + 0.1.9-delta + Setup + RELEASES presentes.

## 2026-09-28 - Modo operador persistente y en vivo (feature 48)

1. **Persistencia**: ConfiguracionViewModel ya no guarda durante el ctor (flag _cargando); el Guardar prematuro pisaba 1 pantalla con el default de 2. OperadorIndice ignora indices negativos (el RadioButtons sin seleccion empujaba -1 = completa y reescribia el JSON).
2. **En vivo**: ConsolaViewModel.RefrescarAjusteOperador relee el JSON y notifica (el panel muestra/oculta el banco B solo); OperadorConsolaPanel lo llama al cerrar Config y ajusta el presentador (EntrarOperador/SalirOperador) sin salir del modo. PrepararOperador reutiliza el mismo metodo.
3. **Docs**: progress/feature_list.json con la feature 48 en done.

### Verificacion
- dotnet build -c Debug: 0 advertencias, 0 errores.
- progress/feature_list.json: feature 48 en done.
- Pendiente funcional del owner: Config -> 1 paleta persiste tras reabrir y reiniciar; dentro de operador el cambio aplica bancos + fullscreen al instante.

## 2026-09-28 - Release v0.1.10 (feature 48) + ejecutable

1. **Versionado**: Version 0.1.9 -> 0.1.10 en BebeRadio.csproj.
2. **Release**: publish.ps1 -SkipUpload (dotnet publish Release win-x64 self-contained + vpk pack: full + delta 0.1.9->0.1.10 + BabyRadio-win-Setup.exe + RELEASES). Sin subida a GitHub (sin token del owner).
3. **Limpieza de releases/**: borrados BabyRadio-0.1.9-*.nupkg; solo queda la version vigente.
4. **Docs**: entrada v0.1.10 en Assets/release_notes.txt; feature 48 en done.
5. Incluye: modo operador persistente y aplicado en vivo (48).

### Verificacion
- dotnet build -c Debug: 0 advertencias, 0 errores (build Release del publish OK).
- releases/: 0.1.10-full + 0.1.10-delta + Setup + RELEASES presentes.

## 2026-10-01 - Volver a configuración local + iconos en menú portable

1. **Salida de sesión**: `ConsolaStore.SalirSesionPortable` limpia `_rutaSesion` y recarga la caché desde el JSON local (intacto); el riel suma `OnConfiguracionLocalClick` que la invoca y reutiliza el evento `ConfiguracionImportada` (redespliega todo). Lo guardado en el portable ya quedó en su archivo, nada se pierde.
2. **Menú**: `CategoriasRailPanel` suma item corto "Configuración local" (icono Home), visible solo con `SesionPortableActiva` (vía `Opening`); Exportar lleva icono Save e Importar FolderOpen (FluentIcons, mismo peso).
3. **Docs**: apéndice en `progress/history.md` (este).

### Verificacion
- dotnet build -c Debug: 0 advertencias, 0 errores.
- Pendiente funcional del owner: importar → aparece "Configuración local"; pulsarlo vuelve a la consola local; sin sesión no se muestra.

## 2026-10-01 - Release v0.1.11 (features 49-52) + ejecutable

1. **Versionado**: `<Version>` 0.1.10 → 0.1.11 en `BebeRadio.csproj`.
2. **Release**: `publish.ps1 -SkipUpload` (dotnet publish Release win-x64 self-contained + `vpk pack`: full + delta 0.1.10→0.1.11 + `BabyRadio-win-Setup.exe` + `RELEASES`). Sin subida a GitHub (sin orden del owner).
3. **Limpieza de `releases/`**: borrados `BabyRadio-0.1.10-*.nupkg`; solo queda la versión vigente.
4. **Docs**: entrada v0.1.11 en `Assets/release_notes.txt`; features 49-52 en done.
5. Incluye: mapeo sin falso conflicto al cambiar modo (49), Momentáneo push-to-talk real (50), fundido de salida 120 ms en reemplazo y Stop (51), volver a configuración local + iconos en menú portable (52).

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores (build Release del publish OK).
- `releases/`: 0.1.11-full + 0.1.11-delta + Setup + RELEASES presentes.

## 2026-10-01 - Menú portable se refresca en cada despliegue

1. **Fix**: `ActualizarMenuPortable()` lee `SesionPortableActiva` en cada despliegue (al pulsar el iconito y en `Opening` del flyout, cubriendo clic izquierdo y derecho) y alterna "Configuración local" + separador. Antes solo se evaluaba en `Opening`.
2. **Docs**: apéndice en `progress/history.md` (este).

### Verificacion
- dotnet build -c Debug: 0 advertencias, 0 errores.

## 2026-10-05 - Fader master en modo operador

1. **UI**: `OperadorConsolaPanel.xaml` suma fila `Auto` con `MasterFaderControl` reutilizado (centrado, `MaxWidth=560`, `ViewModel={x:Bind Consola.Reproductor}`) entre bancos y tira; la tira pasa a `Grid.Row=3`. Cabecera del XAML actualizada. Sin lógica nueva: mismo volumen total (cola vía `MotorAudio.FijarMaestroUsuario` + efectos vía `MezcladorEfectos.FijarMaestro`), mute, curva cuadrática y persistencia de `PlayerViewModel`.
2. **Docs**: feature 53 en done en `progress/feature_list.json` + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores (nota: `dotnet build` pelado falla en esta máquina por `NETSDK1032` win-x64 vs PlatformTarget arm64; quirk de entorno, no del cambio).

## 2026-10-05 - Fader operador arriba amplio y centrado (sin logo)

1. **UI**: `OperadorConsolaPanel.xaml` mueve el `MasterFaderControl` a la barra superior (header de 3 columnas `Auto/*/Auto`, `ColumnSpacing=12`): título izq + fader `Stretch` `MinWidth=320` centrado + Salir der. Se elimina el `Image` watermark del header operador y la fila intermedia (vuelve a 3 filas `Auto/* /Auto`).
2. **Docs**: feature 53 enmendada + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores.

## 2026-10-05 - Fader operador al 60 % del ancho

1. **UI**: el contenedor del fader en el header operador pasa de `1.5*/7*/1.5*` (70 %) a `2*/6*/2*` (60 % centrado, laterales absorben el 40 %); `MinWidth=320` intacto.
2. **Docs**: feature 53 enmendada + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores.

## 2026-10-05 - Fader operador al 50 % del ancho

1. **UI**: el contenedor del fader en el header operador pasa de `2*/6*/2*` (60 %) a `1*/2*/1*` (50 % centrado); `MinWidth=320` intacto.
2. **Docs**: feature 53 enmendada + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores.

## 2026-10-05 - Modo 1 banco: riel derecho sin Stop propio

1. **UI**: `BancoOperadorPanel` suma DP `UsarRielLateral`: colapsa `HeaderClasico` y muestra `RielDerecho` (140px: categorías en 2 columnas con scroll + indicador y ▲▼/gestionar apilados a todo lo ancho, sin Stop pequeño). La `PaletaPanel` es única y compartida por ambos layouts (sin doble suscripción de eventos).
2. **Activación**: `OperadorConsolaPanel.ActualizarBancos` fija `BancoA.UsarRielLateral = !dos`; modo 2 bancos intacto (con Stop propio por banco). Stop global de la tira sigue cortando todo.
3. **Docs**: feature 54 en done + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores.

## 2026-10-05 - Release v0.1.12 (features 53-54) + ejecutable

1. **Versionado**: `<Version>` 0.1.11 → 0.1.12 en `BebeRadio.csproj`.
2. **Release**: `publish.ps1 -SkipUpload` (dotnet publish Release win-x64 self-contained + `vpk pack`: full + `BabyRadio-win-Setup.exe` + `RELEASES`). Sin subida a GitHub (sin orden del owner).
3. **Docs**: entrada v0.1.12 en `Assets/release_notes.txt`; features 53-54 en done.
4. Incluye: fader maestro en la barra superior del operador (53), modo 1 banco con riel derecho sin Stop propio (54).

### Verificacion
- `dotnet build BebeRadio.csproj -c Debug -p:Platform=x64`: 0 advertencias, 0 errores (build Release del publish OK).
- `releases/`: solo la versión vigente (full + Setup + RELEASES).

## 2026-10-05 - Push v0.1.12 al repositorio

- Commit `751e968` + push a `origin/main` (9 archivos; `.opencode/*` excluido: bump de plugin del entorno, ajeno al cambio).
- Sin upload a GitHub Releases (solo `-SkipUpload`, sin orden del owner).

## 2026-10-08 - Popup de Configuración en rejilla horizontal 2×2 (feature 55)

1. **UI**: `ConfiguracionDialog.xaml` pasa de `StackPanel` vertical plano a `Grid` 2×2 (`ColumnSpacing/RowSpacing` 12, `MinWidth` 560) con 4 tarjetas (`BackgroundCard` + `BorderSubtle` + `CornerRadius` 12, `Padding` 14,12): encabezado con `fluent:SymbolIcon` coloreado (`ArrowSync` música / `DualScreen` programas / `Speaker2` IDs / `Sparkle` promo) + título SemiBold.
2. **Horizontal**: duración 3/5/7 s con `RadioButtons MaxColumns="3"` (una fila, textos `3 s`/`5 s`/`7 s`); modo operador con `MaxColumns="2"` (`Pantalla completa` / `Ventana`, recuento de paletas pasa a la nota); toggle de transición a la derecha del título; combo de salida de audio en su tarjeta; nota PRO como píldora `CategoryPromoWash` + `LockClosed`.
3. **Sin lógica**: mismos `x:Bind` (`TransicionActivada`, `TransicionIndice`, `OperadorIndice`, `Dispositivos`, `DispositivoIndice`); `ConfiguracionViewModel` intacto.
4. **Docs**: feature 55 en done + apéndice en `progress/history.md` (este).

### Verificacion
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-10-09 – Latencia de disparo (30 ms + Retrigger async) y log por sesión (feature 56)

1. **Latencia**: `MezcladorEfectos` suma `LatenciaVozMs = 30` (antes 80 ms) como buffer de cada `WasapiOut` de voz; constante única y ajustable si se oyen tirones.
2. **Retrigger sin bloqueo**: `PaletaViewModel.DispararRetrigger` pasa a `Task.Run` (titileo al instante; lector+WASAPI abren en background) con `_pendientesRetrigger` (contador por slot) para que Stop o edición descarte la voz tardía; `DetenerVozDe` corta todas las capas del slot y `MarcarFinVoz` apaga el titileo solo al terminar la última.
3. **Log por sesión**: `RegistroErrores.IniciarSesion()` vacía `baby-radio-error.log` al arrancar (idempotente, best-effort); lo llaman `App.OnLaunched` y la primera escritura.
4. **Nota**: el primer disparo de la sesión sigue tardando ~1 s (frío de Media Foundation/endpoint WASAPI); el precalentamiento quedó fuera por decisión del owner.
5. **Docs**: feature 56 en done + apéndice en `progress/history.md` (este).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-10-09 – Pulso de glow con timer anclado (feature 57)

1. **Síntoma**: al pulsar Arriba/Abajo quedaba encendido el borde del color de la categoría (GlowBorder/GlowWash del `BebeLightButtonStyle`) y no se borraba nunca.
2. **Causa**: `ConsolaSombraHelper.FlashActive` creaba el `DispatcherQueueTimer` del pulso de 300 ms en una variable local (el único timer de la app sin campo): si el GC lo colectaba antes del Tick, el DP `IsActive` quedaba en `true`. Cambiar de página crea 25 carts y dispara la recolección justo dentro de esa ventana de 300 ms.
3. **Fix**: timer anclado en `Dictionary<Button, DispatcherQueueTimer>` con candado (se vacía en cada Tick, <=300 ms, no retiene botones); un pulso nuevo cancela el pendiente del mismo botón y el apagado solo ocurre si el Tick pertenece al pulso vigente. Mismo pulso visual de 300 ms; cubre Flechas, Stop, Config y carts.
4. **Docs**: feature 57 en done + apéndice en `progress/history.md` (este).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-10-09 – Logo marca de agua al fondo de la última página de la paleta (feature 58)

1. **Síntoma**: la última página de la paleta (p. ej. slots 50-59) estiraba sus 2 filas hasta el borde inferior → carts gigantes feos.
2. **Panel**: `UniformGridPanel` suma DP `MaxItems` (0 = sin tope = actual; tira de acciones y riel operador quedan intactos). Con tope, `filasLayout = max(filasHijos, ceil(MaxItems/columns))`: la fila adopta la altura de página completa (5 filas normal / 6 operador) y `Measure`/`Arrange` devuelven solo el alto usado, dejando una banda libre al fondo que el `Background` del panel no pinta.
3. **Marca**: `PaletaPanel` pone el logo (`ms-appx:///Assets/Baby-Radio-Watermark.png`, el mismo de la cola, Opacity 0.55, bordes difuminados del asset) **detrás de los carts**, anclado abajo centrado sin hit-test, sobre la raíz Grid con fondo único `BackgroundCard` (un solo bloque, sin corte de color).
4. **Code-behind**: `SincronizarMarcaDeAgua` fija `MaxItems = ViewModel.TamanoPagina` y ajusta `MarcaAgua.MaxHeight = max(80, banda - 24)` según el hueco real (`RaizPaleta.ActualHeight × filasLibres/filasPagina`); se llama en `Loaded`, cambio de VM, paginar y `SizeChanged`. En página completa la marca queda oculta tras la parrilla opaca.
5. **Docs**: feature 58 en done + apéndice en `progress/history.md` (este).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-10-09 – Preparación de v0.1.13 y pendientes conocidos

1. **Pendientes (no bloqueantes)**:
   - El primer disparo de la sesión aún tarda ~1 s (frío de Media Foundation/endpoint WASAPI); el precalentamiento quedó fuera por decisión del owner (ver feature 56).
   - Tamaño/opacidad del logo marca de agua de la paleta: ajustable a gusto del owner (feature 58).
   - Subida a GitHub Releases de v0.1.13: pendiente de token `GH_TOKEN` (no disponible en el entorno al preparar el paquete).
   - No quedan features `pending`/`in_progress` en `progress/feature_list.json` (58/58 done).
2. **Versionado**: `<Version>` pasa a 0.1.13 + entrada en `Assets/release_notes.txt`.

## 2026-10-09 - Push v0.1.13 al repositorio

- Commit `5cfcc5a` + push a `origin/main` (23 archivos; `.opencode/*` excluido: bump de plugin del entorno, ajeno al cambio).
- Paquete Velopack 0.1.13 generado (`releases/` solo con la versión vigente: full + delta + Setup + Portable + RELEASES).
- Upload a GitHub Releases pendiente de token `GH_TOKEN` (no disponible en el entorno).

## 2026-10-09 – CI publish idempotente (feature 59)

1. **Síntoma**: el run del push de docs (`43c18f0`) falló con `vpk upload github falló (código -1)`; el run anterior (`5cfcc5a`) había publicado v0.1.13 correctamente (5 assets).
2. **Causa**: el workflow corre en cada push a main y volvió a subir la MISMA versión: la limpieza de publish.ps1 borró la release y vpk recreó el tag en carrera → fallo -1. La release v0.1.13 no se tocó.
3. **Fix doble capa**: (a) workflow con `paths` (solo cambian `BebeRadio.csproj`, `publish.ps1` o el workflow) + `permissions: contents: write`; (b) `publish.ps1` consulta la release por API pública y termina con exit 0 si ya existe con assets (`-ForceUpload` para re-subir a mano). Guard probado localmente: exit 0 sobre v0.1.13.
4. **Nota**: vpk local actualizado 1.2.0 → 1.2.161 (en CI ya instalaba la última).
5. **Docs**: feature 59 en done + apéndice en `progress/history.md` (este).

### Verificación
- Guard local: `.\publish.ps1` con v0.1.13 publicada → "Nada que subir", exit 0.
- `dotnet build -c Debug` no aplica (solo PS1/YAML/docs); release v0.1.13 intacta en GitHub.

## 2026-10-09 – Pendiente: paths del workflow publish (requiere PAT con scope workflow)

- Opcional y no bloqueante: acotar `on.push.paths` del `publish.yml` a `BebeRadio.csproj`, `publish.ps1` y el propio workflow (+ `permissions: contents: write`) para que los pushes de docs/código no gasten ~8 min de CI publicando.
- No se aplicó porque el PAT de git local no tiene scope `workflow` (rechaza tocar `.github/workflows/*`); el owner decidió dejarlo así. El guard de `publish.ps1` (feature 59) ya hace que esas corridas terminen en success sin re-subir.

## 2026-10-10 – Ganancia por slot: clamp, herencia y persistencia (fix)

1. **Ganancia positiva no sonaba en efectos**: `MezcladorEfectos.Voz.Arrancar` limitaba el volumen lineal a `1.0` (0 dB) mientras `MotorAudio` lo limita a `8` (+18 dB). El editor permite hasta +6 dB: se oía en la pre-escucha del editor pero al disparar el cart quedaba igual. Clamp unificado a 8 (+ doc del parámetro).
2. **Ganancia fantasma al cambiar de audio**: `PaletaViewModel.SoltarAudio` y `PaletaEditorDialog.CargarArchivo` reseteaban cues pero no `GananciaDb` ⇒ al soltar un audio nuevo sobre un slot ocupado se heredaba el ajuste del anterior. Ahora, si el archivo cambia, la ganancia vuelve a 0; re-soltar el mismo archivo la conserva.
3. **Slider sin número**: `PaletaEditorDialog` muestra el valor en vivo (`TextoGanancia`, formato `+5.5 dB` / `-1.5 dB` / `0.0 dB`, Cascadia Mono) suscrito al `PropertyChanged` del borrador — `x:Bind` no soporta `StringFormat`, por eso va por propiedad calculada.
4. **Armado de Ganancia no persistía**: `ConsolaConfiguracion.GananciaArmada` (default false) + hidratación y guardado en `AccionesConsolaViewModel` con flag `_hidratando` (evita reescribir el JSON durante el ctor). JSONs viejos siguen válidos (campo ausente = false).
5. **Verificado en disco**: `%APPDATA%\BabyRadio\baby-radio-consola.json` guarda `paletas[*].gananciaDb` por slot (8 slots con ganancia ≠ 0: Music 6, Jingle 2) y `volumenMaestro`; no existe ganancia por paleta, solo por slot.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.

## 2026-10-10 – Fin natural de efecto con caída suave de 120 ms (fix)

1. **Síntoma**: al terminar un efecto el corte era seco (bajón/clic): el fundido de salida solo existía en Stop y en reemplazo, nunca en el fin natural.
2. **Causa**: `Voz._parada` cortaba exactamente en el cue (`PararAlFin` → `Stop()`); sin cue no había temporizador y el fin de archivo llegaba seco por `AvisarTermino`.
3. **Fix**: `Arrancar` programa el temporizador `FundidoSalidaMs` (120 ms) **antes** del fin efectivo (`hasta ?? lector.TotalTime`, clampado al fin real del archivo; `restante/2` en clips de <240 ms) y dispara el nuevo `IniciarFinConFundido`: rampa lineal de 120 ms en pasos de 10 ms sobre la etapa maestra de la voz, **sin** marcar `_cierreManual`, de modo que al llegar a 0 el `Stop` avisa el fin natural igual que antes (`MarcarFinVoz` → `Liberar` → apaga cart + restaura maestro).
4. **Blindaje de carreras**: temporizador propio `_faderFin` liberado por `Silenciar`, `FundirYSoltar` y `Soltar`; si un Stop/retrigger entra durante la caída, el fade natural se retira sin cortar (sin timers huérfanos ni doble aviso de fin).
5. **Docs**: cabecera de `MezcladorEfectos`, `FundidoSalidaMs` y `PararAlFin`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: efecto **con cue** y **sin cue** deben bajar suave al final; Stop pulsado justo durante la caída no deja el cart encendido ni voz colgada.

## 2026-10-10 – Modo operador fullscreen anclado a UNA pantalla (fix)

1. **Síntomas**: a veces el modo operador quedaba en ventana (sin reintentar) y a veces el fullscreen abría en el monitor equivocado.
2. **Causas**: `EntrarOperador` marcaba `_enOperador = true` **antes** del `try` con el `catch` vacío ⇒ si `SetPresenter(FullScreen)` no tomaba, la siguiente entrada salía por `if (_enOperador) return;` y la consola se quedaba en ventana sin registro en el log; y no había anclaje de monitor (el `FullScreenPresenter` elige pantalla por la posición de la ventana; cero usos de `DisplayArea` en el repo).
3. **Anclaje a un display**: `AreaDeVentana()` resuelve el monitor con mayor solape (`DisplayArea.GetFromRect(rect, DisplayAreaFallback.Nearest)`) y `AnclarEnUnaPantalla()` centra la ventana dentro de sus `OuterBounds` si no cabe entero (p. ej. a caballo entre dos monitores), **antes** de aplicar el fullscreen.
4. **Estado honesto + reintento**: `IntentarFullscreenOperador()` solo marca `_enOperador` si `Presenter.Kind == FullScreen` y registra la excepción (`RegistroErrores.Registrar`, antes `catch` mudo); si no tomó, `ProgramarReintentoOperador()` reintenta un frame más tarde (un solo intento, se desactiva al ejecutarse ⇒ sin bucle) y, si vuelve a fallar, queda en ventana con `Traza` en el log.
5. **Salida restauradora**: `SalirOperador` vuelve a `Default`, mueve a `_posicionPrevia` (monitor de origen), reescala a `_tamanoPrevio` y muestra la titlebar en su **propio** bloque (un fallo del presentador no la deja oculta). El estado previo se captura una sola vez (`_estadoPrevioGuardado`) para que un reintento no lo pise con la geometría ya anclada.
6. **Docs**: XML de todos los métodos implicados, cabecera de `MainWindow.xaml`, resumen del tipo y mapa + inventario de `AGENTS.md`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: abrir operador fullscreen con la ventana en cada monitor y a caballo entre los dos ⇒ siempre ocupa UNA pantalla; Esc ⇒ posición/tamaño previos; 5 entradas seguidas sin quedarse en ventana.

## 2026-10-12 – Preparación de v0.1.14

1. **Alcance** (todo lo acumulado sin release desde v0.1.13):
   - Feature 60: tira de acciones inferior sin espacios (7 botones a hueso, geometría por posición, `SurfaceCorners`/`SurfaceBorderThickness`/`GlowOnSurface` en `BebeButtonHelper`, sombra en parcial `BebeButtonHelper.Sombra.cs`, MinWidth 72 para Compact); misma geometría en la tira del modo operador.
   - Ganancia por slot: clamp unificado a +18 dB (`MezcladorEfectos` ya no limita a 0 dB), herencia al cambiar de audio reseteada a 0, slider con valor en vivo en el editor y `GananciaArmada` persistente en el JSON.
   - Fin natural de efecto con fundido de salida de 120 ms (sin corte seco al llegar al cue o al final del archivo).
   - Fullscreen del modo operador anclado a UNA pantalla (`AnclarEnUnaPantalla` + reintento único + estado honesto + restauración de posición/tamaño).
   - Config: refresco de la lista de dispositivos de audio en hilo COM encolado al UI, selección preservada y vuelta al predeterminado si el elegido desaparece.
2. **Docs**: feature 60 en done + este apéndice + entrada en `Assets/release_notes.txt`.
3. **Versionado**: `<Version>` 0.1.13 → 0.1.14 en `BebeRadio.csproj`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- `.\publish.ps1 -SkipUpload`: paquete Velopack 0.1.14 en `releases/` (sin subir a GitHub, por orden del owner).

## 2026-10-16 – Botones de ventana estilo macOS (feature 61)

1. **Problema**: los botones nativos del sistema se fundían con el Mica de la titlebar (mismo tono oscuro) y el owner quería botones redondos estilo macOS, siempre visibles, con hover rojo en Cerrar.
2. **Ocultar el sistema**: `Support/ChromeVentana.cs` nuevo. `OcultarBotonesDelSistema` llama a `OverlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` (patrón de microsoft-ui-xaml discussion #8800): se pierde la titlebar del sistema y sus botones, se conserva el borde. Limitación asumida: sin Snap Layouts de Win11.
3. **Fila propia**: `MainWindow.xaml` añade `FilaBotonesVentana` (StackPanel derecho, 3 botones 26px circulares) + estilos `BebeVentanaBotonStyle` / `BebeVentanaBotonCerrarStyle` y plantilla `BotonVentanaTemplate` en `ButtonStyles.xaml`: cuerpo `BackgroundCard` + aro `BorderSubtle` (siempre visibles, no se funden), resalte de hover sirviéndose de `Tag` como pincel (`HighlightTop` blanco8 % en Min/Max, `StateOnAir` rojo en Cerrar tipo macOS) animado por VSM `PointerOver`, `PressedOverlay` en Pressed, opacidad 0.4 en Disabled. Glifos FluentIcons `Subtract`/`Maximize`/`Dismiss`.
4. **Detalle crítico (hover)**: con los botones DENTRO de `AppTitleBar` (zona `SetTitleBar`) el clic llegaba vía región Passthrough pero el `PointerOver` del VSM **no se dispara** (la zona de arrastre se queda con el hover; limitación WinAppSDK). Solución: los botones son **hermanos posteriores** de `AppTitleBar` en el Grid raíz — al superponerse por encima reciben el puntero normal (hover + clic) sin tocar el arrastre del resto de la barra.
5. **Passthrough**: `ChromeVentana.PermitirClics` marca los rects de la fila como `NonClientRegionKind.Passthrough` (bounds DIU × `XamlRoot.RasterizationScale`) en `Loaded`/`SizeChanged` de la titlebar, para que el clic atraviese el área de arrastre.
6. **Handlers**: `MainWindow.BotonesVentana.cs` (parcial) con los 3 handlers clic y `ActualizarGlifoMaximizar` (alterna `Maximize`/`Copy` = Restaurar según `AppWindow.Changed` + `DidPresenterChange`, con nombre UIA y tooltip en español).
7. **Docs**: feature 61 en done + este apéndice + mapa e inventario de `AGENTS.md` (`MainWindow.BotonesVentana.cs`, `Support/ChromeVentana.cs`, estilos en `ButtonStyles.xaml`).

### Verificación
- `dotnet build -c Debug -p:Platform=x64`: 0 advertencias, 0 errores.
- Clics probados con input real: Minimizar → `IsIconic=true`; Maximizar → ventana a pantalla completa + botón pasa a 'Restaurar'; Restaurar → vuelve a ventana.
- Hover capturado con píxeles: el círculo de Cerrar se tiñe de rojo `StateOnAir` al entrar el puntero (diff before/after confirmado).
- Nota de captura: con PowerShell DPI-unaware `CopyFromScreen`/`PrintWindow` escalan1,5× y "pierden" la derecha de la ventana; usar `SetProcessDPIAware()` en los scripts de captura.

## 2026-10-20 – Salida de audio robusta al cambiar de dispositivo (feature 62)

1. **Problema**: al cambiar la salida de audio en Config, la app dejaba de sonar del todo (canciones y paletas), sobre todo con controladores DJ tipo Pioneer — aunque hubiera lista cargada o se pulsara una paleta. La app no debía detenerse: el cambio de salida es un switch, no una parada.
2. **Causas raíz verificadas**:
   - El fallback al predeterminado solo existía en la reapertura de `MotorAudio.EstablecerDispositivo`; la ruta normal (`Abrir`/`ReproducirCola`/`Disparar`) no reintenta → un dispositivo con `Init` fallido mataba **todas** las reproducciones posteriores.
   - `MezcladorEfectos` no tenía **ningún** fallback (asimetría con MotorAudio).
   - `Dispose` de WasapiOut/lector sin try/catch en `MotorAudio.Cerrar` y `Voz.Soltar`: un COMException del driver escapaba al binding TwoWay del ComboBox → cambio a medias y `Guardar()` saltado.
   - El cambio corría en el hilo UI (hasta 6 `Stop`/`Init` event-sync secuenciales podían congelarlo).
   - `ActualizarLista` reentraba a APIs de audio **dentro** del callback COM de `IMMNotificationClient` (bloqueo documentado por Microsoft; los drivers DJ notifican al abrir streams).
   - El `DispositivoSalidaId` guardado nunca se aplicaba al arranque (solo al abrir el diálogo Config).
3. **Solución**:
   - `AdaptadorSalidaAudio.CrearInicializadaConFallback(dispositivoId, ...)`: intenta el pedido y, si el `Init` falla, reintenta con el predeterminado (log + sin reintento si el pedido ya era el default). Centraliza el fallback de MotorAudio y de `Voz.CrearSalida`.
   - `MotorAudio.Cerrar` y `Voz.Soltar`: todos los `Dispose` en try/catch best-effort; el estado se limpia igualmente (sin zombis ni `_cierreManual` trabado).
   - `Voz.ReabrirEnDispositivo`: `Soltar()` protegido + `_cierreManual = false` en `finally`; segundo `Soltar` del catch también protegido. `MezcladorEfectos.EstablecerDispositivo`: try/catch **por voz** (un fallo no aborta la migración de las demás) y `_dispositivoId` fijado **bajo candado antes** del snapshot.
   - `ConfiguracionViewModel.AplicarDispositivo`: persiste al instante y aplica a los motores en `Task.Run` con serie `Interlocked` (solo el último cambio gana; la UI no se congela).
   - `EnumeradorDispositivosAudio.OnDispositivoCambiado`: encola `ActualizarLista` + evento a threadpool con debounce de 200 ms (coalesce del thrash de renegociación; cero reentrancia en el callback COM).
   - `App.OnLaunched`: aplica el `DispositivoSalidaId` guardado fire-and-forget tras abrir la ventana.
   - `Disparar`/`Detener`/`DetenerTodos`: `Soltar`/`Silenciar` **fuera** del candado (patrón ya usado en `Liberar`); `CancelarFundidoBloqueado`/`CancelarFundidosBloqueado` → `QuitarFundidosBloqueado` que solo retira y devuelve las voces para cortarlas fuera del candado (evita deadlock con `OnNivelVoz` del hilo de render a ~40 Hz).
4. **Docs**: feature 62 en `done` + este apéndice; XML actualizado en los métodos tocados.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: cambiar la salida a/un controlador DJ con la app sonando ⇒ la consola no se congela, la cola sigue (o retoma) por el default si el elegido falla, y las paletas siguen disparando; revisar `baby-radio-error.log` si algo queda mudo (etiquetas `[A event-sync]`/`[B polling]`/`[C resample]`/`Fallback`).

## 2026-10-20 – Salida DDJ-FLX4 sin reiniciar (feature 63)

1. **Problema en vivo**: al cambiar la salida a `Línea (2- DDJ-FLX4)` (mix 48000 Hz/4ch), los 3 intentos del adaptador fallaban en `WasapiOut.Init` con `ArgumentException: Value does not fall within the expected range` y la app quedaba muda hasta reiniciar Baby Radio (tras reinicio el mismo código funcionaba ⇒ estado transitorio del driver + formato rechazado). El fallback al default no servía: el default de Windows **era** el propio FLX4.
2. **Causa raíz**: el multiplex fabricaba `WaveFormat.CreateIeeeFloatWaveFormat(48000, 4)` — WAVEFORMATEX pelado sin channel mask — y `IAudioClient.Initialize` lo rechazaba con E_INVALIDARG (el MixFormat del FLX4 es `WaveFormatExtensible` con la mask del driver; WASAPI exige mask en canales >2).
3. **Solución (solo `Support/AdaptadorSalidaAudio.cs`)**:
   - `FormatoMultiplex`: si el MixFormat es float32 con los mismos canales/tasa, **se usa tal cual** (conserva rate + mask; NAudio 2.2.1 no expone `ChannelMask` ni ctor con mask, por eso se reutiliza en vez de clonar). Fallback a `CreateIeeeFloatWaveFormat` solo si no coincide.
   - Intentos **D/E estéreo directo** (sin multiplex, event-sync y polling) tras fallar A/B/C: el motor shared (`AutoConvertPcm`) upmixea 2ch a los canales front = master en la mayoría de controladores DJ.
   - `CrearInicializadaConFallback` + `CrearEnAlternativas`: si el pedido falla, prueba el default **si difiere** y después **los demás endpoints de render activos** (p. ej. Altavoces Realtek) hasta encontrar uno que initialise; log con el fallback efectivo. Si el default ES el fallido, ya no se reintenta contra él en bucle.
4. **Docs**: cabecera/XML del adaptador + inventario de `AGENTS.md` + feature 63 en `done` + este apéndice.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: cambiar la salida al FLX4 con la app corriendo (cola o paleta) ⇒ suena sin reinicio; si el FLX4 fallara, log muestra `[D/E estereo]` o `[fallback <otro endpoint>]` y la app no queda muda.

## 2026-10-20 – Salida ocupada por Serato: fail-fast + aviso + vuelta sola (feature 64)

1. **Problema en vivo**: con Serato DJ Pro corriendo (PID 9248) y `Línea (2- DDJ-FLX4)` como salida, el Init falla con `0x8889000A` (`AUDCLNT_E_DEVICE_IN_USE`): Serato retiene el endpoint WASAPI en **modo exclusivo** (el FLX4 es USB class-compliant y solo usa WASAPI — Pioneer lo documenta; no hay ASIO nativo, así que Serato tampoco "comparte"). El fallback de la feature 63 funcionaba (app no quedaba muda), pero probaba los 5 formatos de más, no dejaba rastro legible y, al cerrar Serato, Baby Radio seguía en el respaldo hasta que el usuario re-seleccionaba o reiniciaba.
2. **Investigación de compartición** (docs Serato/Microsoft): mientras una app tenga el endpoint en exclusivo, **ninguna otra** puede abrirlo (shared o exclusive). Vías de convivencia reales (configuración del usuario): (a) Serato *Setup > Audio > "Use Laptop Speakers"* → el master de Serato sale por el default y el FLX4 queda libre para Baby Radio (el cue sigue por el controlador); (b) `mmsys.cpl` → Propiedades del FLX4 → Avanzado → desmarcar *"Allow applications to take exclusive control of this device"* y *"Give exclusive mode applications priority"* **antes** de abrir Serato → ambos apps pueden convivir en el mezclador shared (riesgo: latencia de Serato); (c) el VAD de Serato (*"Make Audio Available to Other Applications"*) NO sirve: solo deja que otras apps monitoreen el audio de Serato, no al revés.
3. **Solución**:
   - `AdaptadorSalidaAudio`: `EsDispositivoOcupado(ex)` recorre la cadena de inner exceptions buscando `HResult == 0x8889000A`; en `CrearInicializada`, `AbortarSiOcupado` cancela al instante los intentos B–E (fallarían igual) con mensaje *"ocupado por otra aplicación en modo exclusivo (¿Serato DJ/ASIO?)"*; `CrearInicializadaConFallback` lo detecta, loguea con `Traza` y dispara el evento `RespaldoPorOcupacion(id, nombre)`.
   - `AdaptadorSalidaAudio.EstaLibre(id)`: sonda con una `WasapiOut` descartable (Init **sin Play** con `FuenteSilencio`: no suena) — si Serato aún retiene el endpoint, el Init falla barato y no interrumpe el audio del respaldo.
   - **Nuevo** `Support/RecuperacionSalidaAudio.cs` (singleton; suscrito en `App.OnLaunched` **antes** de aplicar el dispositivo guardado): cada 5 s sondea el preferido y, si está libre y sigue siendo el `DispositivoSalidaId` guardado, reaplica a `MotorAudio` + `MezcladorEfectos` (si el usuario cambió de destino, abandona). Eventos `SalidaOcupada`/`SalidaRecuperada` + `OcupadoActual` para la UI.
   - `ConfiguracionViewModel.AvisoDispositivo` (+ suscripción en `FijarDispatcher`, limpieza al cambiar manualmente de dispositivo y en `Dispose`): el diálogo Config muestra el aviso en la tarjeta *Salida de audio* (`TextoAVisibilidadConverter`, tinta `StateOnAir`) y desaparece solo al recuperar.
4. **Docs**: feature 64 en `done` (con la receta de convivencia en la descripción) + este apéndice + inventario/`§1` de `AGENTS.md`.
5. **Fuera de alcance**: código de Serato (imposible compartir un endpoint en exclusivo desde la app) y cambios en la receta de Windows (documentada, no automatizada).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: con Serato corriendo y el FLX4 como salida → reproducir (cae a respaldo con log legible + aviso en Config); cerrar Serato → en ≤ ~10 s Baby Radio vuelve sola al FLX4 sin reiniciar; cambiar a otro dispositivo a mitad → no debe volver al FLX4 viejo.

## 2026-10-20 – Calidad en multicanal: resample antes del multiplex (feature 65)

1. **Problema en vivo**: el owner reporta que la salida suena "muy baja calidad" cuando el sonido sale por los auriculares del controlador. Investigación de la cadena: `AudioFileReader` entrega la tasa del archivo (mp3 ≈ 44.1 kHz float); en endpoints multicanal (FLX4 48 kHz/4ch) el adaptador envuelve la fuente con `MultiplexMasterEstereo`, que declara el MixFormat (48 kHz) pero **reléa las tramas 1:1 sin resamplear** → el motor WASAPI consume48k tramas/s de una fuente real de 44.1k ⇒ audio ~8,8 % rápido y con aliasado (pitch + distorsión de alta frecuencia = "mala calidad"). El intento C ("resample") era un no-op: resampleaba DESPUÉS del multiplex, cuando la cadena ya mentía como 48 kHz (Wdl veía 48k→48k y se saltaba). La ruta estéreo (2 canales, p. ej. auriculares Realtek del PC) no tenía el bug: `formatoMultiplex = null`, la fuente conserva su tasa y el flag `AutoConvertPcm | SrcDefaultQuality` de NAudio/WASAPI resamplea el motor con buena calidad. La normalización por picos de `LoudnessCache` (techo con margen 0.5 dB) ya evita el clipping: descartada como causa.
2. **Solución (solo `Support/AdaptadorSalidaAudio.TryIntento`)**: resamplear la fuente a la tasa del mix con `WdlResamplingSampleProvider` **siempre que haya multiplex**, ANTES de envolver con `MultiplexMasterEstereo` (y en el intento C estéreo como antes). Documentado en la cabecera de la clase y en el XML de `resamplear`.
3. **Docs**: feature 65 en `done` + este apéndice + bullet FIX en `release_notes.txt` (v0.1.14).

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: reproducir un mp3 44.1 kHz por el FLX4 (o endpoint multicanal) ⇒ tono/velocidad correctos (sin sonar "apurado" ni raspado); la salida estéreo y los efectos no cambian.

## 2026-10-20 – Barra de posición con seek (feature 66)

1. **Petición**: el "ubicador" del reproductor mostraba el avance pero no se podía arrastrar para saltar dentro de la canción.
2. **Solución**:
   - `Services/MotorAudio.cs`: nuevo `IrA(TimeSpan)` — clampea a `[0, TotalTime]`; si el destino alcanza/supera el fin efectivo (recorte) hace `_salida.Stop()` para que el fin natural (`PlaybackStopped → AvisarTermino`) avance la cola igual que al terminar; si no, `_lector.CurrentTime = destino` y `RearmarParada` rearma el timer `_parada` con los ms restantes (antes un seek dejaba el timer viejo y cortaba en el momento equivocado). No-op sin pista cargada o si el motor está disposed; try/catch + log.
   - `ViewModels/PlayerViewModel.cs`: `[ObservableProperty] BarraArrastrando` (el ticker de 50 ms omite `RefreshTimeTexts` mientras se arrastra), `PrevisualizarSeek(fracción)` (actualiza `Progress`/textos sin tocar el motor) y `SolicitarSeek(fracción)` (llama `_motor.IrA` con la duración efectiva y refresca al instante).
   - `Controls/PlayerBar.xaml`: el `ProgressBar` (Height 4 intacta) queda dentro de un Grid columna 1 con un overlay `ZonaCursorHand` de 18 px de alto (`Background=Transparent`, `AutomationProperties.Name` en español).
   - **Nuevo** `Controls/ZonaCursorHand.cs`: Grid de una línea con `ProtectedCursor = Hand` (WinUI 3 solo permite asignar `ProtectedCursor` desde un tipo derivado).
   - `Controls/PlayerBar.xaml.cs`: `OnSeekPointerPressed` (capture + previsualiza), `Moved` (scrub), `Released` (aplica `SolicitarSeek`), `Canceled` (solo baja la bandera; el ticker restaura la posición real). `CapturePointer`/`ReleasePointerCapture` reciben `e.Pointer` (firma WinUI 3).
3. **Casos borde**: no-op sin pista/duración 0; recorte de cola clampeado + rearma; Repeat/Stop armados intactos (el seek no toca el fin natural); modo Tramo (pre-escucha) no se ve afectado; en pausa puede soltar ≤~150 ms del buffer viejo al reanudar (aceptable).
4. **Docs**: feature 66 en `done` + este apéndice + bullet NUEVO en `release_notes.txt` (v0.1.14) + inventario/`§1` de `AGENTS.md`.

### Verificación
- `dotnet build -c Debug`: 0 advertencias, 0 errores.
- Pendiente funcional del owner: con música cargada, clic en la barra salta; arrastrar previsualiza y al soltar salta; en recorte no pasa del fin y avanza sola; cancelar el arrastre no mueve nada.
