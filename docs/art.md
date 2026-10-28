# Guía de arte

## Dirección

- 3D low-poly sobre una grilla cuadrada, con cámara inclinada que rota en pasos de 90° (ADR-009). Hasta el 2026-10-09 el plan era pixel art 16×16 con vista cenital.
- Paleta sobria y terrosa para la Edad Media. Cada época podrá tener su paleta.
- Terreno con textura pintada por shader (ADR-012, estilo B de la prueba de IP2.5): pasto con briznas y manchas, tierra con grano, roca con vetas y estratos, madera en tablones, mampostería en ladrillos. Sin realismo: se mantiene el low-poly.
- Legibilidad antes que detalle: muro, suelo, árbol, objeto y colono se distinguen a simple vista desde la altura habitual de la cámara.

## Fuentes de assets

1. **[Nature Kit de Kenney](https://kenney.nl/assets/nature-kit)**, CC0: terreno, acantilados, árboles, arbustos, rocas y troncos para la superficie del valle. Al descargarlo hay que confirmar el contenido exacto y la licencia.
2. Otros kits 3D CC0 de Kenney para lo que falte, sobre todo colonos y objetos. Conviene elegir kits con el mismo estilo low-poly. Los aldeanos son los 12 de Mini Characters, con la ropa recoloreada con tonos de la época (ADR-012).
3. **(Ya no se usa; se quitó el 28 de octubre de 2026 al reemplazar sus muebles por modelos propios)** [KayKit Dungeon Remastered](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0)** de Kay Lousberg, CC0: muebles, barriles, cajas y comida de estética medieval (ADR-012). Usa una textura de degradé; su madera rojiza se entona con la `palette` (material `texture`, el nombre que KayKit da a su único material; otro kit con un material de ese nombre también se teñiría). No guarda sus colores como Kenney, así que no va en `srgbAsLinearKits`.
4. Modelos propios simples: el bloque de estrato para teñir, las escaleras y los marcadores de designación.
5. Otros assets CC0 o CC-BY, con atribución en `CREDITS.md`. Los modelos para épocas futuras pueden tener que encargarse o comprarse.

## Técnica

- Una celda del mapa mide una unidad de Godot de ancho y una de alto. La escala de cada modelo se ajusta en el catálogo, no en el modelo ni en el código.
- Formato `.glb`. Sólo se copian al repositorio los modelos que se usan, en `game/assets/models/<pack>/`, con sus `.import` versionados.
- Los modelos que se tiñen por material usan un material base gris. El color sale del `color` del `MaterialDef` y se aplica por instancia en el `MultiMesh`. Así un mismo bloque sirve para tierra, granito, caliza y arenisca.
- Las piezas del terreno se pintan con `game/assets/shaders/terrain.gdshader`: el patrón de cada material (grano, motas, estratos, tablones, ladrillos) se calcula según la posición en el mundo, sin texturas ni costuras (ADR-012).
- Profundidad: la vista corta el mapa en el nivel actual. La roca sin excavar del nivel actual muestra su cara superior oscurecida, y los niveles inferiores visibles se oscurecen según su distancia.
- Variantes por época con sufijo `@<era>`. El id sin sufijo es el de respaldo.

## Formato de visuals.json

Formato fijado en H1.5 y ampliado en IP2.5 (`game/assets/models/visuals.json`):

```json
{
  "terrain": {
    "cutColor": "#3A332D",
    "grass": { "lush": "#4D752E", "dry": "#728038", "deep": "#355B26" },
    "materials": { "soil": "soil", "granite": "speckled", "timber": "planks" }
  },
  "srgbAsLinearKits": ["res://assets/models/kenney_nature-kit/"],
  "palette": { "leafsGreen": "#4C7A2B", "woodBark": "#7A5233" },
  "decoration": {
    "grass": { "model": "res://assets/models/kenney_nature-kit/grass.glb", "scale": 1.8, "chance": 170 }
  },
  "models": {
    "plant.oak": { "model": "res://assets/models/kenney_nature-kit/tree_oak.glb", "scale": 1.5 },
    "house@modern": { "model": "res://assets/models/…/house_modern.glb" }
  }
}
```

- `terrain`: lo que el shader del terreno no saca de un `MaterialDef` (ADR-012).
  - `cutColor` es el hueco de una escalera hacia abajo y la celda sin material.
  - `grass` son los tres verdes que se mezclan en el pasto: el de siempre (`lush`), las manchas de pasto seco (`dry`) y las de pasto denso (`deep`).
  - `materials` da el patrón con que se pinta cada material, por el id de su def: `plain` (sólo color y tono), `soil` (grano y piedritas), `speckled` (motas y grietas, como el granito), `layered` (estratos, como la caliza y la arenisca), `planks` (tablones) o `bricks` (ladrillos). Cada material del contenido necesita el suyo.
- `srgbAsLinearKits`: carpetas de kits que guardan sus colores sRGB en el campo de color lineal de glTF, como los de Kenney. El importador los aclara una segunda vez, y el juego deshace esa conversión sólo en los modelos de esas carpetas.
- `palette`: reemplaza el color de un material de los modelos, por su nombre dentro del `.glb` (`leafsGreen`, `woodBark`, `grass`). Mantiene la paleta terrosa de la época. Sin entrada, se usa el color del kit corregido.
- `models`: el modelo de cada id visual y su escala (1 si falta). La escala hace que el modelo ocupe una celda.
  - `center: true` centra sobre la celda un modelo cuyo origen está en una esquina.
  - `turn` (0 a 3) son cuartos de vuelta, antihorarios vistos desde arriba, que llevan el frente del modelo hacia +Z, adonde apunta la flecha de la vista previa de muebles sin girar. El baúl de KayKit tiene `turn: 3`.
  - `variants` da otros archivos del mismo modelo por etiqueta; para los colonos, la etiqueta es el género (`female`, `male`) y cada colono toma uno de sus archivos según su id, siempre el mismo.
  - `parts` agrega modelos al principal, cada uno con su `offset` en celdas y su `scale`, sin heredar la escala del principal: las cerezas sobre el arbusto de bayas.
  - `boneScales` agranda o achica huesos del esqueleto por su nombre, alrededor de su articulación: la cabeza de bebés y niños (H3.8). Se escala también el reposo del hueso, así la animación no lo deshace.
  - `animations` cambia una animación que pide el juego (`idle`, `walk`, `emote-yes`…) por otra del modelo: el bebé se sienta (`sit`) donde los demás se quedan parados. Un test comprueba que cada una exista en todos los archivos del modelo.
  - `restHeight`, en una cama, es a qué altura del suelo se acuesta quien duerme en ella: 0,22 en la cama y 0,13 en la cuna.
  - Un colono se dibuja con `<visual>.<etapa>` si existe, como `creature.human.elder`; si no, con el `visual` de su criatura (H3.8).
- `decoration`: modelos que visten el pasto, como matas, flores y piedritas. `chance` dice en cuántas celdas de pasto de cada mil aparece cada uno; la suma no pasa de mil. Un hash de la celda elige el modelo, su giro, su tamaño y su lugar dentro de la celda, así que una celda siempre se ve igual. No son entidades, no se guardan y no cambian la simulación. No aparecen bajo un mueble ni bajo una planta. Los modelos de la decoración no deben ser los de un objeto (la flor roja de las bayas, la piedra chica), para no confundirse con algo que se puede levantar, y no llevan texturas: se dibujan con un color por material. Las matas toman el `grass` de la paleta, a propósito un poco más oscuro que los verdes de `terrain.grass`, para que se distingan del suelo. El jugador elige cuánta decoración ver (`docs/gdd/decoracion-del-pasto.md`): la opción multiplica estas probabilidades.
- El terreno no tiene modelos: sus piezas se generan en código, se tiñen con el `color` del `MaterialDef` y el shader les pinta el patrón (ADR-009 y ADR-012).

Los defs nombran su visual con el campo `visual`. Los tests de contenido (`VisualCatalogTests`) verifican que:

- Cada id exista sin sufijo de época.
- Cada material del contenido tenga su patrón, y cada patrón nombre un material.
- Cada modelo del catálogo lo use algún def.
- Las variantes nombren una época existente.
- Cada `.glb` exista con su `.import`.
- No haya `.glb` fuera del catálogo.
- Cada modelo de un kit de Kenney tenga su carpeta en `srgbAsLinearKits`.
- No haya campos desconocidos: un nombre mal escrito es un error, como en los defs.
- Cada color de la paleta reemplace un material de algún modelo.

## Modelos necesarios en IP1

| Grupo | Modelos | Origen probable |
|---|---|---|
| Terreno | Bloque gris para teñir (muro de tierra o roca), suelo de pasto, suelo de tierra, suelo de piedra para teñir, rampa, escalera hacia arriba, hacia abajo y en ambos sentidos | Nature Kit para el suelo y los acantilados; propios para el bloque y las escaleras |
| Plantas | Roble, pino, arbusto de bayas con y sin bayas | Nature Kit |
| Objetos | Tronco, piedra, bayas, provisiones | Nature Kit para el tronco y la piedra; otro kit o propios para el resto |
| Colonos | Entre 4 y 6 variantes de aldeanos | Kit de personajes CC0 de Kenney |
| Overlays | Marcadores de excavar, escalera, talar y recolectar; cursor de celda y selección | Mallas simples propias, semitransparentes |
| UI | Iconos de pausa y velocidad | Iconos 2D CC0 |

El art-integrator mantiene la lista de modelos que faltan.

### Modelos que faltan

Revisado en V5 de IP2.5, el 14 de octubre de 2026:

| Modelo | Estado | Plan |
|---|---|---|
| Terreno | Resuelto con piezas generadas en código y pintadas por el shader (ADR-012) | — |
| Muros construidos | Resueltos: finos (0,24 de una celda), con un poste y un brazo hacia cada vecino que los une; la roca y la tierra del valle siguen siendo bloques | — |
| Arbusto de bayas con bayas y sin bayas | Resuelto: `plant_bushDetailed` con tres racimos de `cherries` del Food Kit como partes; `plant_bush` sin bayas | — |
| Bayas | Resuelto: `cherries` del Food Kit | — |
| Provisiones | Resuelto: cesta propia `item_provisions` (el barril de KayKit se quitó) | — |
| Aldeanos | Resuelto: los 12 de Mini Characters (6 mujeres y 6 hombres), con la ropa recoloreada y animaciones de caminar, golpear, manipular, levantar, sostener y quieto | Animaciones de comer y de dormir, si las pide el juego |
| Marcas de órdenes, marco del cursor y selección | Resuelto con cajas translúcidas generadas en código (`OrderMarkers`, `GameInput`) | — |
| Iconos de UI | Resueltos para conocimientos: 8 íconos pixel de 16 × 16 (`tools/make-knowledge-icons.py`: ramas, estados y la investigación del HUD); el resto de los botones sigue siendo de texto | Cuando la UI los pida |
| Muebles | Resuelto: cama, mesa, silla y baúl propios (`furniture_*.glb`); el kit KayKit Dungeon Remastered se quitó | — |
| Cuna (H3.7) | Resuelto: cuna propia `furniture_cradle` | — |
| Etapas de la vida (H3.8) | Resuelto: 60 modelos propios (6 hombres y 6 mujeres en bebé, niño, adolescente, adulto y anciano), cada uno con su proporción; el catálogo los dibuja a escala 3,1 | — |
| Globos de interacción (H3.8) | Resuelto: íconos pixel propios de 16 × 16 (`tools/make-interaction-icons.py`) | — |

### Modelos propios pendientes (lista para el modelador)

Seguimiento por modelo en `docs/plan-modelos.md`.

Revisado el 28 de octubre de 2026. Varios objetos compartían un modelo provisorio de KayKit o del Nature Kit, y no se distinguen entre sí. Cada fila es un modelo `.glb` propio, a guardar en `game/assets/models/ourlife/` con el nombre indicado. Al entregarlo se reemplaza la entrada del id visual en `visuals.json` (ajustando `scale` y `turn`) y se quita el modelo provisorio si queda sin uso.

Requisitos comunes:

| Punto | Requisito |
|---|---|
| Formato | Un `.glb` por objeto |
| Tamaño | Cabe en una celda (1 × 1 unidades). La escala final se ajusta en el catálogo. |
| Origen | Centro de la base, apoyada en Y = 0 |
| Frente | Hacia +Z (si queda girado, se corrige con `turn`) |
| Estilo | Low-poly de colores planos, entre 300 y 2.000 triángulos |
| Materiales | Pocos y con nombre claro (`wood`, `metal`, `stone`, `cloth`, `food`), porque el catálogo puede reemplazar el color de un material por su nombre |
| Vista | La cámara rota en pasos de 90°: el objeto debe leerse bien desde los cuatro lados |
| Escala relativa | Probarlo junto a un colono (escala 3,1) antes de darlo por bueno |

**A1. Muebles y puestos de trabajo** (1 celda). Prioridad alta: los cuatro puestos usan hoy la misma mesa.

| Id visual | Archivo | Descripción |
|---|---|---|
| `furniture.carpenter_bench` | `furniture_carpenter_bench.glb` | Banco robusto con tornillo de banco, serrucho colgado, viruta y un tablón |
| `furniture.mason_bench` | `furniture_mason_bench.glb` | Bloque de piedra tosca sobre caballetes, con cincel y martillo y un sillar a medio tallar |
| `furniture.kitchen` | `furniture_kitchen.glb` | Hogar de piedra con olla colgada, parrilla y brasas (se construye con masonería) |
| `furniture.scriptorium` | `furniture_scriptorium.glb` | Pupitre inclinado con pergamino, tintero, pluma y un libro abierto |
| `furniture.charcoal_kiln` | `furniture_charcoal_kiln.glb` | Montículo de tierra y leña con césped, boca baja de aire y humo gris por arriba (O8; hoy usa el modelo de la cocina) |
| `furniture.bloomery` | `furniture_bloomery.glb` | Horno bajo de piedra con boca al frente con brasas, chimenea corta y un fuelle (O8; hoy usa el modelo de la cocina) |
| `furniture.forge` | `furniture_forge.glb` | Fragua con fogón de piedra, brasas, fuelle de cuero y yunque con un martillo apoyado (O8; hoy usa el banco de cantero) |
| `furniture.cradle` | `furniture_cradle.glb` | Cuna de madera con balancines, más pequeña que la cama |
| `furniture.table` | `furniture_table.glb` | Mesa de madera sencilla, distinta de los bancos de trabajo (opcional) |
| `furniture.bed` | `furniture_bed.glb` | Camastro con paja o manta  |
| `furniture.chair` | `furniture_chair.glb` | Banco o silla tosca  |
| `furniture.chest` | `furniture_chest.glb` | Cofre de madera con herrajes  |

**A2. Objetos** (0,2 a 0,5 celdas). Hoy se reutilizan `log`, `rock_smallA` y `cherries` para varios; los de comida y materiales ya tienen modelo propio. `item.log` e `item.stone` ya se ven bien.

| Id visual | Archivo | Descripción |
|---|---|---|
| `item.cabbage` | `item_cabbage.glb` | Cabeza redonda de hojas verdes |
| `item.peas` | `item_peas.glb` | Vainas verdes o un puñado de granos |
| `item.bread` | `item_bread.glb` | Hogaza redonda con cortes |
| `item.pottage` | `item_pottage.glb` | Cuenco u olla de barro con guiso |
| `item.pickled_cabbage` | `item_pickled_cabbage.glb` | Tarro o barrica pequeña, de color distinto a la de provisiones |
| `item.wheat` | `item_wheat.glb` | Gavilla de espigas |
| `item.flour` | `item_flour.glb` | Saco de tela atado |
| `item.provisions` | `item_provisions.glb` | Cesta o barrica con comida variada |
| `item.plank` | `item_plank.glb` | Tabla rectangular de madera |
| `item.handle` | `item_handle.glb` | Palo corto y pulido |
| `item.stone_block` | `item_stone_block.glb` | Sillar rectangular tallado |
| `item.iron_ore` | `item_iron_ore.glb` | Roca con vetas oxidadas |
| `item.iron_ingot` | `item_iron_ingot.glb` | Barra o prisma gris azulado |
| `item.charcoal` | `item_charcoal.glb` | Trozos negros y angulosos |
| `item.iron_axe` | `item_iron_axe.glb` | Mango de madera y hoja de hierro |
| `item.iron_pickaxe` | `item_iron_pickaxe.glb` | Mango de madera y cabeza en punta |
| `item.iron_hoe` | `item_iron_hoe.glb` | Mango de madera y hoja plana en ángulo |
| `item.iron_hammer` | `item_iron_hammer.glb` | Mango corto y cabeza de hierro |

**A3. Cultivos** (una celda de cultivo, varias plantas por modelo). Hoy son pastos y arbustos del Nature Kit. Cada etapa es un modelo distinto: `crop_<cultivo>_sprout.glb`, `crop_<cultivo>_growing.glb` y `crop_<cultivo>.glb`.

| Cultivo | Brote | Creciendo | Maduro |
|---|---|---|---|
| Trigo (`crop.wheat`) | Brotes verdes | Tallos altos verdes | Espigas doradas |
| Arvejas (`crop.peas`) | Brotes con zarcillos | Enredadera sobre estacas | Vainas colgando |
| Repollo (`crop.cabbage`) | Plántulas | Hojas abiertas | Cabezas formadas |

**B. Opcionales**: tocones de roble y de pino (no existen). La puerta, la escalera y la rampa se quedan como piezas generadas en código: se pintan con el shader del terreno (madera o mampostería según el material) y encajan sin costuras con el resto. Los árboles y el entorno ya los cubre el Nature Kit.

Total: 9 muebles (5 imprescindibles), 18 objetos, 9 cultivos y 5 opcionales, 41 modelos en total. No hay ningún objeto "cantera": la piedra se obtiene excavando y el puesto más cercano es el banco de cantero.

## Fuente tipográfica

**Tiny5**, de Stefan Schmidt, con licencia SIL OFL 1.1, en `game/assets/fonts/tiny5`. Se importa sin suavizado, sin hinting, sin posicionamiento subpíxel y sin fuentes de respaldo del sistema (`Tiny5.ttf.import`), y `UiTheme` la usa como fuente por defecto.

Se eligió en H1.3 tras comparar siete fuentes pixel con licencia OFL. El criterio fue cubrir todos los caracteres del español y distinguir a simple vista los caracteres que se confunden, porque el juego muestra muchas cantidades:

| Fuente | Resultado |
|---|---|
| Pixelify Sans | Descartada: 5 y S, 2 y Z, 0 y O se confunden |
| Silkscreen | Descartada: sólo mayúsculas, incómoda para textos largos |
| Micro5 | Descartada: demasiado pequeña |
| VT323 | Legible, con estética de terminal; alternativa si Tiny5 cansa en textos largos |
| DotGothic16, Jersey10 | Legibles, menos nítidas o más angostas |
| **Tiny5** | Elegida: nítida, caracteres bien diferenciados y cobertura completa |

Tamaños probados: 32, 48 y 64 píxeles. Se ven nítidos con zoom entero.

La UI sigue siendo 2D sobre el mapa 3D. En H1.5 se revisa sobre las capturas si la fuente pixel combina con el estilo low-poly; si no combina, se elige otra con el mismo criterio.

## Licencias

Todo asset de terceros se registra en `CREDITS.md` antes de entrar al repositorio.
