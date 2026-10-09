# ADR-009: Render 3D low-poly con el Nature Kit de Kenney

- **Estado:** aceptada
- **Fecha:** 2026-10-09
- **Reemplaza a:** ADR-007

## Contexto

El plan original usaba pixel art 16×16 con vista cenital y `TileMapLayer` (ADR-007). Antes de empezar H1.4 se decidió que el juego se vea en 3D low-poly con el [Nature Kit de Kenney](https://kenney.nl/assets/nature-kit), con licencia CC0.

El cambio llega en buen momento. El render todavía no existe (H1.5 está pendiente), y el Core ya modela un mapa 3D de celdas cuadradas sin depender de Godot (ADR-002 y ADR-003). El Nature Kit está pensado para una grilla cuadrada, así que no hace falta tocar la simulación, el pathfinding ni las designaciones.

Se descartó el Hexagon Kit: obligaba a pasar a una grilla hexagonal, y eso encarecía el Core, la construcción, las designaciones y los tests de escenario en todas las IPs.

## Decisión

**Escala y ejes**

- Una celda del mapa ocupa una unidad de Godot de ancho y una de alto. La `x` del mapa va al eje X de Godot, la `y` al eje Z y el nivel `z` al eje Y, que en Godot apunta hacia arriba.
- La escala, la rotación y el desplazamiento de cada modelo se ajustan en el catálogo de visuales, no en código.

**Catálogo de visuales**

- `game/assets/models/visuals.json` reemplaza a `atlas.json` y asocia cada id visual (`plant.oak`, `terrain.wall`) con un modelo `.glb`, su escala y si se tiñe por material.
- Las variantes por época mantienen el sufijo `@<era>` (ADR-008). Si una variante no existe, se usa el id sin sufijo.
- En los defs, el campo `sprite` pasa a llamarse `visual` en H1.5. Un test de contenido verifica que cada id visual exista en el catálogo.

**Terreno**

- Cada chunk de 16×16 celdas por nivel tiene un `MultiMeshInstance3D` por tipo de pieza: bloque, suelo, rampa y escalera.
- Sólo se instancian las celdas expuestas, es decir, las que tienen al menos una vecina vacía, o las que corta el plano del nivel actual.
- El color de cada instancia sale del `color` del `MaterialDef`. Un mismo bloque gris sirve para tierra, granito, caliza y arenisca.
- Sólo se reconstruyen los chunks marcados como sucios.

**Vegetación, objetos y colonos**

- Árboles, arbustos y objetos se dibujan con un `MultiMesh` por id visual y por chunk.
- Los colonos son nodos `Node3D` en un pool, con interpolación entre ticks.

**Niveles Z**

- La vista corta el mapa en el nivel actual. Los niveles superiores no se dibujan.
- Las celdas sólidas del nivel actual muestran su cara superior en un tono más oscuro, para distinguir la roca sin excavar del suelo.
- Se dibujan hasta 8 niveles inferiores, cada vez más oscuros según su distancia. El límite es un valor de configuración.

**Cámara y selección**

- `Camera3D` con inclinación fija, rotación en pasos de 90°, desplazamiento y zoom continuo con límites.
- La proyección empieza en perspectiva. Si la legibilidad lo pide, se cambia a ortográfica al revisar las capturas de H1.5.
- Para seleccionar una celda, el rayo de la cámara se interseca con el plano del nivel actual. No hacen falta colisionadores ni física.

**Render y UI**

- Se mantiene el renderizador GL Compatibility: alcanza para low-poly con una luz direccional y luz ambiente.
- Se pasará a Forward+ sólo si más adelante hacen falta sombras o efectos que Compatibility no da.
- La UI sigue siendo 2D, construida en C# sobre un `CanvasLayer`. La fuente Tiny5 se mantiene salvo que las capturas de H1.5 muestren que choca con el estilo 3D.
- Las escenas `.tscn` siguen siendo mínimas. El mapa y las entidades se arman en código.

**Origen de los modelos**

- El Nature Kit cubre la superficie del valle: terreno, acantilados, árboles, arbustos, rocas y troncos.
- Los colonos y los objetos que falten salen de otros kits 3D CC0 de Kenney.
- El bloque de estrato y las escaleras pueden ser modelos simples propios.
- Sólo se copian al repositorio los modelos usados, con sus `.import` versionados y su licencia en `CREDITS.md`.

## Consecuencias

- La simulación, el pathfinding, la generación y la i18n no cambian.
- H1.5 pasa de 14–18 h a 24–32 h: catálogo de visuales, chunks con `MultiMesh`, corte por nivel, cámara 3D e importación de modelos.
- H1.7 pasa de 14–18 h a 16–22 h, porque la selección de celdas usa el rayo de la cámara.
- IP1 pasa de 100–135 h a 112–153 h.
- Ya no hay pixel art ni zoom en pasos enteros en el mapa: `atlas.json`, `TileSet`, `TileMapLayer` y `Sprite2D` dejan de aplicar. `docs/art.md` se reescribió para el estilo low-poly.
- Riesgo de legibilidad: lo subterráneo se lee peor en 3D que en una vista cenital. Se mitiga con el corte por nivel y el oscurecido por profundidad, revisando capturas al inicio de H1.5.
- Riesgo de rendimiento: los mapas grandes de IP8 (192×192×64) pueden necesitar combinar mallas por chunk o niveles de detalle. Esto reemplaza al "render por shader" de ADR-007.
- El arte de las épocas Moderna y Contemporánea pasa a necesitar modelos 3D en lugar de sprites.
