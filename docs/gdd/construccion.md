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
| `wall` | Suelo firme | Muro |
| `door` | Suelo firme | Puerta: una celda pisable con su propio dibujo |
| `ramp` | Suelo firme, con un muro al lado (norte, este, sur u oeste) que tiene aire arriba, y sin muro arriba | Rampa hacia ese muro |
| `floor` | Aire (vacío), con un vecino pisable en el mismo nivel | Suelo |
| `stairs` | Suelo firme con aire arriba | Escalera de subida en la celda y de bajada en la de arriba |

- Suelo firme quiere decir la forma `floor` sobre un muro, o un suelo construido. Un suelo natural que quedó sobre aire (porque se excavó el muro de abajo) no sirve: al desmontar lo que se construyó encima volvería como un suelo construido que nadie pagó.
- La puerta es una forma nueva del mapa, `door`. Se pisa como un suelo; en IP2 no se abre ni se cierra ni frena a nadie.
- Arrastrar un muro marca el borde de la caja: una casa de cuatro muros se marca de una vez. Las demás formas llenan la caja.
- Si el terreno cambia y el plano ya no tiene sentido (por ejemplo, alguien canaliza la celda), el plano desaparece y lo entregado cae en su celda o, si no se puede pisar, en la celda pisable vecina más cercana en el orden fijo de las direcciones.

## Traer los materiales

- **Trabajo nuevo:** `deliver` (entregar), del tipo de trabajo nuevo `construction` (construcción), que va antes del acarreo.
- Como al acarrear, el colono elige la pila más cercana a él de algún objeto que falte en un plano, en su región y que nadie tenga reservada; puede ser una pila suelta o una guardada en un almacén. Después elige, entre los planos que necesitan ese objeto y que nadie está abasteciendo, el más cercano a la pila.
- Camina hasta la pila, levanta lo que falta (o toda la pila si no alcanza), lo lleva a una celda vecina del plano y lo deja en el plano. El plano lleva la cuenta de lo entregado.
- Si falta más, el plano vuelve a estar disponible: lo toma el mismo colono u otro.
- Si deja el trabajo con algo en las manos, lo suelta donde está, como al acarrear.
- Sin pila del objeto, el plano espera.

## Construir

- **Trabajo nuevo:** `build` (construir), del tipo `construction`. Un plano con todo su material lo toma un colono, que trabaja desde una celda vecina (las ocho, con la regla de las diagonales de excavar).
- Mientras haya un colono parado en la celda de un plano que no se pisa al terminar (un muro), el trabajo no avanza. Por eso nadie construye, canaliza ni desmonta parado sobre una celda que otro trabajo va a cerrar o abrir (un plano de muro, un canal, un suelo por desmontar): dos constructores de muros vecinos se esperarían para siempre.
- Al terminar, la celda toma la forma y el material del plano, pierde el pasto y el plano desaparece. Las pilas que quedaron en una celda que ya no se pisa caen en la celda del constructor.
- El progreso es del trabajo: si el colono lo deja, el siguiente empieza de cero, como al excavar.

## Desmontar

- **Orden nueva:** Desmontar (V) sobre celdas construidas que no sean aire. Trabajo `deconstruct` (desmontar), del tipo `construction`, desde una celda vecina. Tarda la mitad del `workTicks` del construible de esa forma.
- Resultado:
  - Muro, puerta y rampa pasan a suelo. Si debajo hay un muro, el suelo toma su material, como en el valle; si no, el del muro.
  - Suelo pasa a aire.
  - Escalera: se desmontan las dos celdas, la de subida pasa a suelo (con la misma regla) y la de bajada a aire. Las dos quedan reservadas para quien desmonta, y el trabajo espera si alguien está parado en la de bajada; las pilas que había ahí caen en la celda del colono.
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

## Materiales y avisos (corrección de IP4)

Reporte de juego: "hay maderas, pero no construyen camas ni paredes de madera". Causas encontradas:

1. **Dos "maderas" distintas.** El HUD dice `Madera: N` y cuenta los troncos (`log`). Los muebles y los talleres piden el material `planks` (item `plank`, "Materiales trabajados" en el HUD), que sale del banco de carpintero. Una cama no se construye con troncos aunque haya cientos. Los muros, suelos, rampas y escaleras aceptan `timber` (troncos), `planks`, `masonry` y `ashlar`; el material elegido en la barra se conserva al cambiar de pieza si ésta lo permite, así que quien elige tablones para un muro gasta los tablones de las camas. Eso era información que faltaba: la barra no mostraba costos y el inspector no decía qué esperaba el plano.
2. **Error del Core: unidades repartidas entre muchos planos.** Un colono que traía material elegía el plano más cercano a la pila que no tuviera a nadie trayéndole. Con pocas unidades en pilas pequeñas (por ejemplo cinco troncos sueltos de a uno y diez muros de dos troncos) cada plano recibía una unidad y ninguno se completaba nunca. Ahora se cumple esto:
   - Un plano al que no se le ha entregado nada sólo se abastece si lo que hay en la colonia de su item, menos lo que les falta a los planos ya empezados (o con alguien en camino), alcanza para completarlo.
   - Sólo gastan presupuesto los planos a cuya vecindad llega algún colono que construye (el mismo criterio de alcance que usa quien trae el material); un plano inalcanzable no reserva nada. Tampoco cuentan las unidades que otro trabajo ya reservó (una receta que va a buscar la pila, un equipo, un bebé por alimentar).
   - Entre los planos abastecibles, los que ya tienen algo entregado van antes, aunque estén más lejos.
3. Sin cambio, pero explicado: con la prioridad de Construcción en "nunca" para todos nadie trae ni construye; un plano en una celda a la que nadie llega espera para siempre; las órdenes de producción (y las recetas, como los mangos, que gastan tablones) compiten por los mismos tablones que los planos, y quien llega primero se los lleva; una pila de otra calidad cuenta igual para un plano (la calidad no se mantiene al construir).

Consulta de lectura (sin cambiar el estado), en `BuildQueries`: `Available(item)` (unidades sueltas, almacenadas o cargadas), `Committed(item)` (lo que faltan los planos), `ItemOf(material)` y `StatusOf(plano)`, que devuelve `BuildStatus(Block, Item, Missing, Available)`. `Block` es `Material` (la colonia tiene menos de las que faltan), `Builder` (nadie con Construcción distinta de "nunca" y en edad de trabajar), `Unreachable` (ningún colono que construye llega a una celda junto al plano), `Competing` (hay material en la colonia, pero otros planos o trabajos lo reclaman antes: el mismo reparto que usan los colonos, en una sola función, `SupplyBudget`) o `None`; se evalúan en el orden `Material`, `Builder`/`Unreachable`, `Competing`.

Presentación:

- La pista de la barra de construcción agrega una línea: `Cada pieza: 4 tablones. Tienes 16`; si la colonia tiene menos que lo que pide una pieza, `...: el plano esperará a que haya más`.
- El tooltip de cada botón (pieza y material) dice `Construir cama de tablones: 4 tablones. Tienes 16` (`ui.orders.name.build` con la primera letra en mayúscula), con el material que hay elegido o el del botón, y se actualiza cada medio segundo.
- Al soltar un arrastre con la herramienta (una sola consulta por soltado, con las celdas que el comando va a planear; es una cifra aproximada, 'hasta N piezas', porque el comando se aplica en el siguiente tick), si las piezas piden más de lo que hay libre (lo que hay menos lo que ya les falta a otros planos), una línea naranja bajo la pista lo avisa durante 6 s. No bloquea: el plano espera.
- El inspector de un plano agrega una línea: `Esperando: faltan 2 troncos; hay 0 en la colonia`, `Esperando: ningún colono tiene activa la construcción`, `Esperando: ningún colono llega hasta aquí`, `Esperando: otros planos usan primero tablones; hay 4 en la colonia`, o `Los colonos traen troncos` si va todo bien.

| Clave | es | en |
|---|---|---|
| `ui.build.cost` | {buildable}: {count} {item}. Tienes {available} | {buildable}: {count} {item}. You have {available} |
| `ui.build.cost_short` | ...: el plano esperará a que haya más | ...: the blueprint will wait for more |
| `ui.build.hint_cost`, `ui.build.hint_cost_short` | Cada pieza: {count} {item}. Tienes {available}[: ...] | Each piece: ... |
| `ui.build.placed_short` | Los planos piden {needed} {item} y tienes {available}: esperarán a que haya más | The blueprints ask for ... |
| `ui.inspector.wait.material` | Esperando: faltan {missing} {item}; hay {available} en la colonia | Waiting: {missing} {item} short; the colony has {available} |
| `ui.inspector.wait.competing` | Esperando: otros planos usan primero {item}; hay {available} en la colonia | Waiting: other plans use {item} first; the colony has {available} |
| `ui.inspector.wait.builder`, `.unreachable`, `.bringing` | ver arriba | |

Criterios: (11) cinco troncos en cinco pilas de uno y diez planos de muro de dos troncos, con cinco colonos ⇒ se construyen dos muros (antes, ninguno); (12) una cama con 4 tablones sueltos se construye, con 3 o con sólo troncos no, y `StatusOf` dice `Material` con el faltante y lo disponible; (13) sin colono que construya `StatusOf` dice `Builder`, y en una celda cerrada, `Unreachable`; (14) pilas de dos calidades de tablón suman para una cama; (15) un plano inalcanzable de id bajo no reserva los tablones de uno alcanzable posterior, que se construye; (16) los tablones que una receta va a buscar no cuentan para un plano (`Competing`). Tests: `BuildMaterialsTests`.

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
