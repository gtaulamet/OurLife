# ADR-013: Migración de partidas guardadas

- **Estado:** aceptada
- **Fecha:** 2026-10-16
- **Relacionada con:** ADR-004, ADR-010

## Contexto

ADR-010 anunció que, desde `v0.2.0`, cada cambio de formato del guardado trae su migración. IP3 hace el primero: los colonos suman edad, etapa de la vida y apellido, y la partida suma el ritmo de la vida. Los jugadores tienen partidas de `v0.2.x` que tienen que seguir cargando.

Hay dos lugares posibles para migrar:

- **Sobre el JSON**, antes de leer el estado: hay que escribir JSON a mano para cada componente nuevo, sin las reglas del juego.
- **Sobre la simulación ya cargada**: se cargan los componentes que el archivo tiene y después se agregan los que faltan, con el mismo código que usa una partida nueva.

## Decisión

- `SaveGame.FormatVersion` es la versión que el juego escribe; `SaveGame.OldestFormatVersion`, la más vieja que todavía carga. Una más vieja se rechaza como "de una versión vieja"; una más nueva, como hasta ahora.
- Un campo que el formato nuevo agrega se lee con su valor por defecto cuando falta en un archivo viejo. Lo que cambia la forma de un dato existente necesita además una migración sobre el JSON.
- La migración corre sobre la simulación cargada, antes de devolverla, y usa las reglas del Core: los colonos de una partida de la versión 1 reciben edad y apellido como los fundadores (`ColonyStart.Migrate`).
- La migración es determinista (ADR-004): sortea con su propio flujo de RNG (`life.migration`) y recorre los colonos por `EntityId`. Cargar dos veces el mismo archivo da la misma huella.
- Una partida migrada se guarda en el formato nuevo.
- Cada versión del formato que se puede migrar tiene un archivo real escrito con el código de esa versión en `tests/OurLife.Content.Tests/Fixtures`, y un test lo carga con el contenido de hoy. El de la versión 1 es `v0.2.5.olsave`.

## Consecuencias

- Las migraciones no duplican reglas: si cambia cómo llegan los fundadores, cambia también lo que reciben los colonos de una partida vieja.
- Una migración depende del contenido actual. Si un def que usaba una partida vieja desaparece, la carga falla con "falta contenido", como cualquier otra.
- El formato sube cada vez que llega a `main` un cambio del estado, aunque sea a mitad de una IP: el usuario juega lo que hay en `main`, y sus partidas tienen que seguir cargando. Las migraciones se encadenan: una partida de la versión 1 pasa por la 2, la 3 y así hasta la última. Un formato nuevo sin nada que completar, como el 6, sube igual: un juego viejo rechaza la partida como más nueva en lugar de leerla mal. (Al principio se pensó dejar el formato en 2 durante IP3; la revisión de H3.3 mostró que las partidas de H3.2 se quedaban sin personalidad.)
- Los archivos de prueba se versionan: unos 30 KB por versión. Hasta ahora, `v0.2.5.olsave` (versión 1), `h3.2.olsave` (versión 2), `h3.3.olsave` (versión 3) `h3.4.olsave` (versión 4), `h3.5.olsave` (versión 5), `h3.6.olsave` (versión 6), `v0.3.0.olsave` (versión 7) y `l2.olsave` (versión 8).
