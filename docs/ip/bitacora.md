# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-10.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip1/h1-7-interaccion-ui`, creada desde H1.6 y subida a `origin` |
| Último commit de código | `48dac75 feat(core)` de H1.6; H1.7 está en curso (ver Pendientes) |
| `main` | En `48dac75`, el cierre de H1.6, también en `origin` |
| Hitos hechos | H1.1 a H1.6 |
| Hito en curso | H1.7 Interacción y UI, spec en `docs/gdd/interfaz-ip1.md` |
| Validación | `dotnet build` sin advertencias, 356 tests en verde, smoke con código 0 |
| Huella del smoke | `AED7A63935A0B1F7` con `--seed 1 --ticks 5000`; `0E9BCBB94E6FFC95` con `--seed 42 --ticks 5000` (cambia desde H1.6 porque el smoke trae la colonia) |

## Pendientes

En orden:

1. **H1.7 Interacción y UI, por avances.** Cada avance termina con build y tests en verde, la bitácora al día, un commit en `ip1/h1-7-interaccion-ui` y `git push`:
   1. Spec `docs/gdd/interfaz-ip1.md`. **Hecho.**
   2. Core: rayo sobre la grilla para apuntar celdas (`MapRaycast`, con tests); ajuste de pantalla completa en `Settings`. **Hecho.**
   3. Sesión de juego: bucle de ticks con `TickPacer` y velocidades; dibujo de colonos con interpolación, de pilas de objetos y de plantas que cambian; marcas de órdenes; escalera en ambos sentidos. **Hecho.**
   4. Entrada: apuntar, seleccionar y marcar áreas arrastrando; inspector de colono y de celda. **Hecho.**
   5. HUD: barra superior, barra de órdenes y registro; textos es y en. **Hecho.**
   6. Menús: principal, nueva partida, opciones, pausa y fin de partida; `GameRoot` arranca en el menú y suma `--play`. **Hecho.**
   7. QA: capturas es y en, smoke con la pantalla de partida, `/code-review`, documentación y commit de cierre.
2. **Contrato con la presentación,** en la spec de H1.6 (`docs/gdd/colonos-e-ia.md`, "Contrato con la presentación"):
   - `DesignateCommand` para las órdenes.
   - Eventos `EntitySpawnedEvent`, `EntityChangedEvent`, `EntityRemovedEvent`, `DesignationsChangedEvent`, `NotificationEvent` y `ColonyLostEvent`.
   - Componentes `Colonist`, `Needs`, `Job`, `Movement`, `ItemStack`, `Plant` y `Harvested` para el inspector y el HUD.
   - El smoke todavía vacía los eventos sin mostrarlos.
3. **Pasan de H1.5 a H1.7:**
   - El render de colonos (pool de nodos con interpolación entre ticks) y de objetos.
   - Que `PlantRenderer` siga a las plantas que se crean, se destruyen o cambian de época.
   - La pieza de escalera en ambos sentidos, con hueco.
   - La revisión de Tiny5 sobre el 3D, con el HUD.
   - Las variantes de aldeanos y el arbusto con y sin bayas. La lista de modelos que faltan está en `docs/art.md`.
4. **Para el balance de H1.8:**
   - El fondo del valle tiene muchas rampas, porque el relieve varía de a un nivel. Se puede suavizar bajando `terrainAmplitude` en `worldgen.json`.
   - En la vista 3D se ven como un serrucho.
   - La luz deja el nivel actual un 40 % más claro que el color del catálogo, y eso ayuda a distinguirlo.

El backlog (`docs/ip/backlog.md`) tiene dos temas para más adelante: una pantalla de error al arrancar y la agrupación de miles del español según CLDR.

## Notas prácticas

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
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
