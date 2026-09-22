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
