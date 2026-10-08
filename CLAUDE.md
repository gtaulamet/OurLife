# OurLife: guía para agentes

Simulador de colonias estilo Dwarf Fortress hecho con Godot 4.7 (.NET) y C#. La colonia empieza en la Edad Media; las épocas Moderna y Contemporánea llegan en IPs futuras. El juego está en español por defecto y también en inglés.

- Plan maestro: `docs/plan-maestro.md`
- IP actual y estado de los hitos: `docs/ip/IP1.md`
- Arquitectura y decisiones: `docs/architecture/overview.md` y `docs/architecture/adr/`

## Comandos

```powershell
dotnet build OurLife.sln
dotnet test OurLife.sln
& $env:GODOT_BIN --headless --path game -- --smoke   # código de salida 0 = OK
```

- `GODOT_BIN` apunta al ejecutable de consola de Godot 4.7.2 .NET (`..._mono_win64_console.exe`), que espera al proceso y devuelve su salida y su código.
- Más modos (captura, idioma, semilla): skill `godot-run`.
- Git: el disco D: es exFAT y git rechaza el repositorio por "dubious ownership". Usa `git -c safe.directory=D:/Proyectos/Juegos/ourlife <comando>` salvo que el usuario haya agregado la excepción global.

## Mapa del repositorio

| Ruta | Contenido |
|---|---|
| `src/OurLife.Core` | Simulación pura (.NET 8, sin Godot) |
| `src/OurLife.Localization` | Catálogos de texto y formateo ICU (sin Godot) |
| `game/` | Proyecto Godot `OurLife.csproj` (ensamblado `OurLife`, namespace `OurLife.Game`): código en `game/src`, datos en `game/content`, arte en `game/assets` |
| `tests/` | xUnit + Shouldly |
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
- Usar el RNG de la simulación, con un flujo por sistema, y enteros para cantidades acumulativas.

## Localización

- Ningún texto visible fuera de `game/content/locales/{es,en}`. El español es el idioma por defecto y de respaldo; no se usa el idioma del sistema operativo.
- Toda clave nueva se agrega en `es` y en `en` en el mismo commit, con los mismos argumentos.
- Plural, género y época con ICU MessageFormat. Convención de claves y ejemplos: `docs/i18n.md`.
- Español neutro, sin voseo ni regionalismos.

## Godot

- `Godot.NET.Sdk/4.7.2` en `game/OurLife.csproj` debe coincidir con la versión del editor.
- Todos los proyectos usan `net8.0`, el framework de GodotSharp 4.7.2 (ver ADR-001).
- Escenas `.tscn` mínimas; la UI se construye en C#. Las clases de nodos son `partial` y se llaman igual que su archivo.
- Con usings implícitos, `Environment` es ambiguo entre Godot y System: escribe `System.Environment`.
- API 4.x: `TileMapLayer` (no `TileMap`); `OS.GetCmdlineUserArgs()` para los argumentos después de `--`.
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
