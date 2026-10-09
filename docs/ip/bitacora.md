# Bitácora de sesiones

Registro de trabajo entre sesiones de Claude Code. Al retomar, leer **Estado actual** y **Pendientes**. Al terminar una sesión, actualizar esas dos secciones y agregar una entrada al principio del **Historial**.

## Estado actual

*Actualizado el 2026-10-09.*

| Dato | Valor |
|---|---|
| Rama de trabajo | `ip1/h1-3-contenido-i18n` |
| Último commit de código | `007a389 fix(content): correcciones de la revisión de H1.3` |
| `main` | En `e11e307`, el cierre de H1.2; todavía no incluye H1.3 |
| Hitos hechos | H1.1, H1.2 y H1.3 |
| Próximo hito | H1.4 Mapa y generación |
| Validación | `dotnet build` sin advertencias, 199 tests en verde, smoke con código 0 y capturas es/en revisadas |
| Huella del smoke por defecto | `2012B074B25F81DF` (semilla 1, 1000 ticks) |

## Pendientes

En orden:

1. **Adelantar `main` a H1.3**, pendiente de que el usuario lo confirme: `git fetch . ip1/h1-3-contenido-i18n:main`.
2. **Empezar H1.4 Mapa y generación.** El paso a 3D (ADR-009) no cambia este hito.
   - Rama sugerida: `ip1/h1-4-mapa-generacion`, creada con `git switch -c` desde la rama actual o desde `main` ya adelantado. Crear la rama no toca archivos.
   - Alcance, en `docs/ip/IP1.md`: mapa 3D con chunks y generación con ruido. El valle mide 96×96×32: pasto y tierra en superficie, colinas con roca expuesta, de 2 a 5 niveles de tierra y luego estratos de granito, caliza y arenisca. Lleva robles, pinos, arbustos de bayas y rampas en las pendientes.
   - Entregable: misma semilla ⇒ mismo mapa, superficie conectada y generación en menos de 2 s.
   - Flujo: Plan → spec (game-designer) → Core + tests (sim-engineer) → QA → `/code-review` → commit.
   - Los materiales se guardan en el mapa por índice de `DefDatabase`. Ese índice cambia si cambia el contenido, así que las partidas guardarán ids.

3. **Al empezar H1.5, que ahora es 3D (ADR-009):**
   - Pedirle al art-integrator que descargue el Nature Kit, confirme su contenido y su licencia, y arme la lista de modelos que faltan comparándola con los defs de IP1.
   - Renombrar `sprite` a `visual` en los defs y en el Core.
   - Al inicio del hito, revisar con capturas la legibilidad del corte por nivel, la proyección (perspectiva u ortográfica) y si la fuente Tiny5 combina con el 3D.

El backlog (`docs/ip/backlog.md`) tiene dos temas que no son de H1.4: una pantalla de error al arrancar y la agrupación de miles del español según CLDR.

## Notas prácticas

- **`GODOT_BIN`** está definida como variable de usuario, pero la sesión de PowerShell de Claude Code no la hereda. Leerla con `[System.Environment]::GetEnvironmentVariable('GODOT_BIN','User')`.
- **Mensajes de commit:** en PowerShell, `git commit -F -` con un here-string no funciona porque el texto llega como argumento. Escribir el mensaje en un archivo del scratchpad y usar `git commit -F <archivo>`.
- **Códigos de salida:** no cortar la salida de Godot con `Select-Object -First`, porque `$LASTEXITCODE` deja de ser confiable. Guardar la salida en una variable y filtrarla después.
- **Reimportación:** si un `.import` cambia dentro del mismo segundo que la importación anterior, Godot no lo reimporta. Basta con borrar `game/.godot/imported/<archivo>-*` y volver a correr `--import`.
- **Fuente nítida:** el smoke y `--screenshot` fallan si la caché de la fuente tiene suavizado. El mensaje indica cómo reimportar.

## Historial

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
