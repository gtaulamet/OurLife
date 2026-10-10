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

## Ajustes al implementar H1.5

Del 10 de octubre de 2026, tras revisar las capturas:

- **Niveles inferiores:** se dibujan hasta 24, no 8. La vista arranca en la superficie más alta, y el fondo del valle queda de 10 a 15 niveles más abajo; con 8, el valle desaparecía. El oscurecido baja 2,5 % por nivel hasta un mínimo del 55 %. Los valores están en `LevelShading`, porque son de presentación y no de balance.
- **Tapa del corte:** la cara superior de la roca cortada usa el color del material al 45 %, no un color fijo. Así se leen los estratos en el nivel actual. El `cutColor` del catálogo queda para los huecos de escalera y las celdas sin material.
- **Piezas del terreno:** el bloque, el suelo, la rampa, la escalera, el hueco y la tapa se generan en código con `SurfaceTool`. No hacen falta `.glb` propios para el terreno.
- **Plantas:** cada planta es un nodo con el modelo instanciado, no un `MultiMesh` por id visual y por chunk. Los modelos de Kenney tienen varias mallas y materiales, y el valle de IP1 tiene unas 250 plantas. Se pasará a `MultiMesh` si el rendimiento lo pide, a más tardar con los mapas grandes de IP8.
- **Colores de los kits:** Kenney guarda su paleta sRGB en el campo de color lineal de glTF, y el importador la aclara otra vez. Cada material se copia mate. El color se corrige sólo en los modelos de las carpetas que el catálogo nombra en `srgbAsLinearKits`, para que un modelo bien exportado no se oscurezca. Además, el catálogo tiene una `palette` que reemplaza colores por nombre de material, porque el verde turquesa del kit choca con la paleta terrosa de la Edad Media.
- **Rampas:** una rampa sube hacia la vecina que está exactamente un nivel más arriba. Un acantilado de varios niveles también es un bloque en el nivel de la rampa, así que se usa sólo si no hay otra vecina.
- **Escalera en ambos sentidos:** por ahora se dibuja como la escalera hacia arriba, sin hueco. Una pieza propia llega en H1.7, cuando se puedan excavar escaleras.
- **Rendimiento:** cada chunk se carga en su `MultiMesh` con un solo `Buffer`. Sólo tienen nodo las piezas que el chunk usa, y todos los niveles se construyen al arrancar, en unos 90 ms con el valle de IP1, para que cambiar de nivel no dé tirones. La tapa del corte recorre el nivel entero cuando cambia; pasará a ser por chunk cuando excavar lo vuelva frecuente, a más tardar en IP8.
- **Proyección:** se mantiene la perspectiva; las capturas se leen bien.
- **Colonos y objetos:** su render pasa a H1.7, porque sus entidades llegan con H1.6. El catálogo ya tiene sus modelos.
- **Fuente:** la combinación de Tiny5 con el 3D se revisa en H1.7, cuando haya HUD sobre el mapa.

## Consecuencias

- La simulación, el pathfinding, la generación y la i18n no cambian.
- H1.5 pasa de 14–18 h a 24–32 h: catálogo de visuales, chunks con `MultiMesh`, corte por nivel, cámara 3D e importación de modelos.
- H1.7 pasa de 14–18 h a 16–22 h, porque la selección de celdas usa el rayo de la cámara.
- IP1 pasa de 100–135 h a 112–153 h.
- Ya no hay pixel art ni zoom en pasos enteros en el mapa: `atlas.json`, `TileSet`, `TileMapLayer` y `Sprite2D` dejan de aplicar. `docs/art.md` se reescribió para el estilo low-poly.
- Riesgo de legibilidad: lo subterráneo se lee peor en 3D que en una vista cenital. Se mitiga con el corte por nivel y el oscurecido por profundidad, revisando capturas al inicio de H1.5.
- Riesgo de rendimiento: los mapas grandes de IP8 (192×192×64) pueden necesitar combinar mallas por chunk o niveles de detalle. Esto reemplaza al "render por shader" de ADR-007.
- El arte de las épocas Moderna y Contemporánea pasa a necesitar modelos 3D en lugar de sprites.
