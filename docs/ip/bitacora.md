# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-10.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip1/h1-6-colonos-ia`, creada desde H1.5 |
| Último commit de código | `feat(core)` de H1.6 en `ip1/h1-6-colonos-ia` (ver `git log`) |
| `main` | En `e9e2407`, el cierre de H1.5; todavía no incluye H1.6 |
| Hitos hechos | H1.1 a H1.6 |
| Próximo hito | H1.7 Interacción y UI |
| Validación | `dotnet build` sin advertencias, 356 tests en verde, smoke con código 0 |
| Huella del smoke | `AED7A63935A0B1F7` con `--seed 1 --ticks 5000`; `0E9BCBB94E6FFC95` con `--seed 42 --ticks 5000` (cambia desde H1.6 porque el smoke trae la colonia) |

## Pendientes

En orden:

1. **Adelantar `main` a H1.6**, pendiente de que el usuario lo confirme: `git fetch . ip1/h1-6-colonos-ia:main`.
2. **H1.7 Interacción y UI.** El contrato con la presentación está en la spec de H1.6 (`docs/gdd/colonos-e-ia.md`, "Contrato con la presentación"):
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
