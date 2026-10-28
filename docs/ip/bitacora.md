# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-28.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip4/o9-habitaciones`: Core de O9 hecho (falta la presentación). O8 en `main`; O2 a O7 hechos |
| `main` | Con L2, L3 y L4 de IP3.5 (después de `v0.3.0`), también en `origin` |
| Hitos hechos | IP1 completa (H1.1 a H1.8, `v0.1.0`), IP2 completa (H2.1 a H2.7, cerrada el 13 de octubre de 2026) IP2.5 completa (V1 a V6, cerrada el 15 de octubre de 2026) e IP3 completa (H3.1 a H3.10, cerrada el 19 de octubre de 2026, `v0.3.0`) |
| IP en curso | IP3.5 "Libre albedrío" (`docs/ip/IP3.5.md`), desde el 19 de octubre de 2026; Oficios es IP4 |
| Validación | `dotnet build` sin advertencias, 800 tests en verde, smoke con código 0, benchmark de 30 s dentro del presupuesto (377 FPS, 0,07 ms por tick; con `diez-anos`, 382 FPS y 0,08 ms) |
| Huella del smoke | `2E368CE2376689E4` con `--seed 1 --ticks 5000`; `1284D40F4588F8FE` con `--seed 42 --ticks 5000`, las mismas que en `v0.3.0`: el smoke juega sin libre albedrío, y así la huella no lo incluye (criterio 3 de la spec). Con L4b cambian (el bosque no depende de la opción): `3E6090980A92B35A` (semilla 1) y `5905A0BCAA4F5860` (semilla 42). Con O3 (formato 13): `CFDA6D91EF34C721` (semilla 1) y `BC74B881EBCCDB7F` (semilla 42) . Con O6 (formato 16): `9AF8B6F29DC3B10F` (semilla 1) y `EFDFF865F9D47D99` (semilla 42). Con O7 (formato 17): `62E9519A3F9FEEE9` (semilla 1) y `D6155DFF8353CC4B` (semilla 42); O8 (formato 18) y O9 (formato 19) las dejan iguales: el smoke no fabrica herramientas, ni muebles, ni muros |
| Versión | `v0.3.0` publicada como release de GitHub con los zips de Windows y macOS: https://github.com/gtaulamet/OurLife/releases/tag/v0.3.0. `./tools/package-windows.ps1` y `./tools/package-macos.ps1` los vuelven a armar (no se versionan; el de macOS necesita PowerShell 7.4 o posterior) |

## Pendientes

En orden:

1. **IP4 "Oficios"**: presentación de O9 (capa de habitaciones, inspector, lista, asignación de camas, comedor, capturas es/en, íconos), y después O10 y O11 (`docs/ip/IP4.md`). El Core de O9 está hecho.
2. **Orden manual de cosechar cultivos maduros** (mejora de O10): falta su criterio de aceptación y los textos es/en (`docs/gdd/agricultura.md`).
3. **Modelos propios** (`docs/plan-modelos.md`): 38 de 41 hechos; los de cultivos ya se vieron en el juego.
4. **Balance de IP3:** con 40 % por año, uno de cada tres deseos de un hijo se cumple. Decidido el 28 de octubre de 2026: se deja así y se revisa con las partidas de diez años de O11.
5. **En la otra PC**, para exportar hay que instalar las plantillas de Godot 4.7.2 .NET (README); para macOS, también `macos.zip`.

## Notas prácticas

- **Exportar se colgaba:** los nodos de MSBuild y el servidor del compilador que quedan vivos después de compilar retienen la consola del lanzador de Godot, y la exportación nunca termina. Los scripts de empaquetado exportan con `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0` y `UseSharedCompilation=false`. A mano: `dotnet build-server shutdown`.

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
- **GitHub CLI:** `gh` está instalado en esta PC y autenticado como `gtaulamet`. Una sesión de PowerShell abierta antes de instalarlo no lo tiene en el `PATH`: usar `& 'C:\Program Files\GitHub CLI\gh.exe'`. Las releases se crean con `gh release create <tag> <zip> --verify-tag --notes-file <archivo>`.
- **Archivos `.uid`:** un script nuevo no tiene `.uid` hasta que el editor abre el proyecto. `& $env:GODOT_BIN --headless --path game --import` los genera sin abrirlo; correrlo antes de cerrar un hito y versionarlos.
- **Fuente nítida:** el smoke y `--screenshot` fallan si la caché de la fuente tiene suavizado. El mensaje indica cómo reimportar.
- **Cadenas de comandos:** encadenar con `&&` hasta el final. Un `;` en medio deja correr lo que sigue aunque falle lo anterior: así se adelantó `main` una vez con un commit incompleto (se corrigió adelantándolo de nuevo).
- **Comandos bloqueados:** un filtro de seguridad rechaza los comandos de PowerShell cuyo texto contiene `/code-review`, aunque sea dentro de un string. Esos textos se editan con la herramienta de edición.
- **Mirar el valle sin render:** un programa de consola temporal que referencie `OurLife.Core` puede volcar el valle en ASCII (alturas, materiales y plantas). Así se revisó H1.4.

## Historial

### 2026-10-28: O9, Core de habitaciones, dormitorios y comedor

- Rama `ip4/o9-habitaciones` (desde `main` con O1 a O8). Sólo Core, defs, textos mínimos y tests; la presentación (capa de habitaciones, inspector, lista, asignación de camas en la UI, comedor con colonos sentados, capturas es/en) queda para el godot-engineer y el i18n-specialist.
- Defs (`rooms.json`): `RoomConfig` con los números de la spec, cinco `RoomLevel` (mísero 0, pobre 8, decente 18, cómodo 36, lujoso 61) y cuatro `RoomRole` (dormitorio, comedor, taller, almacén). `Material.beauty {wall, floor}` (madera 0/1, mampostería 1/1, tablones 1/2, sillería 2/3), `Buildable.tags` (`bed`, `table`, `seat`, `chest`, `workshop`) y once pensamientos nuevos en `mood.json` con `MoodConfig.ateAtTable`, `familyHome` y `sleptCrowded`. Ningún número de la spec se movió. Desvío menor: `RoomRole` dice lo que pide con `needs` y el almacén con `storageCellsPercent` (la spec lo decía en prosa).
- Core: `Rooms.At`/`Rooms.All` sobre un índice derivado por simulación (`RoomIndex`, `RoomSetup` con la validación de los defs), búsqueda por cuatro direcciones con tope de `maxCells`, exterior recordado, puntaje (tamaño, muebles por la calidad en centésimas, suelo y límite con las esquinas), nivel y funciones por etiqueta, `roofed`. `GameMap.StructureVersion` (forma, material y marca de mueble) y `RoomSystem` (`RoomsChangedEvent` sólo si cambió el conjunto de habitaciones con muebles). `Bed.Reserved` y `AssignBedCommand` (reservar, liberar, reasignar; rechaza etapa que no cabe), `RoomRules.Owners`, `SuggestAssignments`, y los pensamientos al despertar (nivel, `family_home`, `slept_crowded`). `JobKind.EatAtTable` (pila, camino con la comida en las manos, silla, come sentado, pensamientos) y `JobKind.Gather` (libre albedrío, 18 a 22 h, `gatherTicks`; interrumpible por necesidad o trabajo).
- Desvíos y decisiones anotados en `docs/gdd/habitaciones.md` ("Notas de implementación"): la reunión tira con el flujo `colony.free_will` (la spec decía "ninguno"); `All()` sólo da habitaciones con muebles o zonas de almacén; el radio del comedor se mide en el plano como los demás del libre albedrío; la reserva termina al morir el dueño; los pensamientos de nivel se reemplazan entre sí.
- Formato 19 sin migración (sólo `Bed.Reserved` si es verdadero, los trabajos nuevos y el comando). Fixture `o9.olsave` (formato 18, semilla 7, 4 320 ticks, cama, mesa y silla de tablones, escrito con el código de la rama antes de O9).
- Tests: `RoomTests` (61: detección, cavar, tope, techo, puntaje 20/54/79, calidad, funciones, cambios del mapa y evento, pensamientos de dormir, reservas, familias, sugerencias, comer sentado, crítico y sin comedor, reunión con y sin libre albedrío, guardado a mitad de una comida, determinismo, validación de los defs), `RoomBudgetTests` (20 habitaciones y 15 colonos: unos 0,05 ms por tick; rehacer 20 habitaciones unos 0,2 ms; 300 cofres a cielo abierto unos 4 ms), `RoomContentTests` (los números reales de la spec) y `OldSaveTests` con `o9.olsave`. 861 tests del Core, 261 de contenido y 49 de localización en verde (`FarmContentTests` de tiempo falló una vez con la máquina cargada por correr todo en paralelo y pasa solo; los de presupuesto de habitaciones toman la mejor de varias tandas por lo mismo); smoke con código 0.
- Huellas del smoke con `--ticks 5000`: `62E9519A3F9FEEE9` (semilla 1) y `D6155DFF8353CC4B` (semilla 42), las mismas que en O7 y O8: el smoke no construye muebles ni muros, así que no hay habitaciones ni camas reservadas y la huella no cambia.
- Textos mínimos para que den verde los tests de contenido (los pulirá el i18n-specialist): `def.room_role.*`, `def.room_level.*` (nombre con `select` sobre el género de la función), los 11 `def.thought.*` y `job.eat_at_table.report` y `job.gather.report`. Las claves de la interfaz de la spec (`ui.inspector.room`, `ui.rooms.*`) no están todavía.
- Falta (presentación de O9): botón y capa de habitaciones (paleta por nivel e íconos 16 × 16 de función y nivel), inspector de celda y de colono, lista de habitaciones, panel de camas con `Asignar familia` (manda un `AssignBedCommand` por cada `SuggestAssignments`), pose `sit` del colono sentado (`Job.Kind` `EatAtTable` o `Gather` en etapa `Working`), claves `ui.*` de la spec, capturas es/en del comedor con colonos sentados y de una familia asignada, y el criterio 14 en la partida.
### 2026-10-28: O10, orden manual de cosechar cultivos

Rama `ip4/o10-cosecha` desde `main`. Nueva orden `DesignationKind.Reap` (atajo N, botón Cosechar) que reutiliza `HarvestOrder` y el trabajo `reap` en cultivos maduros, dentro o fuera de zona, y pasan antes que la cosecha automática; Cancelar la quita. Los inmaduros se ignoran. Sin estado nuevo: formato de guardado sin cambios. `Harvest` de plantas silvestres no toca cultivos. Modo `--reap-order` para capturas (`out/reap-es.png`, `out/reap-en.png`). Spec en `agricultura.md`.

- Revisión (/code-review) de la cosecha manual: una sola pasada de candidatos con los cultivos ordenados primero (`Candidate.Ordered`, grupo -1 en `Reach`; sin campo mutable); `CropsInside` y `PlantsInside` comparten `IsInside`; el inspector muestra la orden de cosechar de un cultivo fuera de zona; `HarvestOrder` documenta su doble uso. Cancelar sobre un cultivo de zona quita la celda y el cultivo, así que el trabajo termina sin estado nuevo en `Job` (formato sin cambios). Tests de guardado, cancelar en zona y cultivo inalcanzable.

### 2026-10-28: revisión de cultivos e íconos pixel de O5

- Cultivos: modo de captura nuevo `--crops` (con `--play`: un parche 3 x 3 junto al primer colono, trigo, guisantes y repollo en brote, crecimiento y maduro). Revisado en pantalla (`game/out/cultivos.png`, `cultivos2.png`, zoom 4): los tres cultivos y sus tres etapas se distinguen. Los brotes y el crecimiento se perdían contra el pasto, así que en `visuals.json` los `.sprout` pasan a escala 1,6 y los `.growing` a 1,3 (los maduros quedan en 1,0).
- Íconos de O5: `tools/make-knowledge-icons.py` genera 8 PNG de 16 x 16 en `game/assets/icons/knowledge/` (ramas `branch_crafts`, `branch_field`, `branch_fire_metal`; estados `state_learned`, `state_available`, `state_queued`, `state_blocked`, los cuatro de `KnowledgeState`; y `research` para el HUD). Se cargan con `KnowledgeIcons` (filtro más cercano, sin textos nuevos): ícono de estado en cada tarjeta del panel K, ícono de rama (doble tamaño) en la cabecera de cada fila y la libreta junto a la línea "Investigando" del HUD. El texto se queda al lado como respaldo accesible. Los `.png` y `.import` se versionan.
- Capturas es y en: `game/out/k-es.png`, `game/out/k-en.png` (`--play --knowledge --hour 12`; ahora también refresca la línea del HUD).

### 2026-10-28: O8, presentación de los talleres de fuego y las herramientas

- Inspector del taller: ya listaba las recetas de cualquier taller (`Recipes.Of`), así que carbonera, horno y fragua salen solos. Una receta bloqueada ahora nombra el conocimiento que falta (`ui.workshop.make_missing`, "requiere: herrería"); si sólo bloquea la época queda `make_locked`. El desplegable del panel de producción hace lo mismo (`ui.production.recipe_locked`).
- Hoja del colono: sección "Herramientas" (`ui.sheet.tools`) antes de las habilidades, con una línea por herramienta (`ui.inspector.tool`: nombre, calidad si no es la normal y `Tools.SpeedPercent` en el oficio); se oculta si el colono no tiene ninguna.
- `job.equip.report` ya se mostraba por `TextResolver.Activity` (usa `JobKinds.ReportKey`); sin cambios. La barra de construcción ya traía los tres talleres atenuados por conocimiento; ahora es un `HFlowContainer` porque con 16 piezas la fila se salía de la pantalla a 1280 x 720.
- Textos: la receta `charcoal` pasa a "Quemar carbón" (el botón decía "Hacer 5: hacer carbón").
- Modo de captura `--metals` (con `--play`): una fragua junto al primer colono con órdenes de hacha y carbón, y al colono un hacha obra maestra y un pico. Con `--inspect-furniture` muestra la fragua; con `--sheet`, la hoja. Capturas en `game/out/` (no versionadas): `o8-taller-{es,en}.png`, `o8-hoja-{es,en}.png`, `o8-build-es.png`. La fragua sale "bloqueada" porque la colonia nueva no sabe herrería (no hay forma de aprender desde el modo de captura).
- Modelos: fase 7 en `docs/plan-modelos.md` y filas en `docs/art.md` para `furniture_charcoal_kiln.glb`, `furniture_bloomery.glb` y `furniture_forge.glb`; los provisorios siguen.
- Falta: los tres modelos propios y el criterio 15 en la partida (taller en uso y herramienta forjada, que necesita jugar días o una partida guardada con los conocimientos).

- Revisión (`/code-review`) corregida: el desvío a la herramienta se calcula una vez por tick y sólo para trabajos con bonus; `Equip` se cancela si la pila deja el almacén o el colono ya no puede ese trabajo; `QualityRules.ValuePercentOf`; `Tools.Grant` para el modo `--metals` (fragua en `masonry` y en celda libre); texto "requiere" del panel de producción refrescado. Huellas del smoke sin cambios.

### 2026-10-28: O8, Core de fundición, herrería y herramientas

- Rama `ip4/o8-metales` (desde `main` con O1 a O7). Sólo Core, defs, textos mínimos y tests; la presentación (inspector del taller, modelos propios de los talleres de fuego, capturas) queda para el godot-engineer y el art-integrator.
- Defs: carbonera (`charcoal_kiln`, 8 de mampostería, 300 ticks, belleza -2), horno de fundición (`bloomery`, 12, 450, -4) y fragua (`forge`, 12, 450, -4), cada uno con el conocimiento que ya existía (`charcoal_burning`, `metallurgy`, `ironwork`); recetas `charcoal` (4 troncos, 2 carbones, 240), `iron_ingot` (2 minerales y 2 carbones, 300), `iron_axe` y `iron_pickaxe` (lingote y mango, 360) e `iron_hoe` e `iron_hammer` (300), las cuatro con `quality`. Los números son los de `oficios.md`; los items y las herramientas (`tool`) ya estaban desde O2. Ningún número ni decisión de la spec se movió.
- Core: componente `Equipment` (lista de pilas de una unidad con su calidad; se agrega con la primera herramienta, así una partida sin herramientas se guarda y se huella como antes), `JobKind.Equip` (id `equip`, sin tipo de trabajo ni experiencia), clase `Tools` (consultas `Of`, `EquippedFor`, `SpeedPercent`) y `DecisionSystem.Equip`. `WorkSpeed.PercentOf` multiplica por `(100 + bonus) / 100`, con `bonus = speedPercent × valuePercent / 100` de la herramienta que el colono tiene para ese tipo (hacha normal 30, obra maestra 60, pobre 22). Al morir, el colono deja las herramientas en su celda con su calidad (`ColonistDeath`).
- Decisión (desvío menor de la spec, anotado en `oficios.md`): el colono equipa en el momento de tomar un trabajo, no antes de buscarlo. Si el trabajo que tomó es de un tipo con herramienta que no tiene y hay una libre en un almacén dentro de `craftFetchRadius` (30), suelta ese trabajo (`Unreserve`) y va a buscarla; al volver a decidir toma el trabajo ya equipado. Así nadie busca una herramienta para un oficio que no tiene trabajo. Una pila fuera de un almacén no cuenta (se acarrea primero) y entre varias gana la de más velocidad con su calidad, luego la más cercana, luego la de menor id. Reutiliza `craftFetchRadius` en lugar de un número nuevo.
- Formato 18 sin migración (nadie tiene herramientas en una partida de formato 17). Fixture `o8.olsave` (formato 17, semilla 7, 4 320 ticks, colonia nueva, escrita con el código de `main` antes de O8).
- Tests: `ToolTests` (equipar y talar, no buscar otra, sólo la herramienta del oficio, el pico del minero, la mejor calidad y una herramienta por colono, fuera de almacén y fuera de radio no, velocidad por calidad y ticks de talar, no acelera otros trabajos, cae al morir, determinismo y continuidad del guardado con un colono yendo a equipar), `MetalsTests` (troncos y mineral a carbón, lingote y hacha y el hacha equipada; cada paso espera su conocimiento y la fragua no se planea sin él; misma semilla, mismo hacha y misma huella), `MetalsContentTests` (recetas, herramientas, bonus por calidad y cadena de conocimientos con el contenido real), `OldSaveTests` con `o8.olsave` y un colono que equipa el primer hacha guardado. 795 tests del Core, 250 de contenido, 49 de localización, en verde; smoke con código 0.
- Huellas del smoke con `--ticks 5000`: `62E9519A3F9FEEE9` (semilla 1) y `D6155DFF8353CC4B` (semilla 42), las mismas que en O7: el smoke no fabrica herramientas, así que la huella no cambia.
- Textos mínimos añadidos para que den verde los tests de contenido (los puede pulir el i18n-specialist): `def.buildable.{charcoal_kiln,bloomery,forge}.*`, `def.recipe.{charcoal,iron_ingot,iron_axe,iron_pickaxe,iron_hoe,iron_hammer}.*` y `job.equip.report` en es y en.
- Modelos: los tres talleres de fuego usan modelos provisorios en `visuals.json` (`furniture.charcoal_kiln` y `furniture.bloomery` con `furniture_kitchen.glb`, `furniture.forge` con `furniture_mason_bench.glb`). Faltan los modelos propios: montículo de tierra con humo, horno bajo de piedra con boca y fragua con yunque. Los `item.iron_*` propios ya existen.
- Falta (presentación de O8): inspector del taller con las recetas nuevas y el estado de la orden (`locked` con el conocimiento que falta), mostrar la herramienta equipada en la hoja del colono (`ui.inspector.tool`, `Tools.Of`), el texto de actividad con la clave `job.equip.report`, los tres modelos propios, capturas es/en y el criterio 15 en la partida.

### 2026-10-28: O7, presentación de vetas y niebla

- `MapRenderer`: un muro sólo genera caras si `IsDiscovered` y está expuesto; el corte por nivel pinta la cara superior de una celda oculta con `LevelShading.UnexploredCap` (#26282E, más frío y oscuro que el tono de hueco) y sin patrón. Inspector: celda oculta, "Sin explorar" (no nombra forma ni material); una celda de veta se nombra por su material, como cualquier otra (`ui.inspector.unexplored`, sin argumentos, en es y en). El `/code-review` pidió quitar `ui.inspector.ore_cell`: marcaba como veta también el suelo ya excavado. El borde lateral del mapa cuenta como visto (`IsSeen`), así sus estratos se siguen dibujando.
- Patrón nuevo `ore` (`TerrainPattern.Ore` = 7) con motas oscuras en `terrain.gdshader`; `iron_vein` lo usa en `visuals.json`. Desvío: el tinte rojizo ya lo da el color `#8A4B3A` del material (el catálogo de visuales sólo asigna patrón, no color). El texto de `iron_vein` e `item.iron_ore` ya estaba bien y el modelo `item.iron_ore` ya existía en `visuals.json`.
- Capturas en `game/out/` (no versionadas): `o7-valle-{es,en}.png`, `o7-corte-{es,en}.png` (nivel 12), `o7-tunel-{es,en}.png`, `o7-cima-es.png`, `o7-cima2-es.png`. El tono oscuro se lee como roca y no como hueco. Sin revisar de cerca: el patrón `ore` sobre una veta real (ninguna a la vista en esas tomas).
- `dotnet test`: todo en verde; `FarmContentTests` (tiempo por tick) falló una vez con la máquina cargada y pasa solo. Smoke con código 0.

### 2026-10-28: O7, Core de vetas y niebla subterránea

- Rama `ip4/o7-subsuelo`. Sólo Core, defs y tests; la presentación (render sin celdas ocultas, tono de niebla, inspector, textos de la interfaz, modelo del mineral, capturas) queda para el godot-engineer, el i18n-specialist y el art-integrator.
- Defs: `OreVein` en `worldgen.json` (`iron`: 6 a 9 vetas de 10 a 24 celdas en arenisca y caliza, una a la vista a 40 celdas del centro), `MaterialKind.Ore` y el material `iron_vein` (200 ticks, 100 % de mineral). El item `iron_ore` ya existía desde O2. Desvío de la spec: el def suma `minFit` (6) y `attempts` (200), dos números de balance que la spec no nombraba.
- Core: `TileFlags.Discovered` (bit 128; viaja en los bytes de banderas, así que el guardado y la huella ya lo llevan). `GameMap.IsDiscovered`; `SetTile` descubre la celda y sus 26 vecinas cuando la forma nueva no es muro (excavar, escaleras, canalizar y desmontar pasan todos por ahí), marca sucios los chunks de las celdas recién descubiertas y nunca oculta de nuevo (`SetTile` y `SetFlags` conservan el bit aunque quien llama reconstruya las banderas). `ComputeDiscovered` calcula la niebla inicial desde las formas. `OreVeins` (`WorldGen/OreVeins.cs`) siembra con el flujo `world.ore`: arranque aleatorio en muro de roca anfitriona a 2 o más niveles bajo la superficie, crecimiento por la frontera (a lo ancho: no más de un nivel arriba o abajo del arranque), y la veta a la vista (si ninguna veta tiene una celda descubierta a 40 celdas del centro `(Width/2, Height/2)`, se vuelve a sortear la primera que no la tiene desde la roca ya descubierta; si no hay, `InvalidOperationException`). El generador rechaza antes de sortear un `OreVein` cuyo material no sea mineral o cuyo anfitrión no sea roca natural.
- Formato 17 con migración: una partida de formato 16 o anterior calcula la niebla y recibe vetas con `world.ore` en roca anfitriona sin descubrir, sin veta a la vista (`OreVeins.Migrate`). Fixture `o7.olsave` (formato 16, semilla 7, 4 320 ticks, escaleras y una galería, escrito con el código de la rama antes de O7).
- Tests: `DiscoveryTests` (regla de las 27 celdas, chunks sucios, no se oculta de nuevo, excavar a ciegas, escaleras y canal, 200 ticks y un mineral, presupuesto por excavación), `OreVeinTests` (valle de prueba: determinismo, conteos, solo anfitriones, veta a la vista, error de generación, 70 % oculto, conectividad igual con y sin niebla, migración), `SubsoilTests` (valle real con las semillas 1, 7 y 42; un colono cava una veta a la vista; migración del fixture), continuidad con vetas a medio cavar y celdas ocultas y descubiertas (`SaveGameTests` en Core y en Content) y `o7.olsave` en `OldSaveTests`. 781 tests del Core, 237 de contenido, smoke con código 0.
- Huellas del smoke con `--ticks 5000`: `62E9519A3F9FEEE9` (semilla 1) y `D6155DFF8353CC4B` (semilla 42).
- Añadidos mínimos fuera del Core para que los tests de contenido den verde (los pueden pulir los otros agentes): `def.material.iron_vein.name/desc/gender` en es y en, y `iron_vein` con el patrón `speckled` en `terrain.materials` de `visuals.json`.
- Sin hacer: ver los pendientes (presentación de O7).

### 2026-10-25: O6, revisión

- Revisión de la otra sesión y `/code-review` de `main...HEAD` (sólo O6). Corregidos: siete textos en español con la codificación doble (`otoÃ±o`...; los de los cultivos y `ate_meal`), con un test nuevo (`EncodingTests`) que lo vigila en todos los catálogos; una carga que se pudre en las manos a medianoche dejaba el trabajo de acarreo en la etapa de llevar sin nada (reventaba al llegar): ahora el trabajo termina; los retoños de un bosquecillo se quitan con la zona (cancelar o perder el suelo); `RemoveCrops` con un conjunto en lugar de buscar en una lista; `ColonyStock.FoodNutrition` sin uso, quitado; la firma del inspector de zonas incluye la generación de la entidad. Tests nuevos: carga podrida con acarreo y bosquecillo cancelado.
- Sin cambiar: el índice de cultivos que se arma en cada tick con decisiones (`RefreshFarmIndex`, se mide en O11 con el presupuesto), `cropTilePlantEquivalent` en 0 (decisión de balance ya anotada, se retoma en O11), el smoke que enseña conocimientos por reflexión (sólo prueba) y que una celda de bosquecillo sembrada figure como vacía en `FarmRules.State` (el retoño ya cuenta como planta; el contador de la zona queda para el panel de O10).

### 2026-10-24: O6, presentación de la agricultura

- Rama `ip4/o5-conocimientos`. Herramienta Cultivo (tecla J y botón antes de Cancelar, `ui.orders.farm`): `GameInput.FarmCrop` (excluyente con las órdenes y con construir), barra de cultivos en el HUD (`FarmBar`, sólo los desbloqueados, los de cosecha antes que los bosquecillos; el botón se desactiva sin ningún cultivo y su tooltip dice qué conocimiento falta), vista previa de la caja con `FarmRules.CellsToZone` (`FarmPreview`, losas verdes, se recalcula sólo al cambiar la caja o el tick) y `PlaceFarmZoneCommand` al soltar. Esc y clic derecho salen de la herramienta.
- Dibujo: `OrderMarkers` marca las celdas de las zonas con un tinte dorado propio (rebuild con `FarmZonesChangedEvent`). El pasto decorativo ya no crece en las zonas (`WorldView` suma sus celdas a las de plantas y `MapRenderer.Redress` rehace sólo los chunks afectados al dibujar o quitar una zona). Los cultivos son entidades `Crop` dibujadas con tres etapas: `crop.<id>.sprout`, `crop.<id>.growing` y el visual plano al madurar (`EntityRenderer`; cambian con `EntityChangedEvent`). Modelos provisorios del Nature Kit en matas (varias copias como partes y flores como espigas); `VisualCatalogTests` exige las dos etapas.
- Inspector: en una celda de zona dice la zona y el cultivo, el cultivo sembrado (porcentaje o listo para cosechar) y un botón por cultivo conocido que manda `SetFarmZoneCropCommand` (`InspectorPanel.Farm.cs`); las pilas perecederas muestran `Frescura`. El aviso `event.food.spoiled` ya sale en el registro y la actividad "Sembrando/Cosechando" estaba resuelta.
- Smoke: `CheckFarm` comprueba que la herramienta no se prende sin cultivos, enseña agricultura y cereales (por reflexión, sin scriptorium), que la barra ofrece trigo, guisantes y repollo, que el clic elige el cultivo, que la vista previa coincide con el comando, que la zona hecha por comando se marca, se inspecciona y cambia de cultivo, que Cancelar la quita y que el aviso de comida podrida se lee. Opciones de captura `--farm` (herramienta prendida) y `--inspect-farm` (selecciona una celda con cultivo maduro).
- Capturas es y en revisadas, con la partida `docs/ip/playtest-o6/campo.olsave` (semilla 1, agricultura y cereales aprendidos, un campo de trigo con cultivos en distintas etapas y una pila de bayas al 35 % de frescura): `--saves <ruta absoluta de docs/ip/playtest-o6> --load campo --play --farm --level 13 --zoom 14 --point "42,54" --drag-to "46,58"` (dibujar un campo), `--play --level 13 --zoom 8 --point "38,56"` (etapas) y `--play --inspect-farm --level 13 --zoom 10` (inspector con cultivo y frescura). `--saves` con ruta relativa se resuelve dentro de `game/`.
- Sin hacer: modelos propios de los cultivos y de los alimentos (arte) y capturas de la actividad Sembrando y Cosechando con colonos trabajando.

### 2026-10-24: O6, Core de agricultura y deterioro

- Rama `ip4/o5-conocimientos` (sin cambiar de rama). Sólo Core, contenido y tests; falta la presentación (herramienta Cultivo J, vista previa de la caja con `FarmRules.CellsToZone`, modelos y etapas de los cultivos, inspector de zona/cultivo/frescura, aviso en el registro, capturas es/en).
- Defs: `Crop` (`crops.json`: trigo, guisantes, repollo y los bosquecillos de roble y pino; rendimiento `yieldBasePercent` 70 + `yieldPercentPerLevel` 3 en el def), `SpoilageConfig` (`spoilage.json`), `AiConfig.eatChoiceRadius` (15), `MoodConfig.mealThought` (`ate_meal`, +3, 8 h) y `birth.cropTilePlantEquivalent`. `farming` pasa a ser un tipo con los trabajos `sow` y `reap` (ya no es de taller). Visuales provisorios `crop.*` (modelos del Nature Kit).
- Core: componentes `FarmZone` y `Crop`, comandos `PlaceFarmZoneCommand` y `SetFarmZoneCropCommand`, `FarmZonesChangedEvent`, consultas `FarmRules` (`Zones`, `State`, `ZoneAt`, `CellsToZone`, `HasTimeToMature`, `Yield`). Trabajos `Sow` (reserva la celda) y `Reap` (la cosecha va antes que sembrar), flujo `farm.yield`. `FarmSystem` (medianoche: crecimiento por estación y poda de celdas que dejaron de ser suelo) y `SpoilageSystem` (medianoche: frescura que baja `10 000 × velocidad / (spoilDays × 100)` redondeada hacia arriba, mitad bajo techo o roca, también en las manos; desaparece al llegar a 0 y avisa con `event.food.spoiled` desde 5 unidades). Elección de qué comer (plato, menor frescura, más cercana; hambre crítica come lo más cercano), pensamiento por plato, reserva de concepción con `reserveValue` (`ColonyStock.FoodReserve`). Almacenes, planos, órdenes de escalera/canal y retoños naturales saltean las zonas; Cancelar quita celdas y cultivos.
- Formato 16 sin migración; fixture `o6.olsave` (formato 15, semilla 7, 4 320 ticks, escrito con el código de la rama antes de O6). Huellas del smoke con `--ticks 5000`: `9AF8B6F29DC3B10F` (semilla 1) y `EFDFF865F9D47D99` (semilla 42).
- Balance (criterio 12, `FarmBalanceTests`, semillas 1, 7 y 42, jugador con scriptorium, campo de 8 × 8, cocina y Mantener 20 panes): con los números de la spec la aldea se sobrepoblaba (la reserva de trigo a 800 y las celdas de cultivo permitían 17 bocas) y moría de hambre, porque una cocina sólo hornea para unas 2 personas y las bayas se agotaban. Ajustes en los defs: `reserveValue` del trigo y la harina 800 → 300, tandas de harina 90 → 45 ticks y de pan 150 → 75, y `cropTilePlantEquivalent` 1 → 0 (el mecanismo existe y está cubierto por un test con contenido propio; queda apagado en el juego hasta que haya más cocinas, O11). Resultado: nadie muere de hambre, nacen al menos tantos hijos como sin campo y la población pasa de 8 en las tres semillas.
- Tests: `FarmTests` (31, criterios 1 a 11), `FarmContentTests` (contenido real, continuidad con un campo a medio crecer y presupuesto de 300 celdas y 200 pilas: menos de 0,1 ms por tick), `FarmBalanceTests`, `OldSaveTests` con `o6.olsave`. Build sin advertencias y smoke con código 0.
- Falta para la presentación: herramienta Cultivo (J) con selector de cultivo (`ui.orders.farm`, `ui.farm.choose_crop`), dibujo de las zonas y de los tres estados del cultivo, inspector (`ui.inspector.crop`, `crop_ripe`, `farm_zone`, `freshness`), actividad `job.sow.report` y `job.reap.report` (ya resuelta en `TextResolver.Activity`), modelos propios, capturas es/en y criterio 14.

### 2026-10-23: O5, Core de conocimientos y scriptorium

- Rama `ip4/o5-conocimientos`, sobre `main` con O4 cerrado. Sólo el Core, el contenido y los tests; falta la presentación (panel K, atenuar lo bloqueado en la barra de construcción, ícono del HUD, capturas).
- Defs: `KnowledgeDef` con `branch`, `tier`, `cost`, `practice` y `order`; `KnowledgeConfigDef` (`knowledge.json`: práctica 60 %, tanda de 60 ticks, cola de 5 y el tipo de trabajo `research`). Los 12 conocimientos del árbol medieval (28 000 puntos en diez). Buildable `scriptorium` (6 tablones, 240 ticks, `beauty` 1, taller de `research`), con el modelo de la mesa pequeña de KayKit por ahora.
- Core: `ProgressState` suma `ResearchQueue` y `Points` (investigación y práctica por conocimiento, en centésimas); `ResearchRules` (estado, avance, desbloqueos, `AddResearch`, `AddPractice`), `SetResearchQueueCommand` (rechaza todo si hay uno bloqueado, aprendido, repetido, desconocido o de más), `KnowledgeLearnedEvent`, `ResearchChangedEvent` y el aviso `event.knowledge.learned`. Trabajo `Research`: `DecisionSystem.TryResearch` va al escritorio libre (uno por escritorio) y trabaja una tanda; cada tick suma tantas centésimas como la velocidad del colono, así una interrupción no pierde nada. La práctica se suma en el mismo lugar donde se gana la habilidad. `ColonyRules.WorkTypeOf` resuelve `Research` y `ColonyRules.PracticedBy`.
- Formato 15, sin migración (una partida de formato 14 empieza sin cola ni puntos). Fixture `o5.olsave` (formato 14, escrito con el código de `main` en un worktree: banco, una orden de Mantener y una de Repetir). La huella incluye la cola y los puntos.
- Tests: `ResearchTests` (9), `KnowledgeContentTests` (4), continuidad con cola y `o5.olsave`. 724 tests del Core, 203 de contenido, smoke con código 0. Huellas nuevas con `--ticks 5000`: `92D81D2C63BAB178` (semilla 1) y `9130C54E3B654499` (semilla 42): cambian porque la práctica ya suma puntos.
- Criterio 9 (época posterior bloquea) sigue cubierto por los tests de `UnlockService` de ADR-008.
- Presentación: `KnowledgePanel` (tecla K y botón "Conocimientos (K)"): una fila por rama y una columna por nivel, tarjetas con estado y avance, tooltip con descripción, costo, requisitos, qué desbloquea y cómo se practica; clic pone o saca de la cola; la cola con Subir, Bajar y Quitar; aviso sin scriptorium. El HUD muestra "Investigando: X (n %)" sobre las alertas con algo en cola, y la barra de construcción atenúa lo bloqueado con "Requiere: ..." y se rearma al aprender. Textos `ui.knowledge.*`. El smoke comprueba las 12 tarjetas y la cola. Opción `--knowledge` para capturas (es y en revisadas, también la barra de construcción con el banco de cantero atenuado).
- `/code-review` de `main...HEAD`: corregidos `[DefRef]` en `SetResearchQueueCommand`, `Current` que sólo da la cabeza de la cola si está disponible (una partida con uno bloqueado no lo investiga), el tipo de trabajo de investigación validado como de taller con mensaje claro, el comentario de `WorkTypeOf`, la tanda leída de `ColonyRules`, la rama desconocida que ya no rompe el panel, el smoke y los nombres de los conocimientos en minúscula en los catálogos (la interfaz los pone en mayúscula al empezar una línea, como los demás defs; el aviso "La aldea aprendió agricultura." queda bien). Sin cambiar: el comando se ignora entero si una entrada dejó de estar disponible (el panel manda la cola completa; es una carrera de un tick), `Desks` repetido en el panel (una consulta más en el Core cuando haya un segundo uso) y las ramas del panel fijas en la interfaz.
- Sin hacer: íconos pixel de rama, de estado y del HUD (por ahora texto; van a una pasada de arte), modelo propio del scriptorium (usa la mesa pequeña) y capturas con un scriptorium en uso (se ven jugando).

### 2026-10-23: O4, órdenes de producción

- Rama `ip4/o4-ordenes`, sobre `main` con O3 cerrado (corregido tras `/code-review`: ver la entrada de O3).
- Core: `WorkOrderMode` suma `Keep` y `Forever`; `WorkOrder` suma `Paused` y `Sequence` (el lugar en la lista, ambos escritos sólo si no son los de por defecto). Comandos `SetWorkOrderCommand` (modo, cantidad, pausa) y `MoveWorkOrderCommand` (posición); `CreateWorkOrderCommand` acepta los tres modos. `WorkOrderRules.InOrder` da la lista ordenada y `Wants` decide si una orden pide otra tanda (Hacer N cuenta lo hecho y lo en curso; Mantener N cuenta las existencias, `Stocks.Count`, y lo en curso). Estados nuevos `Paused` y `Satisfied`. `TryCraft` recorre las órdenes en el orden de la lista.
- Formato 14, sin migración. Fixture `o4.olsave` (formato 13, `taller.olsave` de la partida de O3: banco de tablones y una orden de aserrar 5). El test de continuidad suma una orden de Mantener y una de Repetir.
- Presentación: `ProductionPanel` (tecla O y botón "Producción (O)") con una fila por orden (Subir, Bajar, Modo, − y +, Pausar o Reanudar, Quitar) y el formulario de una orden nueva (receta, modo, número). Textos `ui.production.*` y dos estados nuevos en `ui.workshop.status.*`. El smoke crea una orden de Mantener 6 por comando, la ve en el panel y la quita. Opción `--production` para capturas (es y en revisadas con `--saves docs/ip/playtest-o3 --load taller --play --production --zoom 22`).
- Validación: 715 tests del Core, 196 de contenido, smoke con código 0 y las mismas huellas (`CFDA6D91EF34C721`, `BC74B881EBCCDB7F`).
- Sin hacer: orden con `minQuality` (no está en el contrato de O4) y elegir un taller concreto desde el panel (el comando ya lo admite). `/code-review` de `main...HEAD`: corregidos el inspector del taller (texto por modo y firma con modo, cantidad y pausa), `Status` (una orden pausada con un craft en curso sigue en marcha; Mantener cumplida dice "ya hay suficientes" antes que un bloqueo), el comentario de `CreateWorkOrderCommand` y el panel sin abrir. Sin cambiar: una orden Repetir al principio de la lista deja sin taller a las siguientes (es el orden que eligió el jugador; sube la otra), el costo de ordenar la lista por decisión (se mide en O11, criterio 16), el apilado de los paneles Colonia y Producción y el conteo de Mantener, que usa lo mismo que el HUD. O4 cerrado.

### 2026-10-23: O3, presentación de los talleres

- Rama `ip4/o3-talleres`. El inspector de un taller (`InspectorPanel.Workshop.cs`) lista sus recetas, con un botón "Hacer 5" que manda `CreateWorkOrderCommand` (una receta bloqueada se marca como tal), y las órdenes del tipo de taller con su estado calculado (`WorkOrderRules.Status`) y un botón "Quitar" (`RemoveWorkOrderCommand`). Las filas se rearman sólo cuando cambia su firma, así un botón conserva el ratón. Textos `ui.workshop.*` en es y en. Los modelos de los talleres ya estaban en `visuals.json`; la barra de construcción los toma de `BuildablesInOrder`.
- `--inspect-furniture` elige primero un taller.
- Validación: build sin advertencias, 949 tests en verde, smoke con código 0 (sin cambios en la huella).
- Partida preparada `docs/ip/playtest-o3/taller.olsave` (semilla 1, banco de carpintero de tablones ya construido y una orden de aserrar 5 tablones, que espera troncos). Capturas es/en con `--saves docs/ip/playtest-o3 --load taller --play --inspect-furniture --zoom 22`, revisadas: recetas, órdenes con su estado y "Quitar".
- `/code-review` de `main...HEAD`: 8 hallazgos. Corregidos: `ColonyRules.WorkTypeOf` resuelve el trabajo `Craft` por la receta (una crisis lo corta, la habilidad acelera el acarreo del ingrediente, la etapa de vida lo respeta); el inspector rearma las filas al cambiar de idioma; el estado "en marcha" sólo cuenta ingredientes dentro del radio de traída de algún taller; una orden de un taller concreto se quita al desmontarlo; las partidas de formato 12 o anterior reciben el conocimiento inicial del escenario al migrar (`ColonyStart.MigrateKnowledge`). Descartados: el costo de `PileInUse` y de `TryCraft` (escaneos por decisión, sin medir problema; se ve en O4/O11) y los ingredientes que coinciden con dos entradas (ninguna receta real se superpone; se valida en O6 si aparece).
- El usuario revisó las capturas y las dio por buenas. Los modelos propios de los bancos quedan para después (hoy usan la mesa pequeña de KayKit).

### 2026-10-22: O3, Core de talleres y recetas

- Rama `ip4/o3-talleres`, sobre O2. Sólo el Core, los defs y los tests; la presentación (construir talleres, panel de órdenes, modelos, inspector, capturas) queda para la parte de Godot.
- Defs: `Recipe` (`recipes.json`: `plank`, `handle`, `stone_block`, `flour`, `bread`, `pottage`, `pickled_cabbage`), `Buildable.workstation` y `beauty`, tres talleres (`carpenter_bench`, `mason_bench`, `kitchen`), `Scenario.startingKnowledge` (carpentry y cooking) y `knowledge.json` con seis conocimientos mínimos (`carpentry`, `cooking`, `stonecutting`, `joinery`, `milling`, `food_preservation`: sólo `era` y `requires`, sin costo ni forma de ganarlos hasta O5, así que `mason_bench`, `stone_block`, `flour`, `bread` y `pickled_cabbage` quedan bloqueados). `AiConfig.craftFetchRadius` (30). Cama, cuna, mesa, silla y cofre piden `planks`; la puerta ya aceptaba tablones.
- Core: componentes `Workshop` (con `Inputs`) y `WorkOrder`, trabajo `Craft` (`Job.Recipe` y `Job.Order`), `Crafting` y `Recipes`, `DecisionSystem.TryCraft` y `JobSystem.Craft`: elige la primera orden ejecutable, reserva el taller (un colono por taller) y la pila, trae pila por pila, trabaja desde una celda vecina, consume, tira la calidad con `craft.quality`, deja las salidas en la celda del taller y suma a la orden. Una crisis lo deja a medias y los ingredientes quedan en el taller; si nadie los usa en un día caen al suelo, y también al quitar la orden (sin otra del mismo taller) o desmontar el taller. `UnlockService` rige `PlaceBlueprintCommand` (edificio y material) y las recetas; `UnlockService.Missing` y `Simulation.Unlocks`.
- `WorkOrder` mínimo: sólo el modo `Count` (Hacer N), `CreateWorkOrderCommand`, `RemoveWorkOrderCommand`, `WorkOrdersChangedEvent` y `WorkOrderRules.Status` (`running`, `waiting_inputs`, `no_workshop`, `locked`, `no_worker`, `done`). O4 suma `keep`, `forever`, pausa, reordenar y `satisfied`/`paused` sin cambiar lo que hay.
- Muebles con `Furniture.Quality` (tirada de quien termina de construir, con la habilidad de construcción; los talleres no tienen) y cama con descanso por `comfortPercent`.
- Formato 13 (sin migración). Fixture nuevo `o3.olsave` (formato 12, escrito con el código de `main` antes de O3 en una copia del repositorio: semilla 7, 20 500 ticks, con una cama de madera). Un juego del formato 12 o anterior no recibe `startingKnowledge` al migrar (decisión pendiente: O5 migra la lista).
- Tests: `CraftTests` (16), `RecipeDefTests` (21), `WorkshopContentTests` (6), continuidad con taller en uso en `SaveGameTests` y `OldSaveTests` con `o3.olsave`.
- Huellas del smoke (`--ticks 5000`): `CFDA6D91EF34C721` (semilla 1) y `BC74B881EBCCDB7F` (semilla 42). Cambian porque el escenario trae conocimientos, los muebles tiran calidad y hay defs nuevos.

### 2026-10-21: L5, integración y cierre de IP3.5

- Rama `ip3.5/l5-integracion`. Experimento de diez años con libre albedrío y sin órdenes, semillas 1, 7 y 42: sin muertes por hambre, 0,04 ms por tick, pero con la meta de comida en 600 nadie nace (la reserva para concebir son unas 360 bayas por colono). Con 3000, un nacimiento por semilla y la aldea llega a 8; con 6000, la semilla 7 se extinguió (vacían los arbustos). Decisión del usuario: meta de comida por defecto **3000** (`free_will.json`). Test lento nuevo en `BalanceTests` (diez años solos, sin hambre y con un hijo).
- Validación: 816 tests, smoke con código 0 y huella `3E6090980A92B35A`, benchmark de 30 s con 304 FPS y 0,16 ms por tick.
- `/code-review` de `main...HEAD` (incluye L4b y L4c): corregido que un retoño no madura sobre una celda con orden (antes marcaba la celda como bloqueada bajo un plano de excavar) y el comentario de formatos de `SaveGame`. Descartados: retoño bajo zona, plano o mueble (zonas y planos ya esquivan toda celda con planta), estaciones fijas en código (el calendario es de cuatro estaciones por diseño), reconstruir la decoración de todos los chunks al cambiar de estación y recalcular el aspecto con `--season` (una vez por hora, costo medido dentro del ruido), y `TakenCells` (una vez por medianoche). Queda a la vista en el playtest: las flores y piedras de la decoración reciben el mismo tinte del pasto.
- Checklist `docs/ip/playtest-ip3.5.md`: el usuario lo jugó completo, todo en ✅ y sin hallazgos. Versión `0.3.5`, README al día y zips de Windows (con el smoke sobre el exportado) y macOS en `builds/`. IP3.5 cerrada; `main` adelantado. Release publicada: https://github.com/gtaulamet/OurLife/releases/tag/v0.3.5.

### 2026-10-21: L4c, estaciones visibles

- Rama `ip3.5/l4c-estaciones`, sobre `ip3.5/l4b-bosque`. Sólo presentación. Bloque `seasons` en `visuals.json` (`VisualCatalog`: `SeasonCatalog`, `SeasonDef`, `FoliageDef`) con los verdes del pasto, la nieve de la roca (`snowLine` 0,66 de la altura del valle), el tinte de hojas caducas y perennes (con dos tonos entre los que se reparte cada árbol), la nieve de las copas y las flores por estación; campo `foliage` en los modelos de robles, pinos, retoños y arbustos.
- `SeasonLook` (C# puro, con tests en `SeasonLookTests`) mezcla dos estaciones en los últimos 3 días de cada una. `SeasonCycle` la calcula una vez por hora del juego y la reparte: `MapRenderer.ApplySeason` (uniformes del shader del terreno y de la decoración nuevo `decoration.gdshader`; la decoración se rearma sólo al cambiar de estación, por las flores) y `ModelLibrary.ApplySeason` (shader `foliage.gdshader` compartido por todos los árboles; el tono de cada uno sale de su posición).
- `--season <spring|summer|autumn|winter>` fuerza la estación (capturas). Capturas de las cuatro con `--seed 1 --play --hour 12 --level 14 --zoom 22`: otoño amarillo y rojizo con pinos y robles, invierno blanco con nieve sobre las copas, primavera y verano verdes.
- Las huellas del smoke no cambian (`3E6090980A92B35A`, `5905A0BCAA4F5860`). FPS del benchmark de 30 s: base 270 a 273, con L4c 244 a 283 (ruido de la máquina; sin diferencia sostenida).
- Sin cambiar, para ver en el playtest de L5: los arbustos con bayas siguen mostrando las bayas en invierno (la simulación ya los tiene dormidos, pero el modelo no cambia); las flores cambian de golpe al entrar la estación, no en la transición.

### 2026-10-21: L4b, bosque que vuelve

- Rama `ip3.5/l4b-bosque`, desde `main`. `ForestSystem` (a medianoche): los retoños maduran si nada está en su celda, y cada árbol adulto de una especie con `regrowth` deja un retoño con 2 % por día en una celda libre a 3 o menos (suelo con cielo abierto, banda de la regla de vegetación, sin construcciones, muebles, planos, almacenes, órdenes, objetos, colonos ni plantas), hasta el tope: 35 ‰ de las celdas donde crece el roble y 40 ‰ el pino. Los robles no dejan retoños en invierno.
- Componente `Sapling`, formato 11. El retoño no bloquea el paso ni se ordena talar ni lo toma la iniciativa; las zonas de almacén no lo toman. Modelos `plant.oak.sapling` y `plant.pine.sapling` (el árbol en chico), inspector "Roble, retoño".
- 5 tests nuevos en `ForestTests` (tope, retoño que no bloquea hasta crecer, espera con algo encima, nada sobre celdas con órdenes, misma semilla). 800+ tests en verde.
- Las huellas del smoke cambian: `3E6090980A92B35A` y `5905A0BCAA4F5860`. Falta ajustar el balance del bosque en L5 con partidas largas, y capturas con retoños.

### 2026-10-20: L4, vida social con intención

- Rama `ip3.5/l4-social`. Con la opción prendida, un colono sin nada que hacer puede buscar compañía (`DecisionSystem.TrySeek`, después de jugar y antes de deambular): camina hacia la pareja, la familia o un amigo (peso doble), alguien con opinión de 20 o más o el objetivo de un deseo de amistad o de reconciliarse, a 30 celdas o menos y en su región, y se queda cerca un rato (trabajo nuevo `Visit`). Nunca a un rival, a alguien dormido o aislado, ni a quien vio en las últimas 2 horas. Ganas por sociabilidad y ánimo (`FreeWillRules.SeekChance`, flujo `colony.free_will`). Número nuevo en `FreeWillConfig`: `seekMinOpinion` (20).
- Al deambular, aleja de los rivales a 10 celdas o menos: elige el lugar más lejos de dónde están y, si caminan, de adónde van (si no, dos rivales que se esquivan se iban para el mismo lado).
- El inspector dice "Pasando un rato con alguien" / "Spending time with someone" (`job.visit.report`). Capturas es/en revisadas.
- Formato 10: el trabajo `Visit`. Partida real nueva `l3.olsave` (formato 9, 20 500 ticks, con dos colonos recolectando por su cuenta), escrita con el código de `main` antes de tocar nada.
- 13 tests nuevos en `SocialIntentTests` (criterios 7 y 8, a quién busca y a quién no, peso de la pareja, deseos, opción apagada con la misma huella, guardado). Las huellas del smoke siguen siendo las de `v0.3.0`.
- Experimento de tres años en Normal, sin órdenes, con la opción prendida (antes de las revisiones): con la búsqueda de compañía, 11, 14 y 16 parejas de amigos (de 21) en las semillas 1, 7 y 42; sin ella, ninguna. Pasan cerca de un cuarto del tiempo acompañando a alguien. 0,01 ms por tick.
- Revisión, primera ronda: la rivalidad se mira por la opinión de cualquiera de los dos, también con un familiar (una pelea puede agriarla; `Relationships.Kind` dice "familia" antes que "rival"); la visita termina si el otro se duerme, se aísla o, ya esperando, se fue lejos, y en camino lo sigue (`TryFollow`); un niño sólo busca a su familia; el peso de pareja, familia y amigos es el `closeWeightPercent` de las interacciones (sale `seekCloseWeightPercent`), con tope de 10 000 al validar; `SocialSystem.CanMeet`, `InReach` y `PickIndex` compartidos; `JobKinds.IsLeisure`. Sin cambiar: seguir a la misma persona si no hubo interacción (con 60 a 300 ticks a menos de 6 celdas, casi siempre la hay) y el 100 como unidad de los pesos, como en las interacciones. 18 tests en `SocialIntentTests`, 786 en total.
- Segunda ronda: la visita también termina si el visitante se aísla o se vuelven rivales (`DecisionSystem.StillWorthVisiting`, que usan la validez del trabajo y el seguimiento); seguir al otro vuelve a pedir que esté a 30 celdas o menos; nadie va a ver a quien ya viene hacia él; se apunta adonde va quien pasea (`AimOf`), así seguirlo no rehace la ruta en cada revisión; sólo se esquiva a los rivales del mismo nivel; con la opción prendida un niño no juega con un rival; `Relationships.IsRival` y `TryGoTo` compartidos. 21 tests en `SocialIntentTests`, 789 en total.
- Tercera ronda: ya esperando, el alcance se mide desde la celda del visitante; se apunta adonde va también quien camina a trabajar o acarrea; en cualquier crisis no se busca compañía y la visita termina; tampoco se va a ver a un niño que ya viene a jugar con uno; `Kind` no recalcula la rivalidad y `FindLiked` filtra primero por el peso; las reglas de la visita pasan a `FreeWillRules` (`StillWorthVisiting`, `AimOf`). Sin cambiar: la validez de la visita se revisa en cada tick, como la de jugar (0,01 ms por tick). 22 tests en `SocialIntentTests`, 790 en total.
- Cuarta ronda: "adónde va" un colono es el final de su camino, sea cual sea el trabajo (`FreeWillRules.AimOf`), y seguir y esperar miden desde el mismo lugar; no se busca a quien va a más de 30 celdas; si no puede seguirlo, la visita termina y pasea; una sola regla de quién puede buscar compañía (`FreeWillRules.CanSeekCompany`); coma que faltaba en `guardado.md`. Sin cambiar, como en las rondas anteriores: el 100 como unidad de los pesos y la validez revisada en cada tick. Experimento repetido: 14, 15 y 17 parejas de amigos en las semillas 1, 7 y 42.
- Quinta ronda: la evasión de rivales usa el final de su camino; no se busca a quien va a dormir y la visita termina si el otro se va a dormir; quien va de visita mira en cada tick si tiene que seguir al otro (antes sólo cada 30 ticks y podía llegar adonde no había nadie); la rivalidad se mira una vez por candidato; el aviso de inactividad usa `IsLeisure`; el 100 de los pesos es `SocialSystem.OtherWeightPercent`, la unidad de `closeWeightPercent`. Sin cambiar: la validez de la visita en cada tick (0,009 ms por tick) y no anotar como inalcanzable a quien no se llega (el destino es siempre una celda transitable de la misma región). Experimento: 11, 16 y 18 parejas de amigos; en la semilla 1, 6 rivalidades (más encuentros también traen más peleas). 791 tests.
- Sexta ronda: ya esperando, la visita sigue mientras el otro esté o vaya a estar cerca (antes se cortaba apenas el otro empezaba a caminar lejos, aunque siguiera al lado); el radio de las interacciones tiene que ser de 1 o más (con 0 nadie podía quedar cerca); el destino de un rival cuenta sólo en el mismo nivel; `TryFollow` mira primero si el destino del otro cambió; `SocialSystem.InReach` con radio, también para jugar. Sin cambiar: un `closeWeightPercent` de 0 deja fuera a los cercanos, como en las interacciones; leer las opiniones una sola vez por candidato (el costo medido es despreciable); y la forma de la regla del aviso de inactividad, que pidió la ronda anterior.
- Séptima ronda: quien llega de visita no empieza a esperar si el otro ya se va lejos (queda en camino y lo sigue en el tick siguiente); un niño no va a jugar con quien ya viene a acompañarlo; con la opción prendida, jugar termina si se vuelven rivales; una sola regla de a quién se puede visitar (`FreeWillRules.CanVisit`). Tests nuevos: la espera termina si el otro se va lejos, el visitante pasea si el otro se aleja más de 30 celdas, un niño no juega con un rival, el deseo de reconciliarse. Sin cambiar, por lo dicho en las rondas anteriores: el peso 0, el costo de las búsquedas y de la validez en cada tick, y el aviso de inactividad. 27 tests en `SocialIntentTests`, 796 en total.
- Octava ronda: una sola regla de "cerca" para seguir, empezar a esperar y seguir esperando (`FreeWillRules.WillBeNear`: el final del camino de cada uno, al alcance de las interacciones). Antes, al llegar se miraba también dónde estaba el otro, y un visitante podía quedarse esperando a alguien que ya se iba. Ya esperando, la visita termina apenas el otro sale para un lugar a más de 6 celdas. Sin cambiar, por lo dicho antes: el diseño del seguimiento en dos sistemas, el costo de las búsquedas, el 100 de los pesos, el aviso de inactividad y las validaciones de `SocialConfig` (no rechazan ningún contenido real). Experimento: 12, 16 y 17 parejas de amigos; 0,009 ms por tick.
- Novena ronda: un visitante sigue al otro a lo sumo tres veces (`Job.Count`; antes podía perseguir para siempre a alguien que no paraba de moverse); con un contenido sin `FreeWillConfig`, una visita cargada termina en lugar de quedar trabada; `FindLiked` no repite `CanInteract`; tests de las validaciones nuevas de `SocialConfig` y del tope de seguimientos. Sin cambiar: jugar sigue como en IP3 (apunta a donde está el compañero y no mira si va a dormir), porque con la opción apagada tiene que dar la misma huella; los radios del libre albedrío son círculos en el plano, como la aldea; y lo dicho en las rondas anteriores. 31 tests en `SocialIntentTests`, 799 en total.
- Décima ronda: después de abandonar a alguien que no para de moverse, el visitante lo deja tranquilo un rato (lo anota como un objetivo inalcanzable, `unreachableCooldownTicks`); antes podía volver a elegirlo enseguida. El tope de seguimientos pasa a los datos (`seekFollows`, 3); una sola regla de libre albedrío activo (`FreeWillRules.IsActive`: la opción y el contenido) también para jugar con un rival; quien va de visita sólo reserva y libera de nuevo si el destino del otro cambió. Sin cambiar: jugar como en IP3, `FindLiked` sin `CanMeet` (así no repite `CanInteract`) y el aviso de inactividad. 800 tests.
- Undécima ronda: si la visita termina mientras espera porque el otro se fue sin que se encontraran, también lo deja tranquilo un rato (`FreeWillRules.LeaveAlone`); jugar respeta esa marca (con la opción apagada nadie anota colonos, así que la huella no cambia); la evasión de rivales usa `IsActive`. Sin cambiar: jugar como en IP3, reservar de nuevo en cada tick mientras el destino del otro difiere (barato) y lo ya discutido. Experimento: 14, 14 y 18 parejas de amigos; 0,009 ms por tick.
- Duodécima ronda, sin defectos de conducta: `SocialSystem.MetLately` y `PairCooldownTicks` en lugar de cuatro copias; `StillValid` vuelve a ser sólo una comprobación (dejar tranquilo al otro pasa a `JobSystem.GiveUpIfLeft`, al soltar el trabajo); el comentario de los formatos en `SaveGame.cs` llega hasta el 10. Sin cambiar lo ya discutido (costo por tick, jugar como en IP3, aviso de inactividad). L4 cerrado y `main` adelantado. Nota práctica: restaurar un archivo con `cp` en el disco exFAT puede no disparar la recompilación; para una prueba de mutación, compilar con `--no-incremental`.

### 2026-10-20: meta de comida por defecto en 600

- Decisión del usuario, con el experimento de L3: la meta de comida por defecto pasa de 150 a 600 (`free_will.json`). Con 600, sin órdenes del jugador y con la opción prendida, la aldea pasa cinco años sin muertes por hambre en las semillas 1, 7 y 42.

### 2026-10-20: L3, trabajo por iniciativa

- Rama `ip3.5/l3-iniciativa`. `InitiativeTaskDef` (`gather_food`: recolección, cosechar, comida; `chop_wood`: tala, talar, madera) en `free_will.json`. `Job.Initiative`: el trabajo vale sin la orden del jugador. `DecisionSystem.TryInitiative` va después del trabajo ordenado y de las necesidades bajas, y antes de jugar y de deambular; el trabajo por iniciativa lo interrumpe una orden o una necesidad. Etapas, prioridades ("nunca" no), crisis, cerca de la aldea, ganas por diligencia y ánimo (flujo `colony.free_will`).
- Reparto: lo que traerá el trabajo por iniciativa en curso cuenta como existencias, así no van todos a la misma meta.
- El inspector y el panel Colonia dicen "Recolectando por su cuenta" y "Talando un árbol por su cuenta".
- Formato 9: `Job.Initiative` sólo se escribe cuando es verdadero, así las huellas del smoke siguen siendo las de `v0.3.0`. Partida real nueva `l2.olsave` (formato 8), escrita con el código de `main` antes de tocar nada.
- 18 tests nuevos en `InitiativeTests` (criterios 1 a 6, etapas, crisis, trabajo en curso, prioridad contra falta, reparto, guardado).
- Experimento de balance, cinco años en Normal sin órdenes del jugador y con la opción prendida: con la meta de comida de 150 la aldea se extingue en el primer invierno en las semillas 1 y 7 y sobrevive en la 42; con 600 sobrevive en las tres, sin muertes por hambre. Nadie nace en ningún caso, porque concebir pide comida guardada para 30 días por boca. 0,02 ms por tick con la opción prendida.
- Revisión, primera ronda: lo que viene en camino se cuenta en cada decisión, no una vez por tick (un trabajo cambiado en el mismo tick quedaba contado); apagar la opción termina el trabajo por iniciativa en curso; `InitiativeTask` ya no lleva `workType`, que sale del trabajo; `FreeWillRules.TargetOf`, `PlantDef.OutputOf` y la prioridad por etapa compartida con `TryWork`; las plantas se filtran primero por la cercanía a la aldea.
- Segunda ronda: el trabajo por iniciativa sólo cede a otro trabajo o a una necesidad baja mientras va hacia la planta; ya trabajando, termina (antes perdía lo hecho en cada revisión, y una orden sobre el mismo árbol lo hacía empezar de cero). La tirada va antes de contar (`Pcg32.Chance`), el tipo de trabajo de cada tarea se resuelve una vez y la cercanía se calcula con el centro y el radio leídos una vez.
- Tercera ronda, sin defectos de lógica: una tarea cuya categoría no tiene meta en `FreeWillConfig` se rechaza al cargar; un solo recorrido de las plantas por decisión; `FreeWillRules.IsNear` compartido; el test de `l2.olsave` toma la meta de comida del contenido (no se rompe si cambia la de por defecto).
- Cuarta ronda, sin defectos de lógica: la probabilidad va en puntos básicos (`FreeWillRules.InitiativeChance`), sin perder las fracciones; sin tareas posibles no se recorren las plantas. L3 cerrado y `main` adelantado.

### 2026-10-20: metas escritas por el jugador

- Pedido del usuario: la meta de comida y de madera es un campo que completa el jugador, con máximo 100 000. El panel Colonia muestra "Comida: 42 de [150]" con un campo de texto (Enter, Tab o un clic afuera aplica; Esc cancela). `targetMax` pasa a 100 000; salen `FreeWillRules.NextTarget`, la regla de múltiplos del paso y `targetStep`.
- Mientras un campo de texto tiene el foco, `GameInput` y `CameraRig` no leen las teclas del juego (`TextFocus.IsTyping`): escribir "150" ya no cambia la velocidad. Un clic en el mapa suelta el campo.
- El smoke escribe 123, el máximo más uno (queda en el máximo) y el valor original. Capturas es/en revisadas.
- Revisión, primera ronda: un clic en las flechas suelta el campo (si no, las teclas del juego quedaban escribiendo en él); Esc mientras se escribe vuelve a la meta anterior y no abre la pausa; cualquier clic fuera del campo lo suelta y aplica lo escrito. El smoke prueba escribir con Enter y cancelar con Esc. Queda sin cambiar: las teclas del juego se leen por sondeo en `_Process` con la guarda `TextFocus` en lugar de pasar a eventos; es un cambio más grande que no hace falta para un solo campo.
- Segunda ronda: el campo sólo toma dígitos ("10.000" daba 10, porque Godot lo lee como expresión); la rueda del mouse ya no suelta el campo; un clic en las flechas con un número escrito parte de ese número; las teclas del cuadro en que el campo se suelta tampoco actúan en el juego (`TextFocus.Released`); soltar el campo con un clic afuera pasa a `TextFocus.ReleaseOnClickOutside`, que llama `GameInput._Input`, para cualquier campo del juego. Descartado: partir "{category}: {count} de" para que otro idioma ponga la meta antes; queda anotado en la spec.
- Tercera ronda: la ayuda del campo sale traducida (`TooltipAutoTranslateMode`); el clic derecho en las flechas no hace nada (antes saltaba a 0 o al máximo); la rueda con un número escrito parte de ese número; sin `MaxLength`, que cortaba "100.000" antes de quitar el punto; `TextFocus.IsTyping` también vale el cuadro siguiente a uno con un campo escrito, para cualquier campo, sin conectar señales; `UiTheme.FieldBox` y `UiTheme.TextWidth` compartidos con el HUD.
- Cuarta ronda: casi todos los hallazgos venían del `SpinBox` (flechas que no soltaban el campo en los extremos, la rueda, Arriba y Abajo que pasaban a la cámara). Se cambió por un `LineEdit` sin flechas, que es lo que pidió el usuario: el panel aplica lo escrito al soltar el campo, Tab también lo suelta (antes podía pasar el foco a un botón del inspector) y no hay menú contextual, que traía textos del motor en inglés. Sale `targetStep` de `FreeWillConfig`.
- Quinta ronda: ceros a la izquierda no inflan el número; limpiezas menores (comentario suelto, caso de `SpinBox` muerto en `TextFocus`).
- Sexta ronda: al aplicar se vuelven a filtrar los dígitos, porque el campo los limpia de forma diferida y una tecla con Enter en el mismo cuadro llegaba con el punto ("1.000" daba el máximo). El smoke lo prueba así.
- Séptima ronda: un texto sin dígitos (".", "abc") deja la meta como estaba, igual que el campo vacío; antes daba 0. El resto del rango quedó revisado sin hallazgos. `main` adelantado.

### 2026-10-19: L2, presentación del interruptor y las metas

- Opción "Libre albedrío" en Opciones, al lado de "Mostrar FPS", prendida por defecto y guardada en `settings.cfg` (`free_will`). La pantalla de la partida manda `SetFreeWillCommand` al crearse, para una partida nueva o cargada, y cada vez que cambia la opción.
- Panel Colonia: "Metas de existencias", con una línea por meta ("Comida: 42 de 150") y botones de menos y más de a 10; los clics rápidos se suman como en las prioridades.
- Smoke: la partida sigue a la opción y las metas a los botones del panel. Capturas es/en de Opciones a 720p y del panel Colonia. 728 tests en verde.
- `guardado.md` y ADR-013 con el formato 8 y la partida de `v0.3.0`.
- Revisión de código, primera ronda: metas de sólo lectura fuera del Core (`StockTargetList` interna), la huella sin libre albedrío cuando está apagado y sin metas (vuelven las huellas de `v0.3.0`), metas guardadas fuera del máximo que ceden a él, validación más estricta de `FreeWillConfig`, centro migrado en el colono más cerca de la mediana, filas de metas armadas una sola vez y `TextResolver.CategoryName` compartido con el HUD. Descartados: contar existencias cada 0,25 s con el panel abierto (ya lo hacía con las actividades, cuesta poco) y la fila de interruptores a 720p (la captura muestra que entra).
- Segunda ronda: una orden de meta sin categoría (de un guardado dañado) se ignora; las metas van en un solo renglón con la ayuda en un tooltip, para que el panel crezca poco en 720p; el smoke compara el interruptor, el centro y las metas al cargar, ya que la huella no los incluye con la opción apagada; la prueba de los botones del smoke ya no depende de los valores por defecto; las lecturas de lo pendiente en el panel no tienen efectos. Descartados, con el motivo en la spec: el nivel del centro migrado (sólo se usa en el plano) y una categoría borrada que da "falta contenido" (ADR-013). La huella condicional se mantiene: es lo que permite comprobar el criterio 3 con las huellas de `v0.3.0`.
- Tercera ronda: los botones de las metas saltan a los múltiplos de 10 (desde 999, − da 990); la ayuda también en los botones; el comando del interruptor se manda cuando ya escuchan la vista y el HUD; las metas guardadas que el contenido ya no admite se normalizan al cargar; la lista de metas pública es una vista de sólo lectura que no se puede convertir en `List`; la spec dice cuándo entra el libre albedrío en la huella. Descartado otra vez: la fila de Opciones a 720p, donde la única escala posible es 100 % y entra.
- Cuarta ronda: las metas por defecto tienen que ser múltiplos del paso (si no, no se podría volver a elegirlas con los botones); `StepTarget` ignora una categoría sin meta, como el comando; las líneas de las metas tienen un ancho mínimo para que los botones no se muevan cuando crece el número. La spec aclara que la opción también viene prendida al actualizar desde `v0.3.0`.
- Quinta ronda: la regla de los botones pasa al Core (`FreeWillRules.NextTarget`, con tests de 999 a 990 y de los extremos); el panel refresca por separado las filas y las metas, y mide el ancho de las metas sólo al reconstruirse (también al cambiar de idioma); el smoke compara el juego completo (`SameGame`: huella, interruptor, centro y metas) en las tres vueltas de guardado, y los tests de migración comparan el centro.
- Sexta ronda: el smoke dice qué parte del juego difiere (`DifferenceOf`) y calcula lo esperado de los botones con `NextTarget`; un clic en una meta cambia sólo su línea, sin volver a contar las existencias; los interruptores de Opciones comparten una lista y un solo refresco.
- Séptima ronda: el contenido rechaza más de un `FreeWillConfig` al cargarse; la pantalla aplica el interruptor al crearse, antes del primer tick; el ancho de las metas sale del máximo de los datos y crece si las existencias pasan de cuatro cifras.
- Octava ronda sin hallazgos: L2 cerrado y `main` adelantado.

### 2026-10-19: L2, Core del interruptor y las metas

- Rama `ip3.5/l2-metas`. `FreeWillConfig` (`game/content/defs/free_will.json`, radio 30, metas 150 de comida y 40 de madera), `Simulation.FreeWill`, `VillageCenter` y `StockTargets` en la huella y en el guardado, que pasa al formato 8.
- `SetFreeWillCommand` y `SetStockTargetCommand` (entre 0 y 999; una categoría sin meta se ignora). Consultas `FreeWillRules.Targets` e `IsNearVillage` (círculo en el plano, a cualquier nivel).
- El centro es la llegada. Una partida de formato 7 lo recibe al cargarse, en la celda del primer colono. Nueva partida real de `v0.3.0` en las pruebas de partidas viejas (`v0.3.0.olsave`, siete colonos), armada desde el tag.

### 2026-10-19: L1, spec de IP3.5

- Rama `ip3.5/l1-spec`. Spec en `docs/gdd/libre-albedrio.md`: interruptor por comando (`SetFreeWillCommand`), centro de la aldea en la llegada, metas de comida y madera en el panel Colonia (`SetStockTargetCommand`), iniciativa de recolectar y talar cerca de la aldea dentro de las prioridades (`Job.Initiative`), buscar compañía y evitar rivales, ganas por diligencia, sociabilidad y ánimo (`FreeWillConfig`), bosque que vuelve con retoños y tope por especie (`Sapling`, `regrowth` en `PlantDef`), formato 8, trece criterios de aceptación y siete decisiones para el usuario.
- El acarreo ya es por iniciativa desde IP2: la spec lo dice y no lo cambia.
- Aprobada por el usuario con dos cambios: "cerca de la aldea" son 30 celdas alrededor de la llegada (no 20), y en otoño los pinos también se vuelven amarillos y rojizos, con algo de nieve en invierno en todos los árboles; el cambio de estación tiene que notarse en el paisaje.

### 2026-10-19: cierre de IP3 y `v0.3.0`

- El usuario repitió los tres puntos del playtest con los zips del 19 de octubre: todo en ✅. Resultado completado en `docs/ip/playtest-ip3.md`.
- H3.10 e IP3 cerradas en `IP3.md`. Tag `v0.3.0` y release de GitHub con los zips de Windows y macOS. `main` adelantado.
- Pedido nuevo del usuario: agua en el mapa. Decisión: queda en IP6, donde ya estaba; las posibilidades (río y lagos, puentes y vados, hielo en invierno, pesca, pozos, riego y sed) y la estimación quedan anotadas en IP6 del plan maestro para evaluarlas ahí.

### 2026-10-19: siete fundadores y primera pasada del playtest

- Playtest del usuario con los zips anteriores: todo en ✅. Hallazgos que no bloquean: avisos de hambre crítica frecuentes (ya en el backlog) y excavación y escaleras poco legibles (nuevo en el backlog).
- Pedido del usuario: con 5 fundadores y 2 parejas la aldea se extingue. Decisión, sólo para sostener IP3 hasta la agricultura: 7 colonos con 3 parejas (`foundingCouple.count` 3) y 42 provisiones. Para alimentarlos, el valle pasa de unos 55 a unos 100 arbustos (probabilidad 260).
- Con 14 bayas la población se sostenía mejor, pero una aldea sin órdenes pasaba su primer invierno; con 10 bayas y 7 por boca hubo hambrunas. El usuario eligió que el invierno siga matando: 10 bayas por arbusto y 9 por boca. Quince años en seis partidas: nadie muere de hambre; Normal crece de 7 a 10, Rápido termina entre 4 y 8, Muy rápido se extingue o casi.
- `diez-anos.olsave` regenerada (10 colonos al décimo año); benchmark con ella: 280 FPS y 0,11 ms por tick. Zips de `v0.3.0` armados de nuevo. 705 tests; huellas `2E368CE2376689E4` y `1284D40F4588F8FE`.
- En `playtest-ip3.md`, tres puntos quedan "(repetir)" y se corrigieron las huellas que el editor había vuelto atrás.

### 2026-10-19: H3.10, más nacimientos

- Pedido del usuario: la tasa de nacimientos le parecía baja. Cambios: la barra de hambre para concebir baja al 20 % (`hungryBelowPercent` en el bloque `birth`; sin él, el umbral bajo como antes) y el arbusto de bayas da 14 bayas en lugar de 10.
- Se pidieron 15 bayas, pero con 15 una aldea que nunca recolecta pasaba su primer invierno en la semilla 42 (falla el test de IP1); el usuario eligió 14, que mantiene esa amenaza.
- Tope de la tierra: el usuario eligió 6 por boca, pero con 6 la semilla 7 llegó a 10 y murió de hambre en el séptimo invierno; queda en 7 hasta que lo confirme (Pendientes).
- Con 14 bayas, 20 % y 7 por boca, seis partidas de diez años (Normal, Rápido y Muy rápido): nadie muere de hambre, nacen de 2 a 4 hijos y antes, y la aldea llega a 7 u 8.
- Dos tests nuevos de la barra de hambre: 705. Huellas `8992837663AF7AA9` y `C45F2AF564D36705`. `diez-anos.olsave` y los zips de `v0.3.0`, de nuevo.
- Las marcas del playtest del usuario en `playtest-ip3.md` (llegada y ficha en ✅) van en este commit.

### 2026-10-18: H3.10, tope de la tierra en 7

- Decisión del usuario: `foodPlantsPerColonist` baja de 9 a 7 por boca, así el valle sostiene unas 7 personas.
- Quince años en Rápido, semillas 1, 7 y 42: nadie muere de hambre. En la semilla 1 nacen 3 hijos en diez años y 6 en quince; en la 42, 2 (la comida guardada llega a 0 en los inviernos de los años 8 a 10). En la 7 no nace nadie: recolecta poco, la reserva se junta hacia el año 9 y para entonces las fundadoras ya no son fértiles. Queda anotado en la spec como límite conocido.
- `diez-anos.olsave` regenerada con la regla nueva (7 colonos). Para regenerarla: un programa de consola temporal que repite `BalanceTests` y guarda con `SaveGame.Write(..., new SaveInfo("largo", false, "0.3.0-dev", DateTimeOffset.UnixEpoch))`. Benchmark con ella (`--saves docs/ip/playtest-ip3 --load diez-anos --benchmark 30`): 382 FPS y 0,08 ms por tick.
- Zips de `v0.3.0` armados de nuevo en `builds/` (73,3 y 122,5 MB), con el smoke sobre el ejecutable exportado.
- 703 tests en verde.

### 2026-10-18: H3.10, revisión del balance

- Revisión de código de `main...ip3/h3-10-integracion`: 10 hallazgos, todos corregidos.
  - Los dos topes para concebir no contaban los embarazos en curso: con dos parejas, las dos mujeres podían concebir antes del primer parto y pasar el tope de la tierra. Ahora cuentan como bocas los colonos, los hijos en camino y el que se concebiría, en la reserva y en la tierra.
  - El apetito para la reserva es la necesidad de comer que más rápido baja, no la suma, como en `Hunger.UnitsToFeed`. Una madre sin `Needs` ya no rompe el tick.
  - La comida y los arbustos se cuentan una vez por medianoche. `ColonyStock` recorre las pilas y las cargas en un solo lugar.
  - `foundingCouple.count` se valida sin desbordarse. En `ColonyStart`, el número de parejas sale del bucle.
  - `BalanceTests` lleva `[Trait("Category", "Slow")]`, así que se puede filtrar.
  - El checklist del playtest explica el tope de la tierra.
- Efecto: en las semillas 1 y 42 nace un solo hijo en diez años y la aldea queda en 6. Queda para que decida el usuario (Pendientes).
- 703 tests. Smoke con código 0 y las mismas huellas: en 5000 ticks nadie concibe.

### 2026-10-18: H3.10, paquetes y checklist

- Versión `0.3.0` en `game/project.godot` y README al día (cómo se juega con la vida de los colonos y la ficha con F).
- Paquetes: `OurLife-v0.3.0-windows-x86_64.zip` (73,3 MB, con el smoke sobre el ejecutable exportado) y `OurLife-v0.3.0-macos-universal.zip` (122,5 MB), en `builds/`.
- Checklist `docs/ip/playtest-ip3.md` con la pasada automática, y dos partidas preparadas en `docs/ip/playtest-ip3/`: las cinco etapas en fila y una aldea de diez años.

### 2026-10-18: H3.10, balance de población y comida

- Partidas de diez años del juego (semillas 1 y 42, Rápido y Muy rápido, con un jugador que repite la orden de recolectar cada dos días en 25 celdas): sin cambios, dos de cuatro aldeas morían enteras de hambre en un invierno, en la semilla 1 no había amistades en años (2 o 3 interacciones por día) y con una sola pareja los hijos son hermanos.
- Decisiones del usuario: reserva de comida de 30 días por colono para concebir, radio de interacción 6 y 30 % de iniciar, y dos parejas fundadoras (`foundingCouple.count`).
- La reserva sola no alcanzó (con dos parejas una aldea llegó a 12 y murió). Se suma un tope por lo que da la tierra: `foodPlantsPerColonist` 9, contando al que va a nacer. Con eso ninguna aldea murió de hambre en diez años; en Rápido se mantiene en 6 o 7. En Muy rápido tiende a extinguirse: anotado en el backlog para IP4 e IP6, igual que los avisos de hambre crítica frecuentes.
- Tests: reserva, tierra, dos parejas, validaciones y `BalanceTests` (diez años en Rápido, unos 10 s). 702 tests. Huellas nuevas: `5C715E8411C7DFC8` y `40A56EC5ADD2BA9E`.
- Rendimiento de una partida de diez años: 0,017 ms por tick en la simulación; en Godot, 331 FPS y 0,10 ms por tick.

### 2026-10-18: cierre de H3.8

- Cuarta ronda de revisión: 7 hallazgos, todos corregidos. Los íconos se importan sin compresión para 3D (`detect_3d/compress_to=0`), así no se borronean ni cambian sus `.import` al usarlos un `Sprite3D`. La altura con huesos agrandados se mide sólo en esos modelos. Comentarios y documentación al día (jugar con `emote-yes`, tres campos nuevos en el catálogo), la elección de animación más simple y el test de las camas sin `!`.
- 696 tests; smoke con código 0. H3.8 hecho en `IP3.md`; `main` adelantado.

### 2026-10-18: H3.8, tercera ronda de revisión

- 8 hallazgos, todos corregidos:
  - `jump` también es una pose fija: los niños juegan con `emote-yes`, que sí se mueve.
  - La altura de un modelo con la cabeza agrandada se mide escalando sólo lo que está sobre el hueso (`ModelLibrary.HeightOf`), no el modelo entero.
  - El cuadro de clic de quien duerme crece a lo largo del cuerpo.
  - Si falta la animación quieta propia, se usa la original.
  - La altura del colchón es un campo del catálogo (`restHeight`: 0,22 en la cama, 0,13 en la cuna), con un test para cada cama.
  - `WorldView` pasa sus reglas de la colonia al renderer, que no las arma fuera de su `try`.
  - Comentarios ordenados y el factor de altura calculado una vez.
- 696 tests; smoke con código 0.

### 2026-10-18: H3.8, segunda ronda de revisión

- 8 hallazgos, todos corregidos:
  - `crouch` es una pose fija del kit, así que el bebé se deslizaba agachado: ahora camina despacio y quieto se sienta. El kit no trae una animación de gatear.
  - El bebé flotaba sobre la cuna: la altura del colchón sigue la escala del modelo de la cama.
  - El modelo por etapa se guarda por era, así toma la variante de otra época.
  - Un globo se va si su colono se acuesta y se oscurece con la profundidad, como el colono; sólo se le pide a Godot mientras se desvanece.
  - El paso de la animación usa `MovementSystem.ProgressPerTick` y se calcula sólo cuando el colono cambia.
- 694 tests, smoke con código 0, benchmark de 20 s con 361 FPS y 0,08 ms por tick.

### 2026-10-18: H3.8, primera ronda de revisión

- 10 hallazgos, todos corregidos:
  - Las animaciones en bucle se marcaban por biblioteca compartida con las del primer modelo que la usó: un bebé dejaba a los adultos de su archivo sin caminar en bucle, o al revés. Ahora se repiten todas las que pide el renderer y todas las que nombra el catálogo.
  - Un bebé dormido se dibujaba con la pose de sentado acostada: dormido usa la animación quieta original.
  - La altura medida no contaba la cabeza agrandada: el globo y el clic usan el factor del hueso más grande.
  - Un globo ya no desaparece si el colono cambia de etapa mientras se ve.
  - Caminar va a la velocidad real del paso (`MovementSystem.ProgressOf`, con la habilidad al acarrear).
  - Los modelos por etapa se buscan una vez y se guardan; los íconos los carga y libera `ModelLibrary`.
  - Tests: cada animación que el catálogo pone en lugar de otra existe en todos los archivos del modelo; el adulto sin modelo propio se comprueba para cada criatura.
  - `tools/make-elder-models.py` exige UV y posiciones en float y actualiza los mínimos y máximos de los UV que mueve.
- 694 tests; smoke con código 0.

### 2026-10-18: H3.8, arte de las etapas

- Sin campo nuevo en el Core: un colono se dibuja con `<visual>.<etapa>` del catálogo si existe (`creature.human.baby`, `.child`, `.adolescent`, `.elder`); el adulto usa el de la criatura.
- Catálogo: `boneScales` (huesos más grandes, con el reposo escalado para que la animación no lo deshaga) y `animations` (otra animación del modelo). Bebé a escala 0,55 con la cabeza ×1,35, que se sienta y gatea agachado; niño 0,85 con la cabeza ×1,2; adolescente 1,15; adulto 1,3.
- Canas: pelo, ojos y ropa comparten franjas de la textura, así que una textura recoloreada no servía. `tools/make-elder-models.py` genera 12 `*-elder.glb` que llevan los vértices del pelo de la cabeza a la franja gris; los ojos se reconocen por su lugar, igual en todas las cabezas. La primera versión los separaba por UV y dejaba la coronilla oscura; se vio en las capturas.
- Animaciones: alimentar usa la de manipular, jugar la de saltar, y caminar va al ritmo de la etapa.
- Globos de interacción: siete íconos pixel de 16 × 16 (`tools/make-interaction-icons.py`), sobre los dos colonos durante 2,5 s, que se desvanecen al final.
- La cuna sigue siendo la cama de KayKit en chico: se lee como cuna en la vista de la aldea.
- Capturas con partidas generadas por el Core (cinco etapas en fila, filas de una etapa con cinco variantes) y `--load`, sin tocar el estado desde la presentación. Una "mancha" color piel resultó ser una niña pelirroja vista desde arriba.
- Tests: modelo propio para cada etapa menos la adulta, ícono para cada interacción, lectura de los campos nuevos y rechazo de los inválidos. 693 tests; smoke con 18 modelos; benchmark de 30 s con 377 FPS y 0,07 ms por tick.

### 2026-10-18: cierre de H3.7

- Cuarta ronda de revisión: 8 hallazgos, 7 corregidos. Si falla el camino a una pila, quien alimenta prueba otra en la misma decisión. Un padre que pasea cuenta como el que va a alimentar sólo si decide de nuevo en ese tick, así no frena a los demás. El bebé con la madre a más de 12 celdas gatea donde está. `PaceOf` usa `PaceIn`, la ficha usa `IsLivingColonist`, sin copia de `Relative` y `IsFertile` más simple. Descartado: `Beds.Fits` ordena las etapas sólo para un colono sin `Life`, que no hay en partidas reales, y la regla ya es `LifeRules.AdultOf`.
- 1 test nuevo, 688 en total. Huellas sin cambios.
- H3.7 hecho en `IP3.md`; `main` adelantado.

### 2026-10-18: H3.7, tercera ronda de revisión

- 10 hallazgos, todos corregidos:
  - Quien alimenta guarda en el trabajo cuántas unidades reservó (`Job.Count`), de las que nadie más reservó, y no levanta más. Así las reservas son exactas.
  - Alimentar va a la velocidad del trabajo (habilidad, rasgos, ánimo y embarazo), y la experiencia de crianza se cuenta una vez por tick, no dos.
  - Un padre cuenta como el que va a alimentar sólo si ningún otro trabajo suyo tiene más prioridad que la crianza.
  - El bebé deja de gatear en cuanto alguien toma su comida.
  - El niño elige compañeros con las reglas de `SocialSystem` (`CanInteract` y `Met`): nadie en crisis de aislamiento.
  - Una pareja nueva empieza con hijos permitidos.
  - El ritmo de la vida se consulta en un solo lugar (`LifeRules.PaceIn`).
  - El botón de la ficha cambia a partir de lo que muestra: dos clics seguidos con el tiempo corriendo vuelven al valor original.
- 4 tests nuevos, 687 en total. Las dos huellas cambian porque cada trabajo guarda `Count`: `D8FD3784066AEE12` (semilla 1) y `14212A5A5466DDB7` (semilla 42).

### 2026-10-18: H3.7, segunda ronda de revisión

- 10 hallazgos, todos corregidos:
  - Comida reservada por otro por un momento ya no hace ignorar al bebé; la comida se busca una vez por decisión.
  - Un bebé en crisis de aislamiento sigue gateando cerca de su madre.
  - Quien alimenta reserva sólo las unidades que le faltan al bebé, no la pila entera: los demás comen del resto.
  - El deseo de un hijo espera un año de vida (`expireYears` en el def `Desire`), hasta la próxima ocasión, en lugar de 10 días que casi siempre lo frustraban. El balance va a H3.10.
  - Código compartido: `LifeRules.AdultOf`, `LifeRules.ConceptionChance` (con la misma conversión por día que la vejez), `Relationships.IsLivingColonist`, `Relationships.FamilyOf` y `ColonyStart.PickName` para los recién nacidos.
- 3 tests nuevos, 683 en total. Huella de la semilla 1: `049E2C1B43ACAEBB`; la 42 no cambia.

### 2026-10-18: H3.7, primera ronda de revisión

- 10 hallazgos, todos corregidos:
  - El deseo de un hijo nace sólo si la pareja podría concebir: nadie espera un hijo y no hay un bebé de menos de un año (`LifeRules.CouldConceive`). Guarda a la pareja como objetivo y se descarta sin frustrarse si ella muere o se separan.
  - El botón de la ficha muestra la opción de la pareja: permitidos sólo si los dos los permiten.
  - El inspector y el smoke dan por libre la cuna de quien ya creció (`Beds.IsTaken`).
  - Quien alimenta a un bebé reserva la pila entera, como el acarreo, porque puede llevarse varias unidades.
  - Si no encuentra camino a la comida, ignora esa pila y no al bebé.
  - Un niño que juega deja el juego por una necesidad baja.
  - El hambre se consulta en un solo lugar (`Hunger.IsLow` y `Hunger.UnitsToFeed`, que también usa comer).
  - `Beds.HasBed` reutiliza la lista del inspector.
  - El trabajo de alimentar ya no guarda una celda de destino que nadie lee.
- 3 tests nuevos, 680 en total. La huella de la semilla 1 pasa a `3312BA81E1022B34`, porque el deseo de un hijo ahora guarda a la pareja; la de la 42 no cambia.

### 2026-10-18: H3.7, la ficha y el inspector

- La ficha muestra "Hijos: permitidos / no" junto a "Cerrar" a quien tiene pareja viva del otro sexo; con el tiempo corriendo se refresca apenas se aplica el comando. Ficha e inspector muestran "Embarazada (N %)" (`LifeRules.PregnancyPercent`). El inspector usa `Beds.HasBed`, y una cama dice "Es de Elvira", que vale también para la cuna.
- El smoke abre la ficha de un fundador en pareja, aprieta el botón dos veces y comprueba que la opción cambia y vuelve. Huellas nuevas: `CBD31D910E8019D7` (semilla 1) y `95A5B6F5A5B40D9F` (semilla 42).
- Capturas revisadas: la ficha en es a 1080p y en en a 960 × 540, y el panel Colonia a 960 × 540 con la columna Crianza.
- Documentación: "Cómo está hecho (H3.7)" en la spec, con dos ajustes (se llevan al bebé las unidades que le faltan, y el deseo de un hijo se cumple con el embarazo); `guardado.md` y ADR-013 con el formato 7; la cuna en `muebles-y-camas.md`; la cuna provisoria y los bebés en `art.md`.
- Los scripts de Python de la sesión dejaban CRLF en la copia de trabajo; se normalizó a LF (`.gitattributes` ya lo pide). Escribir siempre con `newline=''`.

### 2026-10-18: H3.7, el Core

- Rama `ip3/h3-7-nacimientos` desde `main`. Archivo real `Fixtures/h3.6.olsave`, escrito con el código de H3.6 antes de tocar nada.
- Datos: la etapa suma `eatsAlone`, `crawlRadius` y `playRadius`; `LifeConfig` suma el bloque `birth` (fertilidad, concepción, embarazo de 45 días de vida, último tramo al 90 %, un año entre hijos, opiniones al nacer); tipo de trabajo `childcare` con `feed_baby`, primero en el orden; la cuna con `forStages: [baby]` y la cama para las demás etapas; el deseo `have_child`, el pensamiento `child_born` y `parenthood` (amabilidad +2).
- `BirthSystem` a medianoche, después de `LifeSystem`: avanza los embarazos, da a luz (nombre libre, apellido del padre, rasgos heredados, habilidades en 0, necesidades llenas, familia y opiniones) y concibe con el flujo `life.birth` si nadie tiene hambre baja y la madre no tiene un hijo de menos de un año.
- Decisiones: el bebé no come solo, gatea a 3 celdas de su madre y espera quieto mientras le traen comida; quien cría lleva de la pila las unidades que le faltan al bebé, y van primero la madre o el padre si están libres; los niños van a jugar con otro niño o un familiar a 15 celdas que no hayan visto hace poco; las camas filtran por etapa (`Beds`).
- `SetChildrenAllowedCommand`: vale para la pareja; al prohibir, borra el deseo de un hijo sin frustrarlo. El deseo se cumple con el embarazo o el nacimiento.
- Formato de guardado 7: las partidas viejas reciben la habilidad de crianza sorteada como un fundador (`MigrateNewSkills`) y empiezan sin embarazos.
- Tests nuevos: `BirthTests` (11), `ChildcareTests` (8) y la continuidad del guardado con embarazo, bebé, crisis y deseo. 677 tests.

### 2026-10-18: estaciones visibles en el plan

- Pedido del usuario: que el suelo, los árboles y las plantas cambien de color con la estación. En otoño, árboles amarillos y rojizos; en invierno, pasto y árboles blanquecinos y cumbres de roca nevadas.
- Es factible y barato. Es sólo de presentación: el shader del terreno ya recibe los verdes del pasto como uniformes, y las copas usan materiales compartidos de la paleta. Se cambian una vez por hora del juego, con una transición de unos días. No toca la simulación ni las huellas.
- Va a IP3.5 como hito L4c (8–12 h, adelantado de IP6, que conserva el clima y la nieve que cae). Propuesta en `docs/gdd/estaciones-visibles.md`. IP3.5 pasa a 48–70 h y el total de la 1.0 a 815–1 124 h.

### 2026-10-17: IP3.5, decisiones y bosque que vuelve

- Decisiones del usuario: las metas de existencias se eligen en el panel Colonia; "cerca de la aldea" es un círculo de 40 celdas de diámetro.
- Pedido nuevo: que los árboles vuelvan a crecer sin llenar el mapa, con un tope como porcentaje del terreno. Va a IP3.5 como hito L4b: retoños junto a los árboles adultos, que crecen en días, con un tope por especie propuesto igual a la densidad con que la genera el valle (robles 3,5 %, pinos 4 %); nunca sobre construcciones, almacenes ni órdenes. Sembrar con semillas queda para la agricultura de IP4.
- IP3.5 pasa a 40–58 h; plan maestro y `docs/gdd/libre-albedrio.md` actualizados. Propuestas a confirmar en L1: el centro del círculo de la aldea (el lugar de llegada) y retoños sólo junto a árboles adultos.

### 2026-10-16: IP3.5 "Libre albedrío" planificada

- Respuestas del usuario: el interruptor va en Opciones para toda la aldea; por ahora sólo recolectar, talar y acarrear; las metas de existencias se eligen en el juego; la iniciativa respeta las prioridades; va como IP3.5, después de IP3.
- `docs/ip/IP3.5.md` con cinco hitos (L1 spec, L2 interruptor y metas, L3 trabajo por iniciativa, L4 vida social con intención, L5 integración), 32 a 46 h y versión `v0.3.5`. Fila nueva en el plan maestro, que corre las fechas siguientes unas dos semanas. Sale del backlog.
- Nota de diseño: la opción de Opciones llega a la partida como un comando (determinismo y guardado). Las tres partes van a IP3.5, así que el balance social de H3.10 se hace sin buscar ni evitar personas.

### 2026-10-16: libre albedrío, para planificar

- Pedido del usuario: un libre albedrío en el que el colono con hambre busque o recolecte comida, sin nada que hacer tome tareas según sus prioridades, su personalidad y su ánimo, y busque o evite a otros según el vínculo. No es para ahora.
- `docs/gdd/libre-albedrio.md`: qué ya hace el juego (comer y recolectar con hambre, tomar órdenes por prioridad), la propuesta en tres partes (trabajo por iniciativa, vida social con intención, preferencias por rasgos y ánimo), cinco preguntas para el usuario, tres lugares posibles en el plan y una estimación de 32 a 46 h. Anotado en el backlog y en "Pedidos fuera de los hitos" de `IP3.md`.

### 2026-10-16: ficha del colono (H3.9 adelantada)

- Pedido del usuario: ver en el juego los datos de H3.2 a H3.6. Se adelanta la ficha del colono, que el plan tenía en H3.9; la opción de hijos se suma con H3.7.
- `ColonistSheet`: se abre con F o con el botón del inspector, sigue a la selección y se cierra con F, Esc o "Cerrar". Arriba etapa, edad, ánimo, crisis y orientación; a la izquierda personalidad en palabras, habilidades con su avance y familia; a la derecha pensamientos, deseos, relaciones y últimas interacciones. Fondo opaco y desplazamiento: entra en 960 × 540.
- Datos nuevos: tramos `words` en `PersonalityConfig`, `Relationships.SiblingsOf`, textos `def.trait.<id>.low/high` y `def.interaction.<id>.started`, y `ui.sheet.*`.
- Panel Colonia con Edad y Ánimo. `--sheet` para capturas; el smoke abre la ficha y revisa que no falte ningún texto.
- Capturas en es y en a 1080p y a 960 × 540, revisadas. 647 tests, mismas huellas.
- Revisión de código: 10 hallazgos, todos corregidos. F cierra la ficha aunque se haya seleccionado otra cosa; Esc la cierra a través de `GameInput.CloseFirst`, así no le quita Esc al menú de pausa ni a otras pantallas. La orientación no se muestra sin etapa. El deseo de aprender dice qué y de quién. Los pensamientos repetidos se agrupan. La ficha se rehace sólo cuando pasa el tiempo. `SiblingsOf` más simple, con un padre sin entidad que no empareja a nadie, y con tests. `ColonyText.FullName(name, surname)` para los parientes. El smoke detecta claves sin traducir. 655 tests. `main` adelantado.

### 2026-10-16: cierre de H3.6

- Revisión de código: 10 hallazgos, 9 corregidos. "Reconciliarse" nunca nacía (dos amigos no pueden discutir): ahora nace tras discutir con alguien de quien tenía opinión ≥ 10, si bajó, y se cumple al volver a la de antes. "Hacer una amistad" no apunta a la familia y sólo nace de una charla que salió bien. Los hermanos tienen su pensamiento de duelo (`death_sibling`). Una cama propia no se desea si ya se tiene. El ánimo se calcula sin reservar memoria, porque la velocidad de trabajo lo pide en cada tick. Los límites de deseos valen desde el primero. `TimeSettings.TicksPerHour` y helpers comunes para `Mood` y `Desires`.
- Queda: el duelo arma `PersonalityRules` cuando hace falta (las muertes son raras).
- 646 tests, mismas huellas. `main` adelantado.

### 2026-10-16: H3.6, pensamientos, ánimo y deseos

- Defs `Thought` (21), `Crisis` (2), `Desire` (5) y `MoodConfig`, en `mood.json`. Las interacciones dicen qué pensamiento dejan y qué deseo pueden despertar.
- Ánimo = base 50 + pensamientos de hechos (componente `Mood`, con tick de fin y tope por pensamiento) + pensamientos de situación (hambre y sueño, calculados de las necesidades). Su efecto en el trabajo entra en `WorkSpeed`. Consolar ya funciona.
- `MoodSystem`, cada hora: limpia pensamientos vencidos, empieza (ánimo < 15, 10 %) y termina crisis con aviso amarillo, y cumple o frustra deseos. A medianoche, el deseo anual de formar pareja. Las crisis cortan el trabajo; quien se aísla no interactúa y deambula lejos de los demás.
- Pensamientos al despertar (cama o suelo), al aprender, al formar pareja o separarse y al morir alguien (pareja, hijo, padre o madre, amigo o vecino, con deriva de la sociabilidad por un familiar). Dos noches en el suelo pueden despertar el deseo de una cama.
- Formato de guardado 6 (sin migración: una partida vieja empieza sin pensamientos) y archivo real `Fixtures/h3.5.olsave`.
- El deseo de tener un hijo y "Nació mi hijo" quedan para H3.7.
- 643 tests. Huellas nuevas: `ECB4B600749ABA31` (semilla 1) y `57A3D947DB7DB434` (semilla 42).

### 2026-10-16: cierre de H3.5

- Revisión de código: 10 hallazgos, todos corregidos. Opiniones y romance de los fundadores acotados, y validación de rangos en `SocialConfig`, `Scenario.foundingCouple` y `Trait.interactionChance`. El sorteo de con quién interactuar ya no se cae si todos pesan 0, y la orientación exige pesos para el otro sexo. Una ruptura borra el romance, así que la pareja tiene que cortejarse de nuevo. Nadie corteja ni forma pareja con su familia. Al morir, el muerto sale también de las interacciones recientes. La configuración social se busca en un solo lugar. Al cargar, las relaciones deben estar en orden y sin uno mismo.
- 623 tests, mismas huellas. `main` adelantado.

### 2026-10-16: H3.5, interacciones y relaciones

- Defs `Interaction` (siete, en `social.json`) y `SocialConfig`; `Trait.interactionChance` (sociabilidad, de 0 % a 200 % de la probabilidad base); `LifeStage.interacts` (el bebé no); `Scenario.foundingCouple` (20 a 30 años, opinión 70, romance 100).
- Componentes `Relations` (opinión, romance, última interacción y lazo avisado), `Family` (padre, madre, pareja, parejas fallecidas, hijos, hijos permitidos y viudez) y `RecentInteractions` (las últimas 5).
- `SocialSystem`: cada 30 ticks, escalonado, un colono despierto busca a otro a 4 celdas en su nivel, sortea si empieza, con quién (familia y amigos pesan el doble) y qué interacción. Aplica opiniones, romance, enseñanza, deriva de rasgos y lo recuerda. A medianoche, las opiniones de quienes no se vieron se acercan a 0. `SocialRules.Change` forma parejas, las rompe y avisa amistades y rivalidades una sola vez (histéresis de 10 puntos).
- Al morir, los demás olvidan al muerto, la familia lo guarda como fallecido y la pareja queda viuda un año. Los fundadores se conocen (opinión −10 a 20) y los dos primeros llegan en pareja, con orientaciones compatibles.
- Ajustes de balance, en la spec: la charla termina mal con alguien impaciente, la discusión pesa más según paciencia y amabilidad y se permite hasta opinión 20. Con los números originales dos colonos ásperos no llegaban a rivales.
- Formato de guardado 5, con migración desde el 4 y archivo real `Fixtures/h3.4.olsave`.
- 621 tests. Huellas nuevas: `CE15ED74ECC60360` (semilla 1) y `AC908F5FCB21A9CD` (semilla 42).

### 2026-10-16: cierre de H3.4

- Revisión de código: 10 hallazgos, 9 corregidos. La velocidad de una habilidad se acota al último nivel. La habilidad de acarreo ahora sirve: apura el paso mientras se lleva la carga, y también la de construcción al llevar materiales. Una habilidad que falta se aprende desde nivel 0 (para tipos de trabajo nuevos, como la crianza de H3.7). Un colono sin habilidades usa `SkillConfig.averageLevel`. Se valida que los fundadores no pasen del último nivel. Subir de nivel emite `EntityChangedEvent`. El smoke compara la cantidad exacta de habilidades. `PersonalityRules.ProductOf` junta los porcentajes de los rasgos. Se corrigió la documentación del guardado y del ADR-013.
- Queda sin cambiar: la búsqueda lineal por id repetida en `Personality` y `Skills`.
- Huellas nuevas por el acarreo: `86682D524C6B7F9D` (semilla 1) y `A22F92980217E726` (semilla 42). 597 tests. `main` adelantado.

### 2026-10-16: H3.4, habilidades

- `WorkType.skill` (los cinco tipos de trabajo), def nuevo `SkillConfig` en `skills.json`, `Trait.experience` (inteligencia, de 75 % a 125 %) y las habilidades de los fundadores en `Scenario` (de 3 a 10 y una con 4 más).
- Componente `Skills` (nivel y experiencia en centésimas por tipo de trabajo). `SkillRules`: habilidades de los fundadores, ganancia con subida de nivel, enseñanza (`Teach`, para la interacción de H3.5) y velocidad por nivel (80 % + 2 % por nivel). `WorkSpeed.PercentOf` junta etapa, rasgos y habilidad del trabajo en curso.
- Se gana experiencia en cada tick que hace avanzar un trabajo y en cada tick que se lleva algo para acarrear o abastecer una obra. Un colono sin habilidades trabaja como uno de nivel 10.
- Formato de guardado 4, con migración desde el 3 (habilidades sorteadas) y archivo real `Fixtures/h3.3.olsave`.
- 593 tests. Huellas nuevas: `054F77100603CE30` (semilla 1) y `890D9729617952AA` (semilla 42).

### 2026-10-16: cierre de H3.3

- Revisión de código: 9 hallazgos, 6 corregidos. Lo principal: las partidas guardadas con H3.2 (formato 2) se quedaban sin personalidad. El formato sube a 3 y las migraciones se encadenan (ADR-013 actualizado: el formato sube cada vez que `main` recibe un cambio del estado). Nuevo archivo real `Fixtures/h3.2.olsave`, escrito con el código de `main`. Además: velocidad de trabajo con piso de 1 %, validación de la personalidad al cargar (rango, repetidos y día), `Calendar.DayNumberAt` para el día de la deriva, y la deriva redondea en lugar de truncar.
- Quedan sin cambiar: la búsqueda de comida de un perezoso antes de trabajar (las comidas sin camino ya esperan con el enfriamiento de siempre), el costo de `WorkSpeed.PercentOf` por tick (lo mide el benchmark de H3.10) y la forma de la API de `PersonalityRules`.
- 577 tests. `main` adelantado.

### 2026-10-16: H3.3, personalidad

- Def `Trait` con los seis rasgos de la spec y `PersonalityConfig` (tres tiradas, herencia al 50 %, deriva de 3 por día y pesos de orientación 80/8/12), en `personality.json`. `LifeStage.traitDriftPercent`: niños y adolescentes al 200 %.
- Componente `Personality` (rasgos por id con lo que se movió hoy, orientación y día de la deriva). `PersonalityRules`: sorteo, herencia (mitad de los padres y mitad sorteo), deriva con tope neto diario y consultas. Llegada y migración de la versión 1 sortean personalidad; el formato sigue en 2 y un colono sin personalidad cuenta como promedio.
- Efectos: la diligencia da la velocidad de trabajo, de 90 % a 110 % (`WorkSpeed.PercentOf` junta etapa y rasgos). Decisión mía, anotada en la spec: un perezoso (diligencia < 25) atiende sus necesidades bajas antes de trabajar, para que dos colonos opuestos elijan distinto ya en este hito. Sociabilidad, amabilidad, curiosidad, inteligencia y paciencia actúan desde H3.4 a H3.6.
- La deriva todavía no la llama nada en el juego: las vivencias llegan con las interacciones y los pensamientos.
- 571 tests. Huellas nuevas: `7D3BF3621633EF7B` (semilla 1) y `8D63FDA0CBA18BF8` (semilla 42).

### 2026-10-16: existencias y FPS en el HUD

- Pedido del usuario: un indicador de cuánta comida, madera y piedra tiene la aldea, y la posibilidad de ver los FPS. Spec `docs/gdd/indicadores.md`; anotado en "Pedidos fuera de los hitos" de `IP3.md`. Rama `ip3/indicadores-hud`.
- La barra superior muestra "Comida: 64  Madera: 25  Piedra: 0": todo lo que hay en el mapa, almacenado o no, más lo que llevan los colonos. Las categorías salen del contenido, con el campo nuevo `ItemCategory.order`. Consulta `ColonyStock.ByCategory`.
- Opciones suma "Mostrar FPS", guardado. Comparte fila con "Ciclo de día y noche", porque a 720p otra fila no entraba. Los FPS van en un panel abajo a la derecha, junto al registro, contados sobre medio segundo. `--fps` para capturas.
- La barra superior se apretó un poco para entrar en 960 × 540 con las existencias. Capturas a 1080p, 960 × 540 y 720p en es y en, revisadas.
- 557 tests, misma huella del smoke.
- Revisión de código: 6 hallazgos, todos corregidos. El inspector ya no tapa los FPS. Las cuentas tienen el ancho de tres dígitos, la fecha se recorta antes que los botones y el nombre de la aldea tiene como mucho 24 letras. El filtro de los almacenes usa el mismo orden de categorías (`ItemCategoryDef.InOrder`). Las existencias se cuentan sólo cuando llega un evento de entidad. El smoke comprueba los textos y que el panel de FPS aparece y desaparece. `main` adelantado.

### 2026-10-16: fundadores de los dos sexos

- El usuario probó una partida en la que todos los colonos eran mujeres. Pedido: asegurar siempre al menos una mujer y un varón al empezar.
- `ColonyStart.Settle`: los dos primeros fundadores son una mujer y un varón, en orden al azar; el resto se sortea como antes. Así quedan listos para la pareja fundadora de H3.5 y H3.7. La spec lo dice en "Llegada".
- Test con 40 semillas. Huellas nuevas: `4D9981ED0F339C6D` (semilla 1) y `B229DEF368992238` (semilla 42). 554 tests. Commit en `ip3/h3-2-edad` y `main` adelantado.

### 2026-10-16: cierre de H3.2

- Revisión de código: 9 hallazgos, 8 corregidos. Los bebés y los niños ya no disparan la alerta de "no tiene nada que hacer". Al cambiar de etapa, el colono suelta el trabajo que la etapa nueva no hace, y sólo se avisa si creció. Recolectar para comer ya no va más lento en los ancianos. Una edad negativa en un guardado es una partida dañada. El movimiento cuenta en milésimas de tick con la velocidad en el avance, sin perder la fracción (la migración de la versión 1 escala el avance guardado). `Def.DescKey` junto a `NameKey`. Se corrigieron dos comentarios mal codificados.
- Queda sin cambiar: `LifeRules.From` se arma en Nueva partida, al llegar y al agregar los sistemas, como `ColonyRules`; cuesta microsegundos y sale del mismo contenido inmutable.
- Mismas huellas (a los 5000 ticks nadie está a mitad de un paso). 553 tests. `main` adelantado.
- Nota práctica: las herramientas Write y Edit no tuvieron veredicto del clasificador en esta sesión; los archivos se escribieron con Bash. Los comandos de más de unos 8 KB se cortan, y las barras invertidas dobles pueden llegar simples: en C#, mejor una clase de caracteres como `[0-9]` que un escape.

### 2026-10-16: H3.2, Core de la edad y las etapas de la vida

- El usuario descartó los `.import` que había cambiado el editor.
- Defs nuevos en `life.json`: `LifePace` (normal 1, rápido 4 por defecto, muy rápido 8), `LifeStage` (bebé, niño, adolescente, adulto, anciano, con trabajo, velocidades y porcentaje de cada necesidad) y `LifeConfig` con la tabla de vejez. `Scenario` suma `founderMinYears` y `founderMaxYears` (18 a 40), `NameList` los apellidos y `WorkType` el campo `light` (recolectar y acarrear).
- Componente `Life` (días de vida y etapa), `Colonist.Surname` y `Simulation.LifePace`, en la huella y en el guardado. `LifeSystem` corre a medianoche: suma los días del ritmo, cambia de etapa con aviso y `EntityChangedEvent`, y sortea la muerte por vejez (flujo `life.aging`). Con 100 % por año la muerte es segura. `ColonistDeath` junta la muerte por hambre y por vejez.
- La etapa decide qué trabajos toma el colono, cuánto tarda (`ticks × 100 / velocidad`), a qué velocidad camina y a qué ritmo bajan sus necesidades. Un colono sin `Life` vive como adulto.
- Formato de guardado 2. Una partida de formato 1 se migra al cargarla (flujo `life.migration`). `tests/OurLife.Content.Tests/Fixtures/v0.2.5.olsave` es una partida real escrita con el código de `v0.2.5` (semilla 7, 20 000 ticks), y carga.
- Los textos de los colonos usan nombre y apellido (`ColonyText.FullName`). Textos nuevos en es y en: ritmos, etapas, `event.life.stage.<etapa>`, `event.life.old_age`, `ui.newgame.life_pace` y `ui.inspector.life`.
- Hasta H3.7 los bebés comen solos: no nace nadie todavía, y sólo los tests crean bebés.
- 546 tests en verde.

### 2026-10-16: H3.2, presentación

- Nueva partida tiene "Ritmo de la vida" con Normal, Rápido y Muy rápido, y la descripción del elegido. `--show newgame` la abre para capturas.
- El inspector muestra nombre y apellido, y la etapa con la edad ("Adulto, 25 años"); el nombre de la etapa concuerda con el colono. El registro y las alertas usan el nombre completo.
- El smoke comprueba que los colonos llegan con edad y ritmo. Huellas nuevas: `7C61534966CC37FA` (semilla 1) y `3984B7A57F47499E` (semilla 42).
- ADR-013: las partidas viejas se migran sobre la simulación cargada, con las reglas del Core, y cada versión migrable tiene un archivo real de prueba. `guardado.md` lista las versiones del formato.
- Capturas en es y en a 1080p de Nueva partida y del inspector, revisadas. 547 tests.

### 2026-10-16: cierre de H3.1

- El usuario aprobó la spec `docs/gdd/vida-y-generaciones.md`. Había sumado «inteligente» a los rasgos de `IP3.md`: la spec pasa a seis rasgos, con `intelligence` (Torpe a Inteligente), que decide la experiencia ganada de 75 % a 125 %, trabajando y aprendiendo de otro. `curiosity` queda para las ganas de aprender: el peso de enseñar como aprendiz y los deseos de aprender.
- Hito de documentación, sin código: no lleva revisión de código. `main` adelantado al cierre de H3.1. Sigue H3.2 en `ip3/h3-2-edad`.

### 2026-10-15: IP2.5 completada también en la Mac

- El usuario probó `v0.2.5` en la Mac y dio la sección de macOS de `docs/ip/playtest-ip2.5.md` por completada: los cuatro puntos pasan de ➖ a ✅. IP2.5 queda cerrada sin pendientes.

### 2026-10-15: H3.1, borrador de la spec de IP3

- Rama `ip3/h3-1-spec`, desde `main` en `v0.2.5`.
- Spec `docs/gdd/vida-y-generaciones.md`: tiempo de la vida y los tres ritmos (Rápido por defecto), cinco etapas, fundadores con edad, apellido y la pareja, cinco rasgos y la orientación, habilidades por tipo de trabajo, siete interacciones, relaciones y familia, pensamientos y ánimo, dos crisis, seis deseos, embarazo y parto, crianza con cuna y el trabajo de alimentar, vejez con tabla por edad, migración del guardado a la versión 2, contrato, flujos, defs, textos, modelos y 16 criterios de aceptación. Apellidos castellanos e ingleses con fuentes.
- Lo que quedó fuera, en el backlog. El usuario prueba `v0.2.5` en la Mac mientras tanto.

### 2026-10-15: cierre de IP2.5 (`v0.2.5`)

- El usuario cerró IP2.5 con el playtest de Windows aprobado. Los puntos de macOS quedaron en ➖: los hallazgos de la Mac se corrigieron, pero no se volvieron a probar ahí.
- Revisión de código de V6: 9 hallazgos, todos corregidos. El script de macOS pide PowerShell 7.4, detecta bien Zip64 y da modo de carpeta a las carpetas; el zoom con dos dedos tiene tope por evento y no hace zoom de costado, y el smoke lo comprueba; el registro de las teclas quedó en una sola función.
- `main` adelantado, tag `v0.2.5` y release de GitHub con los dos zips.
- Respuestas del usuario para la spec de IP3: los niños sólo juegan, la orientación forma parte de la personalidad y cualquier pareja es posible, los colonos tienen apellido y los hijos heredan el del padre, la comida es el único tope de población y dos fundadores llegan en pareja.

### 2026-10-15: plan de IP3 "Vida y generaciones"

- Pedido del usuario: colonos con sexo, edad y etapas (bebé, niño, adolescente, adulto, anciano), reproducción, muerte por vejez, personalidad sorteada que cambia con las vivencias, interacciones que dan habilidades y deseos.
- Decisiones del usuario: va antes de Oficios, y el ritmo del envejecimiento se elige al crear la partida.
- La nueva IP3 junta rasgos, relaciones y ánimo (antigua IP4), habilidades (antigua IP3) y generaciones (IP9). Oficios pasa a IP4, con habitaciones y comedor. Plan en `docs/ip/IP3.md`, de 95 a 135 h y diez hitos; `plan-maestro.md`, `eras.md`, `mapa-y-generacion.md` y ADR-008 actualizados.
- También pidió que Claude le responda siempre en español.

### 2026-10-15: V6, hallazgos del playtest en la Mac

- La checklist de Windows quedó toda en ✅.
- Hallazgo 1: la app de Mac no tenía permiso de ejecución. El `ZipArchive` de .NET (PowerShell 7) marca como MS-DOS todas las entradas al agregar archivos, y macOS ignora sus permisos. `package-macos.ps1` reescribe el directorio central para marcarlas como Unix, da `rw-r--r--` a lo agregado y falla si el ejecutable no tiene permiso de ejecución.
- Hallazgo 2: en la Mac, el trackpad no hacía zoom porque no manda rueda. Ahora hacen zoom el pellizco (`InputEventMagnifyGesture`), deslizar dos dedos (`InputEventPanGesture`) y mantener + o - (acciones `camera_zoom_in`/`camera_zoom_out`, por carácter y no por posición). El smoke los comprueba y falla si el pellizco va al revés.
- Mismas huellas, 515 tests. Zips de Windows y macOS rearmados.

### 2026-10-14: V6 Integración (primera parte)

- Versión 0.2.5; paquetes de Windows y macOS armados, el de Windows con el smoke sobre el exportado.
- Checklist `docs/ip/playtest-ip2.5.md` con la pasada automática; capturas es/en a 1080p; benchmark: 261 FPS de media, dentro del presupuesto.
- Falta el playtest humano.

### 2026-10-14: V5 Modelos y animaciones

- Muebles y provisiones de KayKit, con la madera entonada; bayas del Food Kit, sueltas y en los arbustos; los 12 aldeanos de Mini Characters con la ropa recoloreada y animaciones según lo que hacen, a la velocidad del juego.
- El catálogo suma `variants`, `parts` y `turn`. El baúl de KayKit miraba a +X: `turn: 3`.
- 507 tests, mismas huellas. Detalle en el registro de V5 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 8 corregidos; lo principal, las variantes de aldeanos sólo usaban la mitad de los modelos. 509 tests. `main` adelantado.

### 2026-10-14: opción Decoración del pasto

- Pedido del usuario: una opción para la cantidad de decoración, por el rendimiento.
- Opciones suma Decoración del pasto (Nada, Poca, Normal, Mucha), guardada; `--decoration` para capturas y benchmarks. Spec `docs/gdd/decoracion-del-pasto.md`.
- Medición alternada: Nada unos 325 FPS, Mucha unos 300 (0,25 ms por cuadro en la RTX 4090 de portátil).
- A 720p, Opciones queda justa: la próxima opción pide dos columnas o pestañas (riesgo en `IP2.5.md`).
- Revisión de código: 7 hallazgos, 6 corregidos; lo principal, subir el nivel ya no cambia las piezas que había. `main` adelantado.

### 2026-10-14: V4 Terreno y decoración

- Sólo queda el estilo B. Los patrones de cada material y los verdes del pasto pasan al catálogo, con tests.
- Decoración del pasto con siete modelos del Nature Kit, elegida por un hash de la celda: una malla por chunk.
- El cuadro de 100 ms de V1 no era del shader: era la ventana cambiando de tamaño durante el benchmark.
- Las mediciones de la portátil varían mucho entre corridas (232 a 418 FPS); con y sin decoración se mezclan, siempre muy por encima de 60.
- 506 tests, mismas huellas. Detalle en el registro de V4 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 8 corregidos; lo principal, la decoración ya no crece dentro de los arbustos ni se mete en las celdas vecinas. 507 tests. `main` adelantado al cierre de V4.

### 2026-10-14: V3 Vista previa de muebles

- Al colocar un mueble se ve su modelo translúcido y girado dentro del cubo, con una flecha hacia el frente; rojo donde no se puede. En un arrastre, en cada celda. Los planos de muebles se dibujan con su modelo.
- `Construction.CellsToPlan` en Core, que usan el comando y la vista previa. Opciones de captura `--furniture`, `--rotation`, `--point` y `--drag-to`.
- 495 tests, mismas huellas. Detalle en el registro de V3 de `IP2.5.md`.
- Revisión de código: 10 hallazgos, 9 corregidos; lo principal, la vista previa ya no recorre el mundo en cada cuadro. `main` adelantado al cierre de V3.

### 2026-10-14: V2 Tamaño de la interfaz

- Opción "Tamaño de la interfaz" en Opciones: Automático (según la ventana) o de 100 % a 300 %, guardada en `settings.cfg`. Escala sólo la UI 2D con `ContentScaleFactor`.
- El panel Colonia oculta las alertas y empuja al inspector cuando se tocan, para que todo entre en un lienzo de 960×540.
- Smoke con un clic real sobre un colono con la UI al 200 %. 494 tests, mismas huellas. Detalle en el registro de V2 de `IP2.5.md`.
- Revisión de código: 8 hallazgos, todos corregidos. El principal: la prueba del 200 % corría al 100 %, porque el manejador de `SizeChanged` deshacía el factor; ahora pasa por las preferencias y lo comprueba.
- `main` adelantado al cierre de V2.

### 2026-10-14: cierre de V1

- El usuario eligió terreno B, GL Compatibility y muebles de KayKit. Los aldeanos siguen siendo los de Mini Characters, recoloreados.
- ADR-012 (terreno por shader y kits medievales), referenciado en ADR-009, `overview.md` y `plan-maestro.md`; `docs/art.md` con la dirección del terreno y KayKit.
- Se quitó el código que sólo servía para probar Forward+.
- Revisión de código: 10 hallazgos, 6 corregidos, 3 para V4 y 1 que queda (detalle en `IP2.5.md`). 480 tests, mismas huellas.
- `main` adelantado al cierre de V1.

### 2026-10-14: V1 Prueba de estilo

- Shader del terreno con cuatro estilos (actual, A, B y C) y `--terrain-style` para elegirlo; pasto sólo arriba y con borde en los costados de los bloques. Forward+ se prueba con `--rendering-method forward_plus`.
- Kits: KayKit Dungeon Remastered (CC0) para los muebles medievales, probado en la casa del smoke; Mini Characters de Kenney trae 12 aldeanos con animaciones, con ropa moderna a recolorear.
- Benchmark: el shader casi no cuesta; en esta PC Forward+ rinde más (674 contra 420 FPS de media). Un cuadro de unos 100 ms que se atribuyó al shader resultó ser la ventana cambiando de tamaño (ver V4).
- Página de comparación para el usuario (privada): https://claude.ai/artifact/JkWuPmFMuosFSxV8TgAGiD. Detalle en el registro de V1 de `IP2.5.md`.
- 480 tests en verde, mismas huellas.

### 2026-10-14: plan de IP2.5 Pulido visual

- Pedido del usuario antes de IP3: un terreno con textura en lugar de colores planos, mejores objetos, ver hacia dónde mira un mueble al colocarlo y textos más grandes según la resolución.
- Revisé el estado actual: el terreno son cajas generadas en código con un color por material y la marca `Grass` tiñe el bloque entero; la UI está a 16 px sin escalar con la ventana; el modelo de los colonos trae 30 animaciones (caminar, quieto, levantar, acarrear) que no se usan.
- El usuario sumó al alcance los modelos provisionales, los muebles medievales, los colonos animados con variantes y la decoración del suelo, y pidió evaluar Forward+ con capturas.
- Plan en `docs/ip/IP2.5.md`: seis hitos (V1 a V6), 38 a 55 h, versión `v0.2.5`. Empieza con una prueba de estilo con capturas para que el usuario elija el terreno, el renderizador y los kits.
- `plan-maestro.md` con la fila de IP2.5 y los totales nuevos; la escala de UI se adelanta de IP8. Del backlog salen las variantes de aldeanos, el arbusto con bayas y los muebles medievales.
- Rama `ip2.5/v1-prueba-estilo` desde `main`.

### 2026-10-13: H2.7 Integración

- El usuario probó en el editor H2.1 a H2.6: todo bien.
- Rama `ip2/h2-7-integracion` desde `main`.
- Playtest automático: smoke de 30 días en las semillas 1, 42 y 7, con los 5 colonos vivos y la casa construida (entera en la 42; en la 1 y la 7 se acaban los troncos del radio de 8 celdas que tala el smoke). Al año y medio la colonia muere en el invierno porque el smoke recolecta una sola vez, como en IP1; el test del contenido real que recolecta en otoño sigue pasando el invierno.
- `--benchmark 20 --seed 42 --ticks 5000` con la casa construida: 406 FPS de media, 360 en el 1 % más lento, 0,027 ms por tick.
- Checklist `docs/ip/playtest-ip2.md`, con la pasada automática.
- Versión 0.2.0 en `project.godot` y en el preset de Windows. Paquete de Windows armado; el smoke del exportado pasa.
- Versión para macOS: plantilla `macos.zip` del `.tpz` oficial (1,2 GB) instalada; preset "macOS" universal (la plantilla sólo trae binarios universales), identificador `com.gtaulamet.ourlife` y firma ad hoc; `import_etc2_astc` activado en el proyecto, como pide Godot para Apple Silicon. Exportar C# a macOS desde Windows funciona. `tools/package-macos.ps1` agrega `LICENSES.txt` y `CREDITS.md` al zip sin perder el permiso de ejecución del binario. README con la sección de macOS.
- La exportación quedaba colgada por los nodos de MSBuild (ver Notas prácticas): arreglado en los dos scripts y verificado con los nodos vivos.
- Playtest humano: Windows aprobado entero. macOS no abría: Gatekeeper la bloqueaba y, desde la terminal, el sistema mataba el proceso aunque `codesign --verify` daba la firma por válida. Firmada de nuevo en la Mac, arranca. Pasos en el README y en el hallazgo 1 de `playtest-ip2.md`.
- Revisión de código: 9 hallazgos, 5 corregidos y 4 que quedan como están (detalle en `IP2.md`). `tools/common.ps1`, presets sin versión propia y scripts que restauran la sesión. Notarización de macOS y muebles medievales al backlog.
- Cierre: `main` adelantado, tag `v0.2.0` y release de GitHub con los zips de Windows y macOS. IP2 cerrada.

### 2026-10-12: H2.6 Gestión de la colonia

- Rama `ip2/h2-6-gestion` desde `main`.
- Avance 1: spec `docs/gdd/gestion.md`. Prioridades de 1 a 4 o No por colono y tipo de trabajo (por defecto 3, en `AiConfig`); la elección recorre las prioridades y, dentro de cada una, el orden de los tipos. Panel Colonia (P) con la lista de colonos, su actividad y los botones de prioridad. Alertas de colono ocioso (2 horas paseando) y de hambre crítica (`Need.alertWhenCritical`): un mensaje al empezar y una línea en el HUD mientras duran, con estado guardado. `TextResolver` expande colonos en nombre y género, y el registro fija el nombre al recibir el mensaje.
- Avance 2 (Core):
  - `AiConfig.defaultWorkPriority` (3) e `idleAlertHours` (2); `Need.alertWhenCritical` (hambre). Componente `WorkPriorities` (sólo lo que difiere del valor por defecto, ordenado) y `SetWorkPriorityCommand`, en `SaveTypes`. `WorkPriorityLevels` (0 a 4).
  - La decisión recorre las prioridades de 1 a 4 y, en cada una, los tipos de trabajo en su orden.
  - `AlertSystem`, último del tick: ocioso si pasea o está sin trabajo `idleAlertHours` seguidas (entre paseo y paseo hay un tick sin trabajo); hambre crítica hasta volver al umbral bajo. Estado en `Colonist.IdleSinceTick`, `Colonist.IdleAlerted` y `NeedState.CriticalAlerted`. Mensajes con el colono como `EntityId`. `ColonyAlerts.Active` para el HUD.
  - Textos de eventos y alertas en es y en (`event.need.hunger.critical` sin concordancia de género: el inglés no la usa y la paridad de argumentos lo exige). Test de contenido de las claves de alertas.
  - Tests: `ManagementTests` (prioridad alta primero en los dos sentidos, acarreo en No y en 4, comando que limpia el valor por defecto e ignora lo inválido, alerta de ocioso que avisa una vez y vuelve a avisar, hambre crítica que termina al comer). La continuidad del guardado incluye prioridades. 478 tests.
- Avance 3 (Godot):
  - `ColonyText.Freeze` (Core) cambia un colono de un texto por su nombre y su género (`<arg>_gender`); `TextResolver.Resolve(texto, mundo)` lo usa, y el registro fija los nombres al recibir cada mensaje. Test con un colono que muere después.
  - Panel Colonia (P, y botón en la barra de arriba): fila por colono con nombre (clic: lo selecciona), actividad y un botón de prioridad por tipo de trabajo; clic pasa a la siguiente (1, 2, 3, 4, No) y clic derecho a la anterior. Texto de ayuda aclarado en la spec.
  - Lista de alertas bajo la barra de arriba: amarillo los avisos, rojo los peligros, clic selecciona; se rehace cuando cambian las alertas (también al cambiar el tick, porque terminan sin mensaje).
  - El smoke abre el panel; opción `--colony` para capturas. Capturas revisadas en es y en: cinco alertas de ocioso, el panel y los mensajes con nombres en el registro.
- Avance 4 (2026-10-13, revisión y cierre): 10 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: los mensajes salen del Core con el nombre fijado, el panel no pierde clics rápidos y P no funciona con un menú abierto. 480 tests, mismas huellas. `main` adelantado a H2.6.

### 2026-10-12: H2.5 Muebles y camas

- Rama `ip2/h2-5-muebles-camas` desde `main`.
- Avance 1: spec `docs/gdd/muebles-y-camas.md`. Los muebles son `Buildable` con `shape: furniture` y reusan planos, entrega, construcción y desmontaje de H2.4. Giro de 0 a 3 con la tecla G. La cama tiene dueño (la toma quien se acuesta sin cama) y da `restRecoveryPercent` 150; el cofre es un almacén de una celda; mesa y silla son decorativas. Modelos: `bedSingle`, `table` y `chair` del Furniture Kit y `chest` del Survival Kit de Kenney (CC0), descargados de kenney.nl.
- Avance 2 (Core):
  - `BuildShape.Furniture` con `visual`, `restRecoveryPercent` y `storage` en `Buildable`; `IHasVisual.Visual` admite nulo para los construibles del terreno. Cama, mesa, silla y cofre en `buildables.json` (`order` 10 a 13).
  - `PlaceBlueprintCommand` lleva `Rotation` (0 a 3), que guardan el plano y el mueble. Componentes `Furniture` y `Bed`, en `SaveTypes`. Al terminarse, la cama suma `Bed` y el cofre un `Stockpile` de su celda.
  - Reglas: un mueble va en suelo firme, fuera de almacenes y de otros muebles; nada se planea sobre un mueble; un almacén nuevo saltea los muebles y Cancelar no quita un cofre. Desmontar sobre un mueble desmonta el mueble; un mueble que pierde el suelo firme se pierde y deja su reintegro.
  - Dormir: el colono va a su cama o toma la libre más cercana a la que llega; una cama con dueño muerto está libre. En su cama recupera a `restRecoveryPercent` (150).
  - Modelos copiados a `game/assets/models/kenney_furniture-kit` y `kenney_survival-kit`, en el catálogo y en `CREDITS.md`.
  - Tests: `FurnitureTests` (descanso en cama, tomar la cama libre, cama libre al morir el dueño, cofre que recibe troncos y se desmonta, giro, reglas de colocación, almacén y Cancelar con muebles) y validaciones de defs. La continuidad del guardado incluye cama, cofre y una mesa por construir. 471 tests.
- Avance 3 (Godot):
  - Los muebles se dibujan con su modelo, girados de a cuartos. El catálogo de visuales acepta `center: true` para modelos con el origen en una esquina (Furniture Kit); escalas: cama 1, mesa 1,1, silla 1,6, cofre 2.
  - Tecla G: gira el mueble elegido; la pista suma una segunda línea con el giro. El cofre no se tiñe como almacén; el plano de un mueble es una caja baja.
  - Inspector: "Silla de madera", el dueño de una cama o "Libre", y en el colono "Duerme en su cama" o "Sin cama".
  - La casa del smoke se amuebla con los muebles del contenido en sus rincones; el smoke informa muebles y camas tomadas. Opción `--inspect-furniture`.
  - Capturas revisadas: la casa amueblada en es (cama, mesa y silla) y de noche en en, con un colono en la cama.
- Avance 6 (pedido del usuario): colonos más chicos, a escala 1,3 en lugar de 2 (0,87 de alto), para que entren en las camas. El colono acostado se corre la mitad de la altura medida de su modelo, no una distancia fija. Sólo presentación: misma huella.
- Avance 5 (pedido del usuario): los colonos que duermen se dibujan acostados boca arriba, centrados en la celda; en la cama, a lo largo de ella y sobre el colchón. Sólo presentación: misma huella. Captura de noche revisada.
- Avance 4 (revisión y cierre): 9 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: almacenes que saltean planos de muebles, el índice de almacenes al perderse un cofre, la cama se toma sólo con camino, el resto de las necesidades en centésimas y la marca `TileFlags.Furniture`. 471 tests. `main` adelantado a H2.5.

### 2026-10-12: H2.4 Construcción

- Rama `ip2/h2-4-construccion` desde `main`.
- Avance 1: spec `docs/gdd/construccion.md`. Decisiones: los materiales de construcción son defs `Material` con `builtFrom` (madera desde troncos, mampostería desde piedra) y una celda es construida si su material lo tiene; def `Buildable` con forma, materiales, costo, trabajo y `outline` (el muro marca el borde de la caja); la puerta es una forma nueva del mapa, pisable; los planos son entidades; trabajos `deliver` y `build` en el tipo nuevo `construction` (antes del acarreo), `deconstruct` y `channel` (este en minería); la escalera construida ocupa dos niveles. Atajos: Construir (C), Desmontar (V), Canalizar (H), porque B y D ya están en uso.
- Avance 2 (Core):
  - `MaterialDef.builtFrom` y `MaterialKind.Wood`; materiales `timber` y `masonry`. Def `Buildable` (`buildables.json`): muro, suelo, puerta, rampa y escalera. `AiConfig.deconstructRefundPercent` (50). Validaciones: un objeto construye un solo material, un construible sólo usa materiales de construcción y uno construido no tiene `minedItem`.
  - Forma `Door` del mapa; marcas `DeconstructDesignation` y `ChannelDesignation`. `DesignationKind` suma Desmontar y Canalizar (antes de Cancelar); Excavar y las escaleras sólo toman celdas naturales; Cancelar quita planos y deja lo entregado.
  - `PlaceBlueprintCommand` y el componente `Blueprint`, registrados en `SaveTypes`. `Construction` reúne las reglas: dónde va cada forma, terminar, desmontar, cancelar y quitar planos que perdieron su lugar (se revisa cuando cambia `NavigationVersion`).
  - Trabajos `deliver`, `build` y `deconstruct` en el tipo nuevo `construction` (orden 3; acarreo pasa a 4) y `channel` en minería. Entregar funciona como acarrear: la pila más cercana de lo que falta y el plano más cercano a la pila (`Job.Recipient`). Nadie come de una pila que alguien entrega.
  - Los materiales del contenido se ordenan por id: el banco de pruebas rellena el mapa con `rock` explícitamente (`AsciiMap.ParseFilled`).
  - Tests: `ConstructionTests` (muro de troncos del almacén, plano que espera material, el muro no sube sobre un colono, borde de la caja, casa de cuatro muros con puerta, canal con rampa, desmontar, escalera de dos niveles, rampa junto a un muro, cancelar, plano que pierde su lugar, órdenes y planos que se excluyen) y validaciones de defs. La continuidad del guardado incluye planos, un canal y un muro por desmontar. 458 tests.
- Avance 3 (Godot):
  - Herramienta Construir (C): abre una fila con los construibles (por `order` del def) y los materiales que admite el elegido; arrastrar manda `PlaceBlueprintCommand`. La tecla y el botón recuerdan lo último elegido. Desmontar (V) y Canalizar (H) salen solas de `DesignationKind`.
  - Planos como cajas translúcidas del color del material, más opacas con todo entregado; marcas de desmontar y de canalizar. Pieza `Door`: dos postes y una hoja más baja que los muros, girada en la línea de los muros. Madera un poco más oscura (`#7E5531`).
  - Inspector: el plano con lo entregado ("Plano de muro de madera: 0 de 2") y las órdenes nuevas.
  - Las órdenes del smoke y de `--orders` suman una casa de 5 × 5 con puerta en el primer sitio llano cercano con el borde libre de plantas; el smoke informa cuánto se construyó. Opciones `--build` e `--inspect-blueprint` para capturas.
  - `terrainAmplitude` pasa de 3 a 2: con 3 no había sitio llano de 5 × 5 cerca de la llegada en la semilla 42; con 2 lo hay en las semillas 1, 7 y 42 (no en la 100). Las plantas también cerraban sitios: por eso sólo el borde tiene que estar libre.
  - Capturas revisadas: planos con la barra y el inspector en es y en; la casa terminada con su puerta en es.
- Avance 4 (revisión y cierre): 10 hallazgos, todos atendidos (detalle en `IP2.md`). Lo principal: nadie trabaja parado sobre una celda que otro trabajo va a cerrar (dos constructores vecinos se esperaban para siempre), desmontar una escalera reserva y despeja sus dos mitades, y muros, puertas, rampas y escaleras van sólo sobre suelo firme. Sin ADR nuevo: lo construido se reconoce por el material, dentro de las reglas existentes. 461 tests. `main` adelantado a H2.4.

### 2026-10-11: H2.3 Almacenes y acarreo

- Rama `ip2/h2-3-almacenes` desde `main`. Spec `docs/gdd/almacenes.md`.
- Avance 1 (Core):
  - Def `ItemCategory` (madera, piedra y comida) y `Item.category`. Tipo de trabajo `hauling` con el trabajo `haul`, después de recolectar.
  - Componentes `Stockpile` (celdas ordenadas y categorías permitidas) y `Carried`. `DesignateCommand` con `Stockpile` crea una zona con las celdas libres y pisables; `Cancel` además saca celdas de las zonas. `SetStockpileFilterCommand` cambia un filtro. `StockpilesChangedEvent` para la vista.
  - `StorageIndex` (derivado, se arma una vez por tick si hace falta): zona de cada celda y qué hay en cada celda de almacén.
  - Acarreo: el colono elige la pila suelta más cercana con destino; el destino es la celda de almacén más cercana a la pila que acepta el objeto y tiene lugar. Reserva la pila y la celda. Levanta lo que entra, lo lleva y lo deja. Si deja el trabajo con algo en las manos, lo suelta donde está; si muere, también.
  - Tests: `StockpileTests` (troncos apilados en el almacén, filtro de comida, el que tiene hambre suelta la carga y después se guarda, dos acarreadores nunca comparten pila ni destino, Cancelar y celdas ocupadas o no pisables). La continuidad del guardado incluye un almacén, un filtro y un colono cargando, en Core y con el contenido real. 435 tests.
- Avance 3 (revisión): 10 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: una celda de almacén guarda una sola pila y las demás quedan sueltas, y las reservas de celdas se cuentan. 436 tests.
- Avance 4 (2026-10-12): segunda ronda de revisión, 8 hallazgos, 7 corregidos (detalle en `IP2.md`): nadie come de una pila que alguien acarrea, las pilas sin destino no gastan intentos, sin camino se deja de lado la celda de destino y no la pila, el índice se rearma tras mover pilas y las categorías de un almacén se validan al cargar. 439 tests, mismas huellas. H2.3 cerrado y `main` adelantado.
- Avance 2 (Godot):
  - Herramienta Almacén (Z): sale sola de `DesignationKind`, con su tecla. Las celdas de almacén se pintan de azul, finas y sobre el suelo.
  - El inspector de una celda de almacén lista las categorías con un botón Sí/No que manda `SetStockpileFilterCommand`; el de un colono dice qué lleva. El colono muestra el objeto en las manos.
  - Los almacenes sólo toman suelos planos: sobre las rampas la losa quedaba enterrada y las pilas se veían mal (spec actualizada).
  - Las órdenes del smoke y de `--orders` incluyen un almacén; el smoke informa cuánto quedó guardado (todos los troncos en las semillas 1 y 42). Opción `--inspect-stockpile` para capturas.
  - Huellas del smoke nuevas por el almacén en sus órdenes (ver Estado actual).
  - Capturas revisadas: acarreo y carga en es, filtros en en.

### 2026-10-11: H2.2 Día, noche y sueño

- Rama `ip2/h2-2-dia-noche` desde `main`. Spec `docs/gdd/dia-y-noche.md`.
- Avance 1 (Core):
  - `AiConfig` tiene `nightStartHour` y `nightEndHour` (21 a 6 en el contenido; la noche puede cruzar la medianoche). `NeedDef.nightThreshold` (9000 para el descanso): de noche, un colono con el descanso por debajo se acuesta antes que cualquier trabajo. De día sólo duerme con el descanso crítico, y de noche no se levanta aunque esté lleno.
  - Balance: el descanso baja 500 por hora (antes 600) y sube 1200 durmiendo (antes 900): 15 h despierto y 9 h dormido se compensan.
  - Tests: `NightSleepTests` (se acuestan a las 21 y se levantan a las 6, de día no se acuestan cansados pero sí agotados, siguen durmiendo llenos hasta las 6, noches que cruzan o no la medianoche), validación de los defs y, con el contenido real, la aldea duerme entera a las 2 y nadie duerme al mediodía durante tres días. Los tests del invierno siguen en verde. 427 tests.
- Avance 2 (Godot):
  - `DayNightCycle` dentro de `WorldView`: el sol cruza el cielo de 6 a 21 y una luna fría hace el mismo arco de noche; luz, ambiente y cielo se mezclan entre amanecer, día, atardecer y noche. Apagado, la luz es la del mediodía de IP1.
  - Opción "Ciclo de día y noche: Sí | No" en Opciones, guardada en `settings.cfg`.
  - El registro agrega una línea con la fecha antes del primer mensaje de cada día.
  - `--hour <h>` lleva la partida hasta esa hora para capturas.
  - 4 claves nuevas en es y en. Capturas revisadas a las 6, 12, 20 y 0, y Opciones en inglés.
  - Huellas nuevas del smoke por el balance del descanso (ver Estado actual).
- Avance 4: segunda ronda de revisión, 10 hallazgos, 7 corregidos y 3 que son lo que pide la spec (detalle en `IP2.md`). Mismas huellas.
- Avance 3 (revisión y cierre): 9 hallazgos, todos corregidos (detalle en `IP2.md`). Lo principal: `nightThreshold` pasa a 10 000 para que nadie quede despierto de noche, y la luz ya no salta al amanecer. Documentación y `main` adelantado a H2.2. 429 tests.

### 2026-10-11: planificación de IP2

- `docs/ip/IP2.md`: siete hitos (guardado, día y noche con sueño, almacenes, construcción, muebles y camas, gestión, integración), con horas, entregables y definición de hecho.
- El guardado va primero para que el test de continuidad cubra todo lo que se agregue después.
- Pendientes de IP1 repartidos: el nombre de la aldea y la pantalla de error en H2.1, el día en el registro y el balance del invierno en H2.2, `terrainAmplitude` se evalúa en H2.4 y los argumentos de entidad en `TextResolver` en H2.6. Lo de arte que no entra quedó en el backlog.
- Rama `ip2/h2-1-guardado` creada desde `main`.
- H2.1, avance 1: spec `docs/gdd/guardado.md` (qué se guarda, ranuras, autoguardado a las 00:00, pantallas, errores, textos y criterios) y ADR-010 (ZIP con `header.json` y `state.json`, tipos con nombre estable en `SaveTypes`, materiales por id, versión de formato y errores tipados).
- H2.1, avance 2 (Core):
  - `Simulation.ColonyName`, que fija `ColonyStart.Settle` y entra en la huella. El smoke usa el nombre por defecto del español, para que la huella no dependa de `--lang`.
  - `SaveGame` (`OurLife.Core.Save`): `Write`, `ReadHeader` y `Load` sobre un `Stream`. `SaveTypes` registra los nombres estables de componentes y comandos. `[DefRef]` marca las propiedades con ids de defs, que se validan al cargar.
  - Ganchos internos para restaurar: ranuras de entidades, flujos del RNG y tick.
  - Las entradas del ZIP llevan una fecha fija (1980): el ZIP no admite fechas fuera de 1980–2107 y así el archivo no depende del reloj.
  - Tests: 16 en Core (continuidad con una colonia que excava, tala, recolecta y tiene una orden inalcanzable y un comando pendiente; versiones; archivos dañados; contenido faltante) y 4 con el contenido real (continuidad en las semillas 1, 7 y 42 cortando a los 20 000 ticks; un valle guardado pesa menos de 1 MB).
- H2.1, avance 3 (Godot):
  - `SaveSlots`: un archivo `.olsave` por ranura en `user://saves`, escrito en un temporal que después reemplaza al anterior; ranuras dañadas listadas al final para poder borrarlas.
  - `GameScreen` tiene un identificador de partida y autoguarda en `auto-<id>` al empezar cada día del juego. Sin ranuras (capturas y smoke sin `--saves`) no guarda ni autoguarda.
  - Pantallas `LoadGameScreen`, `SaveGameScreen` (desde la pausa, con "Partida guardada" al volver) y `ErrorScreen`; confirmación de Sí/No en `UiBuilder.Confirm`. "Cargar partida" del menú principal se habilita con alguna ranura.
  - Error al arrancar: con un jugador mirando, el juego muestra la pantalla de error en lugar de cerrarse; si fallan los textos, muestra el detalle tal cual.
  - Opciones `--saves <carpeta>`, `--load <ranura>` y `--show load|save|error`.
  - El smoke guarda y carga en memoria (misma huella, y la misma 600 ticks después), escribe, lista y carga una ranura en `user://smoke-saves` y arma las pantallas nuevas. Un valle guardado pesa 33 KB y tarda unos 33 ms en guardarse y otro tanto en cargarse.
  - 23 claves nuevas en es y en; se quitó `ui.mainmenu.load_later`.
- H2.1, avance 7: tercera ronda de revisión, 10 hallazgos, 8 corregidos (detalle en `IP2.md`). Validación más estricta al cargar y `ConfirmDialog` con Esc propio. 409 tests, mismas huellas. `main` adelantado a H2.1.
- H2.1, avance 6: segunda ronda de revisión, 10 hallazgos, 8 corregidos (detalle en `IP2.md`). El smoke ahora guarda a mitad de sus ticks; las huellas no cambiaron. 406 tests.
- H2.1, avance 5 (revisión de código y cierre): 10 hallazgos, 9 corregidos y uno descartado con su motivo (detalle en el registro de H2.1 de `IP2.md`). Documentación: `overview.md` (sección Guardado), ADR-010 en la lista, skill `godot-run`, agentes y `CLAUDE.md` (registrar cada componente o comando en `SaveTypes`). H2.1 queda hecho; falta adelantar `main`.
- H2.1, avance 4 (capturas): menú principal, Cargar, Guardar y Error revisadas en es y en (en `game/out/h21-*.png`, con `--saves out/saves`; la carpeta es relativa a `game/`). El autoguardado apareció al pasar la medianoche con `--ticks 1500`. Los botones de menú se estiraban en las columnas anchas: ahora mantienen su ancho, centrados.

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

### 2026-10-11: H1.8 Integración y cierre de IP1

- El usuario hizo el playtest humano con el checklist: todo aprobado, sin hallazgos nuevos, y visto bueno para publicar `v0.1.0`. El hallazgo menor de la pasada automática (el día en el registro) pasó al backlog.
- Registro de H1.8 en `IP1.md` e IP1 marcada como cerrada; README con el estado de la versión.
- Adelanté `main` al cierre, creé el tag anotado `v0.1.0` y subí los dos.
- El usuario pidió publicarla como release de GitHub. Instalé `gh` con winget, el usuario se autenticó con `gh auth login`, rearmé el zip desde el commit del tag (el anterior ya no estaba en `builds/`) y creé la release "OurLife v0.1.0: Primeros colonos" con el zip y notas en español.

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
  - El usuario preguntó por un ciclo de día y noche. Decidió que vaya en IP2, junto con que los colonos prefieran dormir de noche, y no en `v0.1.0`. Quedó en `plan-maestro.md`: IP2 pasa a 77–101 h, el ciclo se adelanta de IP6 y a IP6 le quedan las antorchas.
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

### 2026-10-27: colonos por etapa de vida

- Reemplacé los 12 aldeanos de Kenney Mini Characters y sus 12 ancianos derivados por los 60 modelos propios de `MODELS/colonos-vida/glb` (6 hombres y 6 mujeres en bebé, niño, adolescente, adulto y anciano), copiados a `game/assets/models/settlers` con sus `.import`.
- `visuals.json`: las cinco etapas de `creature.human*` usan los 6 tipos por género, a escala 3,1 (corregida de 1,3, que los dejaba a menos de la mitad del tamaño de los Kenney) (cada modelo ya trae su altura y su proporción, así que se quitaron `boneScales`). El tipo se elige por el hash del id, igual en todas las etapas, así que un colono conserva su aspecto al crecer. El bebé sigue con `idle` → `sit`.
- Se borraron `kenney_mini-characters` y `tools/make-elder-models.py`; se actualizaron `CREDITS.md`, `docs/art.md` y el GDD de vida.
- `dotnet test` en verde y smoke con código 0. La captura no mostró bien a los colonos (la cámara no los encuadra), así que **falta revisar a ojo** el aspecto, la escala y el color de los modelos en el juego.
- `MODELS/` (fuentes, imágenes, scripts de Blender) no se versiona.

### 2026-10-28: plan de modelos propios

- Nuevo `docs/plan-modelos.md`: una casilla por cada uno de los 41 modelos pendientes (6 fases, de los 5 imprescindibles a los opcionales), con id visual, archivo, descripción, modelo provisorio actual y los pasos para darlo por hecho.
- `docs/art.md` enlaza al plan. Hecho: 0 de 41 al crear el plan.

### 2026-10-28: modelos de la fase 1 generados

- `MODELS/` se organiza por tipo de objeto: `puestos-trabajo`, `muebles`, `objetos/{comida,materiales,herramientas}`, `cultivos`, `construccion`, `entorno` y `generador` (scripts y vistas previas).
- `MODELS/generador/lib.py` y `fase1.py` generan los 5 modelos de la fase 1 (banco de carpintero, banco de cantero, cocina, escritorio de copista y cuna). Pendiente: probarlos en el juego, ajustar `visuals.json` y copiarlos a `game/assets/models/ourlife/`.

### 2026-10-28: fase 1 de modelos integrada

- Los 5 modelos de la fase 1 están en `game/assets/models/ourlife/` (con sus `.import`) y `visuals.json` los usa: banco de carpintero, banco de cantero, cocina y escritorio de copista a escala 0,8; cuna a 0,7 con `restHeight` 0,2. Antes usaban `table_small` y `bed_frame`.
- `dotnet build`, `dotnet test` (49 + 757 + 215) y el smoke con código 0. Capturas de la vista previa de cada mueble con `--furniture` revisadas.
- Pendiente: ver en el juego la cuna con el bebé dentro y los puestos con un colono trabajando, y retocar escala o color si hace falta. Sigue la fase 2 (objetos de comida y materiales).

### 2026-10-28: fase 2 de modelos integrada

- Los 10 objetos de comida y materiales (repollo, arvejas, pan, potaje, repollo en conserva, trigo, harina, provisiones, tablón y mango) están en `game/assets/models/ourlife/` y en `visuals.json`, a su tamaño final (escala 1,0). Se generaron con `MODELS/generador/fase2.py`; la paleta de `lib.py` ganó los colores de comida.
- Se borró `barrel_small` de KayKit (y su textura), porque ningún id lo usa ya: el test `Only_the_models_of_the_catalog_are_in_the_repository` lo exigía.
- Progreso: 15 de 41. Sigue la fase 3 (hierro, piedra y herramientas).

### 2026-10-28: fase 3 de modelos integrada

- Los 8 objetos de piedra, hierro y herramientas (sillar, mineral de hierro, lingote, carbón, hacha, pico, azada y martillo) están en `game/assets/models/ourlife/` y en `visuals.json`: los materiales a escala 1,0 y las herramientas a 0,8. Se generaron con `MODELS/generador/fase3.py`.
- `dotnet build`, `dotnet test` (49 + 757 + 215) y el smoke con código 0.
- Progreso: 23 de 41. Sigue la fase 4 (cultivos, 9 modelos).

### 2026-10-28: fase 4 de modelos integrada

- Los 9 modelos de cultivo (trigo, arvejas y repollo en brote, creciendo y maduro) están en `game/assets/models/ourlife/` y en `visuals.json`, a escala 1,0 y sin `parts`: cada modelo ya trae todas las plantas de la celda. Se generaron con `MODELS/generador/fase4.py`.
- `dotnet build`, `dotnet test` (49 + 757 + 215) y el smoke con código 0.
- Pendiente: ver un campo real en el juego (la captura con `--farm --inspect-farm` no mostró cultivos) y ajustar escala o color si hace falta.
- Progreso: 32 de 41, todos los imprescindibles. Quedan las fases 5 y 6, opcionales (muebles de KayKit y piezas de construcción).

### 2026-10-28: fase 5 de modelos integrada y KayKit retirado

- Mesa, cama, silla y baúl propios en `game/assets/models/ourlife/` y en `visuals.json` (escala 1,0; la cama con `restHeight` 0,24), generados con `MODELS/generador/fase5.py`.
- Con ellos no quedó ningún modelo de KayKit Dungeon Remastered en uso: se borró la carpeta del kit, su fila en `CREDITS.md` y el color `texture` de la paleta (el test `Every_palette_colour_replaces_a_material_of_some_model` lo pedía). `docs/art.md` actualizado; los ADR y las IPs antiguas conservan la mención histórica.
- `dotnet build`, `dotnet test` y el smoke con código 0. Captura de la casa del smoke revisada.
- Progreso: 36 de 41. Queda la fase 6 (puerta, escalera, rampa y 2 tocones), opcional.

### 2026-10-28: muros finos y revisión de puerta, escalera y rampa

- Rama `ip4/muros-finos`. Los muros construidos (los de material con `builtFrom`) ya no son bloques de una celda: son finos, de 0,24 (`TerrainMeshes.WallThickness`), con un poste en el centro y un brazo hacia cada vecino que es muro o puerta (`TerrainPiece.WallPost` y `WallArm`). La roca y la tierra del valle siguen siendo bloques. La puerta ya tenía ese grosor.
- Efectos en `MapRenderer`: los bloques vecinos de un muro construido se dibujan como si hubiera aire (un muro fino no tapa sus caras); el corte por nivel no pone tapa oscura sobre un muro construido; bajo el muro queda el suelo de abajo, con pasto si lo tiene alrededor, o una losa del material si no hay suelo debajo (segundo piso).
- Sin cambios en Core: la simulación sigue tratándolos como celdas `Wall` que bloquean el paso.
- Puerta, escalera y rampa: se quedan como piezas de código, porque el shader del terreno las pinta con el patrón de su material y encajan sin costuras. Los tocones pasan a `docs/ip/backlog.md`.
- `dotnet build`, `dotnet test` (49 + 757 + 215) y el smoke con código 0. Captura de la casa del smoke revisada. Falta ver un muro de dos pisos y las esquinas en T con el juego en marcha.

### 2026-10-28: tocones modelados, plan de modelos cerrado

- `MODELS/generador/fase6.py` genera `stump_oak.glb` y `stump_pine.glb` en `MODELS/entorno`. No entran al juego todavía: `Every_model_of_the_catalog_is_used_by_a_def` exige que un def use cada modelo del catálogo, y el talado aún no deja tocón (backlog). Se probó y se revirtió.
- Plan de modelos cerrado: 38 modelos hechos (36 integrados y 2 tocones listos), 3 descartados (puerta, escalera y rampa siguen como piezas de código).

- Los scripts generadores pasaron de `MODELS/generador` a `tools/models/` (versionados, con `README.md`), y los dos tocones se guardan en `tools/models/pendientes/`. Las vistas previas se ignoran con `.gitignore`.

### 2026-10-28: /code-review de los muros finos

- Hallazgos corregidos en `MapRenderer`: un muro construido en el nivel 0 junto a pasto lanzaba una excepción al armar el chunk (se pedía el material del nivel -1); si debajo hay otro muro fino, ahora lleva su losa en lugar de quedar colgado ni de poner pasto flotando; un muro sólo une con una puerta que está en su misma línea (`JoinsWall`); y la pregunta "es un muro construido" usa una tabla de materiales por índice en lugar de buscar el def.
- Sin cambios: no hay prueba automática del dibujo de muros (la presentación sólo tiene el smoke); queda cubierto por la captura de la casa y el smoke.
- `dotnet build`, `dotnet test` (49 + 757 + 215) y el smoke con código 0.
