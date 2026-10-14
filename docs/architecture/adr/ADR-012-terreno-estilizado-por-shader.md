# ADR-012: Terreno estilizado por shader y kits medievales

- **Estado:** aceptada
- **Fecha:** 2026-10-14
- **Amplía:** ADR-009

## Contexto

Hasta IP2, cada pieza del terreno se dibujaba con un solo color por material (ADR-009). El pasto teñía el bloque entero, también los costados de las rampas. El valle se leía bien, pero se veía plano: el usuario pidió que el pasto parezca pasto y la tierra parezca tierra, sin buscar realismo (IP2.5, `docs/ip/IP2.5.md`).

En V1 se hizo una prueba de estilo con capturas de la misma escena:

- Cuatro estilos del terreno: el plano de IP2, A (variación de tono), B (textura pintada) y C (B en píxeles).
- GL Compatibility contra Forward+, con oclusión ambiental.
- Muebles del Furniture Kit de Kenney contra los de KayKit Dungeon Remastered.

El usuario eligió el 14 de octubre de 2026: **terreno B, GL Compatibility y KayKit**.

## Decisión

**Terreno**

- El terreno usa un shader propio (`game/assets/shaders/terrain.gdshader`) en lugar de `StandardMaterial3D`.
- El patrón se calcula en el shader según la posición en el mundo: no usa texturas, no tiene costuras entre celdas y cuesta lo mismo con cualquier cantidad de materiales.
- Estilo B: el color base sigue saliendo del `MaterialDef`, con variación por celda y manchas suaves, y un detalle según el patrón del material:
  - Grano y piedritas en la tierra.
  - Motas y grietas en el granito.
  - Estratos en la caliza y la arenisca.
  - Tablones en la madera.
  - Ladrillos en la mampostería.
- Pasto: la marca `Grass` pinta la cara de arriba del suelo y de la rampa; los costados de la rampa son del material. El bloque que está bajo un suelo con pasto lleva un borde verde irregular arriba de sus costados.
- Los bordes de cada celda se ven como un bisel leve arriba, y la base de los costados se oscurece un poco, para que se lean los escalones del terreno.
- Cada instancia del `MultiMesh` lleva, en sus datos propios, el patrón de su material y sus marcas de pasto. El oscurecido por nivel (`LevelShading`) es un parámetro del material, como antes.
- Los parámetros de cada material (patrón, color secundario, intensidad) pasan al catálogo de visuales en V4, en `terrain.materials`, por id de material. Son de presentación, por eso no van en los defs. Mientras tanto viven en `TerrainStyle`.

**Renderizador**

- Se mantiene GL Compatibility, como decía ADR-009. Forward+ daba una luz más natural y oclusión ambiental, pero sube los requisitos de la placa de video y agrega un riesgo en la Mac. La profundidad que daba la oclusión la cubren en parte el bisel y el oscurecido de los costados.
- Se puede volver a evaluar en IP8, con una opción de calidad gráfica.

**Modelos**

- Los muebles salen de **KayKit Dungeon Remastered** de Kay Lousberg (CC0, versión gratuita en GitHub): cama, mesa, silla y baúl. El kit también trae barriles, cajas y platos de comida para reemplazar los modelos provisionales. Usa una textura de degradé, así que no va en `srgbAsLinearKits`. Su madera rojiza se entona con la paleta del catálogo.
- Los aldeanos siguen siendo los de **Mini Characters** de Kenney, ahora con sus 12 variantes (6 mujeres y 6 hombres) y sus animaciones. Su ropa moderna se recolorea con tonos de la época, cambiando la textura de colores que comparten.
- Los kits nuevos siguen las reglas de `docs/art.md`: sólo se copian los modelos usados, con su licencia en `CREDITS.md`.
- En V5 el catálogo suma `variants` (los aldeanos por género), `parts` (las cerezas sobre el arbusto) y `turn` (el frente de un modelo). Las bayas salen del Food Kit de Kenney y las provisiones de un barril de KayKit.

## Consecuencias

- El costo es despreciable: en el benchmark de la aldea, 420 FPS de media con el estilo B contra 431 con el terreno plano (RTX 4090 de portátil).
- En V1 se atribuyó al shader un cuadro de unos 100 ms. En V4 se vio que no era eso: aparecía cuando la ventana del benchmark cambiaba de tamaño a mitad de la medición. Con la ventana fija en 1280×720, el peor cuadro con el estilo B es de 3,8 ms.
- Los estilos A, C y el plano se quitaron en V4, con `--terrain-style`.
- Un material nuevo necesita su patrón en el catálogo; el test del catálogo lo verifica.
- La decoración del pasto (V4) es una malla por chunk con todos sus modelos, horneados con su color en los vértices: una llamada de dibujo por chunk. Con un `MultiMesh` por modelo y por chunk el valle sumaba hasta siete llamadas por chunk.
- Las épocas futuras pueden tener otra paleta de patrones sin tocar el shader, con variantes `@<era>` en el catálogo.
