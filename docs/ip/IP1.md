# IP1: Primeros colonos

**Objetivo.** Abrir el juego en español, generar un valle con niveles Z y mantener vivos a 5 colonos dándoles órdenes. Poder cambiar a inglés en vivo.

**Estimación:** 100 a 135 h. **Versión al cerrar:** `v0.1.0`.

## Estado de los hitos

| Hito | Estado | Rama | Notas |
|---|---|---|---|
| H1.1 Fundaciones | Hecho | `ip1/h1-1-fundaciones` | Validado con Godot 4.7.2: importación y smoke test con código 0 |
| H1.2 Núcleo | En revisión | `ip1/h1-2-nucleo` | Misma semilla ⇒ misma huella tras 10 000 ticks; el smoke corre la simulación |
| H1.3 Contenido e i18n | Pendiente | | |
| H1.4 Mapa y generación | Pendiente | | |
| H1.5 Render y cámara | Pendiente | | |
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
- Cámara: desplazamiento, zoom entero, subir y bajar de nivel; niveles inferiores oscurecidos a través del aire.
- HUD: fecha con año, hora, pausa, 1×, 2× y 4×, nivel Z y barra de órdenes.
- Inspector de colono y de tile. Registro de eventos localizado.
- Preparación de épocas: calendario desde el año 1200, defs con época y conocimientos requeridos, variantes de sprite por época.
- Exportación Windows jugable.

## Hitos en detalle

| Hito | Contenido | Agentes | Horas | Entregable verificable |
|---|---|---|---|---|
| H1.1 Fundaciones | git, solución y proyectos, configuración común, proyecto Godot, CI, `CLAUDE.md`, agentes, skill `godot-run`, docs y ADRs, modo `--smoke` | Sesión principal, godot-engineer | 8–12 | `dotnet test` en verde, Godot abre el proyecto, `--smoke` sale con 0 |
| H1.2 Núcleo | ECS ligero, bucle de ticks, scheduler, RNG, calendario con año absoluto, comandos, eventos, hash de estado | Plan, sim-engineer, qa-tester | 10–14 | Misma semilla ⇒ mismo hash tras 10 000 ticks |
| H1.3 Contenido e i18n | `DefDatabase` y validación, `EraDef`, campos `era` y `requires`, `UnlockService`, defs iniciales, `Localization` con ICU, puente Godot, ajustes, fuente pixel | game-designer, sim-engineer, i18n-specialist, godot-engineer | 14–18 | Tests de defs, épocas e i18n en verde; una etiqueta cambia en vivo entre "Nueva partida" y "New game" |
| H1.4 Mapa y generación | Mapa 3D, chunks, generación con ruido, estratos, vegetación, rampas, conectividad | sim-engineer, game-designer | 10–14 | Misma semilla ⇒ mismo mapa; superficie conectada; generación en menos de 2 s |
| H1.5 Render y cámara | Sprites CC0, `atlas.json` con variantes por época, TileSet en código, capas de profundidad, entidades, cámara, nivel Z, modo `--screenshot` | art-integrator, godot-engineer | 14–18 | Captura del valle con colinas y niveles inferiores oscurecidos |
| H1.6 Colonos e IA | A* 3D, reservas, decisión priorizada, WorkGivers y JobDrivers, necesidades, muerte | Plan, sim-engineer, qa-tester | 20–26 | Escenarios "5 colonos 3 días con bayas ⇒ todos vivos" y "área designada ⇒ excavada" |
| H1.7 Interacción y UI | Modos de entrada, designación por arrastre, HUD, inspector, registro, menú principal, pausa, opciones | godot-engineer, i18n-specialist | 14–18 | Partida completa jugable con ratón y teclado en es y en |
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
- Restaurar `.claude/` después de reiniciar VS Code. Al adelantar `main` se cambió a un commit sin esa carpeta y Windows la dejó bloqueada mientras Claude Code la tenía abierta. Los archivos están a salvo en git, marcados como `skip-worktree`:

  ```powershell
  git update-index --no-skip-worktree -- (git ls-files .claude)
  git checkout -- .claude
  ```

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
