# IP1: Primeros colonos

**Objetivo.** Abrir el juego en español, generar un valle con niveles Z y mantener vivos a 5 colonos dándoles órdenes. Poder cambiar a inglés en vivo.

**Estimación:** 112 a 153 h; eran 100 a 135 h antes del paso a 3D. **Versión al cerrar:** `v0.1.0`.

## Cambios de alcance

- **2026-10-09, antes de H1.4:** el render pasa de pixel art 16×16 con `TileMapLayer` a 3D low-poly con el Nature Kit de Kenney (ADR-009, que reemplaza a ADR-007). Lo decidió el usuario.
  - Cambian el alcance y las horas de H1.5 (de 14–18 h a 24–32 h) y de H1.7 (de 14–18 h a 16–22 h).
  - H1.4 y H1.6 no cambian.
  - En los defs, el campo `sprite` pasa a llamarse `visual` en H1.5.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| H1.1 Fundaciones | Hecho | `ip1/h1-1-fundaciones` | Validado con Godot 4.7.2: importación y smoke test con código 0 |
| H1.2 Núcleo | Hecho | `ip1/h1-2-nucleo` | Misma semilla ⇒ misma huella tras 10 000 ticks; el smoke corre la simulación |
| H1.3 Contenido e i18n | Hecho | `ip1/h1-3-contenido-i18n` | Contenido en JSON validado, español e inglés con cambio en vivo, fuente Tiny5 |
| H1.4 Mapa y generación | Hecho | `ip1/h1-4-mapa-generacion` | Valle de 96×96×32 generado en unos 20 ms, superficie conectada, regiones de conectividad |
| H1.5 Render y cámara | Hecho | `ip1/h1-5-render-camara` | Valle en 3D con terreno por chunks, corte por nivel, plantas y cámara; colonos y objetos pasan a H1.7 |
| H1.6 Colonos e IA | Pendiente | | |
| H1.7 Interacción y UI | Pendiente | | |
| H1.8 Integración | Pendiente | | |

**Ruta crítica:** H1.1 → H1.2 → (H1.3 ∥ H1.4) → (H1.5 ∥ H1.6) → H1.7 → H1.8.

## Alcance

- Menú principal: Nueva partida, Opciones, Salir. "Cargar" visible pero deshabilitado hasta IP2.
- Opciones: idioma (Español por defecto / English) aplicado en vivo y guardado; pantalla completa.
- Nueva partida: nombre de la colonia y semilla.
- Valle de 96×96×32: pasto y tierra en superficie, colinas con roca expuesta, 2 a 5 niveles de tierra y luego estratos de piedra (granito, caliza, arenisca), robles, pinos, arbustos de bayas y rampas en las pendientes.
- 5 colonos humanos con nombre y género, y 30 raciones de provisiones.
- Órdenes: excavar muros, escalera hacia abajo, talar, recolectar bayas y cancelar.
- Trabajos automáticos con reservas.
- Necesidades: hambre y descanso. Muerte por inanición y fin de partida si mueren todos.
- Vista 3D low-poly con el Nature Kit de Kenney. Cámara con desplazamiento, zoom, rotación en pasos de 90° y cambio de nivel. La vista corta el mapa en el nivel actual y muestra los niveles inferiores oscurecidos.
- HUD: fecha con año, hora, pausa, 1×, 2× y 4×, nivel Z y barra de órdenes.
- Inspector de colono y de tile. Registro de eventos localizado.
- Preparación de épocas: calendario desde el año 1200, defs con época y conocimientos requeridos, variantes de modelo por época.
- Exportación Windows jugable.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| H1.1 Fundaciones | git, solución y proyectos, configuración común, proyecto Godot, CI, `CLAUDE.md`, agentes, skill `godot-run`, docs y ADRs, modo `--smoke` | Sesión principal, godot-engineer | 8–12 | `dotnet test` en verde, Godot abre el proyecto, `--smoke` sale con 0 |
| H1.2 Núcleo | ECS ligero, bucle de ticks, scheduler, RNG, calendario con año absoluto, comandos, eventos, hash de estado | Plan, sim-engineer, qa-tester | 10–14 | Misma semilla ⇒ mismo hash tras 10 000 ticks |
| H1.3 Contenido e i18n | `DefDatabase` y validación, `EraDef`, campos `era` y `requires`, `UnlockService`, defs iniciales, `Localization` con ICU, puente Godot, ajustes, fuente pixel | game-designer, sim-engineer, i18n-specialist, godot-engineer | 14–18 | Tests de defs, épocas e i18n en verde; una etiqueta cambia en vivo entre "Nueva partida" y "New game" |
| H1.4 Mapa y generación | Mapa 3D, chunks, generación con ruido, estratos, vegetación, rampas, conectividad | sim-engineer, game-designer | 10–14 | Misma semilla ⇒ mismo mapa; superficie conectada; generación en menos de 2 s |
| H1.5 Render y cámara | Modelos CC0 del Nature Kit de Kenney y faltantes; `visuals.json` con variantes por época; campo `visual` en los defs; terreno por chunks con `MultiMesh` y tinte por material; corte por nivel Z; vegetación, objetos y colonos; cámara 3D; luz | art-integrator, godot-engineer | 24–32 | Captura del valle en 3D con colinas y árboles, y otra con el corte en un nivel subterráneo y los inferiores oscurecidos |
| H1.6 Colonos e IA | A* 3D, reservas, decisión priorizada, WorkGivers y JobDrivers, necesidades, muerte | Plan, sim-engineer, qa-tester | 20–26 | Escenarios "5 colonos 3 días con bayas ⇒ todos vivos" y "área designada ⇒ excavada" |
| H1.7 Interacción y UI | Modos de entrada, selección de celdas con el rayo de la cámara, designación por arrastre, HUD, inspector, registro, menú principal, pausa, opciones | godot-engineer, i18n-specialist | 16–22 | Partida completa jugable con ratón y teclado en es y en |
| H1.8 Integración | Playtest, balance, bugs, ms/tick, README, plantillas de exportación, export Windows | qa-tester, game-designer, `/code-review`, humano | 10–14 | `v0.1.0` exportada y checklist de playtest completo |

## Definición de hecho de IP1

- Con un perfil de usuario limpio, el juego arranca en español y todo texto visible está en español.
- Cambiar a inglés en Opciones cambia todos los textos sin reiniciar, incluido el historial de eventos. La preferencia persiste.
- Misma semilla ⇒ mismo mapa.
- Los colonos ejecutan las órdenes, comen, duermen y pueden morir de hambre.
- Un def de prueba de otra época queda bloqueado: nada medieval está fijado en código.
- `dotnet test`, smoke test y test de determinismo en verde. Capturas es/en revisadas.
- Presupuesto: menos de 2 ms por tick de media y 60 FPS en el mapa de IP1.

## Registro de H1.1: Fundaciones

**Entregado**

- Rama `ip1/h1-1-fundaciones` sobre el commit inicial del repositorio.
- `OurLife.sln` con las configuraciones Debug, Release, ExportDebug y ExportRelease.
- Proyectos `OurLife.Core`, `OurLife.Localization`, `game/OurLife.csproj` con Godot 4.7.2, y tests con xUnit + Shouldly.
- Tests de arquitectura: Core y Localization sin Godot y sin dependencias cruzadas.
- Tests de reglas de determinismo sobre el código fuente de Core.
- `GameRoot` con el modo `--smoke` y el parser de argumentos del juego.
- CI de GitHub Actions con build y tests, `CLAUDE.md`, agentes, skill `godot-run`, documentación y ADR-001 a ADR-008.

**Decisiones**

- Godot 4.7.2, la última versión estable publicada, y `net8.0`, el framework de GodotSharp 4.7.2.
- El proyecto del juego se llama `game/OurLife.csproj` porque Godot deriva los nombres de la solución y del proyecto del nombre de ensamblado.
- Renderizador GL Compatibility, suficiente para 2D y compatible con más equipos.

**Validación**

- `dotnet build` sin advertencias y todos los tests en verde.
- Godot 4.7.2 importa el proyecto sin errores.
- `--smoke --seed 42 --ticks 5000` sale con código 0; un argumento inválido sale con código 2.
- El editor ejecuta el juego sobre el runtime .NET 10.0.8, aunque los proyectos compilan para `net8.0`.
- `/code-review` sobre la rama no reportó hallazgos.

**Pendiente**

- Opcional: crear un repositorio remoto para activar la CI.

## Registro de H1.2: Núcleo

**Entregado**

- ECS ligero en `OurLife.Core.Ecs`: `EntityId` con generación, `EntityManager` que reutiliza huecos en orden de llegada, `ComponentStore<T>` ordenado por id y `World` con consultas de uno y dos componentes.
- Simulación en `OurLife.Core.Sim`: `Simulation` con el bucle de ticks, sistemas con intervalo y desfase, `CommandQueue`, `EventBuffer` y `NotificationEvent`.
- Aleatoriedad: `Pcg32`, verificado contra la salida de la implementación de referencia, y `RngStreams` con un flujo independiente por nombre.
- Tiempo: `TimeSettings`, `Calendar` y `GameDate` con año absoluto, y `TickPacer` para convertir tiempo real en ticks según la velocidad.
- `StateHasher`: huella XxHash64 del estado completo.
- `LocalizedText` en `OurLife.Core.Text`, que valida la convención de claves.
- La prueba de humo de Godot corre la simulación y muestra la fecha, los milisegundos por tick y la huella.

**Decisiones**

- Los componentes son clases simples que los sistemas modifican en el lugar.
- Las consultas copian los resultados a una lista que aporta quien llama: no asignan memoria y permiten crear o destruir entidades durante el recorrido.
- Los huecos de entidades se reutilizan en orden de llegada y con una generación nueva, así una referencia vieja nunca apunta a otra entidad.
- La huella usa la forma JSON de componentes y comandos, que por eso deben ser datos planos y sin ciclos.
- Los valores de tiempo viven en `TimeSettings.PlanDefaults` hasta que H1.3 los lea de un def de configuración.

**Validación**

- `dotnet build` sin advertencias y todos los tests en verde.
- Misma semilla ⇒ misma huella tras 10 000 ticks. Otra semilla o un tick más ⇒ otra huella. Los mismos comandos en los mismos ticks ⇒ la misma huella.
- Smoke en Godot con `--seed 42 --ticks 5000`: código 0, la misma huella en dos corridas y la fecha esperada. Un `--ticks` negativo sale con código 2.
- `/code-review` encontró dos detalles menores, ya corregidos: la validación de un nombre nulo de flujo aleatorio y el orden de actualización al reutilizar un hueco de entidad.

## Registro de H1.3: Contenido e i18n

**Entregado**

- Defs en JSON validados en `game/content/defs`: configuración de tiempo, época medieval, materiales, plantas, objetos, necesidades, humano y nombres medievales. La spec con el balance está en `docs/gdd/contenido-ip1.md`.
- `OurLife.Core.Defs`: carga con errores agrupados y claros, `DefDatabase` con índices compactos, referencias cruzadas verificadas y `UnlockService` para épocas y conocimientos. La simulación se crea desde el contenido y su progreso entra en la huella del estado.
- `OurLife.Localization`: catálogos por idioma, subconjunto propio de ICU MessageFormat, reglas de plural, `Localizer` con español por defecto y respaldo.
- Textos en español e inglés para la interfaz, el calendario y cada def, con nombre, descripción y género gramatical.
- Godot: lectura de `res://content`, ajustes en `user://settings.cfg`, puente con `TranslationServer`, `TextResolver`, tema con la fuente Tiny5 y una pantalla temporal de prueba de idioma, que el menú principal de H1.7 reemplazará.
- Modos `--locale` y `--screenshot`, que se adelantaron desde H1.5 para verificar los idiomas con capturas.
- `tests/OurLife.Content.Tests`: paridad entre idiomas, textos de cada def, claves usadas en el código y ausencia de texto visible fuera de los catálogos.

**Decisiones**

- ICU propio en lugar de MessageFormat.NET, para poder comparar argumentos entre idiomas en los tests (ADR-006).
- Fuente Tiny5 en lugar de Pixelify Sans, que confunde 5 y S, 2 y Z, 0 y O (`docs/art.md`).
- Los JSON de contenido son JSON estricto, porque el editor de Godot también los lee. Los cargadores lo exigen, y un `null` en un def es un error.
- Los valores de tiempo salen del def `TimeConfig`; ya no hay valores de balance en el código.
- La fuente se importa sin suavizado, sin hinting, sin posicionamiento subpíxel y sin respaldo del sistema desde `Tiny5.ttf.import`, no desde el código. El smoke y las capturas fallan si la caché de importación quedó vieja.
- El progreso de la colonia sólo cambia desde Core: `ProgressState` no tiene setters públicos.

**Validación**

- `dotnet build` sin advertencias y todos los tests en verde.
- Smoke en Godot: el contenido y los idiomas cargan, el texto de `ui.mainmenu.new_game` cambia en vivo de "Nueva partida" a "New Game" y la fecha se muestra traducida.
- Capturas en español e inglés revisadas: todos los textos cambian de idioma y se ven todos los caracteres del español.
- Con `--screenshot`, una ruta relativa se guarda dentro de `game/`, porque Godot arranca en esa carpeta.
- `/code-review` encontró 15 hallazgos, todos corregidos:
  - Un error inesperado al cargar el contenido dejaba colgado el smoke.
  - Un `null` en un def rompía la validación.
  - Godot recibía el patrón ICU sin formatear.
  - El test de texto visible aceptaba cualquier texto con forma de clave.
  - Un `select` sin argumento elegía `other` en silencio.
  - Los casos `=N` se analizaban en cada formateo y podían desbordar.
  - Se ignoraba el estilo `integer`.
  - Faltaba un test de `ui.language.<idioma>`.
  - La caché vieja de la fuente no se detectaba.
  - `ProgressState` se podía modificar desde fuera de Core.
  - Los tests leían archivos ocultos que el juego ignora.
  - Los cargadores aceptaban comentarios.
  - Los tests duplicaban el cargador de catálogos.
  - `Localizer` asignaba memoria en cada formateo.
  - La documentación llamaba "estables" a los índices de defs.
- Una segunda ronda de `/code-review` sobre esas correcciones encontró 13 hallazgos más:
  - Todo el arranque queda protegido para que el smoke nunca se cuelgue.
  - Una fuente sin importar ya no rompe el arranque, y una importación vieja avisa también en el juego normal.
  - El test de texto visible rechaza claves con argumentos.
  - `=1` y `=01` cuentan como el mismo caso, y el valor exacto de un caso se deriva de su selector.
  - `{n, number}` redondea mitades al par.
  - La regla de nombres con punto vive en `IContentSource` y la cumplen las tres fuentes.
  - Una carpeta ilegible falla en lugar de omitirse.
  - Volvió el test de JSON estricto sobre toda la carpeta de contenido.
  - `CurrentEra` valida su valor y `overview.md` ya no habla de índices estables.
- Todos se corrigieron salvo una parte del de `ProgressState`: con un cast, `Knowledge` todavía se puede convertir en un conjunto modificable. No vale la pena un envoltorio para impedir un cast deliberado.
- Una tercera ronda encontró 10 detalles menores:
  - Seis se corrigieron. El test de JSON estricto ignora los archivos con punto, las fuentes de disco y de Godot omiten los mismos archivos, y `IcuCase` es inmutable y normaliza `=N`. La huella del estado ordena los conocimientos por su cuenta. La regla de rutas ignoradas y las opciones de enumeración ya no están duplicadas.
  - Dos quedan en el backlog: una pantalla de error al arrancar y la agrupación de miles del español según CLDR.
  - Dos no se cambian. Un `select` con un valor sin caso usa `other`, como en ICU. Y la fuente se sigue configurando sólo en la importación: una caché vieja hace fallar el smoke y las capturas, y en el juego normal deja un aviso.

## Registro de H1.4: Mapa y generación

**Entregado**

- Spec en `docs/gdd/mapa-y-generacion.md`: modelo del mapa, reglas de movimiento, pasos de la generación, valores iniciales y criterios de aceptación.
- `OurLife.Core.Map`:
  - `GameMap` con arrays densos de forma, material y banderas.
  - Chunks de 16×16 por nivel que se marcan sucios al cambiar una celda o una vecina.
  - `MapNavigation` con las reglas de movimiento por suelo, diagonales, rampas y escaleras, que también usará el A* de H1.6.
  - `Connectivity` con las regiones por relleno.
- `OurLife.Core.WorldGen`: `GradientNoise` y `WorldGenerator`, que arma el relieve, las pendientes, la tierra, la roca expuesta, los estratos, las rampas, el pasto y la vegetación.
- Def `WorldGen` validado y `game/content/defs/worldgen.json` con el valle de 96×96×32.
- Entidades `Position` y `Plant` para las plantas. El mapa entra en la huella del estado.
- Tests:
  - `AsciiMap` arma mapas de escenario desde texto.
  - Tests del mapa, la conectividad, el ruido, la generación y el def.
  - Tests del valle real en `OurLife.Content.Tests`.
- El smoke genera el valle y muestra el tiempo, las plantas y las regiones de la superficie.

**Decisiones**

- `z` crece hacia arriba. Un suelo es una celda abierta con piso, y su material es el del piso.
- La superficie de dos vecinas norte-sur o este-oeste difiere como mucho en un nivel, y cada escalón lleva una rampa. Eso garantiza una superficie conectada sin un paso de reparación.
- Ruido propio en lugar de FastNoiseLite, como preveía el plan: un archivo, sin código de terceros, y sólo operaciones de punto flotante exactas.
- Cada capa de la generación usa su propio flujo aleatorio (`worldgen.terrain`, `worldgen.vegetation`…), así un cambio en una capa no altera las demás.
- Sólo el Core cambia el mapa: `SetTile` es interno, como el progreso de la colonia.
- Los árboles no cuentan para la conectividad en H1.4; se suman con el pathfinding de H1.6.
- El juego genera el único def `WorldGen` del contenido hasta que IP7 traiga la elección de sitio.

**Validación**

- `dotnet build` sin advertencias y todos los tests en verde.
- La misma semilla da el mismo mapa y la misma huella; otra semilla, otro mapa.
- La superficie forma una sola región con las semillas 1, 2, 3, 42 y 20261009.
- Con las semillas 1 y 42, el valle real tiene 57 y 55 arbustos de bayas, 122 y 137 robles, y 64 y 75 pinos.
- Smoke en Godot con `--seed 1` y `--seed 42`: código 0, la misma huella en dos corridas y el valle generado en unos 20 ms, muy por debajo de los 2 s pedidos.
- Revisé un volcado en ASCII del valle: el fondo serpentea por el centro, las colinas muestran arenisca en las cimas y los estratos van de granito a arenisca.
- `/code-review` en dos rondas, con 10 y 8 hallazgos, todos corregidos:
  - `Generate` valida el def y comprueba que no haya mapa antes de tocar la simulación.
  - `RegionAt` devuelve "sin región" fuera del mapa.
  - Un solo helper calcula las regiones de la superficie para el smoke y los tests, y el smoke rechaza una superficie donde nadie puede pararse.
  - El test de la huella detecta si el mapa deja de entrar en ella.
  - El pasto respeta los extremos de `grassCoverage`.
  - `rockExposureWobble` y `groveFactorMax` pasan al def, con su validación.
  - Se validan los estratos contra `strataWobble` y la roca expuesta contra el fondo del valle.
  - La huella usa little-endian en cualquier máquina.
  - La cola del relleno de regiones crece según la región.
- No pasan al def las octavas del ruido, su factor de estiramiento ni el desplazamiento entre estratos. Son la calibración del algoritmo, no balance de juego.

## Registro de H1.5: Render y cámara

**Entregado**

- Rama `ip1/h1-5-render-camara`, creada desde H1.4.
- Modelos CC0 de Kenney en `game/assets/models`: 7 del Nature Kit 2.1 y 1 de Mini Characters, con sus `.import` y su licencia en `CREDITS.md`.
- Catálogo `visuals.json` con colores del terreno, kits a corregir, paleta y modelos con variantes por época. Formato en `docs/art.md`.
- En los defs, `sprite` pasa a ser `visual`, con la interfaz `IHasVisual` en Core.
- `game/src/Rendering`:
  - `VisualCatalog` y `ModelLibrary`, que copia los materiales mate, aplica la paleta y oscurece por profundidad.
  - `MapRenderer`: terreno por chunk de 16×16 por nivel, con un `MultiMeshInstance3D` por pieza y tinte por material. Sólo dibuja los bloques expuestos y reconstruye los chunks sucios.
  - `TerrainMeshes`: piezas del terreno generadas en código, que son bloque, suelo, rampa, escalera, hueco y tapa del corte.
  - `LevelShading`: corte por nivel y oscurecido por profundidad.
  - `PlantRenderer` y `WorldView`, con luz direccional, sombras y luz ambiente.
- `game/src/Input`: `CameraRig` e `InputActions`.
  - WASD o flechas para desplazar.
  - Q y E para girar en pasos de 90°.
  - Rueda para el zoom.
  - Re Pág y Av Pág para cambiar de nivel.
- El juego abre directamente el valle generado. `--level <z>` elige el nivel del corte, y `--probe` muestra la pantalla de prueba de idiomas.
- El smoke verifica que carguen el catálogo y todos sus modelos, y arma la vista 3D del mundo simulado.
- Tests en `OurLife.Content.Tests` (`VisualCatalogTests`), que comprueban:
  - Cada visual tiene modelo, y cada modelo lo usa un def.
  - Las variantes nombran épocas existentes.
  - Los archivos existen con su `.import`, y no hay modelos fuera del catálogo.
  - La paleta reemplaza materiales que existen, y los kits están declarados.
  - El formato estricto del catálogo.

**Decisiones**

- Se dibujan hasta 24 niveles inferiores, no 8, para que el valle se vea desde la superficie más alta.
- La tapa del corte usa el color del material oscurecido, así se leen los estratos.
- Los colores de Kenney se corrigen por carpeta de kit. La `palette` reemplaza el verde turquesa del kit por verdes y marrones terrosos, como pide `docs/art.md`.
- Una planta es un nodo con su modelo y no un `MultiMesh`, porque los modelos tienen varias mallas y el valle tiene unas 250 plantas.
- Se mantiene la perspectiva.
- El render de colonos y objetos pasa a H1.7, porque sus entidades llegan con H1.6. El catálogo ya tiene sus modelos provisionales.
- La revisión de Tiny5 sobre el 3D también pasa a H1.7, con el HUD.
- La liberación de los recursos de Godot va en `NotificationPredelete` y no en `_ExitTree`, así funciona aunque el nodo nunca entre al árbol.
- El detalle está en ADR-009, en "Ajustes al implementar H1.5".

**Validación**

- `dotnet build` sin advertencias y todos los tests en verde.
- Smoke con `--seed 1` y `--seed 42`, también con `--level 12`: código 0, la misma huella que en H1.4 y la vista armada en unos 90 ms.
- Capturas revisadas con `--seed 1`:
  - El valle desde la superficie más alta, con colinas de arenisca, robles, pinos y arbustos.
  - El corte en el nivel 12, con la tierra y el granito distinguibles y las zonas abiertas visibles.
  - Un primer plano para revisar la paleta y la escala de los modelos.
  - La pantalla de idiomas en es y en, con `--probe`.
- `/code-review` en tres rondas, con 10, 10 y 9 hallazgos.
  - Primera ronda:
    - Liberar los recursos de Godot que retenía el C#: teclas, `SurfaceTool`, mallas y texturas.
    - Liberar la vista si falla al armarse.
    - El smoke verifica todos los modelos del catálogo y arma la vista.
    - El catálogo rechaza `null` y colores con espacios.
    - Sólo tienen nodo las piezas que el chunk usa.
    - La tapa del corte se reconstruye sólo si cambia su nivel.
    - Un solo lugar genera el mundo.
    - Un solo `ApplyLevel`.
    - El flag `VertexColorIsSrgb` se mantiene para Forward+. Medí que con GL Compatibility no cambia el color.
  - Segunda ronda:
    - La limpieza pasa a `Predelete`.
    - La primera construcción arma todos los chunks sin depender de la cola de sucios.
    - El catálogo rechaza campos desconocidos.
    - El smoke respeta `--level`.
    - El color se valida con `MaterialDef.IsHexColor`, que ahora usa `\z` y rechaza un salto de línea final.
    - Las plantas guardan sus mallas y se sombrean sólo si cambia su profundidad.
  - Tercera ronda:
    - Las rampas miran a la vecina un nivel más alta.
    - La corrección de color es por kit.
    - Ya no se retienen las mallas de las plantas ocultas.
    - El `MultiMesh` se carga con un solo `Buffer`.
    - El smoke carga el catálogo una vez.
    - Los agentes y el plan sacan las capturas de idioma con `--probe`.
  - Quedan anotados, y no se corrigen ahora:
    - Las plantas son una foto del momento en que se arma la vista, porque todavía nada las crea ni las destruye. Seguir la tala y el rebrote es de H1.6 y H1.7.
    - La escalera en ambos sentidos no muestra el hueco; llega en H1.7.
    - La tapa del corte recorre el nivel entero y se pasará a una por chunk cuando se excave.
    - Construir los chunks recién al mostrarlos se descartó para evitar tirones al cambiar de nivel.
