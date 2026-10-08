# ADR-005: Contenido en JSON validado

- **Estado:** aceptada
- **Fecha:** 2026-10-08

## Contexto

Materiales, plantas, objetos, criaturas, trabajos, recetas, conocimientos y épocas van a crecer mucho. Agregar contenido no debería requerir tocar código, y el mismo formato servirá para mods.

## Decisión

- Defs en JSON con comentarios, en `game/content/defs/`.
- `DefDatabase` carga los defs, valida las referencias cruzadas y asigna índices compactos para el mapa.
- `IContentSource` abstrae la lectura: `FileAccess` de Godot en el juego y `System.IO` en los tests, porque en el juego exportado `res://` no se puede leer con `System.IO`.
- Los textos de cada def salen de claves por convención: `def.<tipo>.<id>.name` y `.desc`.
- Todo def puede declarar `era` y `requires` (ADR-008).

## Consecuencias

- Contenido nuevo sin código y base para el soporte de mods en IP8.
- Hace falta una validación propia con mensajes claros.
- La exportación de Godot debe incluir los archivos `.json`; se verifica en H1.8.
