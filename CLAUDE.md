# OurLife: guía para agentes

Simulador de colonias estilo Dwarf Fortress hecho con Godot 4.7 (.NET) y C#. La colonia empieza en la Edad Media; las épocas Moderna y Contemporánea llegan en IPs futuras. El juego está en español por defecto y también en inglés.

- Plan maestro: `docs/plan-maestro.md`
- IP actual y estado de los hitos: `docs/ip/IP1.md`
- Bitácora entre sesiones, con el estado actual y los pendientes: `docs/ip/bitacora.md`. Leerla al retomar el trabajo. Actualizarla después de cada avance, no sólo al terminar, hacer commit y subir la rama del hito a `origin` (GitHub): el usuario sigue el trabajo desde otra PC.
- Arquitectura y decisiones: `docs/architecture/overview.md` y `docs/architecture/adr/`

## Comandos

```powershell
dotnet build OurLife.sln
dotnet test OurLife.sln
& $env:GODOT_BIN --headless --path game -- --smoke   # código de salida 0 = OK
```

- `GODOT_BIN` apunta al ejecutable de consola de Godot 4.7.2 .NET (`..._mono_win64_console.exe`), que espera al proceso y devuelve su salida y su código.
- Más modos (captura, idioma, semilla): skill `godot-run`.
- Git: el disco D: es exFAT. La excepción `safe.directory` ya está configurada en esta máquina; si git rechaza el repositorio por "dubious ownership", usa `git -c safe.directory=D:/Proyectos/Juegos/ourlife <comando>`.
- Git: nunca cambies a un commit sin `.claude/` mientras Claude Code o VS Code están abiertos. Windows deja la carpeta pendiente de borrado y bloqueada, y eso impide cualquier checkout posterior. Para adelantar `main` usa `git fetch . <rama>:main`.

## Mapa del repositorio

| Ruta | Contenido |
|---|---|
| `src/OurLife.Core` | Simulación pura (.NET 8, sin Godot) |
| `src/OurLife.Localization` | Catálogos de texto y formateo ICU (sin Godot) |
| `game/` | Proyecto Godot `OurLife.csproj` (ensamblado `OurLife`, namespace `OurLife.Game`): código en `game/src`, datos en `game/content`, arte en `game/assets` |
| `tests/` | xUnit + Shouldly: `OurLife.Core.Tests`, `OurLife.Localization.Tests` y `OurLife.Content.Tests`, que valida el contenido real y los textos del código |
| `docs/` | Plan, GDD, arquitectura, i18n, arte, IPs y backlog |

## Reglas de arquitectura

1. `OurLife.Core` no referencia Godot ni `OurLife.Localization`. Lo verifican tests.
2. La presentación cambia el estado sólo con Comandos. La simulación informa con Eventos y consultas de solo lectura.
3. La simulación nunca genera texto final: emite `LocalizedText(clave, args)`.
4. Los números de balance viven en defs JSON (`game/content/defs`), no en código.
5. Nada específico de una época va en código: épocas, conocimientos y desbloqueos son datos.

## Determinismo en Core

- Prohibido: `System.Random`, `Random.Shared`, `DateTime.Now/UtcNow`, `Guid.NewGuid`, `Environment.TickCount`, `Stopwatch` y `Parallel`. Lo verifica un test sobre el código fuente.
- Prohibido decidir iterando `HashSet` o `Dictionary`: iterar en orden estable, por `EntityId` ascendente.
- Usar el RNG de la simulación, con un flujo por sistema (`simulation.Random.Get("<nombre>")`), y enteros para cantidades acumulativas.
- Componentes y comandos son datos planos y serializables: la huella del estado (`ComputeStateHash`) y el guardado los recorren en JSON. Cada componente o comando nuevo se registra en `SaveTypes` con un nombre estable, y sus ids de defs llevan `[DefRef]` (ADR-010).
- Cómo escribir un sistema: ejemplo en `docs/architecture/overview.md`.

## Localización

- Ningún texto visible fuera de `game/content/locales/{es,en}`. El español es el idioma por defecto y de respaldo; no se usa el idioma del sistema operativo.
- Toda clave nueva se agrega en `es` y en `en` en el mismo commit, con los mismos argumentos.
- Plural, género y época con el subconjunto de ICU MessageFormat de `OurLife.Localization`. Convención de claves y ejemplos: `docs/i18n.md`.
- Todo def con textos tiene `name`, `desc` y `gender` en cada idioma.
- En Godot, un control cuyo texto es una clave lo traduce Godot solo. Un texto con argumentos se formatea con `TextResolver` en un control con `AutoTranslateMode` desactivado, y se vuelve a formatear con `Localizer.LocaleChanged`.
- Español neutro, sin voseo ni regionalismos.

## Contenido

- Los JSON de `game/content` son JSON estricto: sin comentarios ni comas finales, porque el editor de Godot también los lee.
- Un def nuevo necesita su spec en `docs/gdd`, sus textos en todos los idiomas y, desde H1.5, su modelo en el catálogo de visuales (`game/assets/models/visuals.json`).

## Godot

- `Godot.NET.Sdk/4.7.2` en `game/OurLife.csproj` debe coincidir con la versión del editor.
- Todos los proyectos usan `net8.0`, el framework de GodotSharp 4.7.2 (ver ADR-001).
- Escenas `.tscn` mínimas; la UI se construye en C#. Las clases de nodos son `partial` y se llaman igual que su archivo.
- Con usings implícitos, `Environment` es ambiguo entre Godot y System: escribe `System.Environment`.
- Render 3D low-poly con el Nature Kit de Kenney (ADR-009): terreno por chunk con `MultiMeshInstance3D`, tinte por material, corte por nivel Z y renderizador GL Compatibility. Guía de arte en `docs/art.md`.
- API 4.x: `OS.GetCmdlineUserArgs()` para los argumentos después de `--`.
- Los archivos `.uid` e `.import` que genera el editor se versionan; la carpeta `game/.godot/` no.

## Convenciones

- Código, identificadores y comentarios en inglés. Documentación, specs y mensajes de commit en español.
- Conventional Commits en español, por ejemplo `feat(core): agrega el bucle de ticks`.
- Una rama por hito, por ejemplo `ip1/h1-2-nucleo`. Nada nuevo entra a mitad de una IP: va a `docs/ip/backlog.md`.
- Advertencias como errores en todos los proyectos salvo el de Godot.

## Definición de hecho de un hito

- `dotnet build` y `dotnet test` en verde.
- Smoke test con código 0 si Godot está instalado. Capturas en es y en revisadas cuando haya UI.
- `docs/ip/` actualizado y ADR nuevo si hubo una decisión de arquitectura.
- `/code-review` sin hallazgos pendientes y commit en la rama del hito.

## Agentes del proyecto

En `.claude/agents/`: game-designer, sim-engineer, godot-engineer, i18n-specialist, art-integrator y qa-tester.

Flujo de un hito: Plan → spec (game-designer) → Core + tests (sim-engineer) → render y UI (godot-engineer) → textos (i18n-specialist) → QA (qa-tester) → `/code-review` → commit. Se trabaja en paralelo sólo cuando el contrato de comandos, eventos y consultas ya está fijado.
