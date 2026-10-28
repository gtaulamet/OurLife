# Plan de modelos propios

Seguimiento de los 41 modelos `.glb` propios que faltan. Los requisitos y las descripciones de arte están en `docs/art.md` (sección "Modelos propios pendientes"). Este documento lleva la cuenta de qué falta y qué ya se hizo.

Los archivos van en `game/assets/models/ourlife/`. Los scripts que generan los modelos están en `tools/models/` (ver su `README.md`); la salida va a `MODELS/`, que no se versiona.

## Progreso

| Fase | Contenido | Modelos | Hechos |
|---|---|---|---|
| 1 | Puestos de trabajo y cuna (imprescindibles) | 5 | 5 |
| 2 | Objetos de comida y materiales | 10 | 10 |
| 3 | Objetos de hierro, piedra y herramientas | 8 | 8 |
| 4 | Cultivos (3 cultivos × 3 etapas) | 9 | 9 |
| 5 | Muebles opcionales (mesa, cama, silla, baúl) | 4 | 4 |
| 6 | Piezas opcionales (puerta, escalera y rampa descartadas; 2 tocones modelados) | 5 | 2 modelados, 3 descartadas |
| | **Total** | **41** (32 sin los opcionales) | **38** (más 3 descartadas) |

Actualizar la columna "Hechos" y marcar las casillas al cerrar cada modelo. Un modelo está hecho cuando cumple todos los pasos de la sección siguiente.

## Pasos de cada modelo

Cada casilla de las fases lleva implícita esta secuencia:

1. **Modelar** según la descripción: low-poly de colores planos, 300 a 2.000 triángulos, cabe en 1 celda, origen en el centro de la base (Y = 0), frente hacia +Z.
2. **Materiales** con nombre claro (`wood`, `metal`, `stone`, `cloth`, `food`), pocos.
3. **Exportar** `.glb` al nombre indicado en `game/assets/models/ourlife/`.
4. **Probar** junto a un colono (escala 3,1) y girando la cámara por los 4 lados.
5. **Catálogo**: reemplazar la entrada del id en `game/assets/models/visuals.json`, ajustando `scale` y `turn`.
6. **Limpiar**: quitar el modelo provisorio si queda sin uso y revisar `CREDITS.md`.
7. **Verificar**: `dotnet build`, `dotnet test`, smoke con código 0 y captura revisada.
8. **Registrar**: marcar la casilla aquí y anotar en `docs/ip/bitacora.md`.

Se puede entregar por lotes (una fase entera por rama), pero el catálogo se actualiza modelo a modelo para que nada quede a medias.

Nada nuevo entra a mitad de una IP: si este trabajo cae en medio de una, se agenda como hito propio o va a `docs/ip/backlog.md`.

## Fase 1: puestos de trabajo y cuna (prioridad alta)

Estado (28 de octubre): integrada. Los 5 `.glb` están en `game/assets/models/ourlife/` y en `visuals.json`: los cuatro puestos a escala 0,8 y la cuna a 0,7 con `restHeight` 0,2. Generados con `tools/models/fase1.py`. Probados en la vista previa de muebles (`--furniture`), con `dotnet test` y el smoke en verde. Falta revisar a ojo la cuna con el bebé dentro y los puestos con un colono trabajando, y el color de cada material.

Los cuatro puestos usan hoy la misma `table_small`.

- [x] `furniture.carpenter_bench` → `furniture_carpenter_bench.glb`. Mesa baja y robusta con tornillo de banco, serrucho colgado, algo de viruta y un tablón. Hoy: `table_small`.
- [x] `furniture.mason_bench` → `furniture_mason_bench.glb`. Bloque de piedra tosca sobre caballetes, con cincel y martillo y un sillar a medio tallar. Hoy: `table_small`.
- [x] `furniture.kitchen` → `furniture_kitchen.glb`. Hogar de piedra con olla colgada, parrilla y brasas. Se construye con masonería. Hoy: `table_small`.
- [x] `furniture.scriptorium` → `furniture_scriptorium.glb`. Pupitre inclinado con pergamino, tintero, pluma y un libro abierto. Hoy: `table_small`.
- [x] `furniture.cradle` → `furniture_cradle.glb`. Cuna de madera con balancines, más pequeña que la cama. El bebé que la ocupa usa el modelo `sit`, así que revisar que no la atraviese. Hoy: `bed_frame` a escala 0,2.

## Fase 2: objetos de comida y materiales

Objetos de 0,2 a 0,5 celdas. Hoy se reutilizan `log`, `rock_smallA` y `cherries` (`barrel_small` ya se quitó). `item.log` e `item.stone` ya se ven bien: no se tocan.

Estado (28 de octubre): integrada. Los 10 `.glb` están en `game/assets/models/ourlife/` a su tamaño final (escala 1,0 en `visuals.json`, entre 0,15 y 0,7 de largo) y se quitó `barrel_small` de KayKit, que quedó sin uso. Generados con `tools/models/fase2.py`. La tabla y el mango tienen pocos triángulos (48 y 96) porque son formas simples. Falta ver cada objeto de cerca en el juego.

- [x] `item.cabbage` → `item_cabbage.glb`. Cabeza redonda de hojas verdes.
- [x] `item.peas` → `item_peas.glb`. Vainas verdes o un puñado de granos.
- [x] `item.bread` → `item_bread.glb`. Hogaza redonda con cortes.
- [x] `item.pottage` → `item_pottage.glb`. Cuenco u olla de barro con guiso.
- [x] `item.pickled_cabbage` → `item_pickled_cabbage.glb`. Tarro o barrica pequeña, de color distinto a la de provisiones.
- [x] `item.wheat` → `item_wheat.glb`. Gavilla o haz de espigas.
- [x] `item.flour` → `item_flour.glb`. Saco de tela atado.
- [x] `item.provisions` → `item_provisions.glb`. Cesta o barrica con comida variada. Hoy: `barrel_small`.
- [x] `item.plank` → `item_plank.glb`. Tabla rectangular de madera, no un tronco.
- [x] `item.handle` → `item_handle.glb`. Palo corto y pulido.

## Fase 3: objetos de hierro, piedra y herramientas

Estado (28 de octubre): integrada. Los 8 `.glb` están en `game/assets/models/ourlife/`, generados con `tools/models/fase3.py`. Los materiales de piedra y carbón van a escala 1,0 y las herramientas a 0,8 (miden entre 0,37 y 0,62 de largo, acostadas en el suelo). Las partes de hierro usan el material `iron` y los mangos `wood`, para poder teñirlos. Los modelos provisorios de `log` y `rock_smallA` siguen en uso por otros ids. Falta verlos de cerca en el juego.

- [x] `item.stone_block` → `item_stone_block.glb`. Sillar rectangular tallado.
- [x] `item.iron_ore` → `item_iron_ore.glb`. Roca con vetas oxidadas.
- [x] `item.iron_ingot` → `item_iron_ingot.glb`. Barra o prisma gris azulado.
- [x] `item.charcoal` → `item_charcoal.glb`. Trozos negros y angulosos.
- [x] `item.iron_axe` → `item_iron_axe.glb`. Mango de madera y hoja de hierro.
- [x] `item.iron_pickaxe` → `item_iron_pickaxe.glb`. Mango de madera y cabeza en punta.
- [x] `item.iron_hoe` → `item_iron_hoe.glb`. Mango de madera y hoja plana en ángulo.
- [x] `item.iron_hammer` → `item_iron_hammer.glb`. Mango corto y cabeza de hierro.

Las cuatro herramientas comparten mango: conviene modelar uno y reutilizar su material `wood`. Que se distingan por la silueta de la cabeza, porque se ven muy pequeñas.

## Fase 4: cultivos

Cubren una celda de cultivo, con varias plantas por modelo. Cada etapa es un modelo distinto. Hoy son pastos y arbustos del Nature Kit. Nombres: `crop_<cultivo>_sprout.glb`, `crop_<cultivo>_growing.glb` y `crop_<cultivo>.glb` (maduro).

Estado (28 de octubre): integrada. Los 9 `.glb` están en `game/assets/models/ourlife/`, generados con `tools/models/fase4.py`, a escala 1,0 y cubriendo casi toda la celda (0,75 a 0,94 de lado, 0,05 a 0,58 de alto). Las plantas de un cultivo ocupan los mismos puntos en sus tres etapas. Reemplazan a los pastos y arbustos del Nature Kit, que siguen en uso por la decoración y el arbusto de bayas. No pude ver un campo real en el juego (la captura con `--farm` no mostró cultivos): falta revisarlo de cerca.

Trigo (`crop.wheat`):
- [x] Brote → `crop_wheat_sprout.glb`. Brotes verdes.
- [x] Creciendo → `crop_wheat_growing.glb`. Tallos altos verdes.
- [x] Maduro → `crop_wheat.glb`. Espigas doradas.

Arvejas (`crop.peas`):
- [x] Brote → `crop_peas_sprout.glb`. Brotes con zarcillos.
- [x] Creciendo → `crop_peas_growing.glb`. Enredadera sobre estacas.
- [x] Maduro → `crop_peas.glb`. Vainas colgando.

Repollo (`crop.cabbage`):
- [x] Brote → `crop_cabbage_sprout.glb`. Plántulas.
- [x] Creciendo → `crop_cabbage_growing.glb`. Hojas abiertas.
- [x] Maduro → `crop_cabbage.glb`. Cabezas formadas.

Las tres etapas de un cultivo deben leerse como la misma planta en distintos momentos: mismo ancho de parcela y misma paleta de verdes. Revisarlas en fila antes de cerrar el cultivo.

## Fase 5: muebles opcionales

Los modelos actuales de KayKit funcionan. Reemplazarlos sirve para unificar el estilo con el resto de los modelos propios, así que van después de las fases 1 a 4.

Estado (28 de octubre): integrada. Los 4 `.glb` están en `game/assets/models/ourlife/`, generados con `tools/models/fase5.py`, a escala 1,0 (la cama con `restHeight` 0,24). Como ya no quedaba ningún modelo de KayKit en uso, se quitó el kit entero (`game/assets/models/kaykit_dungeon-remastered`), su fila de `CREDITS.md` y el color `texture` de la paleta, que sólo teñía esa madera. En la casa del smoke se ven la cama, la mesa y el baúl (teñidos por su material).

- [x] `furniture.table` → `furniture_table.glb`. Mesa de madera sencilla, distinta de los bancos de trabajo. Hoy: `table_small`.
- [x] `furniture.bed` → `furniture_bed.glb`. Camastro con paja o manta. Hoy: `bed_frame`.
- [x] `furniture.chair` → `furniture_chair.glb`. Banco o silla de madera tosca. Hoy: `chair`.
- [x] `furniture.chest` → `furniture_chest.glb`. Cofre de madera con herrajes. Hoy: `trunk_medium_A`.

## Fase 6: piezas opcionales

Revisada el 28 de octubre. La puerta, la escalera y la rampa son piezas del terreno generadas en código (`TerrainMeshes`): las pinta el shader del terreno con el patrón de su material (tablones, mampostería), se unen sin costuras a los bloques vecinos y giran hacia donde corresponde. Un `.glb` propio no se tiñe por material de la misma manera ni gira solo, así que **se descartan los tres modelos**. La puerta ya tiene el mismo grosor que los muros finos (0,24), así que encaja con ellos.

- [x] `door`, `stairs`, `ramp`: se mantienen como piezas de código (descartados como modelos propios).
- [x] `plant.oak.stump` → `stump_oak.glb`. Tocón del roble talado (330 triángulos, 0,47 de ancho, 0,28 de alto).
- [x] `plant.pine.stump` → `stump_pine.glb`. Tocón del pino talado (220 triángulos, 0,37 de ancho, 0,25 de alto).

Los dos modelos están hechos y a su tamaño final (escala 1,0), generados con `tools/models/fase6.py` en `MODELS/entorno/` y una copia versionada en `tools/models/pendientes/`. **No están en el juego**: el test `Every_model_of_the_catalog_is_used_by_a_def` exige que todo modelo del catálogo lo use un def, y hoy el talado no deja tocón. Cuando se haga esa función (`docs/ip/backlog.md`), se copian a `game/assets/models/ourlife/` (desde `tools/models/pendientes/`) y se agregan al catálogo como `plant.oak.stump` y `plant.pine.stump`. 

Muros finos (pedido del usuario, 28 de octubre): los muros construidos dejaron de ser bloques de una celda y pasaron a ser finos. Ver `docs/ip/bitacora.md`.

## Fuera de alcance

- Versiones por época con el formato `id@epoca` (por ejemplo `house@modern`): se preven en el catálogo pero no se hacen ahora. Entran con las IPs de las épocas Moderna y Contemporánea.
- Árboles de roble y de pino, y variedad de piedras, arbustos y flores: el Nature Kit ya los cubre. Se agregan sólo si el terreno se ve repetitivo.
