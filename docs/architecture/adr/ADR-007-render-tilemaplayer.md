# ADR-007: Render por capas TileMapLayer con TileSet generado

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

El mapa es 3D por capas y se ve un nivel a la vez. El arte viene de packs gratuitos y se reemplazará con el tiempo. Los agentes editan mal las escenas y recursos complejos de Godot.

## Decisión

- El `TileSet` se construye en código desde `game/assets/sprites/atlas.json`.
- Sprites base en gris teñidos por material con "alternative tiles" (`TileData.Modulate`).
- Cuatro `TileMapLayer` de profundidad: el nivel actual y 3 inferiores vistos a través del aire, cada vez más oscuros.
- Las entidades se dibujan con `Sprite2D` en pool, sólo las del nivel visible, con interpolación entre ticks.
- Filtro Nearest y zoom en pasos enteros para un pixel art nítido.
- La UI se construye en C#; las escenas `.tscn` se mantienen mínimas.

## Consecuencias

- El arte se reemplaza cambiando PNG y JSON, sin tocar código.
- Hay que escribir la fábrica del `TileSet`.
- Si el rendimiento no alcanza en mapas grandes, se pasará a un render por shader en IP8.
