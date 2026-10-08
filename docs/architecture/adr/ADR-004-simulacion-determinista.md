# ADR-004: Simulación determinista de paso fijo

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

Un simulador con historias emergentes produce bugs difíciles de reproducir. Si la misma semilla y los mismos comandos dan siempre el mismo resultado, cualquier bug se reproduce y se convierte en test.

## Decisión

- Paso fijo: 10 ticks por segundo a velocidad 1×. 1 tick equivale a 1 minuto de juego. Los valores viven en un def de configuración.
- RNG propio (PCG32) con estado serializable y un flujo independiente por sistema.
- En Core están prohibidos `System.Random`, `Random.Shared`, `DateTime.Now`, `Guid.NewGuid`, `Environment.TickCount`, `Stopwatch` y `Parallel`. Un test revisa el código fuente.
- Se itera en orden estable y nunca se decide según el orden de un `HashSet` o un `Dictionary`.
- Enteros para las cantidades acumulativas, por ejemplo necesidades de 0 a 10 000.
- Hash del estado con XxHash64 para comparar ejecuciones en los tests.

## Consecuencias

- Los bugs se reproducen con semilla y registro de comandos.
- Los tests de escenario son estables.
- Guardar y cargar a mitad de partida debe dar el mismo resultado que seguir jugando, y se puede testear.
- Exige disciplina extra; el multihilo futuro deberá usar particiones deterministas.
