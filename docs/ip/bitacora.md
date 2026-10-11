# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-11.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip1/h1-8-integracion`, creada desde `main` (cierre de H1.7) y subida a `origin` |
| Último commit de código | El avance 1 de H1.8, en `ip1/h1-8-integracion` |
| `main` | En el cierre de H1.7 (`f2905a5`), también en `origin` |
| Hitos hechos | H1.1 a H1.7 |
| Hito en curso | H1.8 Integración: playtest, balance, bugs, ms/tick, README y export de Windows (`v0.1.0`) |
| Validación | `dotnet build` sin advertencias, 379 tests en verde, smoke con código 0; `--benchmark 20` con 447 FPS de media y 0,033 ms por tick |
| Huella del smoke | `AED7A63935A0B1F7` con `--seed 1 --ticks 5000`; `05BB6F15EEBBEDC9` con `--seed 42 --ticks 5000` (cambia en H1.8 por el balance de las bayas) |

## Pendientes

En orden:

1. **H1.8 Integración, por avances.** Cada avance termina con build y tests en verde, la bitácora al día, un commit en `ip1/h1-8-integracion` y `git push`:
   1. Playtest automático largo y medida del presupuesto (`--benchmark`). **Hecho.**
   2. Checklist del playtest humano en `docs/ip/playtest-ip1.md`, a partir de la definición de hecho de IP1 y de los criterios de las specs. **Hecho.**
   3. README y CREDITS al día para `v0.1.0`. **Hecho.**
   4. Exportación de Windows. **Hecho:** `tools/package-windows.ps1` arma `builds/OurLife-v0.1.0-windows-x86_64.zip`. Las plantillas de Windows x86_64 de Godot 4.7.2 .NET están instaladas en esta PC, en `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`; en la otra PC habría que instalarlas (README, "Exportar para Windows").
   5. Balance: invierno sin bayas. **Hecho** (ver Historial).
   6. Playtest humano con el checklist, revisión de código, cierre de IP1 y tag `v0.1.0`. **Esperando el playtest del usuario** con `docs/ip/playtest-ip1.md`. La revisión de código de H1.8 ya se hizo. Después: corregir los hallazgos, registro de H1.8 en `IP1.md`, adelantar `main` y, con el visto bueno del usuario, el tag `v0.1.0`.
2. **Quedan de H1.7 para H1.8 o el backlog** (detalle en `docs/art.md` y el registro de H1.7 de `IP1.md`):
   - Variantes de aldeanos y una animación de caminar: hoy hay un solo modelo, en pose de T.
   - Un arbusto que muestre las bayas; hoy con y sin frutos se distinguen sólo por la forma.
   - Guardar el nombre de la aldea en la simulación, para el guardado de IP2.
   - Que `EntityRenderer` cambie los modelos cuando cambie la época (llega con la segunda época).
   - Los argumentos de entidad y de def en `TextResolver`, con el primer texto que los use (`docs/i18n.md`).
3. **Para el balance de H1.8:**
   - El fondo del valle tiene muchas rampas, porque el relieve varía de a un nivel. Se puede suavizar bajando `terrainAmplitude` en `worldgen.json`.
   - En la vista 3D se ven como un serrucho.
   - La luz deja el nivel actual un 40 % más claro que el color del catálogo, y eso ayuda a distinguirlo.

El backlog (`docs/ip/backlog.md`) tiene dos temas para más adelante: una pantalla de error al arrancar y la agrupación de miles del español según CLDR.

## Notas prácticas

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
- **Archivos `.uid`:** un script nuevo no tiene `.uid` hasta que el editor abre el proyecto. `& $env:GODOT_BIN --headless --path game --import` los genera sin abrirlo; correrlo antes de cerrar un hito y versionarlos.
- **Fuente nítida:** el smoke y `--screenshot` fallan si la caché de la fuente tiene suavizado. El mensaje indica cómo reimportar.
- **Comandos bloqueados:** un filtro de seguridad rechaza los comandos de PowerShell cuyo texto contiene `/code-review`, aunque sea dentro de un string. Esos textos se editan con la herramienta de edición.
- **Mirar el valle sin render:** un programa de consola temporal que referencie `OurLife.Core` puede volcar el valle en ASCII (alturas, materiales y plantas). Así se revisó H1.4.

## Historial

### 2026-10-10: H1.7 Interacción y UI (en curso)

- Adelanté `main` a H1.6 y creé la rama `ip1/h1-7-interaccion-ui`.
- El usuario pidió guardar cada avance en la bitácora y subirlo a GitHub para seguir desde otra PC. Subí `main` (estaba en H1.2 en `origin`) y la rama, y lo anoté en `CLAUDE.md`.
- Avance 1: spec `docs/gdd/interfaz-ip1.md`, con pantallas, controles, HUD, inspector, selección de celdas, textos nuevos y criterios de aceptación.
- Avance 2: `MapRaycast` en `OurLife.Core.Map` (recorrido de grilla de Amanatides y Woo que salta los niveles ocultos), con 8 tests; `Settings` guarda la pantalla completa. 364 tests en verde.

- Avance 3:
  - `GameSession` corre la simulación en tiempo real con `TickPacer`, con pausa, velocidades y espera de menú, y aplica en el acto las órdenes dadas en pausa (`Simulation.ApplyCommandsNow`, con test de que da la misma huella).
  - `GameScreen` une la sesión y la vista.
  - `EntityRenderer` reemplaza a `PlantRenderer`: plantas, pilas y colonos que siguen los eventos, con colonos que avanzan entre celdas y miran hacia donde caminan.
  - `OrderMarkers` marca las órdenes con colores.
  - Escalera en ambos sentidos con hueco.
  - Arbusto sin frutos (`plant.berry_bush.harvested`, `plant_bush.glb`).
  - Colonos a escala 2.
  - Opciones `--zoom` y `--ticks` fuera del smoke, para capturas.
  - Revisado en captura con `--seed 42 --level 14 --zoom 12 --ticks 300`. Los colonos están en pose de T porque el modelo no trae animación; queda para H1.8 o el backlog.
- Avance 4:
  - Excavar pasa a la tecla M, porque la E ya rotaba la cámara (spec y plan actualizados).
  - Textos de todo H1.7 en es y en: menús, HUD, órdenes, inspector y registro.
  - `CellPicker` usa el rayo de la cámara con `MapRaycast`.
  - `GameInput`: marco bajo el ratón, arrastre con orden que envía `DesignateCommand`, clic que selecciona colono o celda, teclas de órdenes y de tiempo, y Esc o clic derecho para salir.
  - `InspectorPanel` dentro de `Hud` (una `CanvasLayer`); `TextResolver.Capitalized`.
  - `UiTextTests` comprueba las claves que se arman en ejecución.
  - Opciones `--inspect` (selecciona al primer colono) y `--orders` (las órdenes del smoke) para capturas.
  - Las marcas de órdenes y de selección no proyectan sombra: rayaban el terreno.
  - Revisado en capturas es y en con `--seed 42 --level 14 --zoom 22 --ticks 200 --orders --inspect`.
- Avance 5:
  - `Hud` completo: fecha y hora, pausa y velocidades (resaltan la activa), nivel Z, inspector, registro, ayuda de la orden activa y barra de órdenes.
  - `EventLog` guarda `LocalizedText` y retraduce el historial al cambiar de idioma.
  - `ColonyStart` emite `event.colony.arrived` ("Llegan 5 colonos al valle"), que es el primer mensaje del registro.
  - Clave `ui.hud.date` en lugar de armar la fecha en el código.
  - Revisado en capturas es y en. Tiny5 a 16 px se lee sobre el 3D, aunque es chica: decidir en QA si se agranda.
- Avance 6:
  - `UiBuilder` y un archivo por pantalla: `MainMenu` (Cargar deshabilitado con aviso), `NewGameScreen` (nombre de la aldea y semilla, con "Al azar"), `OptionsScreen` (idioma en vivo y pantalla completa, guardados), `PauseMenu` y `GameOverScreen`.
  - `GameScreen` abre los menús en una capa encima del HUD y detiene el tiempo y la entrada mientras están abiertos; la partida termina con `ColonyLostEvent`.
  - `GameRoot` arranca en el menú principal y aplica la pantalla completa guardada.
  - Opciones `--play` (partida directa) y `--options` (opciones, sobre la partida con `--play`).
  - El nombre de la aldea se ve en la barra superior. Todavía no se guarda en la simulación: hará falta para el guardado de IP2.
  - El smoke arma la pantalla de partida con el inspector, la pausa, las opciones y los tres menús, y falla si falta un texto.
  - Revisadas las capturas del menú principal y de las opciones en es y en. La casilla de pantalla completa tiene poco contraste sobre el fondo oscurecido: revisar en QA.

### 2026-10-11: H1.8 Integración (en curso)

- Creé la rama `ip1/h1-8-integracion` desde `main` y dividí el hito en seis avances (ver Pendientes).
- Avance 1:
  - Playtest automático con el smoke: 30 días (43 200 ticks) en las semillas 1, 42 y 7, y un año y medio (150 000 ticks) en la 1 y la 42. Los 5 colonos sobreviven siempre; unos 0,006 ms por tick.
  - Modo `--benchmark <s>`: juega a 4× con las órdenes, sin vsync, descarta 2 s y mide FPS y ms por tick; sale con 1 si no cumple 60 FPS y 2 ms por tick. `GameSession.TickTime` mide el tiempo de los ticks fuera del Core.
  - Resultado en esta PC (RTX 4090 de portátil, ventana de 1280×720): 447 FPS de media, 360 en el 1 % más lento, 0,033 ms por tick.
  - El def de otra época bloqueado ya lo cubre `UnlockServiceTests`.
- Hallazgo del playtest automático: no se podía perder, porque las bayas rebrotaban todo el año. El usuario eligió que el invierno sea la amenaza. Además pidió que yo descargara las plantillas de exportación: bajé el `.tpz` oficial de 4.7.2 mono (1,2 GB), instalé sólo las de Windows x86_64 y borré el paquete.
- Avance 5 (balance), adelantado porque cambia el checklist:
  - `PlantDef.dormantSeasons`: en esas estaciones una planta recolectada no rebrota. El cargador valida que las estaciones existan en el calendario. El arbusto de bayas duerme en invierno.
  - Con 8 bayas y 4 días de rebrote, los arbustos apenas daban lo que comen 5 colonos: aun recolectando, morían en invierno. Pasan a 10 bayas y 3 días.
  - Resultado, en tests del contenido real (`ColonyTests`, semillas 1 y 42): sin recolectar, la colonia muere en su primer invierno; recolectando en otoño en un radio de 25 celdas, lo pasa.
  - Al probarlo apareció un problema de la interfaz: la caja de una orden quedaba en un solo nivel y no tomaba las plantas de una ladera. Excavar y Escalera siguen en un nivel; Talar, Recolectar y Cancelar toman todo lo visible en el rectángulo.
  - El smoke hace clic en cualquier colono a la vista: en la semilla 42 el primero quedaba tapado por el terreno.
- Avances 3 y 4 (exportación, README y CREDITS):
  - Preset "Windows Desktop" en `game/export_presets.cfg`: incluye los JSON de contenido y `visuals.json`, excluye `out/` y exporta a `builds/windows` (ignorado por git). `config/version="0.1.0"` en `project.godot`.
  - El ejecutable exportado pasa el smoke con la misma huella que el editor (`05BB6F15EEBBEDC9`), aunque corre sobre el .NET 8.0.27 empaquetado y el editor sobre .NET 10. La captura es idéntica.
  - Modo `--licenses <archivo>` (`LicenseNotices`): escribe la licencia de Godot, los avisos de sus componentes y el de .NET, leídos del propio motor. Godot exige distribuirlos con el juego.
  - `tools/package-windows.ps1`: exporta, corre el smoke sobre el exportado, agrega `LICENSES.txt` y `CREDITS.md`, y arma el zip (75 MB).
  - README con cómo se juega, cómo exportar y cómo medir; CREDITS con el motor y el runtime.
  - Checklist del playtest humano en `docs/ip/playtest-ip1.md`, con atajos `--ticks` para empezar en otoño (43 200), en invierno (64 800) o en el día 8 del invierno (74 880). El tick 0 es el día 1 a las 06:00.
  - Revisión de código de H1.8: 10 hallazgos, 8 corregidos.
    - Cancelar vuelve a un solo nivel, para no borrar órdenes enterradas que no se ven.
    - Talar y Recolectar siempre incluyen el nivel de la celda inicial.
    - El benchmark falla sin ventana, si no corrió ningún tick, o combinado con `--smoke` o `--screenshot`.
    - El script de empaquetado pone comillas a la ruta de `LICENSES.txt` y sus comentarios pasan al inglés.
    - El cargador rechaza `dormantSeasons` con todas las estaciones.
    - Quedan como están: que la caja de Talar y Recolectar se dibuje en un nivel, porque coincide en x e y con lo que se marca; y el costo de `PlantGrowthSystem` en invierno, que son unos 57 arbustos por tick con el benchmark en 0,03 ms por tick.
  - El lanzador de consola (`OurLife.console.exe`) sólo se genera en las exportaciones debug: el ejecutable de release no devuelve su salida a la terminal, así que se espera con `Start-Process -Wait` y se lee `%APPDATA%\OurLife\logs\godot.log`.

### 2026-10-11: H1.7, avance 7 (QA y cierre)

- El botón presionado del tema por defecto era apenas más oscuro que el normal: no se veía qué idioma, velocidad u orden estaba activa. `UiTheme` le da un fondo ocre (`PressedColor`) con texto blanco, y copia los márgenes por defecto para que los botones no cambien de tamaño.
- La casilla de pantalla completa pasa a una fila "Pantalla: Ventana | Pantalla completa", igual que la del idioma. Claves nuevas `ui.options.display` y `ui.options.windowed` en es y en, y en la spec.
- Tiny5 a 16 px se lee bien sobre el 3D en las capturas: no se agranda.
- Capturas revisadas en es y en: menú principal, opciones (sobre la partida y desde el menú) y partida con órdenes, inspector y registro.
- 367 tests en verde; smoke con código 0 y las mismas huellas (`AED7A63935A0B1F7` con la semilla 1).
- Primera ronda de revisión de código sobre toda la rama: 10 hallazgos, todos corregidos. El detalle está en el registro de H1.7 de `IP1.md`. Lo más importante: el rayo elegía la celda de adelante al apuntar un suelo con la cámara inclinada, y arrastrar una orden sobre una ladera marcaba todos los niveles enterrados. El smoke ahora dibuja la partida que trabajó, y `GameScreen` es el único lugar que arma una partida. 368 tests.
- Segunda ronda: 10 hallazgos más, todos corregidos (detalle en `IP1.md`). Lo más importante: un clic en el cuerpo de un colono elegía la celda de atrás; ahora se elige por la caja del modelo dibujado, y el smoke lo comprueba. La lista de órdenes sale del enum (`DesignationKinds.Id`). 369 tests y las mismas huellas.
- Tercera ronda: 10 hallazgos, 9 corregidos y uno descartado con su motivo (detalle en `IP1.md`). Lo más importante: un colono detrás de una loma ganaba el clic; `MapRaycast` ahora informa la distancia del impacto. También faltaban los `.uid` de 11 scripts: `--import` los genera sin abrir el editor. 370 tests.
- Documentación de cierre: `IP1.md` (H1.7 hecho), `plan-maestro.md` (árbol de `game/src`), `overview.md`, ADR-009 ("Resuelto en H1.7"), `art.md` (modelos que faltan), `i18n.md`, la skill `godot-run` y los agentes godot-engineer y qa-tester, con las opciones nuevas de captura. Los ejemplos de captura del valle necesitan `--play`, porque sin él el juego abre el menú.
- Cuarta ronda: 10 hallazgos, 7 corregidos y 3 descartados con su motivo (detalle en `IP1.md`). Lo principal: soltar una orden fuera del mapa o sobre el HUD marca la caja que se veía, un clic sobre un colono contra el cielo lo selecciona, y el primer cuadro ya no corre 40 ticks de golpe. Las capturas ignoran el ratón real.
- Adelanté `main` al cierre de H1.7 y lo subí.

### 2026-10-10: H1.6 Colonos e IA

- Retomé el trabajo sin commitear de la sesión anterior, que no había quedado en la bitácora:
  - Spec `colonos-e-ia.md` y ADR-011.
  - Defs `Scenario`, `AiConfig` y `WorkType`.
  - Sistemas de la colonia, A*, conectividad incremental y árboles que bloquean.
  - Tests de escenario y el smoke con la colonia.
- El smoke no llegaba a cavar la escalera: la caja de la orden era fija y caía en rampas o plantas. Ahora busca la celda válida más cercana a la llegada.
- `/code-review` en dos rondas, con 10 y 7 hallazgos, todos corregidos. El detalle está en el registro de H1.6 de `IP1.md`. Lo más importante: la búsqueda de árboles que cierran la superficie podía hacer fallar la generación del mundo, y ahora sigue las mismas reglas de movimiento que los colonos.
- `main` ya estaba en H1.5 (`e9e2407`); la bitácora decía H1.4.

### 2026-10-10: H1.5 Render y cámara

- Retomé el trabajo sin commitear de la sesión anterior, que no había quedado en la bitácora:
  - Modelos de Kenney, `visuals.json` y el cambio de `sprite` a `visual`.
  - Terreno por chunks, corte por nivel, plantas, cámara y los modos `--level` y `--probe`.
- Agregué `VisualCatalogTests`, que pedía ADR-009.
- Revisé las capturas y encontré tres problemas, que corregí:
  - Los árboles se veían celestes, porque Kenney guarda sRGB en el campo lineal de glTF. Ahora se corrige por kit.
  - El turquesa del kit chocaba con la paleta terrosa. Se reemplaza con la `palette` del catálogo.
  - La tapa del corte era de un solo color. Ahora toma el color del material oscurecido, así se leen los estratos.
- Ajusté la escala de los pinos y de los arbustos.
- `/code-review` en tres rondas, con 10, 10 y 9 hallazgos. El detalle está en el registro de H1.5 de `IP1.md`, y lo que quedó para después está en Pendientes.
- Actualicé ADR-009 ("Ajustes al implementar H1.5"), `art.md` (formato del catálogo y modelos que faltan), `plan-maestro.md`, `overview.md`, la skill `godot-run` y los agentes qa-tester y godot-engineer, que ahora sacan las capturas de idioma con `--probe`.
- Para mirar el valle de cerca, cambié a mano `StartDistance` de `WorldView` a 22 y lo volví a 85. Si hace falta seguido, conviene una opción `--zoom`.


### 2026-10-09: H1.4 Mapa y generación

- Rama `ip1/h1-4-mapa-generacion`, creada desde `ip1/h1-3-contenido-i18n`.
- Spec en `docs/gdd/mapa-y-generacion.md`. Hice yo mismo los pasos de spec, Core, tests y QA, siguiendo las reglas de los agentes, sin lanzar subagentes.
- Core:
  - `GameMap` con chunks sucios, `MapNavigation`, `Connectivity`, `GradientNoise` y `WorldGenerator`.
  - El def `WorldGen` con `worldgen.json` y las entidades `Position` y `Plant`. El mapa entra en la huella.
- El smoke genera el valle en unos 20 ms, con 55 a 57 arbustos de bayas y la superficie conectada.
- `/code-review` en dos rondas, con 10 y 8 hallazgos, todos corregidos. El detalle está en el registro de H1.4 de `IP1.md`.

### 2026-10-09: paso a 3D low-poly

- Analicé dos kits de Kenney. El Hexagon Kit se descartó porque obliga a usar una grilla hexagonal, y eso encarecía el Core y la construcción. El usuario eligió el Nature Kit, que es CC0 y está pensado para una grilla cuadrada.
- Escribí ADR-009, que reemplaza a ADR-007. Define la escala de una celda por unidad de Godot, el catálogo `visuals.json`, el terreno por chunk con `MultiMesh` y tinte por material, el corte por nivel Z, la cámara 3D, la selección por rayo y que se mantiene GL Compatibility.
- Actualicé `plan-maestro.md`, `IP1.md`, `art.md`, `overview.md`, `eras.md`, ADR-001, ADR-007, ADR-008, `CLAUDE.md` y los agentes art-integrator, godot-engineer y game-designer.
- El guardado pasa de ADR-009 a ADR-010.
- IP1 pasa de 100–135 h a 112–153 h, por los cambios en H1.5 y H1.7. La simulación no cambia.

### 2026-10-09: restauración de `.claude/`

- Tras reiniciar VS Code, `.claude/` dejó de estar bloqueada. Restauré sus archivos desde git y quité la marca `skip-worktree`.
- Actualicé la skill `godot-run`: `--locale` y `--screenshot` disponibles desde H1.3, rutas relativas desde `game/`, capturas sin `--headless`, reimportación tras cambiar un `.import` y cómo leer `GODOT_BIN` si la sesión no la hereda.
- Borré la memoria `claude-dir-restore-pending` y los pendientes correspondientes de `IP1.md`.

### 2026-10-09: cierre de H1.3

- Terminé el cambio que estaba sin commitear: la fuente Tiny5 se configura en `Tiny5.ttf.import` (sin suavizado, sin hinting, sin subpíxel y sin respaldo del sistema) y ya no desde el código.
- `/code-review` en tres rondas, con 15, 13 y 10 hallazgos. El detalle está en el registro de H1.3 de `docs/ip/IP1.md`. Lo principal:
  - El arranque está protegido: cualquier error sale con código y el smoke no se cuelga.
  - Godot recibe los mensajes ya formateados, no el patrón ICU.
  - ICU: estilo `integer`, un `select` sin argumento muestra `⟦nombre⟧`, los casos `=N` se analizan una sola vez y se normalizan, y el redondeo lleva las mitades al par.
  - Los cargadores exigen JSON estricto y rechazan `null` en los defs.
  - Las fuentes de contenido ignoran los archivos ocultos y los que empiezan con punto, con la regla en `IContentSource`.
  - `ProgressState` sólo cambia desde Core.
  - Tests nuevos: claves inexistentes o con argumentos usadas como texto visible, `ui.language.<idioma>` en cada idioma, y el cargador de catálogos real del juego enlazado en los tests.
- Decisiones que se mantienen frente a la revisión:
  - La fuente se configura sólo en la importación.
  - Un `select` con un valor sin caso usa `other`, como ICU.
  - Un cast deliberado a `ProgressState.Knowledge` no se impide.
- Se actualizaron `IP1.md` (H1.3 hecho), `backlog.md`, `i18n.md`, `art.md`, `overview.md`, ADR-005 y `plan-maestro.md`.
- Commit `007a389` en `ip1/h1-3-contenido-i18n`.
