# Construcción (H2.4)

Spec de H2.4 de IP2: planos de muros, suelos, puertas, escaleras y rampas, con el material elegido. Los colonos traen los materiales y después construyen. Desmontar devuelve parte del material. Canalizar abre un hueco hacia el nivel de abajo.

## Materiales de construcción

- Un material de construcción es un def `Material` con `builtFrom`: el objeto con el que se construye. Lo que se construye toma ese material en el mapa, así que se tiñe con su color y el inspector lo nombra.
- `MaterialKind` suma `wood` (madera). Contenido de IP2:

| Material | `kind` | `builtFrom` | Color |
|---|---|---|---|
| `timber` (madera) | `wood` | `log` (tronco) | `#7E5531` |
| `masonry` (mampostería) | `stone` | `stone` (piedra) | `#9E9A92` |

- Un objeto es `builtFrom` de un solo material.
- Lo construido se reconoce por su material: una celda es **construida** si su material tiene `builtFrom`. La roca y la tierra del valle son **naturales**.
- Los materiales de construcción no tienen `minedItem`: lo construido no se excava, se desmonta.

## Construibles

Un def nuevo, `Buildable`, con nombre, descripción y género en cada idioma:

| Campo | Qué es |
|---|---|
| `shape` | Lo que queda en el mapa: `wall`, `floor`, `door`, `ramp` o `stairs` |
| `materials` | Materiales de construcción permitidos, el primero es el de por defecto |
| `cost` | Unidades del objeto del material que hay que traer |
| `workTicks` | Ticks de trabajo, con todo el material en el plano |
| `outline` | Al arrastrar una caja, el plano va sólo en el borde (para muros) |
| `order` | Lugar en la barra de construcción |

Contenido de IP2:

| Construible | `shape` | Materiales | Costo | Trabajo | `outline` |
|---|---|---|---|---|---|
| Muro | `wall` | madera, mampostería | 2 | 150 | sí |
| Suelo | `floor` | madera, mampostería | 1 | 60 | no |
| Puerta | `door` | madera | 3 | 180 | no |
| Rampa | `ramp` | madera, mampostería | 2 | 120 | no |
| Escalera | `stairs` | madera, mampostería | 3 | 200 | no |

`AiConfig.deconstructRefundPercent` (50) dice cuánto devuelve desmontar.

## Dónde va cada plano

Un plano ocupa una celda. No se pone en una celda con otro plano, con una orden de excavar, de escalera, de canal o de desmontar, ni con una planta. Las reglas por forma:

| Forma | Celda | Resultado |
|---|---|---|
| `wall` | Suelo plano | Muro |
| `door` | Suelo plano | Puerta: una celda pisable con su propio dibujo |
| `ramp` | Suelo plano, con un muro al lado (norte, este, sur u oeste) que tiene aire arriba, y sin muro arriba | Rampa hacia ese muro |
| `floor` | Aire (vacío), con un vecino pisable en el mismo nivel | Suelo |
| `stairs` | Suelo plano con aire arriba | Escalera de subida en la celda y de bajada en la de arriba |

- Suelo plano quiere decir la forma `floor`, natural o construida, sin nada que la bloquee.
- La puerta es una forma nueva del mapa, `door`. Se pisa como un suelo; en IP2 no se abre ni se cierra ni frena a nadie.
- Arrastrar un muro marca el borde de la caja: una casa de cuatro muros se marca de una vez. Las demás formas llenan la caja.
- Si el terreno cambia y el plano ya no tiene sentido (por ejemplo, alguien canaliza la celda), el plano desaparece y lo entregado cae en su celda o, si no se puede pisar, en la celda pisable vecina más cercana en el orden fijo de las direcciones.

## Traer los materiales

- **Trabajo nuevo:** `deliver` (entregar), del tipo de trabajo nuevo `construction` (construcción), que va antes del acarreo.
- El colono elige el plano más cercano al que le falta material y que nadie está abasteciendo. Después elige la pila del objeto del material más cercana al plano, en su región, que nadie tenga reservada. Puede ser una pila suelta o una guardada en un almacén.
- Camina hasta la pila, levanta lo que falta (o toda la pila si no alcanza), lo lleva a una celda vecina del plano y lo deja en el plano. El plano lleva la cuenta de lo entregado.
- Si falta más, el plano vuelve a estar disponible: lo toma el mismo colono u otro.
- Si deja el trabajo con algo en las manos, lo suelta donde está, como al acarrear.
- Sin pila del objeto, el plano espera.

## Construir

- **Trabajo nuevo:** `build` (construir), del tipo `construction`. Un plano con todo su material lo toma un colono, que trabaja desde una celda vecina (las ocho, con la regla de las diagonales de excavar).
- Mientras haya un colono parado en la celda de un plano que no se pisa al terminar (un muro), el trabajo no avanza.
- Al terminar, la celda toma la forma y el material del plano, pierde el pasto y el plano desaparece. Las pilas que quedaron en una celda que ya no se pisa caen en la celda del constructor.
- El progreso es del trabajo: si el colono lo deja, el siguiente empieza de cero, como al excavar.

## Desmontar

- **Orden nueva:** Desmontar (V) sobre celdas construidas que no sean aire. Trabajo `deconstruct` (desmontar), del tipo `construction`, desde una celda vecina. Tarda la mitad del `workTicks` del construible de esa forma.
- Resultado:
  - Muro, puerta y rampa pasan a suelo. Si debajo hay un muro, el suelo toma su material, como en el valle; si no, el del muro.
  - Suelo pasa a aire.
  - Escalera: se desmontan las dos celdas, la de subida pasa a suelo (con la misma regla) y la de bajada a aire.
- Devuelve `cost × deconstructRefundPercent / 100` unidades, redondeado hacia arriba, del objeto del material, en la celda desmontada si se pisa o en la del colono si no.

## Canalizar

- **Orden nueva:** Canalizar (H) sobre un suelo plano natural, por encima del nivel más profundo, con un muro o un suelo debajo.
- Trabajo `channel` (canalizar), del tipo `mining`, desde una celda vecina del mismo nivel.
- Resultado: la celda pasa a aire. Si debajo había un muro, pasa a rampa y puede dejar su `minedItem` como al excavar; desde la rampa se sube a los vecinos del nivel de arriba. Si debajo había un suelo, queda un hueco por el que se ve, sin paso.
- Tarda el `mineWorkTicks` del material de abajo si es un muro, y si no el del suelo.

## Excavar

- Excavar (Mine) sólo marca muros naturales. Un muro construido se desmonta.

## Lo que se ve

- **Barra de órdenes:** Construir (C) abre una fila con los construibles y el material elegido (Madera / Mampostería). Elegido uno, se arrastra la caja como con las órdenes. Desmontar (V) y Canalizar (H) son órdenes como las demás.
- Un plano se dibuja como una caja translúcida del tamaño aproximado del resultado (un muro entero, una losa de suelo, media altura para la rampa, dos niveles para la escalera), con el color del material. Con todo el material entregado, se ve más opaca.
- La puerta tiene su pieza propia: dos postes y una hoja más baja que los muros, teñidos con el material y girados en la línea de los muros.
- Muros, suelos, rampas y escaleras construidos usan las piezas del terreno, teñidas con el material.
- Las celdas con orden de desmontar o de canalizar llevan una marca de color, como las demás órdenes.
- El inspector de una celda con plano dice qué es, el material y lo entregado ("Plano de muro de madera: 1 de 2"). El de una celda construida nombra la forma y el material como hasta ahora.

## Comandos y eventos

| Comando | Efecto |
|---|---|
| `PlaceBlueprintCommand(construible, material, desde, hasta)` | Pone planos en las celdas válidas de la caja (sólo el borde si `outline`) |
| `DesignateCommand` con `Deconstruct` | Marca para desmontar las celdas construidas de la caja |
| `DesignateCommand` con `Channel` | Marca para canalizar los suelos naturales válidos de la caja |
| `DesignateCommand` con `Cancel` | Además, quita los planos de la caja; lo entregado cae en su celda |

- Los planos son entidades con `Blueprint` y `Position`, y avisan con los eventos de entidad. Las órdenes nuevas son marcas del mapa y avisan con `DesignationsChangedEvent`.

## Textos nuevos

| Clave | es | en |
|---|---|---|
| `ui.orders.build` | Construir (C) | Build (C) |
| `ui.orders.deconstruct` | Desmontar (V) | Deconstruct (V) |
| `ui.orders.channel` | Canalizar (H) | Channel (H) |
| `ui.orders.name.*` | nombre corto de cada orden | |
| `ui.inspector.blueprint` | Plano de {buildable} de {material}: {delivered} de {cost} | {material} {buildable} blueprint: {delivered} of {cost} |
| `ui.inspector.shape.door` | Puerta | Door |
| `job.deliver.report`, `job.build.report`, `job.deconstruct.report`, `job.channel.report` | Entregando materiales, Construyendo, Desmontando, Canalizando | Delivering materials, Building, Deconstructing, Channeling |
| `def.buildable.*` | muro, suelo, puerta, rampa, escalera | wall, floor, door, ramp, stairs |
| `def.material.timber.*`, `def.material.masonry.*` | madera, mampostería | timber, masonry |
| `def.work_type.construction.*` | construcción | construction |

El i18n-specialist ajusta los textos finales; la tabla fija claves y argumentos.

## Relieve del valle

- `terrainAmplitude` en `worldgen.json` baja de 3 a 2. Con 3, en la semilla 42 no había un sitio llano de 5 × 5 cerca de la llegada para una casa; con 2 lo hay en las semillas 1, 7 y 42. Cambió las huellas.

## Criterios de aceptación

1. Escenario: un plano de muro y troncos en un almacén ⇒ un colono los trae, construye y queda un muro de madera; el almacén tiene dos troncos menos.
2. Escenario: un canal sobre un suelo con roca debajo ⇒ la celda queda en aire, la de abajo es una rampa y un colono baja y vuelve a subir por ella.
3. Escenario: un muro sobre una celda con un colono parado no se termina mientras siga ahí.
4. Escenario: arrastrar un muro sobre una caja de 5 × 5 pone 16 planos; con la puerta en uno de ellos (cancelar ese plano y poner una puerta), los colonos construyen la casa y entran por la puerta.
5. Escenario: desmontar un muro de madera deja un suelo con el material de abajo y un tronco en la celda.
6. Escenario: un plano sin material cerca no lo toma nadie; al aparecer troncos, sí.
7. Cancelar un plano con material entregado deja ese material en su celda.
8. Excavar no marca muros construidos; Desmontar no marca celdas naturales.
9. El test de continuidad del guardado incluye planos a medio abastecer, un colono que lleva material, órdenes de desmontar y de canalizar, y muros construidos.
10. En la partida: una casa de cuatro muros con puerta, construida en es y en. Smoke y capturas revisadas.
