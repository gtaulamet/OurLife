# ADR-002: Núcleo de simulación en .NET puro

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

La simulación es el corazón del juego y la parte más compleja. Tiene que probarse rápido y sin el editor, para que los agentes verifiquen su trabajo con `dotnet test`. También conviene poder moverla a otro hilo más adelante.

## Decisión

- `OurLife.Core` (simulación) y `OurLife.Localization` (textos) son bibliotecas .NET sin referencias a Godot.
- El proyecto Godot es una capa de presentación: traduce la entrada del jugador en Comandos y los Eventos de la simulación en render y UI.
- `OurLife.Core` tampoco depende de `OurLife.Localization`: emite claves y argumentos, nunca texto final.

## Consecuencias

- Tests rápidos y deterministas sin abrir Godot.
- La simulación puede pasar a un hilo propio sin reescribirse.
- Hay que mantener los contratos de comandos, eventos y consultas.
- Core define sus propios tipos básicos, como coordenadas, en lugar de usar los de Godot.
- Las reglas se verifican con tests de arquitectura desde H1.1.
